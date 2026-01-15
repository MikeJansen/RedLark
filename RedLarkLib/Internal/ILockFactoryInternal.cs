using RedLarkLib.Implementation;

namespace RedLarkLib.Internal;

/// <summary>
/// Factory interface for creating lock instances.
/// Used internally by <see cref="RedLark"/> to create <see cref="ILockInternal"/> instances
/// after successful lock acquisition.
/// </summary>
/// <remarks>
/// <para>
/// This factory pattern enables:
/// </para>
/// <list type="bullet">
///   <item><description>Dependency injection of lock creation for testing</description></item>
///   <item><description>Potential customization of lock behavior</description></item>
///   <item><description>Clean separation between lock acquisition logic and lock instantiation</description></item>
/// </list>
/// </remarks>
public interface ILockFactoryInternal
{
    /// <summary>
    /// Creates a new lock instance with the specified parameters.
    /// </summary>
    /// <param name="a_redlark">The RedLark instance that will manage this lock.</param>
    /// <param name="a_serverLockCount">Number of servers that successfully acquired the lock.</param>
    /// <param name="a_validity">Remaining validity time in milliseconds.</param>
    /// <param name="a_resource">The resource name being locked.</param>
    /// <param name="a_uniqueValue">Unique token identifying this lock instance.</param>
    /// <param name="a_ttl">Original TTL in milliseconds.</param>
    /// <param name="a_maxRenew">Maximum number of automatic renewals allowed.</param>
    /// <param name="a_onAbort">Callback invoked if the lock is aborted.</param>
    /// <returns>A new lock instance ready to be initialized.</returns>
    ILockInternal New(
        IRedLarkInternal a_redlark,
        int a_serverLockCount,
        int a_validity,
        string a_resource,
        string a_uniqueValue,
        int a_ttl,
        int a_maxRenew,
        LockAbortDelegate? a_onAbort);
}
