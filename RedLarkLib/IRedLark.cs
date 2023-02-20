using RedLarkLib.Implementation;

namespace RedLarkLib;

public interface IRedLark: IAsyncDisposable
{
    Task Connect();
    Task<ILock?> Lock(string a_resource, int a_ttl, int a_maxRenew = 0, LockAbortDelegate? a_onAbort = null);
}
