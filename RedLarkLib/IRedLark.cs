using RedLarkLib.Implementation;

namespace RedLarkLib;

/// <summary>
/// The main interface for the RedLark distributed lock manager (DLM).
/// Provides methods to connect to Redis servers and acquire distributed locks
/// using an improved version of the Redlock algorithm.
/// </summary>
/// <remarks>
/// <para>
/// RedLark implements the Redlock distributed locking algorithm with enhancements:
/// </para>
/// <list type="bullet">
///   <item><description>Parallel requests to multiple Redis masters for faster lock acquisition</description></item>
///   <item><description>Short-circuit optimization when a quorum is reached, avoiding unnecessary waits</description></item>
///   <item><description>Automatic retry with randomized backoff on lock failure</description></item>
///   <item><description>Optional automatic lock renewal to extend lock duration</description></item>
/// </list>
/// <para>
/// <b>Thread Safety:</b> Instances are designed to be shared across threads.
/// Multiple locks can be acquired concurrently from a single IRedLark instance.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Create and configure RedLark
/// var factory = new DefaultRedLarkFactory();
/// await using var redlark = factory.New(new[] {
///     "redis1.example.com:6379",
///     "redis2.example.com:6379", 
///     "redis3.example.com:6379"
/// });
/// 
/// // Connect to all Redis instances
/// await redlark.Connect();
/// 
/// // Acquire a lock with 30 second TTL
/// await using var lockObj = await redlark.Lock("my-resource", ttl: 30000);
/// if (lockObj != null)
/// {
///     // Critical section - protected by distributed lock
///     await DoWork();
/// }
/// </code>
/// </example>
public interface IRedLark: IAsyncDisposable
{
    /// <summary>
    /// Establishes connections to all configured Redis servers.
    /// Must be called before attempting to acquire any locks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method attempts to connect to all Redis servers provided during factory creation.
    /// A minimum quorum (N/2 + 1) of servers must be reachable for the connection to succeed.
    /// </para>
    /// <para>
    /// If the quorum cannot be established, a <see cref="Exceptions.CannotObtainLockException"/>
    /// is thrown and the instance cannot be used for locking.
    /// </para>
    /// </remarks>
    /// <exception cref="Exceptions.CannotObtainLockException">
    /// Thrown when unable to connect to a quorum of Redis servers.
    /// </exception>
    /// <returns>A task that completes when connections are established.</returns>
    Task Connect();

    /// <summary>
    /// Attempts to acquire a distributed lock on the specified resource.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The lock is acquired by setting a key with the resource name on a quorum
    /// of Redis servers. The lock includes:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>A unique token to identify the lock owner</description></item>
    ///   <item><description>A TTL (time-to-live) after which the lock expires</description></item>
    ///   <item><description>Optional automatic renewal to prevent expiration during long operations</description></item>
    /// </list>
    /// <para>
    /// The method will retry up to the configured retry count with randomized delays
    /// between attempts if the initial lock acquisition fails.
    /// </para>
    /// </remarks>
    /// <param name="a_resource">
    /// The name of the resource to lock. This should be a unique identifier for the
    /// shared resource being protected (e.g., "user:123:profile", "orders:processing").
    /// The actual Redis key will be prefixed with "dlm:" automatically.
    /// </param>
    /// <param name="a_ttl">
    /// Time-to-live in milliseconds. The lock will automatically expire after this duration.
    /// Must be at least 200ms. Recommended: Set based on expected operation duration plus buffer.
    /// </param>
    /// <param name="a_maxRenew">
    /// Maximum number of automatic renewals allowed. Set to 0 (default) to disable auto-renewal.
    /// When enabled, the lock will attempt to renew itself before expiration.
    /// </param>
    /// <param name="a_onAbort">
    /// Optional callback invoked if the lock cannot be renewed and is lost.
    /// Use this to handle lock loss gracefully (e.g., abort operations, rollback changes).
    /// </param>
    /// <returns>
    /// An <see cref="ILock"/> instance if the lock was successfully acquired, or <c>null</c>
    /// if the lock could not be obtained after all retry attempts.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if <see cref="Connect"/> has not been called successfully.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if <paramref name="a_ttl"/> is less than 200 milliseconds.
    /// </exception>
    /// <example>
    /// <code>
    /// // Simple lock acquisition
    /// await using var myLock = await redlark.Lock("resource-name", 30000);
    /// 
    /// // Lock with auto-renewal and abort handler
    /// await using var myLock = await redlark.Lock(
    ///     "resource-name",
    ///     ttl: 5000,
    ///     maxRenew: 10,
    ///     onAbort: (lock) => Console.WriteLine("Lock lost!")
    /// );
    /// </code>
    /// </example>
    Task<ILock?> Lock(string a_resource, int a_ttl, int a_maxRenew = 0, LockAbortDelegate? a_onAbort = null);
}
