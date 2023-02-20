namespace RedLarkLib;

public delegate void LockAbortDelegate(ILock lockObj);

public interface ILock: IAsyncDisposable
{
    string Resource { get; }
    string UniqueValue { get; }
    int Ttl { get; }

    Task Unlock();
}
