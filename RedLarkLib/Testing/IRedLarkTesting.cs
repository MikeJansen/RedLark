namespace RedLarkLib.Testing;

/// <summary>
/// Testing interface that exposes internal RedLark state for unit testing purposes.
/// Allows tests to verify connection and quorum behavior.
/// </summary>
/// <remarks>
/// <para>
/// This interface is implemented by <see cref="Implementation.RedLark"/> and provides
/// access to internal state that is useful for testing but should not be part of
/// the public API.
/// </para>
/// <para>
/// Cast a RedLark instance to this interface to access testing properties:
/// </para>
/// <code>
/// var redlark = factory.New(hosts);
/// await redlark.Connect();
/// var testing = (IRedLarkTesting)redlark;
/// Assert.Equal(3, testing.ConnectServerCount);
/// </code>
/// </remarks>
public interface IRedLarkTesting
{
    /// <summary>
    /// Gets the number of Redis servers that are currently connected.
    /// </summary>
    /// <value>The count of successfully connected servers.</value>
    /// <remarks>
    /// After a successful <see cref="IRedLark.Connect"/> call, this should be
    /// at least the quorum count. Useful for testing partial connectivity scenarios.
    /// </remarks>
    int ConnectServerCount { get; }
}
