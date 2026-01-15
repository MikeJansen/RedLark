namespace RedLarkLib.Implementation;

using RedLarkLib.Exceptions;
using RedLarkLib.Internal;
using RedLarkLib.Testing;
using RedLarkLib.Utilities;
using System.Diagnostics;

/// <summary>
/// Core implementation of the RedLark distributed lock manager (DLM).
/// Implements the Redlock algorithm with parallel server communication and quorum-based short-circuiting.
/// </summary>
/// <remarks>
/// <para>
/// <b>Algorithm Overview:</b>
/// </para>
/// <para>
/// RedLark implements an improved version of the Redlock distributed locking algorithm:
/// </para>
/// <list type="number">
///   <item><description>
///     <b>Connect Phase:</b> Establish connections to all configured Redis servers.
///     A quorum (N/2 + 1) must be reachable to proceed.
///   </description></item>
///   <item><description>
///     <b>Lock Acquisition:</b> Attempt to set a key atomically on all servers in parallel.
///     A unique value (token) identifies this specific lock acquisition.
///   </description></item>
///   <item><description>
///     <b>Quorum Check:</b> If a majority of servers accept the lock within the validity window
///     (TTL minus elapsed time minus clock drift), the lock is granted.
///   </description></item>
///   <item><description>
///     <b>Retry Logic:</b> If the lock cannot be acquired, unlock any partial locks and retry
///     after a randomized delay.
///   </description></item>
/// </list>
/// <para>
/// <b>Key Improvements Over Standard Redlock:</b>
/// </para>
/// <list type="bullet">
///   <item><description>
///     <b>Parallel Requests:</b> Lock requests are sent to all servers simultaneously,
///     not sequentially. This reduces acquisition latency significantly.
///   </description></item>
///   <item><description>
///     <b>Short-Circuit on Quorum:</b> Once a quorum is achieved, the lock is granted immediately
///     without waiting for slower servers to respond.
///   </description></item>
///   <item><description>
///     <b>Auto-Renewal:</b> Optional automatic lock renewal prevents locks from expiring
///     during long-running operations.
///   </description></item>
/// </list>
/// <para>
/// <b>Clock Drift Compensation:</b>
/// </para>
/// <para>
/// The algorithm accounts for clock drift between servers using the formula:
/// <c>drift = (TTL * CLOCK_DRIFT_FACTOR) + 2ms</c>
/// where CLOCK_DRIFT_FACTOR is 0.01 (1%). This ensures locks remain valid across
/// servers with slightly unsynchronized clocks.
/// </para>
/// </remarks>
public class RedLark : IRedLark, IRedLarkInternal, IRedLarkTesting
{
    #region Private 

    /// <summary>
    /// Random number generator for retry delay randomization.
    /// Shared instance is acceptable as random delays don't need cryptographic security.
    /// </summary>
    private static readonly Random m_random = new();

    /// <summary>
    /// Default number of retry attempts when lock acquisition fails.
    /// </summary>
    private const int DEFAULT_RETRY_COUNT = 3;

    /// <summary>
    /// Default minimum delay (ms) between retry attempts.
    /// </summary>
    private const int DEFAULT_RETRY_DELAY_MIN = 100;

    /// <summary>
    /// Default maximum delay (ms) between retry attempts.
    /// </summary>
    private const int DEFAULT_RETRY_DELAY_MAX = 300;

    /// <summary>
    /// Default name identifier for RedLark instances.
    /// </summary>
    private const string DEFAULT_NAME = "default";

    /// <summary>
    /// Clock drift factor used to account for time differences between Redis servers.
    /// A value of 0.01 means 1% of the TTL is reserved as safety margin.
    /// </summary>
    private const float CLOCK_DRIFT_FACTOR = 0.01F;

    /// <summary>Factory for creating server connection instances.</summary>
    private readonly IServerFactoryInternal m_serverFactory;

    /// <summary>Factory for creating lock instances.</summary>
    private readonly ILockFactoryInternal m_lockFactory;

    /// <summary>Maximum number of retry attempts for lock acquisition.</summary>
    private readonly int m_retryCount;

    /// <summary>Minimum delay between retries in milliseconds.</summary>
    private readonly int m_retryDelayMin;

    /// <summary>Maximum delay between retries in milliseconds.</summary>
    private readonly int m_retryDelayMax;

    /// <summary>Calculated range for random delay (max - min).</summary>
    private readonly int m_retryDelayRange;

    /// <summary>Identifier name for this RedLark instance.</summary>
    private readonly string m_name;

    /// <summary>
    /// Number of servers required for a quorum (N/2 + 1).
    /// Locks are only valid when this many servers confirm acquisition.
    /// </summary>
    private readonly int m_quorum;

    /// <summary>Collection of Redis server connection strings.</summary>
    private readonly IEnumerable<string> m_hosts;

    /// <summary>
    /// When true, returns immediately once quorum is achieved without waiting for all servers.
    /// </summary>
    private readonly bool m_shortcircuitOnQuorum;

    /// <summary>All server instances (including disconnected ones).</summary>
    private readonly List<IServerInternal> m_allServers;

    /// <summary>Only the successfully connected server instances.</summary>
    private readonly List<IServerInternal> m_connectedServers;

    /// <summary>Flag to track disposal state and prevent double-disposal.</summary>
    private bool m_disposed;

    /// <summary>
    /// Gets the count of connected servers for testing purposes.
    /// </summary>
    int IRedLarkTesting.ConnectServerCount => m_connectedServers.Count;

    /// <summary>
    /// Attempts to connect to a single Redis server.
    /// </summary>
    /// <param name="a_server">The server instance to connect.</param>
    /// <returns>The server instance if connection succeeded, null otherwise.</returns>
    private async Task<IServerInternal?> ConnectHost(IServerInternal a_server)
    {
        try
        {
            return await a_server.Connect() ? a_server : null;
        }
        catch (Exception)
        {
            //TODO: Log the exception
            return null;
        }
    }

    /// <summary>
    /// Releases a lock on all connected servers using the resource name and unique value.
    /// Called when a lock acquisition fails or when explicitly unlocking.
    /// </summary>
    /// <param name="a_resource">The resource name to unlock.</param>
    /// <param name="a_uniqueValue">The unique token that identifies the lock owner.</param>
    private async Task UnlockAll(string a_resource, string a_uniqueValue)
    {
        if (m_connectedServers == null)
            throw new InvalidOperationException("Connect() has not been successfully called.");

        await m_connectedServers.ForEachExecAndWait(s => s.Unlock(a_resource, a_uniqueValue));
    }

    /// <summary>
    /// Introduces a randomized delay between retry attempts to prevent thundering herd.
    /// The delay is uniformly distributed between m_retryDelayMin and m_retryDelayMax.
    /// </summary>
    private async Task Delay()
    {
        int delay = m_retryDelayMin + m_random.Next(m_retryDelayRange);
        await Task.Delay(delay);
    }

    /// <summary>
    /// Calculates the clock drift compensation for a given TTL.
    /// Formula: (TTL * 1%) + 2ms base margin.
    /// </summary>
    /// <param name="a_ttl">The TTL in milliseconds.</param>
    /// <returns>The drift compensation in milliseconds.</returns>
    private static int GetDrift(int a_ttl)
    {
        return (int)(a_ttl * CLOCK_DRIFT_FACTOR) + 2;
    }

    /// <summary>
    /// Closes and disposes all connected server instances.
    /// </summary>
    private async Task Close()
    {
        if (m_connectedServers.Any())
        {
            await m_connectedServers.ForEachAsyncDispose();
            m_connectedServers.Clear();
        }
    }

    #endregion

    #region Internal

    /// <summary>
    /// Initializes a new instance of the <see cref="RedLark"/> class.
    /// </summary>
    /// <param name="a_serverFactory">Factory for creating server connections.</param>
    /// <param name="a_lockFactory">Factory for creating lock instances.</param>
    /// <param name="a_hosts">Redis server connection strings.</param>
    /// <param name="a_retryCount">Number of retry attempts (default: 3).</param>
    /// <param name="a_retryDelayMin">Minimum retry delay in ms (default: 100).</param>
    /// <param name="a_retryDelayMax">Maximum retry delay in ms (default: 300).</param>
    /// <param name="a_name">Instance name for identification (default: "default").</param>
    /// <param name="a_shortcircuitOnQuorum">Whether to return immediately on quorum (default: true).</param>
    public RedLark(IServerFactoryInternal a_serverFactory, ILockFactoryInternal a_lockFactory, 
        IEnumerable<string> a_hosts, int? a_retryCount = null, int? a_retryDelayMin = null, int? a_retryDelayMax = null, string? a_name = null, bool a_shortcircuitOnQuorum = true)
    {
        m_serverFactory = a_serverFactory;
        m_lockFactory = a_lockFactory;
        m_retryCount = a_retryCount ?? DEFAULT_RETRY_COUNT;
        m_retryDelayMin = a_retryDelayMin ?? DEFAULT_RETRY_DELAY_MIN;
        m_retryDelayMax = a_retryDelayMax ?? DEFAULT_RETRY_DELAY_MAX;
        m_retryDelayRange = m_retryDelayMax - m_retryDelayMin;
        m_name = a_name ?? DEFAULT_NAME;
        m_quorum = a_hosts.Count() / 2 + 1;
        m_hosts = a_hosts;
        m_shortcircuitOnQuorum = a_shortcircuitOnQuorum;
        m_allServers = m_hosts.Select(h => m_serverFactory.New(h)).ToList();
        m_connectedServers = new List<IServerInternal>();
    }

    #endregion

    #region IRedLarkInternal

    /// <summary>
    /// Attempts to renew a lock on all servers.
    /// </summary>
    /// <param name="a_lock">The lock to renew.</param>
    /// <param name="a_ttl">The new TTL to set.</param>
    /// <returns>The remaining validity time if renewed successfully, 0 if renewal failed.</returns>
    /// <remarks>
    /// Renewal uses a quorum-based approach similar to initial acquisition.
    /// If a quorum cannot be achieved or the validity window has expired,
    /// the lock is released and 0 is returned.
    /// </remarks>
    async Task<int> IRedLarkInternal.Renew(ILock a_lock, int a_ttl)
    {
        if (m_connectedServers == null)
            throw new InvalidOperationException("Connect() has not been successfully called.");

        var stopwatch = Stopwatch.StartNew();
        var locksRenewed = 0;
        var drift = GetDrift(a_ttl);
        await foreach (var task in m_connectedServers.ForEachEnumerateAsCompleted(s => s.Renew(a_lock, a_ttl)))
        {
            if (task.Value.IsCompletedSuccessfully && task.Value.Result)
            {
                locksRenewed++;
                if (locksRenewed >= m_quorum)
                {
                    var elapsedTime = stopwatch.ElapsedMilliseconds;
                    var validity = (int)(a_ttl - elapsedTime - drift);
                    if (validity > 0)
                    {
                        return validity;
                    }
                    await ((IRedLarkInternal)this).Unlock(a_lock);
                    return 0;
                }
            }
        }
        await ((IRedLarkInternal)this).Unlock(a_lock);
        return 0;
    }

    /// <summary>
    /// Releases a lock on all connected servers.
    /// </summary>
    /// <param name="a_lock">The lock to release.</param>
    async Task IRedLarkInternal.Unlock(ILock a_lock)
    {
        await UnlockAll(a_lock.Resource, a_lock.UniqueValue);
    }

    #endregion

    #region IRedLark

    /// <summary>
    /// The prefix added to all lock keys in Redis to namespace distributed locks.
    /// </summary>
    public const string KEY_PREFIX = "dlm:";

    /// <inheritdoc />
    async Task IRedLark.Connect()
    {
        await Close();
        var tasks = await m_allServers.ForEachExecAndWait(ConnectHost);
        m_connectedServers.AddRange(tasks
                        .Where(t => t.Value.IsCompletedSuccessfully && t.Value.Result != null)
                        .Select(t => t.Value.Result)
                        .Cast<IServerInternal>());

        if (m_connectedServers.Count < m_quorum)
        {
            await Close();
            throw new CannotObtainLockException();
        }
    }

    /// <inheritdoc />
    async Task<ILock?> IRedLark.Lock(string a_resource, int a_ttl, int a_maxRenew, LockAbortDelegate? a_onAbort)
    {
        if (m_connectedServers == null)
            throw new InvalidOperationException("Connect() has not been successfully called.");

        if (a_ttl < 200)
            throw new ArgumentOutOfRangeException("a_ttl", a_ttl, "TTL must be >= 200 milliseconds");

        var uniqueValue = SimpleUtils.GetUniqueValue();
        var drift = GetDrift(a_ttl);
        for (int retry = 0; retry < m_retryCount; retry++)
        {
            var locksAquired = 0;
            var stopwatch = Stopwatch.StartNew();
            var responses = 0;
            await foreach (var task in m_connectedServers.ForEachEnumerateAsCompleted(s => s.Lock(a_resource, uniqueValue, a_ttl)))
            {
                responses++;
                if (task.Value.IsCompletedSuccessfully)
                {
                    if (task.Value.Result)
                    {
                        locksAquired++;
                    }
                    if (locksAquired >= m_quorum && (m_shortcircuitOnQuorum || responses == m_connectedServers.Count))
                    {
                        var elapsedTime = stopwatch.ElapsedMilliseconds;
                        var validity = (int)(a_ttl - elapsedTime - drift);
                        if (validity > 0)
                        {
                            var lockObj = m_lockFactory.New(this, locksAquired, validity, a_resource, uniqueValue, a_ttl, a_maxRenew, a_onAbort);
                            lockObj.Initialize();
                            return lockObj;
                        }
                    }
                }
            }
            await UnlockAll(a_resource, uniqueValue);
            await Delay();
        }
        return null;
    }

    /// <inheritdoc />
    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        if (!m_disposed)
        {
            m_disposed = true;
            await m_allServers.ForEachAsyncDispose();
            m_connectedServers.Clear();
        }
    }

    #endregion

}
