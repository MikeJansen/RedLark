namespace RedLarkLib.Implementation;

using RedLarkLib.Exceptions;
using RedLarkLib.Internal;
using RedLarkLib.Testing;
using RedLarkLib.Utilities;
using System.Diagnostics;

public class RedLark : IRedLark, IRedLarkInternal, IRedLarkTesting
{
    #region Private 

    private static readonly Random m_random = new();

    private const int DEFAULT_RETRY_COUNT = 3;
    private const int DEFAULT_RETRY_DELAY_MIN = 100;
    private const int DEFAULT_RETRY_DELAY_MAX = 300;
    private const string DEFAULT_NAME = "default";
    private const float CLOCK_DRIFT_FACTOR = 0.01F;
    private readonly IServerFactoryInternal m_serverFactory;
    private readonly ILockFactoryInternal m_lockFactory;
    private readonly int m_retryCount;
    private readonly int m_retryDelayMin;
    private readonly int m_retryDelayMax;
    private readonly int m_retryDelayRange;
    private readonly string m_name;
    private readonly int m_quorum;
    private readonly IEnumerable<string> m_hosts;
    private readonly bool m_shortcircuitOnQuorum;
    private readonly List<IServerInternal> m_allServers;
    private readonly List<IServerInternal> m_connectedServers;

    private bool m_disposed;

    int IRedLarkTesting.ConnectServerCount => m_connectedServers.Count;

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

    private async Task UnlockAll(string a_resource, string a_uniqueValue)
    {
        if (m_connectedServers == null)
            throw new InvalidOperationException("Connect() has not been successfully called.");

        await m_connectedServers.ForEachExecAndWait(s => s.Unlock(a_resource, a_uniqueValue));
    }

    private async Task Delay()
    {
        int delay = m_retryDelayMin + m_random.Next(m_retryDelayRange);
        await Task.Delay(delay);
    }

    private static int GetDrift(int a_ttl)
    {
        return (int)(a_ttl * CLOCK_DRIFT_FACTOR) + 2;
    }

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

    async Task IRedLarkInternal.Unlock(ILock a_lock)
    {
        await UnlockAll(a_lock.Resource, a_lock.UniqueValue);
    }

    #endregion

    #region IRedLark

    public const string KEY_PREFIX = "dlm:";

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
