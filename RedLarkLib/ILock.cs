namespace RedLarkLib;

/// <summary>
/// Delegate that is invoked when a lock is automatically aborted due to failure to renew.
/// This callback allows the application to handle lock loss gracefully, such as rolling back
/// operations or releasing resources that were protected by the lock.
/// </summary>
/// <param name="lockObj">The lock instance that was aborted.</param>
/// <example>
/// <code>
/// LockAbortDelegate onAbort = (lock) => {
///     Console.WriteLine($"Lock on {lock.Resource} was aborted!");
///     // Perform cleanup or rollback operations
/// };
/// 
/// var myLock = await redlark.Lock("my-resource", ttl: 30000, maxRenew: 5, onAbort: onAbort);
/// </code>
/// </example>
public delegate void LockAbortDelegate(ILock lockObj);

/// <summary>
/// Represents a distributed lock acquired through the RedLark distributed lock manager.
/// Implements <see cref="IAsyncDisposable"/> for automatic lock release when disposed.
/// </summary>
/// <remarks>
/// <para>
/// The lock is held across multiple Redis instances using the Redlock algorithm.
/// The lock automatically expires after the TTL (Time-To-Live) period unless renewed.
/// </para>
/// <para>
/// <b>Usage Pattern:</b>
/// Always use the lock within an <c>await using</c> block to ensure proper cleanup:
/// </para>
/// </remarks>
/// <example>
/// <code>
/// await using (var lockObj = await redlark.Lock("my-resource", 30000))
/// {
///     if (lockObj != null)
///     {
///         // Critical section - only one instance holds this lock
///         await PerformCriticalOperation();
///     }
/// }
/// // Lock is automatically released when exiting the using block
/// </code>
/// </example>
public interface ILock: IAsyncDisposable
{
    /// <summary>
    /// Gets the name of the resource that is locked.
    /// This is the unique identifier used to coordinate locking across distributed systems.
    /// </summary>
    /// <value>The resource name as specified when acquiring the lock.</value>
    string Resource { get; }

    /// <summary>
    /// Gets the unique value (token) that identifies this specific lock instance.
    /// This value is used to ensure that only the lock owner can release the lock,
    /// preventing accidental release of locks held by other processes.
    /// </summary>
    /// <value>A unique random string generated when the lock was acquired.</value>
    string UniqueValue { get; }

    /// <summary>
    /// Gets the Time-To-Live (TTL) in milliseconds for this lock.
    /// The lock will automatically expire on the Redis servers after this duration
    /// unless renewed. This ensures locks are eventually released even if the
    /// holding process crashes.
    /// </summary>
    /// <value>The TTL duration in milliseconds.</value>
    int Ttl { get; }

    /// <summary>
    /// Explicitly releases the distributed lock across all Redis instances.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method releases the lock on all Redis servers where it was acquired.
    /// It is safe to call multiple times; subsequent calls are no-ops.
    /// </para>
    /// <para>
    /// When using the lock within an <c>await using</c> block, this method is
    /// called automatically via <see cref="IAsyncDisposable.DisposeAsync"/>.
    /// </para>
    /// </remarks>
    /// <returns>A task that completes when the unlock operation finishes.</returns>
    Task Unlock();
}
