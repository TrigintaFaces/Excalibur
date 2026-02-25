# Leader Election Sample

This sample demonstrates distributed leader election using Redis with the Excalibur.LeaderElection.Redis package.

## Overview

Leader election is a distributed systems pattern where multiple instances of an application coordinate to ensure that only one instance (the "leader") performs certain tasks at any given time. This is essential for:

- **Background job processing** - Only one instance should process scheduled jobs
- **Singleton services** - Tasks that must run in exactly one place
- **Cluster coordination** - Managing state across distributed nodes
- **Cache warming** - One instance pre-populates caches for all

## Prerequisites

- .NET 9.0 SDK
- Docker (for Redis)

## Quick Start

1. **Start Redis:**
   ```bash
   docker-compose up -d
   ```

2. **Run the sample:**
   ```bash
   dotnet run
   ```

3. **Multi-instance test:** Open multiple terminals and run `dotnet run` in each. Only one will become the leader.

## Key Concepts

### ILeaderElection Interface

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

    // Lifecycle
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}
```

### Configuration Options

| Option | Default | Description |
|--------|---------|-------------|
| `LeaseDuration` | 15s | Time before the leader lock expires if not renewed |
| `RenewInterval` | 5s | How often the leader renews its lease |
| `GracePeriod` | 5s | Time to wait before declaring a leader dead |
| `InstanceId` | MachineName | Unique identifier for this candidate |

### Registration

```csharp
// 1. Register Redis connection
services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect("localhost:6379"));

// 2. Register leader election
services.AddRedisLeaderElection("myapp:leader", options =>
{
    options.LeaseDuration = TimeSpan.FromSeconds(30);
    options.RenewInterval = TimeSpan.FromSeconds(10);
    options.GracePeriod = TimeSpan.FromSeconds(15);
    options.InstanceId = Environment.MachineName;
});
```

## Demo Scenarios

### 1. Single-Leader Election Lifecycle

The sample demonstrates the complete lifecycle:
- Start participating in election
- Acquire leadership (if available)
- Maintain leadership through renewal
- Release leadership on shutdown

### 2. Leadership Change Callbacks

Events fire when leadership changes:
- `OnBecameLeader` - This instance is now the leader
- `OnLostLeadership` - This instance lost leadership
- `OnLeaderChanged` - Any leadership change (useful for followers)

### 3. Leader Work Pattern

```csharp
if (leaderElection.IsLeader)
{
    // Perform leader-only work
    await ProcessBackgroundJobsAsync();
}
else
{
    // Follower mode - wait or perform follower tasks
    await WaitForLeadershipAsync();
}
```

### 4. Graceful Shutdown

On shutdown, the leader gracefully releases its lock, allowing another instance to take over immediately rather than waiting for the lease to expire.

## Provider Comparison

| Provider | Best For | Infrastructure | Failover Speed |
|----------|----------|----------------|----------------|
| **Redis** | High availability, fast failover | Redis cluster | Sub-second |
| SQL Server | Existing SQL infrastructure | SQL Server | 5-15 seconds |
| Kubernetes | K8s-native deployments | Kubernetes API | 15-30 seconds |
| Consul | Service mesh integration | Consul cluster | 1-5 seconds |

## How Redis Leader Election Works

1. **Acquire**: Uses `SET key value NX PX milliseconds` (set if not exists with TTL)
2. **Renew**: Lua script extends TTL only if the caller owns the lock
3. **Release**: Lua script deletes the key only if the caller owns it

```redis
# Acquire lock
SET myapp:leader "Instance-12345" NX PX 30000

# Check current leader
GET myapp:leader

# Check TTL
TTL myapp:leader
```

## Redis Commands

Connect to Redis CLI:
```bash
docker exec -it leader-election-redis redis-cli
```

Inspect leader state:
```redis
KEYS *leader*
GET myapp:leader
TTL myapp:leader
```

## Configuration (appsettings.json)

```json
{
  "Redis": {
    "ConnectionString": "localhost:6379"
  },
  "LeaderElection": {
    "LockKey": "myapp:leader",
    "LeaseDurationSeconds": 30,
    "RenewIntervalSeconds": 10,
    "GracePeriodSeconds": 15
  }
}
```

## Best Practices

### 1. Lease Duration vs. Renew Interval

- `LeaseDuration` should be 2-3x `RenewInterval`
- Allows for network hiccups without losing leadership
- Example: 30s lease with 10s renewal

### 2. Grace Period

- Time to wait for renewal before declaring leader dead
- Should be less than `LeaseDuration`
- Prevents flapping during temporary network issues

### 3. Instance ID

- Use a stable, unique identifier
- Machine name + process ID works well for most cases
- In Kubernetes, use pod name

### 4. Handle Lost Leadership

Always check `IsLeader` before performing leader work:

```csharp
while (!cancellationToken.IsCancellationRequested)
{
    if (!leaderElection.IsLeader)
    {
        await Task.Delay(1000, cancellationToken);
        continue;
    }

    // Perform work, but check periodically
    for (int i = 0; i < 10; i++)
    {
        if (!leaderElection.IsLeader) break;
        await ProcessBatchAsync(i);
    }
}
```

## Troubleshooting

### Cannot connect to Redis

```
Failed to connect to Redis. Ensure Redis is running (docker-compose up -d)
```

**Solution:** Start Redis with `docker-compose up -d`

### Multiple leaders

This should never happen with Redis leader election. If it does:
1. Check network connectivity between instances
2. Ensure clocks are synchronized (NTP)
3. Verify lease duration is appropriate for your network latency

### Leadership not transferring

If an instance stops but leadership doesn't transfer:
1. Wait for lease to expire (`LeaseDuration`)
2. Check Redis connectivity for remaining instances
3. Verify the lock key is correct

## Related Packages

| Package | Description |
|---------|-------------|
| `Excalibur.Dispatch.LeaderElection.Abstractions` | Core interfaces (`ILeaderElection`) |
| `Excalibur.LeaderElection` | Base implementation and DI extensions |
| `Excalibur.LeaderElection.Redis` | Redis provider (this sample) |
| `Excalibur.LeaderElection.SqlServer` | SQL Server provider |
| `Excalibur.LeaderElection.Kubernetes` | Kubernetes Lease API provider |
| `Excalibur.LeaderElection.Consul` | Consul provider |

## Sample Output

```
=================================================
  Leader Election Sample
=================================================

=== Demo 1: Subscribe to Leadership Events ===

Candidate ID: Instance-12345
Lock Key: myapp:leader
Lease Duration: 00:00:30
Renew Interval: 00:00:10
Grace Period: 00:00:15

Event handlers registered for:
  - OnBecameLeader
  - OnLostLeadership
  - OnLeaderChanged

=== Demo 2: Start Leader Election ===

Starting leader election...

*** LEADERSHIP ACQUIRED ***
  Candidate: Instance-12345
  Lock Key: myapp:leader
  This instance is now the leader!

Initial election result:
  Is Leader: True
  Current Leader: Instance-12345

=== Demo 3: Monitor Leadership Status ===

Monitoring leadership status for 10 seconds...

Status check 1/5:
  Is Leader: True
  Current Leader: Instance-12345
...
```
