namespace RedLarkLib.Implementation;

using RedLarkLib.Internal;
using StackExchange.Redis;

public class Server : IServerInternal
{
    #region Private

    private const string UNLOCK_SCRIPT = @"
        if redis.call(""get"",@key) == @value then
            return redis.call(""del"",@key)
        else
            return 0
        end";
    private const string RENEW_SCRIPT = @"
        if redis.call(""get"",@key == @value then
            return redis.call(""pexpire"",@key, @ttl)
        else
            return 0
        end";

    private readonly LuaScript m_unlockScript = LuaScript.Prepare(UNLOCK_SCRIPT);
    private readonly LuaScript m_renewScript = LuaScript.Prepare(RENEW_SCRIPT);

    private readonly string m_connectionInfo;
    private ConnectionMultiplexer? m_connection;
    private bool m_disposed;

    private IDatabase GetDb()
    {
        if (m_connection == null)
        {
            throw new InvalidOperationException("Not connected.");
        }

        return m_connection.GetDatabase();
    }

    #endregion

    #region Public

    public Server(string a_connectionInfo)
    {
        m_connectionInfo = a_connectionInfo;
    }

    async Task<bool> IServerInternal.Connect()
    {
        try
        {
            m_connection = await ConnectionMultiplexer.ConnectAsync(m_connectionInfo);
            return true;
        }
        catch (Exception)
        {
            //TODO log
            return false;
        }

    }
    async Task<bool> IServerInternal.Lock(string a_resource, string a_uniqueValue, int a_ttl)
    {
        try
        {
            var db = GetDb();
            var key = new RedisKey(RedLark.KEY_PREFIX + a_resource);
            var value = new RedisValue(a_uniqueValue);
            return await db.StringSetAsync(key, value, TimeSpan.FromMilliseconds(a_ttl), when: When.NotExists);
        }
        catch (Exception)
        {
            //TODO log
            return false;
        }
    }

    async Task IServerInternal.Unlock(string a_resource, string a_uniqueValue)
    {
        try
        {
            var db = GetDb();
            var result = await db.ScriptEvaluateAsync(m_unlockScript, new
            {
                key = (RedisKey)(RedLark.KEY_PREFIX + a_resource),
                value = a_uniqueValue
            });
        }
        catch
        {
            //TODO log
        }
    }

    async Task<bool> IServerInternal.Renew(ILock a_lock, int a_ttl)
    {
        try
        {
            var db = GetDb();
            var result = await db.ScriptEvaluateAsync(m_renewScript, new
            {
                key = (RedisKey)(RedLark.KEY_PREFIX + a_lock.Resource),
                value = a_lock.UniqueValue,
                ttl = a_lock.Ttl
            });
            return (bool?)result ?? false;
        }
        catch
        {
            //TODO log
            return false;
        }
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        if (!m_disposed)
        {
            m_disposed = true;
            if (m_connection != null)
            {
                await m_connection.DisposeAsync();
            }
        }
    }

    #endregion

}
