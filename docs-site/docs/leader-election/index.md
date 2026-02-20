---
sidebar_position: 6
title: Leader Election
description: Coordinate distributed workloads with leader election
---

# Leader Election

Leader election ensures only one instance in a distributed system performs a specific task at a time. Excalibur provides pluggable leader election for scenarios like background job processing, outbox publishing, and scheduled tasks.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required packages:
  ```bash
  dotnet add package Excalibur.LeaderElection
  dotnet add package Excalibur.LeaderElection.SqlServer  # or Redis provider
  ```
- A distributed storage backend (SQL Server or Redis) for lease management
- Familiarity with [Dispatch hosting](../deployment/index.md)

## Packages

| Package | Purpose |
|---------|---------|
| `Excalibur.Dispatch.LeaderElection.Abstractions` | Core interfaces: `ILeaderElection`, `ILeaderElectionFactory`, `IHealthBasedLeaderElection` |
| `Excalibur.LeaderElection` | Registration, telemetry decorator, and health check |
| `Excalibur.LeaderElection.SqlServer` | SQL Server-based leader election |
| `Excalibur.LeaderElection.Redis` | Redis-based leader election |
| `Excalibur.LeaderElection.Consul` | Consul-based leader election |
| `Excalibur.LeaderElection.Kubernetes` | Kubernetes lease-based leader election |
| `Excalibur.LeaderElection.InMemory` | In-memory leader election (testing/development) |

## When to Use Leader Election

| Scenario | Why Leader Election |
|----------|---------------------|
| Outbox message publishing | Prevent duplicate message sends |
| Scheduled job processing | Run cron jobs exactly once |
| Cache warming | Single instance warms cache |
| Event projection updates | Prevent duplicate projections |
| Singleton background services | Only one active instance |

## Core Concepts

### The Leader Election Pattern

```
Instance A ──┬── Acquires Lock ──▶ Becomes Leader ──▶ Processes Work
             │
Instance B ──┼── Waits ───────────▶ Standby ─────────▶ Ready to Take Over
             │
Instance C ──┴── Waits ───────────▶ Standby ─────────▶ Ready to Take Over

If Instance A fails:
Instance B ──▶ Acquires Lock ──▶ Becomes Leader ──▶ Processes Work
```

### Leader Responsibilities

1. **Acquire Leadership**: Obtain exclusive lock
2. **Renew Leadership**: Keep lock alive with heartbeats
3. **Perform Work**: Execute the singleton workload
4. **Release Leadership**: Clean up on shutdown

## The ILeaderElection Interface

```csharp
public interface ILeaderElection
{
    /// <summary>
    /// Event raised when this instance becomes the leader.
    /// </summary>
    event EventHandler<LeaderElectionEventArgs>? OnBecameLeader;

    /// <summary>
    /// Event raised when this instance loses leadership.
    /// </summary>
    event EventHandler<LeaderElectionEventArgs>? OnLostLeadership;

    /// <summary>
    /// Event raised when the leader changes (any instance).
    /// </summary>
    event EventHandler<LeaderChangedEventArgs>? OnLeaderChanged;

    /// <summary>
    /// Gets the unique identifier for this election participant.
    /// </summary>
    string CandidateId { get; }

    /// <summary>
    /// Gets a value indicating whether this instance is currently the leader.
    /// </summary>
    bool IsLeader { get; }

    /// <summary>
    /// Gets the current leader's identifier.
    /// </summary>
    string? CurrentLeaderId { get; }

    /// <summary>
    /// Starts participating in leader election.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stops participating and relinquishes leadership if held.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken);
}
```

## Basic Usage

### Event-Based Leadership Pattern

```csharp
public class OutboxProcessor : BackgroundService
{
    private readonly ILeaderElection _leaderElection;
    private readonly IOutboxService _outbox;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(
        ILeaderElection leaderElection,
        IOutboxService outbox,
        ILogger<OutboxProcessor> logger)
    {
        _leaderElection = leaderElection;
        _outbox = outbox;
        _logger = logger;

        // Subscribe to leadership events
        _leaderElection.OnBecameLeader += (_, args) =>
            _logger.LogInformation("Became leader: {CandidateId}", args.CandidateId);

        _leaderElection.OnLostLeadership += (_, args) =>
            _logger.LogInformation("Lost leadership: {CandidateId}", args.CandidateId);
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Start participating in leader election
        await _leaderElection.StartAsync(ct);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                // Only process if we're the leader
                if (_leaderElection.IsLeader)
                {
                    await _outbox.ProcessPendingMessagesAsync(ct);
                }

                await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }
        }
        finally
        {
            // Stop participating on shutdown
            await _leaderElection.StopAsync(CancellationToken.None);
        }
    }
}
```

### Scheduled Job Runner

```csharp
public class ScheduledJobRunner : BackgroundService
{
    private readonly ILeaderElection _leaderElection;
    private readonly ILogger<ScheduledJobRunner> _logger;

    public ScheduledJobRunner(
        ILeaderElection leaderElection,
        ILogger<ScheduledJobRunner> logger)
    {
        _leaderElection = leaderElection;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await _leaderElection.StartAsync(ct);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (_leaderElection.IsLeader)
                {
                    _logger.LogInformation("Running scheduled jobs as leader");
                    await RunScheduledJobsAsync(ct);
                }

                await Task.Delay(TimeSpan.FromMinutes(1), ct);
            }
        }
        finally
        {
            await _leaderElection.StopAsync(CancellationToken.None);
            _logger.LogInformation("Stopped leader election");
        }
    }

    private Task RunScheduledJobsAsync(CancellationToken ct)
    {
        // Execute scheduled jobs
        return Task.CompletedTask;
    }
}
```

## Implementations

### SQL Server Leader Election

Uses database locks for coordination:

```csharp
// Installation
dotnet add package Excalibur.LeaderElection.SqlServer

// Configuration
builder.Services.AddSqlServerLeaderElection(
    connectionString,
    "my-app-leader",  // Lock resource name
    options =>
    {
        options.LeaseDuration = TimeSpan.FromSeconds(30);
        options.RenewInterval = TimeSpan.FromSeconds(10);
    });
```

SQL Server implementation features:
- Uses `sp_getapplock` for distributed locking
- Automatic heartbeat renewal
- Graceful handoff on shutdown
- Works with existing SQL Server infrastructure

### Redis Leader Election

Uses Redis for high-performance coordination:

```csharp
// Installation
dotnet add package Excalibur.LeaderElection.Redis

// First register Redis connection
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect("localhost:6379"));

// Configuration
builder.Services.AddRedisLeaderElection(
    "myapp:leader",  // Redis lock key
    options =>
    {
        options.LeaseDuration = TimeSpan.FromSeconds(30);
        options.RenewInterval = TimeSpan.FromSeconds(10);
    });
```

Redis implementation features:
- Uses `SET NX EX` for atomic lock acquisition
- Lua scripts for atomic operations
- Low latency lock acquisition
- Suitable for high-frequency leadership checks

### Consul Leader Election

Uses Consul sessions for coordination:

```csharp
// Installation
dotnet add package Excalibur.LeaderElection.Consul

// Configuration
builder.Services.AddConsulLeaderElection(options =>
{
    options.ConsulAddress = "http://localhost:8500";
    options.SessionTTL = TimeSpan.FromSeconds(30);
});

// Register a singleton election for a specific resource
builder.Services.AddConsulLeaderElectionForResource("my-processor");
```

### Kubernetes Leader Election

Uses Kubernetes Lease objects for coordination:

```csharp
// Installation
dotnet add package Excalibur.LeaderElection.Kubernetes

// Configuration with hosted service
builder.Services.AddExcaliburKubernetesLeaderElectionHostedService(
    "my-processor",
    options =>
    {
        options.LeaseDurationSeconds = 15;
        options.RenewIntervalMilliseconds = 10_000;
    });
```

Kubernetes implementation features:
- Uses native Kubernetes Lease objects
- Auto-detects in-cluster vs local kubeconfig
- Integrates as a hosted service for automatic lifecycle management

### In-Memory (Testing)

For unit tests and local development:

```csharp
builder.Services.AddInMemoryLeaderElection();
```

:::note
`AddInMemoryLeaderElection` registers `ILeaderElectionFactory`, not `ILeaderElection` directly.
Use the factory to create election instances for specific resources:

```csharp
var factory = serviceProvider.GetRequiredService<ILeaderElectionFactory>();
var election = factory.CreateElection("my-resource");
```
:::

## Configuration Options

```csharp
public class LeaderElectionOptions
{
    /// <summary>
    /// How long a lease is valid before it expires.
    /// </summary>
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// How often to renew the lease (should be less than LeaseDuration).
    /// </summary>
    public TimeSpan RenewInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Retry interval when not leader.
    /// </summary>
    public TimeSpan RetryInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Grace period before declaring leadership lost.
    /// </summary>
    public TimeSpan GracePeriod { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Unique identifier for this instance.
    /// </summary>
    public string InstanceId { get; set; } = Environment.MachineName;

    /// <summary>
    /// Enable health-based leader election.
    /// </summary>
    public bool EnableHealthChecks { get; set; } = true;

    /// <summary>
    /// Minimum health score (0.0 to 1.0) required to become or remain leader.
    /// </summary>
    public double MinimumHealthScore { get; set; } = 0.8;

    /// <summary>
    /// Automatically step down when health drops below MinimumHealthScore.
    /// </summary>
    public bool StepDownWhenUnhealthy { get; set; } = true;

    /// <summary>
    /// Custom metadata for this candidate (e.g., region, version).
    /// </summary>
    public IDictionary<string, string> CandidateMetadata { get; } = new Dictionary<string, string>();
}
```

## Patterns and Best Practices

### Graceful Shutdown

Release leadership cleanly on application shutdown:

```csharp
public class LeaderAwareService : BackgroundService
{
    private readonly ILeaderElection _leaderElection;
    private readonly ILogger<LeaderAwareService> _logger;

    public LeaderAwareService(
        ILeaderElection leaderElection,
        ILogger<LeaderAwareService> logger)
    {
        _leaderElection = leaderElection;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await _leaderElection.StartAsync(ct);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (_leaderElection.IsLeader)
                {
                    await DoWorkAsync(ct);
                }

                await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }
        }
        finally
        {
            // Always stop on shutdown to release leadership
            await _leaderElection.StopAsync(CancellationToken.None);
            _logger.LogInformation("Released leadership on shutdown");
        }
    }
}
```

### Handling Leadership Loss

React appropriately when leadership is lost using events:

```csharp
public class LeadershipAwareProcessor : BackgroundService
{
    private readonly ILeaderElection _leaderElection;
    private readonly IWorkQueue _queue;
    private volatile bool _isLeader;

    public LeadershipAwareProcessor(ILeaderElection leaderElection, IWorkQueue queue)
    {
        _leaderElection = leaderElection;
        _queue = queue;

        // Track leadership state via events
        _leaderElection.OnBecameLeader += (_, _) => _isLeader = true;
        _leaderElection.OnLostLeadership += (_, _) => _isLeader = false;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await _leaderElection.StartAsync(ct);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (_isLeader)
                {
                    var item = await _queue.DequeueAsync(ct);

                    // Check leadership before processing
                    if (!_isLeader)
                    {
                        await _queue.RequeueAsync(item);
                        continue;
                    }

                    await ProcessItemAsync(item, ct);
                }
                else
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), ct);
                }
            }
        }
        finally
        {
            await _leaderElection.StopAsync(CancellationToken.None);
        }
    }
}
```

### Multiple Leader Elections

Use factory for multiple independent elections:

```csharp
// Register factory for multiple lock resources
builder.Services.AddSqlServerLeaderElectionFactory(connectionString);

public class MultiResourceLeaderService : BackgroundService
{
    private readonly ILeaderElectionFactory _factory;
    private readonly List<ILeaderElection> _elections = new();

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Create separate leader elections for each resource
        var outboxElection = _factory.CreateElection("outbox-processor");
        var projectorElection = _factory.CreateElection("event-projector");
        var cleanupElection = _factory.CreateElection("cleanup-job");

        _elections.AddRange(new[] { outboxElection, projectorElection, cleanupElection });

        // Start all elections
        await Task.WhenAll(_elections.Select(e => e.StartAsync(ct)));

        try
        {
            while (!ct.IsCancellationRequested)
            {
                // Process each workload if we're leader for it
                if (outboxElection.IsLeader)
                    await ProcessOutboxAsync(ct);

                if (projectorElection.IsLeader)
                    await ProcessProjectionsAsync(ct);

                if (cleanupElection.IsLeader)
                    await RunCleanupAsync(ct);

                await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }
        }
        finally
        {
            await Task.WhenAll(_elections.Select(e => e.StopAsync(CancellationToken.None)));
        }
    }
}
```

### Health Checks

A built-in health check is provided in the `Excalibur.LeaderElection` package. Register it with the standard ASP.NET Core health checks builder:

```csharp
builder.Services.AddSqlServerLeaderElection(connectionString, "my-resource");

builder.Services.AddHealthChecks()
    .AddLeaderElectionHealthCheck();
```

The built-in `LeaderElectionHealthCheck` reports:
- **Healthy**: This instance is the leader, or a valid leader is observed
- **Degraded**: No leader is detected, but the service is running
- **Unhealthy**: An exception occurs when querying leader election state

It is provider-agnostic and works with any `ILeaderElection` implementation (SQL Server, Redis, Consul, Kubernetes).

## Common Use Cases

### Outbox Pattern

Ensure only one instance publishes outbox messages:

```csharp
public class OutboxPublisher : BackgroundService
{
    private readonly ILeaderElection _leaderElection;
    private readonly IOutboxStore _outbox;
    private readonly IMessageBus _messageBus;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await _leaderElection.StartAsync(ct);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (_leaderElection.IsLeader)
                {
                    var messages = await _outbox.GetPendingAsync(100, ct);

                    foreach (var message in messages)
                    {
                        await _messageBus.PublishAsync(message, ct);
                        await _outbox.MarkAsPublishedAsync(message.Id, ct);
                    }
                }

                await Task.Delay(TimeSpan.FromMilliseconds(100), ct);
            }
        }
        finally
        {
            await _leaderElection.StopAsync(CancellationToken.None);
        }
    }
}
```

### Scheduled Tasks

Run scheduled jobs on exactly one instance:

```csharp
public class DailyReportGenerator : BackgroundService
{
    private readonly ILeaderElection _leaderElection;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await _leaderElection.StartAsync(ct);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                // Wait until midnight
                var now = DateTime.UtcNow;
                var nextRun = now.Date.AddDays(1);
                await Task.Delay(nextRun - now, ct);

                // Only the leader generates the report
                if (_leaderElection.IsLeader)
                {
                    await GenerateDailyReportAsync(ct);
                }
            }
        }
        finally
        {
            await _leaderElection.StopAsync(CancellationToken.None);
        }
    }
}
```

### Event Projection Processing

Single instance processes event projections:

```csharp
public class ProjectionWorker : BackgroundService
{
    private readonly ILeaderElection _leaderElection;
    private readonly IEventStore _eventStore;
    private readonly ICheckpointStore _checkpointStore;
    private readonly IEnumerable<IProjectionHandler> _projections;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await _leaderElection.StartAsync(ct);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (_leaderElection.IsLeader)
                {
                    var position = await _checkpointStore.GetPositionAsync("main", ct);

                    await foreach (var @event in _eventStore.ReadAllAsync(position, ct))
                    {
                        // Check leadership before each event
                        if (!_leaderElection.IsLeader)
                            break;

                        foreach (var projection in _projections)
                        {
                            await projection.HandleAsync(@event, ct);
                        }

                        await _checkpointStore.SavePositionAsync("main", @event.Position, ct);
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }
        }
        finally
        {
            await _leaderElection.StopAsync(CancellationToken.None);
        }
    }
}
```

## Health-Based Elections

`IHealthBasedLeaderElection` extends standard elections with health awareness. Unhealthy leaders automatically step down, and only healthy candidates can become leader.

```csharp
var election = _electionFactory.CreateHealthBasedElection(
    resourceName: "critical-processor");

await election.StartAsync(ct);

// Report health status
await election.UpdateHealthAsync(
    isHealthy: true,
    metadata: new Dictionary<string, string>
    {
        ["cpu"] = "45%",
        ["memory"] = "2.1GB",
        ["queue_depth"] = "12"
    });

// Query all candidates' health
var candidates = await election.GetCandidateHealthAsync(ct);

foreach (var candidate in candidates)
{
    // candidate.CandidateId
    // candidate.IsHealthy
    // candidate.HealthScore (0.0 to 1.0)
    // candidate.IsLeader
    // candidate.LastUpdated
    // candidate.Metadata
}
```

### CandidateHealth

Each candidate exposes health information:

| Property | Type | Description |
|----------|------|-------------|
| `CandidateId` | `string` | Unique candidate identifier |
| `IsHealthy` | `bool` | Whether the candidate is healthy |
| `HealthScore` | `double` | Score from 0.0 to 1.0 |
| `IsLeader` | `bool` | Whether this candidate is the current leader |
| `LastUpdated` | `DateTimeOffset` | When health was last reported |
| `Metadata` | `IDictionary<string, string>` | Custom health metadata |

## Troubleshooting

### Lock Not Released

If a process crashes without releasing the lock, the lease will automatically expire after `LeaseDuration`. Another instance will then acquire leadership.

### Split Brain

Configure appropriate timeouts to prevent split brain:

```csharp
builder.Services.AddSqlServerLeaderElection(
    connectionString,
    "my-resource",
    options =>
    {
        // Lease duration must be longer than renewal interval
        options.LeaseDuration = TimeSpan.FromSeconds(30);
        options.RenewInterval = TimeSpan.FromSeconds(10);

        // Account for network latency and clock skew
        // Renewal should happen at least 2-3 times before expiry
    });
```

## Next Steps

- **[Event Sourcing](../event-sourcing/index.md)** - Use with event projections
- **[CQRS](../cqrs/index.md)** - Coordinate read model updates
- **[Dispatch Introduction](/docs/intro)** - Background service patterns

## See Also

- [Kubernetes Deployment](../deployment/kubernetes.md) - Kubernetes deployment with leader election
- [Resilience with Polly](../operations/resilience-polly.md) - Circuit breakers and retry policies
- [Patterns Overview](../patterns/index.md) - Architectural patterns for distributed systems

