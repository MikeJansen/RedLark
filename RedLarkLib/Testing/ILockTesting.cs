namespace RedLarkLib.Testing;

/// <summary>
/// Testing interface that exposes internal lock state for unit testing purposes.
/// Allows tests to verify lock behavior without relying solely on external observations.
/// </summary>
/// <remarks>
/// <para>
/// This interface is implemented by <see cref="Implementation.Lock"/> and provides
/// access to internal state that is useful for testing but should not be part of
/// the public API.
/// </para>
/// <para>
/// Cast a lock to this interface to access testing properties:
/// </para>
/// <code>
/// var lockObj = await redlark.Lock("resource", 1000);
/// var testing = (ILockTesting)lockObj;
/// Assert.True(testing.IsLocked);
/// </code>
/// </remarks>
public interface ILockTesting
{
    /// <summary>
    /// Gets the auto-renewal timer, or null if auto-renewal is disabled.
    /// </summary>
    /// <value>The Timer instance managing automatic renewals, or null.</value>
    /// <remarks>
    /// Useful for verifying that auto-renewal is properly configured and
    /// for testing timer disposal on unlock.
    /// </remarks>
    Timer? RenewTimer { get; }

    /// <summary>
    /// Gets whether the lock is currently held (not yet released).
    /// </summary>
    /// <value>True if the lock is active, false if it has been unlocked or aborted.</value>
    bool IsLocked { get; }

    /// <summary>
    /// Gets the number of Redis servers that successfully acquired the lock.
    /// </summary>
    /// <value>The count of servers holding this lock.</value>
    /// <remarks>
    /// This should always be at least the quorum count (N/2 + 1) for a valid lock.
    /// Useful for testing quorum behavior.
    /// </remarks>
    int ServerLockCount { get; }
}
