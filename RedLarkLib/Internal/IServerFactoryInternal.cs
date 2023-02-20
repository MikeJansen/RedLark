namespace RedLarkLib.Internal;

using StackExchange.Redis;

public interface IServerFactoryInternal
{
    IServerInternal New(string a_connectionInfo);
}
