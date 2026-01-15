namespace RedLarkLib.Internal;

/// <summary>
/// Internal interface for server connection operations.
/// Defines the contract for interacting with a single Redis server in the distributed lock cluster.
/// </summary>
/// <remarks>
/// <para>
/// This interface abstracts Redis operations to enable:
/// </para>
/// <list type="bullet">
///   <item><description>Unit testing with mock implementations</description></item>
///   <item><description>Potential support for different Redis clients</description></item>
///   <item><description>Clear separation of concerns between lock logic and Redis communication</description></item>
/// </list>
/// <para>
/// Each server instance maintains its own connection and can operate independently,
/// allowing parallel operations across multiple servers.
/// </para>
/// </remarks>
public interface IServerInternal : IAsyncDisposable
{
    /// <summary>
    /// Establishes a connection to the Redis server.
    /// </summary>
    /// <returns>True if connection succeeded, false if connection failed.</returns>
    /// <remarks>
    /// Connection failures are expected in distributed systems. The RedLark implementation
    /// handles partial connectivity by requiring only a quorum of servers to be available.
    /// </remarks>
    Task<bool> Connect();

    /// <summary>
    /// Attempts to acquire a lock on this Redis server.
    /// </summary>
    /// <param name="a_resource">The resource name to lock (will be prefixed with "dlm:").</param>
    /// <param name="a_uniqueValue">A unique token identifying the lock owner.</param>
    /// <param name="a_ttl">Time-to-live in milliseconds before the lock expires.</param>
    /// <returns>True if the lock was acquired, false if the resource is already locked.</returns>
    /// <remarks>
    /// Uses Redis SET with NX (only set if not exists) and PX (TTL in milliseconds)
    /// options for atomic lock acquisition.
    /// </remarks>
    Task<bool> Lock(string a_resource, string a_uniqueValue, int a_ttl);

    /// <summary>
    /// Releases a lock on this Redis server.
    /// </summary>
    /// <param name="a_resource">The resource name to unlock.</param>
    /// <param name="a_uniqueValue">The unique token that must match the current lock owner.</param>
    /// <returns>A task that completes when the unlock operation finishes.</returns>
    /// <remarks>
    /// Uses a Lua script to atomically verify ownership and delete the key.
    /// This prevents releasing a lock that has been acquired by another process.
    /// </remarks>
    Task Unlock(string a_resource, string a_uniqueValue);

    /// <summary>
    /// Renews (extends) a lock's TTL on this Redis server.
    /// </summary>
    /// <param name="a_lock">The lock to renew.</param>
    /// <param name="a_ttl">The new TTL in milliseconds.</param>
    /// <returns>True if the renewal succeeded, false if the lock is not owned or expired.</returns>
    /// <remarks>
    /// Uses a Lua script to atomically verify ownership and update the TTL.
    /// This ensures only the current lock owner can extend the lock duration.
    /// </remarks>
    Task<bool> Renew(ILock a_lock, int a_ttl);
}
