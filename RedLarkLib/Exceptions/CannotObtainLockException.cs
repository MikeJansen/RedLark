namespace RedLarkLib.Exceptions;

/// <summary>
/// Exception thrown when the distributed lock manager cannot establish
/// connections to a quorum of Redis servers during the <see cref="IRedLark.Connect"/> call.
/// </summary>
/// <remarks>
/// <para>
/// A quorum requires (N/2 + 1) servers to be reachable, where N is the total number
/// of configured Redis servers. For example:
/// </para>
/// <list type="bullet">
///   <item><description>3 servers: at least 2 must be reachable</description></item>
///   <item><description>5 servers: at least 3 must be reachable</description></item>
///   <item><description>7 servers: at least 4 must be reachable</description></item>
/// </list>
/// <para>
/// When this exception is thrown, the <see cref="IRedLark"/> instance cannot be used
/// for lock operations. You should either:
/// </para>
/// <list type="bullet">
///   <item><description>Retry the connection after some delay</description></item>
///   <item><description>Check Redis server availability and network connectivity</description></item>
///   <item><description>Fail the application startup if distributed locking is critical</description></item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// try
/// {
///     await redlark.Connect();
/// }
/// catch (CannotObtainLockException)
/// {
///     // Not enough Redis servers are available
///     logger.Error("Unable to connect to Redis quorum. Distributed locking unavailable.");
///     throw;
/// }
/// </code>
/// </example>
public class CannotObtainLockException : Exception { }

