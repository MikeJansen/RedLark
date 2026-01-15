namespace RedLarkLib.Implementation;

using RedLarkLib;
using RedLarkLib.Internal;
using RedLarkLib.Testing;
using System.Diagnostics;

/// <summary>
/// Implementation of a distributed lock acquired through the RedLark lock manager.
/// Manages lock state, automatic renewal, and safe release.
/// </summary>
/// <remarks>
/// <para>
/// <b>Lock Lifecycle:</b>
/// </para>
/// <list type="number">
///   <item><description>
///     Lock is created by <see cref="RedLark"/> after successfully acquiring locks on a quorum of servers.
///   </description></item>
///   <item><description>
///     If auto-renewal is enabled (maxRenew > 0), a timer is started to renew the lock before expiration.
///   </description></item>
///   <item><description>
///     Lock remains valid until: explicitly unlocked, disposed, renewal fails, or max renewals exceeded.
///   </description></item>
///   <item><description>
///     On unlock/dispose, the lock is released on all Redis servers.
///   </description></item>
/// </list>
/// <para>
/// <b>Auto-Renewal Mechanism:</b>
/// </para>
/// <para>
/// When <c>maxRenew</c> is greater than 0, a timer schedules renewal attempts 50ms before
/// the current validity expires. Each successful renewal resets the validity window.
/// If renewal fails (cannot achieve quorum) or max renewals is exceeded, the lock is
/// aborted and the <c>onAbort</c> callback is invoked.
/// </para>
/// <para>
/// <b>Thread Safety:</b>
/// </para>
/// <para>
/// The lock uses a <see cref="SemaphoreSlim"/> to synchronize renewal and unlock operations,
/// ensuring that concurrent calls do not corrupt lock state.
/// </para>
/// </remarks>
public class Lock : ILock, ILockInternal, ILockTesting
{
    /// <summary>
    /// Buffer time (in ms) before validity expires to trigger renewal.
    /// Set to 50ms to allow time for the renewal operation to complete.
    /// </summary>
    private const int AUTO_RENEW_BUFFER_MS = 50;

    /// <summary>Callback invoked when the lock is aborted due to renewal failure.</summary>
    private readonly LockAbortDelegate? m_onAbort;

    /// <summary>Reference to the RedLark instance that manages this lock.</summary>
    private readonly IRedLarkInternal m_redlark;

    /// <summary>Number of servers that successfully acquired the lock.</summary>
    private readonly int m_serverLockCount;

    /// <summary>Current validity time remaining in milliseconds.</summary>
    private int m_validity;

    /// <summary>The resource name that is locked.</summary>
    private readonly string m_resource;

    /// <summary>Unique token identifying this lock instance.</summary>
    private readonly string m_uniqueValue;

    /// <summary>Original TTL in milliseconds.</summary>
    private readonly int m_ttl;

    /// <summary>Maximum number of automatic renewals allowed.</summary>
    private readonly int m_maxRenew;

    /// <summary>Current count of renewals performed.</summary>
    private int m_renewCount;

    /// <summary>Timestamp when validity was last calculated (for tracking).</summary>
    private long m_validityTime;

    /// <summary>Semaphore for synchronizing renewal and unlock operations.</summary>
    private readonly SemaphoreSlim m_lockSync = new(1, 1);

    /// <summary>Flag indicating whether the lock is currently held.</summary>
    private bool m_locked = true;

    /// <summary>Timer for scheduling automatic renewal operations.</summary>
    private Timer? m_renewTimer;

    /// <inheritdoc />
    string ILock.Resource { get { return m_resource; } }

    /// <inheritdoc />
    string ILock.UniqueValue { get { return m_uniqueValue; } }

    /// <inheritdoc />
    int ILock.Ttl { get { return m_ttl; } }

    /// <summary>Gets the renewal timer for testing purposes.</summary>
    Timer? ILockTesting.RenewTimer => m_renewTimer;

    /// <summary>Gets whether the lock is currently held for testing purposes.</summary>
    bool ILockTesting.IsLocked => m_locked;

    /// <summary>Gets the number of servers holding this lock for testing purposes.</summary>
    int ILockTesting.ServerLockCount => m_serverLockCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="Lock"/> class.
    /// </summary>
    /// <param name="a_redlark">The RedLark instance managing this lock.</param>
    /// <param name="a_serverLockCount">Number of servers that successfully acquired the lock.</param>
    /// <param name="a_validity">Initial validity time in milliseconds.</param>
    /// <param name="a_resource">The resource name being locked.</param>
    /// <param name="a_uniqueValue">Unique token for this lock instance.</param>
    /// <param name="a_ttl">TTL in milliseconds for the lock.</param>
    /// <param name="a_maxRenew">Maximum number of automatic renewals (0 to disable).</param>
    /// <param name="a_onAbort">Callback invoked if the lock is aborted.</param>
    public Lock(
        IRedLarkInternal a_redlark,
        int a_serverLockCount,
        int a_validity,
        string a_resource,
        string a_uniqueValue,
        int a_ttl,
        int a_maxRenew,
        LockAbortDelegate? a_onAbort)
    {
        m_redlark = a_redlark;
        m_serverLockCount = a_serverLockCount;
        m_validity = a_validity;
        m_resource = a_resource;
        m_uniqueValue = a_uniqueValue;
        m_ttl = a_ttl;
        m_maxRenew = a_maxRenew;
        m_renewCount = 0;
        m_validityTime = Stopwatch.GetTimestamp();
        m_onAbort = a_onAbort;
    }

    /// <summary>
    /// Initializes the lock after construction, starting the auto-renewal timer if enabled.
    /// </summary>
    /// <remarks>
    /// Called by <see cref="RedLark"/> after creating the lock. Separated from constructor
    /// to allow the lock to be fully constructed before timer starts.
    /// </remarks>
    void ILockInternal.Initialize()
    {
        if (m_maxRenew > 0)
        {
            var initialInterval = m_validity < AUTO_RENEW_BUFFER_MS ? 0 : m_validity - AUTO_RENEW_BUFFER_MS;
            m_renewTimer = new Timer(OnRenewTimer);
            m_renewTimer.Change(initialInterval, Timeout.Infinite);
        }
    }

    /// <summary>
    /// Timer callback that handles automatic lock renewal.
    /// </summary>
    /// <param name="state">Timer state (unused).</param>
    /// <remarks>
    /// This method:
    /// 1. Acquires the synchronization lock
    /// 2. Increments renewal count and checks if within limits
    /// 3. Attempts to renew the lock via RedLark
    /// 4. On success, reschedules the timer for the next renewal
    /// 5. On failure, unlocks and invokes the abort callback
    /// </remarks>
    private async void OnRenewTimer(object? state)
    {
        await m_lockSync.WaitAsync();
        try
        {
            m_renewCount++;
            m_validity = 0;
            if (m_renewCount <= m_maxRenew && m_locked)
            {
                m_validity = await m_redlark.Renew(this, m_ttl);
                if (m_validity > 0)
                {
                    m_renewTimer?.Change(m_validity - AUTO_RENEW_BUFFER_MS, Timeout.Infinite);
                    m_validityTime = Stopwatch.GetTimestamp();
                }
            }
            if (m_validity == 0)
            {
                await InternalUnlock();
                m_onAbort?.Invoke(this);
            }
        }
        finally
        {
            m_lockSync.Release();
        }
    }

    /// <inheritdoc />
    async Task ILock.Unlock()
    {
        if (m_locked)
        {
            await m_lockSync.WaitAsync();
            try
            {
                await InternalUnlock();
            }
            finally
            {
                m_lockSync.Release();
            }
        }
    }

    /// <summary>
    /// Internal unlock implementation that releases the lock without synchronization.
    /// Must be called while holding m_lockSync.
    /// </summary>
    private async Task InternalUnlock()
    {
        if (m_locked)
        {
            m_locked = false;
            if (m_renewTimer != null)
            {
                m_renewTimer.Dispose();
                m_renewTimer = null;
            }
            await m_redlark.Unlock(this);
        }
    }

    /// <inheritdoc />
    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await ((ILock)this).Unlock();
    }
}