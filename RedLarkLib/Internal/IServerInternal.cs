namespace RedLarkLib.Internal;

public interface IServerInternal : IAsyncDisposable
{
    Task<bool> Connect();
    Task<bool> Lock(string a_resource, string a_uniqueValue, int a_ttl);
    Task Unlock(string a_resource, string a_uniqueValue);
    Task<bool> Renew(ILock a_lock, int a_ttl);
}
