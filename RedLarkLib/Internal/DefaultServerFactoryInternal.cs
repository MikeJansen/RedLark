namespace RedLarkLib.Internal;

using RedLarkLib.Implementation;

/// <summary>
/// Default factory implementation for creating <see cref="Server"/> instances.
/// </summary>
/// <remarks>
/// Creates standard Server instances that use StackExchange.Redis for Redis communication.
/// Each server instance maintains its own connection to a Redis host.
/// </remarks>
public class DefaultServerFactoryInternal : IServerFactoryInternal
{
    /// <inheritdoc />
    IServerInternal IServerFactoryInternal.New(string a_connectionInfo)
    {
        return new Server(a_connectionInfo);
    }
}
