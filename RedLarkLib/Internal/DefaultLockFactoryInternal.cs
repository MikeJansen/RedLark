using RedLarkLib.Implementation;

namespace RedLarkLib.Internal;

/// <summary>
/// Default factory implementation for creating <see cref="Lock"/> instances.
/// </summary>
/// <remarks>
/// Creates standard Lock instances that implement the full lock lifecycle including
/// auto-renewal support and safe unlock operations.
/// </remarks>
public class DefaultLockFactoryInternal : ILockFactoryInternal
{
    /// <inheritdoc />
    ILockInternal ILockFactoryInternal.New(IRedLarkInternal a_redlark, int a_serverLockCount, int a_validity, string a_resource, 
        string a_uniqueValue, int a_ttl, int a_maxRenew, LockAbortDelegate? a_onAbort)
    {
        return new Lock(a_redlark, a_serverLockCount, a_validity, a_resource, a_uniqueValue, a_ttl, a_maxRenew, a_onAbort);
    }
}
