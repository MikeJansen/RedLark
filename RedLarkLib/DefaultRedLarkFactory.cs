namespace RedLarkLib;

using RedLarkLib.Implementation;
using RedLarkLib.Internal;

public class DefaultRedLarkFactory : IRedLarkFactory
{
    private static readonly ILockFactoryInternal sm_defaultLockFactoryInternal = new DefaultLockFactoryInternal();
    private static readonly IServerFactoryInternal sm_defaultServerFactoryInternal = new DefaultServerFactoryInternal();

    IRedLark IRedLarkFactory.New(IEnumerable<string> a_hosts, int? a_retryCount, int? a_retryDelayMin, int? a_retryDelayMax, string? a_name)
    {
        return new RedLark(sm_defaultServerFactoryInternal, sm_defaultLockFactoryInternal, 
            a_hosts, a_retryCount, a_retryDelayMin, a_retryDelayMax, a_name);
    }
}
