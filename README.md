# RedLark - Distributed Lock Manager

A high-performance, Redis-based distributed lock manager (DLM) written in C#. Built on the [Redis Redlock](https://redis.com/redis-best-practices/communication-patterns/redlock/) algorithm with significant performance optimizations for modern distributed systems.

[![.NET](https://img.shields.io/badge/.NET-6.0-blue)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)

## Features

- **Parallel Lock Acquisition**: Sends lock requests to all Redis servers simultaneously
- **Quorum-Based Short-Circuiting**: Returns immediately when a quorum is achieved, without waiting for slower servers
- **Automatic Lock Renewal**: Optional auto-renewal prevents locks from expiring during long operations
- **Abort Callbacks**: Get notified when a lock is lost due to renewal failure
- **Retry with Randomized Backoff**: Prevents thundering herd problems during contention
- **Async/Await Native**: Fully asynchronous API designed for modern .NET applications

## Table of Contents

- [Installation](#installation)
- [Quick Start](#quick-start)
- [Configuration](#configuration)
- [Architecture](#architecture)
- [API Reference](#api-reference)
- [Advanced Usage](#advanced-usage)
- [Testing](#testing)
- [Best Practices](#best-practices)
- [Limitations](#limitations)

## Installation

RedLark uses [StackExchange.Redis](https://github.com/StackExchange/StackExchange.Redis) for Redis communication.

```bash
# Add the project reference or package
dotnet add package StackExchange.Redis
```

## Quick Start

```csharp
using RedLarkLib;

// Create a factory (can be registered as singleton in DI)
var factory = new DefaultRedLarkFactory();

// Create RedLark instance with Redis hosts
await using var redlark = factory.New(new[] {
    "redis1.example.com:6379",
    "redis2.example.com:6379",
    "redis3.example.com:6379"
});

// Connect to all Redis servers (establishes quorum)
await redlark.Connect();

// Acquire a distributed lock
await using var lockObj = await redlark.Lock("my-resource", ttl: 30000);

if (lockObj != null)
{
    // Critical section - only one instance holds this lock across your cluster
    await PerformCriticalOperation();
}
// Lock automatically released when disposed
```

## Configuration

### Factory Parameters

```csharp
IRedLark New(
    IEnumerable<string> hosts,      // Redis server connection strings
    int? retryCount = 3,            // Number of lock acquisition retries
    int? retryDelayMin = 100,       // Minimum retry delay (ms)
    int? retryDelayMax = 300,       // Maximum retry delay (ms)  
    string? name = "default"        // Instance identifier for logging
);
```

### Lock Parameters

```csharp
Task<ILock?> Lock(
    string resource,                 // Resource name to lock
    int ttl,                         // Time-to-live in milliseconds (min: 200)
    int maxRenew = 0,               // Max auto-renewals (0 = disabled)
    LockAbortDelegate? onAbort = null // Callback on lock loss
);
```

### Example Configurations

```csharp
// High-availability setup with longer TTL
var redlark = factory.New(
    hosts: new[] { "redis1:6379", "redis2:6379", "redis3:6379", "redis4:6379", "redis5:6379" },
    retryCount: 5,
    retryDelayMin: 50,
    retryDelayMax: 200,
    name: "payment-service"
);

// Low-latency setup
var redlark = factory.New(
    hosts: new[] { "redis1:6379", "redis2:6379", "redis3:6379" },
    retryCount: 3,
    retryDelayMin: 10,
    retryDelayMax: 50
);
```

## Architecture

### Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                      Application Code                            │
├─────────────────────────────────────────────────────────────────┤
│                    IRedLark / ILock                              │
│                   (Public Interfaces)                            │
├─────────────────────────────────────────────────────────────────┤
│                 DefaultRedLarkFactory                            │
│              (Creates RedLark Instances)                         │
├─────────────────────────────────────────────────────────────────┤
│                      RedLark                                     │
│         (Lock Manager - Quorum Coordination)                     │
├─────────────────────────────────────────────────────────────────┤
│                       Lock                                       │
│           (Lock Instance - Auto-Renewal)                         │
├─────────────────────────────────────────────────────────────────┤
│     Server          Server          Server                       │
│   (Redis Node 1)  (Redis Node 2)  (Redis Node 3)                │
└─────────────────────────────────────────────────────────────────┘
```

### Project Structure

```
RedLarkLib/
├── IRedLark.cs              # Main DLM interface
├── IRedLarkFactory.cs       # Factory interface
├── ILock.cs                 # Lock interface and delegate
├── DefaultRedLarkFactory.cs # Default factory implementation
├── Implementation/
│   ├── RedLark.cs           # Core lock manager logic
│   ├── Lock.cs              # Lock instance with auto-renewal
│   └── Server.cs            # Redis server connection wrapper
├── Internal/
│   ├── IRedLarkInternal.cs  # Internal RedLark operations
│   ├── ILockInternal.cs     # Internal lock operations
│   ├── IServerInternal.cs   # Server connection interface
│   ├── ILockFactoryInternal.cs
│   ├── IServerFactoryInternal.cs
│   └── Default*Internal.cs  # Default factory implementations
├── Utilities/
│   ├── SimpleUtils.cs       # Unique value generation
│   └── TaskExtensions.cs    # Parallel task execution helpers
├── Testing/
│   ├── ILockTesting.cs      # Test access to lock internals
│   └── IRedLarkTesting.cs   # Test access to RedLark internals
└── Exceptions/
    └── CannotObtainLockException.cs
```

### Redlock Algorithm Implementation

RedLark implements the Redlock distributed locking algorithm with these key steps:

1. **Get Unique Token**: Generate a random 20-character string to identify this lock acquisition

2. **Acquire Locks in Parallel**: Send SET NX PX commands to all Redis servers simultaneously

3. **Wait for Quorum**: Monitor responses as they arrive. A lock is acquired when:
   - At least N/2+1 servers accept the lock (quorum)
   - The remaining validity time (TTL - elapsed - drift) is positive

4. **Short-Circuit on Success**: Return immediately when quorum is achieved (don't wait for slower servers)

5. **Retry on Failure**: If quorum isn't achieved:
   - Release any partial locks
   - Wait a random delay (prevents thundering herd)
   - Retry up to `retryCount` times

6. **Safe Unlock**: Use Lua script to atomically verify ownership before deletion

### Key Improvements Over Standard Redlock

| Feature | Standard Redlock | RedLark |
|---------|------------------|---------|
| Server Communication | Sequential | Parallel |
| Quorum Detection | Wait for all | Short-circuit on quorum |
| Lock Renewal | Manual | Optional auto-renewal |
| Abort Handling | N/A | Callback support |

### Clock Drift Compensation

RedLark accounts for clock drift between servers:

```
drift = (TTL × 0.01) + 2ms
validity = TTL - elapsedTime - drift
```

This ensures locks remain valid even with slight clock differences between Redis servers.

## API Reference

### IRedLark

```csharp
public interface IRedLark : IAsyncDisposable
{
    // Connect to Redis servers (must be called before Lock)
    Task Connect();
    
    // Acquire a distributed lock
    Task<ILock?> Lock(string resource, int ttl, int maxRenew = 0, 
                      LockAbortDelegate? onAbort = null);
}
```

### ILock

```csharp
public interface ILock : IAsyncDisposable
{
    string Resource { get; }      // Locked resource name
    string UniqueValue { get; }   // Lock ownership token
    int Ttl { get; }              // Original TTL in ms
    
    Task Unlock();                // Explicitly release the lock
}
```

### LockAbortDelegate

```csharp
public delegate void LockAbortDelegate(ILock lockObj);
```

Called when a lock is automatically aborted (renewal failed or max renewals exceeded).

## Advanced Usage

### Auto-Renewal

For long-running operations, enable auto-renewal to prevent lock expiration:

```csharp
await using var lockObj = await redlark.Lock(
    "long-running-job",
    ttl: 5000,              // 5 second TTL
    maxRenew: 60,           // Up to 60 renewals (5 minutes total)
    onAbort: (lock) => {
        logger.Error($"Lost lock on {lock.Resource}!");
        cancellationSource.Cancel();
    }
);

if (lockObj != null)
{
    // This operation can run for up to 5 minutes
    // Lock will auto-renew every ~5 seconds
    await LongRunningOperation(cancellationSource.Token);
}
```

### Error Handling

```csharp
try
{
    await redlark.Connect();
}
catch (CannotObtainLockException)
{
    // Unable to connect to quorum of Redis servers
    logger.Error("Redis cluster unavailable");
    throw;
}

var lockObj = await redlark.Lock("resource", 30000);
if (lockObj == null)
{
    // Lock acquisition failed after all retries
    logger.Warn("Could not acquire lock - resource is busy");
    return;
}
```

### Dependency Injection

```csharp
// In Startup.cs or Program.cs
services.AddSingleton<IRedLarkFactory, DefaultRedLarkFactory>();
services.AddSingleton<IRedLark>(sp => {
    var factory = sp.GetRequiredService<IRedLarkFactory>();
    return factory.New(Configuration.GetSection("Redis:Hosts").Get<string[]>());
});

// In your service
public class MyService
{
    private readonly IRedLark _redlark;
    
    public MyService(IRedLark redlark)
    {
        _redlark = redlark;
    }
    
    public async Task Initialize()
    {
        await _redlark.Connect();
    }
}
```

## Testing

RedLark includes testing interfaces to verify lock behavior:

```csharp
using RedLarkLib.Testing;

// Access internal lock state
var lockObj = await redlark.Lock("test-resource", 1000);
var lockTesting = (ILockTesting)lockObj;

Assert.True(lockTesting.IsLocked);
Assert.Null(lockTesting.RenewTimer);  // Auto-renewal disabled
Assert.Equal(3, lockTesting.ServerLockCount);

// Access internal RedLark state
var redlarkTesting = (IRedLarkTesting)redlark;
Assert.Equal(3, redlarkTesting.ConnectServerCount);
```

### Mocking for Unit Tests

The internal factory interfaces allow mocking Redis servers:

```csharp
var serverMock = new Mock<IServerInternal>();
serverMock.Setup(s => s.Connect()).ReturnsAsync(true);
serverMock.Setup(s => s.Lock(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
          .ReturnsAsync(true);

var serverFactory = new Mock<IServerFactoryInternal>();
serverFactory.Setup(f => f.New(It.IsAny<string>())).Returns(serverMock.Object);

var redlark = new RedLark(serverFactory.Object, new DefaultLockFactoryInternal(), 
                          new[] { "host" });
```

## Best Practices

### Choosing TTL

- **Short TTL (1-5 seconds)**: Fast operations, quick lock release on failure
- **Long TTL (30+ seconds)**: Complex operations, less renewal overhead
- **Use auto-renewal**: For operations with unpredictable duration

### Number of Redis Servers

- **3 servers**: Tolerates 1 failure (recommended minimum)
- **5 servers**: Tolerates 2 failures (recommended for production)
- **7 servers**: Tolerates 3 failures (high availability)

### Resource Naming

```csharp
// Good: Specific, hierarchical names
await redlark.Lock("orders:processing:12345", ttl);
await redlark.Lock("user:preferences:user-id", ttl);

// Avoid: Generic names that might conflict
await redlark.Lock("lock", ttl);
await redlark.Lock("resource", ttl);
```

### Always Use Using Blocks

```csharp
// Good: Lock is guaranteed to be released
await using var lockObj = await redlark.Lock("resource", 30000);

// Avoid: Lock might not be released on exception
var lockObj = await redlark.Lock("resource", 30000);
// ... if exception occurs here, lock is held until TTL expires
await lockObj.Unlock();
```

## Limitations

- **Experimental Status**: This library is under development and not production-tested
- **No Persistence Guarantees**: Redis data loss can affect lock consistency
- **Clock Synchronization**: Requires reasonably synchronized clocks (NTP recommended)
- **Network Partitions**: Split-brain scenarios can cause temporary lock violations

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Contributing

Contributions are welcome! Please feel free to submit issues and pull requests.

## Acknowledgments

- [Redis Redlock Algorithm](https://redis.io/docs/manual/patterns/distributed-locks/)
- [StackExchange.Redis](https://github.com/StackExchange/StackExchange.Redis)
