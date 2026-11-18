# Excalibur.Dispatch.LeaderElection.Abstractions

Core abstractions for distributed leader election in .NET applications. See the selected package's target frameworks for deployment compatibility.

## Overview

This package provides the foundational interfaces and types for implementing leader election patterns in distributed systems. It contains **no implementation** - install a provider package (Consul, Kubernetes, or InMemory) for concrete functionality.

**Key Features**:
- **Provider abstraction** - Share application-facing contracts; verify provider-specific failure and recovery behavior when changing providers
- **Event-driven** - React to leadership changes via events
- **Health-aware** - Optional health-based leadership with automatic step-down
- **NativeAOT verification required** - The project enables AOT compatibility analysis; publish and execute the selected application and provider paths before claiming native compatibility
- **No provider dependency** - References Dispatch abstractions and Microsoft.Extensions dependency injection, logging and options packages; install the selected provider separately

---

## Installation

```bash
dotnet add package Excalibur.Dispatch.LeaderElection.Abstractions
```

This package alone provides **no functionality**. You must also install a provider:

### Choose a Provider

| Provider | Package | Use Case | Native Verification |
|----------|---------|----------|-------------|
| **Consul** | `Excalibur.Dispatch.LeaderElection.Consul` | Deployments with HashiCorp Consul | Verify selected provider/dependency paths |
| **Kubernetes** | `Excalibur.Dispatch.LeaderElection.Kubernetes` | Apps running in Kubernetes clusters | Verify selected provider/dependency paths |
| **InMemory** | `Excalibur.Dispatch.LeaderElection.InMemory` | Unit/integration testing, development | Verify selected native application; not a distributed-provider substitute |

**Example**: Install Consul provider

```bash
dotnet add package Excalibur.Dispatch.LeaderElection.Abstractions
dotnet add package Excalibur.Dispatch.LeaderElection.Consul
```

---

## Core Interfaces

### ILeaderElection

The primary interface for participating in leader election.

```csharp
public interface ILeaderElection
{
    // Events
    event EventHandler<LeaderElectionEventArgs>? OnBecameLeader;
    event EventHandler<LeaderElectionEventArgs>? OnLostLeadership;
    event EventHandler<LeaderChangedEventArgs>? OnLeaderChanged;

    // Properties
    string CandidateId { get; }
    bool IsLeader { get; }
    string? CurrentLeaderId { get; }

    // Methods
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}
```

**Usage Pattern**:

```csharp
public class MyService : BackgroundService
{
    private readonly ILeaderElection _election;

    public MyService(ILeaderElectionFactory factory)
    {
        _election = factory.CreateElection("my-resource");
        _election.OnBecameLeader += OnBecameLeader;
        _election.OnLostLeadership += OnLostLeadership;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _election.StartAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (_election.IsLeader)
            {
                // Perform leader-only work
                await DoLeaderWorkAsync();
            }
            else
            {
                // Perform follower work (or idle)
                await Task.Delay(1000, stoppingToken);
            }
        }

        await _election.StopAsync();
    }

    private void OnBecameLeader(object? sender, LeaderElectionEventArgs e)
    {
        Console.WriteLine($"This instance ({e.CandidateId}) became the leader!");
        // Initialize leader-specific resources
    }

    private void OnLostLeadership(object? sender, LeaderElectionEventArgs e)
    {
        Console.WriteLine($"This instance ({e.CandidateId}) lost leadership");
        // Clean up leader-specific resources
    }

    private async Task DoLeaderWorkAsync()
    {
        // Example: Process pending jobs, update aggregates, etc.
        await Task.Delay(100);
    }
}
```

---

### IHealthBasedLeaderElection

Extends leader election with health awareness - leaders can step down when unhealthy.

```csharp
public interface IHealthBasedLeaderElection : ILeaderElection
{
    Task UpdateHealthAsync(bool isHealthy, IDictionary<string, string>? metadata, CancellationToken cancellationToken);
    Task<IEnumerable<CandidateHealth>> GetCandidateHealthAsync(CancellationToken cancellationToken);
}
```

**Usage Pattern**:

```csharp
public class HealthMonitoredService : BackgroundService
{
    private readonly IHealthBasedLeaderElection _election;

    public HealthMonitoredService(ILeaderElectionFactory factory)
    {
        _election = factory.CreateHealthBasedElection("health-aware-resource");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _election.StartAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            // Continuously monitor health
            bool isHealthy = await CheckApplicationHealthAsync();

            // Update health status with metadata
            await _election.UpdateHealthAsync(isHealthy, new Dictionary<string, string>
            {
                ["cpu_usage"] = "65%",
                ["memory_usage"] = "80%",
                ["disk_space"] = "healthy"
            }, cancellationToken);

            // If unhealthy and leader, will automatically step down
            if (!isHealthy && _election.IsLeader)
            {
                Console.WriteLine("Unhealthy leader - stepping down automatically");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task<bool> CheckApplicationHealthAsync()
    {
        // Implement health checks
        // - CPU usage < 90%
        // - Memory usage < 90%
        // - Disk space available
        // - Database connectivity
        return true; // Example
    }
}
```

---

### ILeaderElectionFactory

Factory for creating leader election instances.

```csharp
public interface ILeaderElectionFactory
{
    ILeaderElection CreateElection(string resourceName, string? candidateId = null);
    IHealthBasedLeaderElection CreateHealthBasedElection(string resourceName, string? candidateId = null);
}
```

**Dependency Injection** (Builder API -- recommended):

```csharp
services.AddExcalibur(x => x.AddLeaderElection(le => le
    .UseConsul(opts => opts.ConsulAddress("http://localhost:8500"))
    .WithHealthChecks()
    .WithFencingTokens()));
```

**Dependency Injection** (Factory pattern):

```csharp
// Inject factory to create elections for different resources
services.AddSingleton(sp =>
{
    var factory = sp.GetRequiredService<ILeaderElectionFactory>();
    return factory.CreateElection("my-resource-lock");
});
```

---

## Configuration

### LeaderElectionOptions

Base configuration for all leader election implementations.

```csharp
using Excalibur.Dispatch.LeaderElection;

public static class LeaderElectionConfiguration
{
    public static LeaderElectionOptions Create() => new()
    {
        LeaseDuration = TimeSpan.FromSeconds(15),
        RenewInterval = TimeSpan.FromSeconds(5),
        RetryInterval = TimeSpan.FromSeconds(2),
        GracePeriod = TimeSpan.FromSeconds(5),
        InstanceId = null,
        EnableHealthChecks = true,
        MinimumHealthScore = 0.8,
        StepDownWhenUnhealthy = true,
    };
}
```

`CandidateMetadata` is an initialized dictionary owned by these options; populate it with application-specific candidate metadata.

An unset `InstanceId` resolves from `HOSTNAME`, then `POD_NAME`. If neither supplies a nonblank value,
the resolver generates a complete 32-character GUID without separators. This fallback does not include
the machine name, cannot lose randomness through hostname truncation, and changes on restart. Configure
a stable identity when an instance must recognize its previous tenure. Configured and environment
identifiers are preserved as supplied.

**Configuration Example**:

```csharp
services.AddConsulLeaderElection(options =>
{
    // Lease expires after 30 seconds of no renewal
    options.LeaseDuration = TimeSpan.FromSeconds(30);

    // Renew lease every 10 seconds
    options.RenewInterval = TimeSpan.FromSeconds(10);

    // Retry acquiring leadership every 5 seconds
    options.RetryInterval = TimeSpan.FromSeconds(5);

    // Grace period before declaring leader dead
    options.GracePeriod = TimeSpan.FromSeconds(5);

    // Unique identifier for this instance
    options.InstanceId = $"{Environment.MachineName}-{Guid.NewGuid()}";

    // Enable health-based leadership
    options.EnableHealthChecks = true;
    options.MinimumHealthScore = 0.75; // 75% health minimum
    options.StepDownWhenUnhealthy = true; // Auto step-down when unhealthy

    // Metadata visible to other candidates
    options.CandidateMetadata["version"] = "1.0.0";
    options.CandidateMetadata["region"] = "us-east-1";
});
```

---

## Event Handling

### Leadership Events

```csharp
public class LeaderAwareService
{
    private readonly ILeaderElection _election;

    public LeaderAwareService(ILeaderElectionFactory factory)
    {
        _election = factory.CreateElection("my-resource");

        // Subscribe to all events
        _election.OnBecameLeader += OnBecameLeader;
        _election.OnLostLeadership += OnLostLeadership;
        _election.OnLeaderChanged += OnLeaderChanged;
    }

    private void OnBecameLeader(object? sender, LeaderElectionEventArgs e)
    {
        // This instance acquired leadership
        Console.WriteLine($"✓ Became leader: {e.CandidateId} at {e.Timestamp}");

        // Initialize leader-only resources
        // - Start scheduled jobs
        // - Acquire exclusive locks
        // - Begin processing work queues
    }

    private void OnLostLeadership(object? sender, LeaderElectionEventArgs e)
    {
        // This instance lost leadership (lease expired, stepped down, etc.)
        Console.WriteLine($"✗ Lost leadership: {e.CandidateId} at {e.Timestamp}");

        // Clean up leader-only resources
        // - Stop scheduled jobs
        // - Release exclusive locks
        // - Drain work queues gracefully
    }

    private void OnLeaderChanged(object? sender, LeaderChangedEventArgs e)
    {
        // Any candidate detected a leadership change
        Console.WriteLine($"Leader changed: {e.OldLeaderId ?? "none"} → {e.NewLeaderId ?? "none"}");

        // Update routing, caches, or external monitoring
    }
}
```

---

## Common Patterns

### Pattern 1: Single Leader for Singleton Work

Ensure only one instance performs a task (e.g., scheduled job, cleanup, aggregation).

```csharp
public class SingletonJobService : BackgroundService
{
    private readonly ILeaderElection _election;

    public SingletonJobService(ILeaderElectionFactory factory)
    {
        _election = factory.CreateElection("singleton-job");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _election.StartAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (_election.IsLeader)
            {
                // Only the leader processes this job
                await ProcessJobAsync();
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }

        await _election.StopAsync();
    }

    private async Task ProcessJobAsync()
    {
        Console.WriteLine("Leader processing job...");
        // Job logic here
    }
}
```

---

### Pattern 2: Active-Passive Failover

Primary instance handles traffic; standby takes over on failure.

```csharp
public class ActivePassiveService : BackgroundService
{
    private readonly ILeaderElection _election;
    private CancellationTokenSource? _activeWorkCts;

    public ActivePassiveService(ILeaderElectionFactory factory)
    {
        _election = factory.CreateElection("active-passive");
        _election.OnBecameLeader += async (s, e) => await ActivateAsync();
        _election.OnLostLeadership += async (s, e) => await DeactivateAsync();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _election.StartAsync(stoppingToken);

        // Keep election alive
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }

        await _election.StopAsync();
    }

    private async Task ActivateAsync()
    {
        Console.WriteLine("Activating as leader");

        _activeWorkCts = new CancellationTokenSource();

        // Start leader work
        _ = Task.Run(async () =>
        {
            while (!_activeWorkCts.Token.IsCancellationRequested)
            {
                // Active processing
                await Task.Delay(100, _activeWorkCts.Token);
            }
        });
    }

    private async Task DeactivateAsync()
    {
        Console.WriteLine("Deactivating (no longer leader)");

        // Stop leader work gracefully
        _activeWorkCts?.Cancel();
        await Task.Delay(100); // Allow work to drain

        _activeWorkCts?.Dispose();
        _activeWorkCts = null;
    }
}
```

---

### Pattern 3: Health-Based Leadership with Step-Down

Leader monitors its own health and steps down if unhealthy.

```csharp
public class HealthAwareLeaderService : BackgroundService
{
    private readonly IHealthBasedLeaderElection _election;

    public HealthAwareLeaderService(ILeaderElectionFactory factory)
    {
        _election = factory.CreateHealthBasedElection("health-leader");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _election.StartAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            // Check system health
            var health = await MeasureHealthAsync();

            // Update election with health status
            await _election.UpdateHealthAsync(
                isHealthy: health.Score >= 0.75,
                metadata: health.Metadata,
                cancellationToken
            );

            if (_election.IsLeader)
            {
                // Do leader work
                await DoLeaderWorkAsync();
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }

        await _election.StopAsync();
    }

    private async Task<(double Score, Dictionary<string, string> Metadata)> MeasureHealthAsync()
    {
        // Example health calculation
        var cpuUsage = 0.6; // 60%
        var memoryUsage = 0.7; // 70%
        var diskHealthy = true;

        var score = ((1.0 - cpuUsage) + (1.0 - memoryUsage) + (diskHealthy ? 1.0 : 0.0)) / 3.0;

        var metadata = new Dictionary<string, string>
        {
            ["cpu"] = $"{cpuUsage * 100:F0}%",
            ["memory"] = $"{memoryUsage * 100:F0}%",
            ["disk"] = diskHealthy ? "healthy" : "unhealthy"
        };

        return (score, metadata);
    }

    private async Task DoLeaderWorkAsync()
    {
        // Leader-only processing
        await Task.Delay(100);
    }
}
```

---

## Thread Safety

All implementations of `ILeaderElection` and `IHealthBasedLeaderElection` **must be thread-safe** for concurrent access to properties (`IsLeader`, `CurrentLeaderId`) and event subscriptions.

**Safe Usage**:

```csharp
// Safe: Multiple threads can check leadership
if (_election.IsLeader)
{
    // This is safe - leadership state is consistent
}

// Safe: Event subscriptions are thread-safe
_election.OnBecameLeader += Handler;
```

---

## Testing

For unit and integration testing, the **InMemory provider** avoids an external coordination service. It does not establish distributed failure behavior or native compatibility for a production provider; verify those paths separately.

```bash
dotnet add package Excalibur.Dispatch.LeaderElection.InMemory
```

**Test Example**:

```csharp
[Fact]
public async Task LeaderElection_ShouldElectSingleLeader()
{
    // Arrange
    var services = new ServiceCollection();
    services.AddInMemoryLeaderElection();
    var provider = services.BuildServiceProvider();

    var factory = provider.GetRequiredService<ILeaderElectionFactory>();
    var election1 = factory.CreateElection("test-resource");
    var election2 = factory.CreateElection("test-resource");

    // Act
    await election1.StartAsync();
    await election2.StartAsync();
    await Task.Delay(100); // Allow election to settle

    // Assert
    var leaders = new[] { election1.IsLeader, election2.IsLeader };
    Assert.Single(leaders.Where(x => x)); // Exactly one leader
}
```

---

## See Also

- **[Consul Provider](../../Excalibur/Excalibur.LeaderElection.Consul/README.md)** - Production Consul-based leader election
- **[Kubernetes Provider](../../Excalibur/Excalibur.LeaderElection.Kubernetes/README.md)** - Cloud-native Kubernetes Lease coordination
- **[InMemory Provider](../../Excalibur/Excalibur.LeaderElection.InMemory/README.md)** - Testing and development

---

## License

This package is part of the **Excalibur.Dispatch** framework and is licensed under multiple licenses. See the project root for license details.

---

## Support

- **GitHub Issues**: [https://github.com/TrigintaFaces/Excalibur/issues](https://github.com/TrigintaFaces/Excalibur/issues)
- **Documentation**: [https://docs.excalibur-dispatch.dev](https://docs.excalibur-dispatch.dev)
- **Changelog**: [CHANGELOG.md](../../../CHANGELOG.md)

