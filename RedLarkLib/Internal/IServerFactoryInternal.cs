namespace RedLarkLib.Internal;

using StackExchange.Redis;

/// <summary>
/// Factory interface for creating Redis server connection instances.
/// Used internally by <see cref="Implementation.RedLark"/> to create <see cref="IServerInternal"/>
/// instances for each configured Redis host.
/// </summary>
/// <remarks>
/// <para>
/// This factory pattern enables:
/// </para>
/// <list type="bullet">
///   <item><description>Dependency injection for testing with mock servers</description></item>
///   <item><description>Potential support for different Redis client implementations</description></item>
///   <item><description>Clean separation of server connection creation from lock logic</description></item>
/// </list>
/// </remarks>
public interface IServerFactoryInternal
{
    /// <summary>
    /// Creates a new server connection instance for the specified Redis host.
    /// </summary>
    /// <param name="a_connectionInfo">
    /// Redis connection string in StackExchange.Redis format.
    /// Examples: "localhost:6379", "redis.example.com:6379,password=secret,ssl=true"
    /// </param>
    /// <returns>A new server instance (not yet connected).</returns>
    /// <remarks>
    /// The returned server instance is not connected. Call <see cref="IServerInternal.Connect"/>
    /// to establish the connection.
    /// </remarks>
    IServerInternal New(string a_connectionInfo);
}
