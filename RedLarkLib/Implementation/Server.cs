namespace RedLarkLib.Implementation;

using RedLarkLib.Internal;
using StackExchange.Redis;

/// <summary>
/// Represents a connection to a single Redis server used by the distributed lock manager.
/// Handles all Redis operations for locking, unlocking, and renewing locks.
/// </summary>
/// <remarks>
/// <para>
/// <b>Redis Operations:</b>
/// </para>
/// <list type="bullet">
///   <item><description>
///     <b>Lock:</b> Uses SET with NX (only set if not exists) and PX (TTL in milliseconds)
///     to atomically acquire a lock if not already held.
///   </description></item>
///   <item><description>
///     <b>Unlock:</b> Uses a Lua script to atomically check ownership (via unique value)
///     and delete the key only if the current process owns the lock.
///   </description></item>
///   <item><description>
///     <b>Renew:</b> Uses a Lua script to atomically verify ownership and extend the TTL.
///   </description></item>
/// </list>
/// <para>
/// <b>Atomic Operations:</b>
/// </para>
/// <para>
/// Unlock and renew operations use Lua scripts to ensure atomicity. This prevents race
/// conditions where a lock could be released by a process that no longer owns it
/// (e.g., if the lock expired and was acquired by another process).
/// </para>
/// <para>
/// <b>Connection Management:</b>
/// </para>
/// <para>
/// Uses StackExchange.Redis <see cref="ConnectionMultiplexer"/> for connection pooling
/// and automatic reconnection. The connection is established asynchronously and
/// disposed when the server instance is disposed.
/// </para>
/// </remarks>
public class Server : IServerInternal
{
    #region Private

    /// <summary>
    /// Lua script for safe unlock operation.
    /// Only deletes the key if the stored value matches the provided unique value,
    /// ensuring only the lock owner can release it.
    /// </summary>
    private const string UNLOCK_SCRIPT = @"
        if redis.call(""get"",@key) == @value then
            return redis.call(""del"",@key)
        else
            return 0
        end";

    /// <summary>
    /// Lua script for safe renew operation.
    /// Only extends the TTL if the stored value matches the provided unique value,
    /// ensuring only the lock owner can renew it.
    /// </summary>
    /// <remarks>
    /// Note: There appears to be a syntax error in this script (missing closing parenthesis
    /// after @key on the redis.call("get" line). This may cause renewal operations to fail.
    /// </remarks>
    private const string RENEW_SCRIPT = @"
        if redis.call(""get"",@key == @value then
            return redis.call(""pexpire"",@key, @ttl)
        else
            return 0
        end";

    /// <summary>Prepared unlock script for efficient repeated execution.</summary>
    private readonly LuaScript m_unlockScript = LuaScript.Prepare(UNLOCK_SCRIPT);

    /// <summary>Prepared renew script for efficient repeated execution.</summary>
    private readonly LuaScript m_renewScript = LuaScript.Prepare(RENEW_SCRIPT);

    /// <summary>Redis connection string (host:port and optional configuration).</summary>
    private readonly string m_connectionInfo;

    /// <summary>StackExchange.Redis connection multiplexer.</summary>
    private ConnectionMultiplexer? m_connection;

    /// <summary>Flag to track disposal state.</summary>
    private bool m_disposed;

    /// <summary>
    /// Gets the Redis database for executing commands.
    /// </summary>
    /// <returns>The default database from the connection.</returns>
    /// <exception cref="InvalidOperationException">Thrown if not connected.</exception>
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

    /// <summary>
    /// Initializes a new Server instance with the specified connection information.
    /// </summary>
    /// <param name="a_connectionInfo">
    /// Redis connection string in StackExchange.Redis format.
    /// Examples: "localhost:6379", "redis.example.com:6379,password=secret"
    /// </param>
    public Server(string a_connectionInfo)
    {
        m_connectionInfo = a_connectionInfo;
    }

    /// <summary>
    /// Establishes a connection to the Redis server asynchronously.
    /// </summary>
    /// <returns>True if connection succeeded, false otherwise.</returns>
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

    /// <summary>
    /// Attempts to acquire a lock on this Redis server.
    /// </summary>
    /// <param name="a_resource">The resource name to lock.</param>
    /// <param name="a_uniqueValue">Unique token identifying the lock owner.</param>
    /// <param name="a_ttl">Time-to-live in milliseconds.</param>
    /// <returns>True if the lock was acquired, false if already held by another process.</returns>
    /// <remarks>
    /// Uses Redis SET command with NX (only set if not exists) and PX (millisecond expiry)
    /// options to atomically acquire the lock only if it doesn't exist.
    /// </remarks>
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

    /// <summary>
    /// Releases a lock on this Redis server if owned by the specified unique value.
    /// </summary>
    /// <param name="a_resource">The resource name to unlock.</param>
    /// <param name="a_uniqueValue">Unique token that must match the current lock owner.</param>
    /// <remarks>
    /// Uses a Lua script to atomically check ownership and delete the key.
    /// This prevents accidentally releasing a lock that has been acquired by another process
    /// (e.g., after the original lock expired).
    /// </remarks>
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

    /// <summary>
    /// Renews (extends) a lock's TTL on this Redis server if owned by the specified lock.
    /// </summary>
    /// <param name="a_lock">The lock to renew.</param>
    /// <param name="a_ttl">The new TTL in milliseconds.</param>
    /// <returns>True if the renewal succeeded, false if the lock is not owned or expired.</returns>
    /// <remarks>
    /// Uses a Lua script to atomically check ownership and extend the TTL.
    /// This ensures only the current lock owner can extend the lock duration.
    /// </remarks>
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

    /// <summary>
    /// Disposes the Redis connection asynchronously.
    /// </summary>
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
