namespace RedLarkLib.Internal;

/// <summary>
/// Internal interface for lock management operations.
/// Extends <see cref="ILock"/> with methods needed by the RedLark implementation
/// but not exposed to library consumers.
/// </summary>
/// <remarks>
/// This interface is used internally to allow <see cref="Implementation.RedLark"/>
/// to initialize locks after construction, enabling separation of construction
/// and activation for auto-renewal setup.
/// </remarks>
public interface ILockInternal: ILock
{
    /// <summary>
    /// Initializes the lock after construction, starting any automatic renewal timers.
    /// </summary>
    /// <remarks>
    /// This method should be called immediately after the lock is created by the factory.
    /// It sets up the auto-renewal timer if <c>maxRenew</c> was specified during lock acquisition.
    /// Separating initialization from construction allows the lock to be fully constructed
    /// before the timer starts, preventing potential race conditions.
    /// </remarks>
    void Initialize();
}
