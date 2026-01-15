namespace RedLarkLib.Internal;

/// <summary>
/// Internal interface for RedLark operations needed by lock instances.
/// Provides methods for lock renewal and release that are called by <see cref="Implementation.Lock"/>.
/// </summary>
/// <remarks>
/// This interface separates the internal lock management operations from the public
/// <see cref="IRedLark"/> interface. It allows locks to communicate back with their
/// parent RedLark instance without exposing these methods to library consumers.
/// </remarks>
public interface IRedLarkInternal : IAsyncDisposable
{
    /// <summary>
    /// Attempts to renew a lock on all connected Redis servers.
    /// </summary>
    /// <param name="a_lock">The lock to renew.</param>
    /// <param name="a_ttl">The new TTL in milliseconds to set.</param>
    /// <returns>
    /// The remaining validity time in milliseconds if renewal succeeded on a quorum
    /// of servers, or 0 if renewal failed.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Renewal follows the same quorum-based approach as initial lock acquisition:
    /// </para>
    /// <list type="number">
    ///   <item><description>Send renewal requests to all servers in parallel</description></item>
    ///   <item><description>Wait for quorum of successful renewals</description></item>
    ///   <item><description>Calculate remaining validity (TTL - elapsed - drift)</description></item>
    ///   <item><description>If validity > 0, return it; otherwise unlock and return 0</description></item>
    /// </list>
    /// </remarks>
    Task<int> Renew(ILock a_lock, int a_ttl);

    /// <summary>
    /// Releases a lock on all connected Redis servers.
    /// </summary>
    /// <param name="a_lock">The lock to release.</param>
    /// <returns>A task that completes when unlock operations finish on all servers.</returns>
    /// <remarks>
    /// This method sends unlock requests to all servers where the lock may be held.
    /// The unlock is performed atomically on each server using a Lua script that
    /// verifies ownership before deletion.
    /// </remarks>
    Task Unlock(ILock a_lock);

}
