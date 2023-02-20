namespace RedLarkLib.Internal;

public interface IRedLarkInternal : IAsyncDisposable
{
    Task<int> Renew(ILock a_lock, int a_ttl);
    Task Unlock(ILock a_lock);

}
