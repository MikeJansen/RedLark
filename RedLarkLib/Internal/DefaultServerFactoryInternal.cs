namespace RedLarkLib.Internal;

using RedLarkLib.Implementation;

public class DefaultServerFactoryInternal : IServerFactoryInternal
{
    IServerInternal IServerFactoryInternal.New(string a_connectionInfo)
    {
        return new Server(a_connectionInfo);
    }
}
