using RedLarkLib.Implementation;

namespace RedLarkLib.Internal;

public interface ILockFactoryInternal
{
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
