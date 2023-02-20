namespace RedLarkLib.Testing;

public interface ILockTesting
{
    Timer? RenewTimer { get; }
    bool IsLocked { get; }

    int ServerLockCount { get; }
}
