namespace RedLarkLib;

using RedLarkLib.Implementation;
using RedLarkLib.Internal;

/// <summary>
/// Default implementation of <see cref="IRedLarkFactory"/> that creates production-ready
/// <see cref="IRedLark"/> instances using the standard Redis server and lock implementations.
/// </summary>
/// <remarks>
/// <para>
/// This factory creates RedLark instances configured with:
/// </para>
/// <list type="bullet">
///   <item><description>Standard StackExchange.Redis connections to Redis servers</description></item>
///   <item><description>Default lock factory for creating lock instances</description></item>
///   <item><description>Quorum-based short-circuit optimization enabled by default</description></item>
/// </list>
/// <para>
/// For testing purposes, you can create mock implementations of <see cref="IRedLarkFactory"/>
/// or use the internal factory interfaces for fine-grained control.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Create a factory instance (can be registered as singleton in DI)
/// var factory = new DefaultRedLarkFactory();
/// 
/// // Create RedLark instance for your application
/// await using var redlark = factory.New(new[] {
///     "redis-node-1.internal:6379",
///     "redis-node-2.internal:6379",
///     "redis-node-3.internal:6379"
/// });
/// 
/// // Connect and start using
/// await redlark.Connect();
/// 
/// await using var myLock = await redlark.Lock("shared-resource", 30000);
/// if (myLock != null)
/// {
///     // Work with the locked resource
/// }
/// </code>
/// </example>
public class DefaultRedLarkFactory : IRedLarkFactory
{
    /// <summary>
    /// Default lock factory instance used to create Lock objects.
    /// Shared across all RedLark instances created by this factory.
    /// </summary>
    private static readonly ILockFactoryInternal sm_defaultLockFactoryInternal = new DefaultLockFactoryInternal();

    /// <summary>
    /// Default server factory instance used to create Server connection objects.
    /// Shared across all RedLark instances created by this factory.
    /// </summary>
    private static readonly IServerFactoryInternal sm_defaultServerFactoryInternal = new DefaultServerFactoryInternal();

    /// <inheritdoc />
    IRedLark IRedLarkFactory.New(IEnumerable<string> a_hosts, int? a_retryCount, int? a_retryDelayMin, int? a_retryDelayMax, string? a_name)
    {
        return new RedLark(sm_defaultServerFactoryInternal, sm_defaultLockFactoryInternal, 
            a_hosts, a_retryCount, a_retryDelayMin, a_retryDelayMax, a_name);
    }
}
