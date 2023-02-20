namespace RedLarkLib.Implementation;

using RedLarkLib;
using RedLarkLib.Internal;
using RedLarkLib.Testing;
using System.Diagnostics;

public class Lock : ILock, ILockInternal, ILockTesting
{
    private const int AUTO_RENEW_BUFFER_MS = 50;

    private readonly LockAbortDelegate? m_onAbort;
    private readonly IRedLarkInternal m_redlark;
    private readonly int m_serverLockCount;
    private int m_validity;
    private readonly string m_resource;
    private readonly string m_uniqueValue;
    private readonly int m_ttl;
    private readonly int m_maxRenew;
    private int m_renewCount;
    private long m_validityTime;
    private readonly SemaphoreSlim m_lockSync = new(1, 1);
    private bool m_locked = true;
    private Timer? m_renewTimer;

    string ILock.Resource { get { return m_resource; } }
    string ILock.UniqueValue { get { return m_uniqueValue; } }
    int ILock.Ttl { get { return m_ttl; } }

    Timer? ILockTesting.RenewTimer => m_renewTimer;
    bool ILockTesting.IsLocked => m_locked;
    int ILockTesting.ServerLockCount => m_serverLockCount;

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

    void ILockInternal.Initialize()
    {
        if (m_maxRenew > 0)
        {
            var initialInterval = m_validity < AUTO_RENEW_BUFFER_MS ? 0 : m_validity - AUTO_RENEW_BUFFER_MS;
            m_renewTimer = new Timer(OnRenewTimer);
            m_renewTimer.Change(initialInterval, Timeout.Infinite);
        }
    }

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

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await ((ILock)this).Unlock();
    }
}