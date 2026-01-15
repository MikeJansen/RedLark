namespace RedLarkLib;

/// <summary>
/// Factory interface for creating <see cref="IRedLark"/> distributed lock manager instances.
/// Use this factory to configure and instantiate RedLark with the desired Redis servers
/// and retry settings.
/// </summary>
/// <remarks>
/// <para>
/// The factory pattern allows for dependency injection and testability.
/// Use <see cref="DefaultRedLarkFactory"/> for production scenarios.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Using the factory to create a RedLark instance
/// IRedLarkFactory factory = new DefaultRedLarkFactory();
/// 
/// // Create with default settings
/// var redlark = factory.New(new[] {
///     "redis1:6379",
///     "redis2:6379",
///     "redis3:6379"
/// });
/// 
/// // Create with custom retry settings
/// var redlark = factory.New(
///     hosts: new[] { "redis1:6379", "redis2:6379", "redis3:6379" },
///     retryCount: 5,
///     retryDelayMin: 50,
///     retryDelayMax: 200,
///     name: "my-app-locks"
/// );
/// </code>
/// </example>
public interface IRedLarkFactory
{
    /// <summary>
    /// Creates a new <see cref="IRedLark"/> instance configured with the specified Redis hosts
    /// and optional retry parameters.
    /// </summary>
    /// <param name="a_hosts">
    /// A collection of Redis server connection strings. Should contain an odd number of hosts
    /// (typically 3, 5, or 7) for optimal quorum behavior. Connection strings can include
    /// host:port format (e.g., "redis.example.com:6379").
    /// </param>
    /// <param name="a_retryCount">
    /// Number of retry attempts when lock acquisition fails. Default is 3.
    /// Higher values increase the chance of eventual lock acquisition but may increase latency.
    /// </param>
    /// <param name="a_retryDelayMin">
    /// Minimum delay in milliseconds between retry attempts. Default is 100ms.
    /// The actual delay is randomized between min and max to prevent thundering herd.
    /// </param>
    /// <param name="a_retryDelayMax">
    /// Maximum delay in milliseconds between retry attempts. Default is 300ms.
    /// The actual delay is randomized between min and max to prevent thundering herd.
    /// </param>
    /// <param name="a_name">
    /// Optional name identifier for this RedLark instance. Useful for logging and debugging
    /// when multiple instances are in use. Default is "default".
    /// </param>
    /// <returns>
    /// A new <see cref="IRedLark"/> instance. The instance must have <see cref="IRedLark.Connect"/>
    /// called before it can be used to acquire locks.
    /// </returns>
    /// <example>
    /// <code>
    /// IRedLarkFactory factory = new DefaultRedLarkFactory();
    /// 
    /// // Minimal configuration
    /// var redlark = factory.New(new[] { "host1:6379", "host2:6379", "host3:6379" });
    /// 
    /// // Full configuration
    /// var redlark = factory.New(
    ///     a_hosts: new[] { "host1:6379", "host2:6379", "host3:6379" },
    ///     a_retryCount: 5,
    ///     a_retryDelayMin: 100,
    ///     a_retryDelayMax: 500,
    ///     a_name: "payment-service"
    /// );
    /// </code>
    /// </example>
    IRedLark New(IEnumerable<string> a_hosts, int? a_retryCount = null, int? a_retryDelayMin = null, int? a_retryDelayMax = null, string? a_name = null);
}
