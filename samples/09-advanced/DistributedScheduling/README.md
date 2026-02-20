# Distributed Scheduling Coordination Example

This example demonstrates how to use the distributed scheduling coordination features in Excalibur.Dispatch.

## Features Demonstrated

- **Leader Election**: Nodes compete to become the coordination leader
- **Schedule Ownership**: Schedules are distributed across nodes with ownership tracking
- **Lock Management**: Exclusive locks prevent duplicate processing
- **Orphan Detection**: Schedules from failed nodes are automatically reassigned
- **Load Balancing**: Leader redistributes schedules to maintain balance
- **Event Notifications**: Real-time updates on ownership and leadership changes

## Prerequisites

- Redis server running on localhost:6379
- .NET 8.0 or later

## Running the Example

### Single Node
```bash
dotnet run
```

### Multiple Nodes (Recommended)
Open multiple terminals and run with different node IDs:

```bash
# Terminal 1
dotnet run -- node-1

# Terminal 2
dotnet run -- node-2

# Terminal 3
dotnet run -- node-3
```

## What to Observe

1. **Leader Election**: One node will become the leader and log "Leadership attempt result: True"
2. **Heartbeats**: All nodes send regular heartbeats to stay registered
3. **Schedule Processing**: Each node processes its owned schedules with distributed locks
4. **Failover**: Stop a node (Ctrl+C) and watch its schedules get reassigned
5. **Load Balancing**: The leader will transfer schedules if load becomes imbalanced

## Key Concepts

### Distributed Locks
Prevents multiple nodes from processing the same schedule simultaneously:
```csharp
if (await coordinator.TryAcquireScheduleLockAsync(scheduleId, duration))
{
    // Process schedule
    await coordinator.ReleaseScheduleLockAsync(scheduleId);
}
```

### Leader Tasks
The leader node performs cluster-wide coordination:
- Monitors node health
- Detects orphaned schedules
- Performs load balancing
- Manages cluster membership

### Event-Driven Updates
Nodes react to cluster events in real-time:
- Schedule ownership changes
- Leadership transitions
- Node joins/leaves

## Configuration Options

```csharp
services.AddRedisSchedulingCoordination(options =>
{
    options.ConnectionString = "localhost:6379";
    options.KeyPrefix = "myapp:scheduling:";
    options.MaxSchedulesPerNode = 100;
    options.HeartbeatInterval = TimeSpan.FromSeconds(30);
    options.LeaderLeaseDuration = TimeSpan.FromMinutes(1);
    options.EnableAutoRebalancing = true;
    options.RebalancingThreshold = 0.2;
});
```

## Production Considerations

1. **Redis Persistence**: Enable Redis persistence for schedule ownership data
2. **Network Partitions**: Consider using Redis Sentinel or Cluster for HA
3. **Lock Duration**: Balance between safety and responsiveness
4. **Monitoring**: Add metrics for lock acquisition, leadership changes, and load distribution
5. **Graceful Shutdown**: Always renounce leadership and release locks on shutdown

## Troubleshooting

### Node Not Becoming Leader
- Check Redis connectivity
- Verify no other node holds leadership
- Check leader lease hasn't expired

### Schedules Not Processing
- Verify schedule ownership is registered
- Check lock acquisition is succeeding
- Monitor for lock timeout issues

### Load Imbalance
- Ensure leader is running
- Check rebalancing threshold configuration
- Verify all nodes are healthy

## Next Steps

- Implement custom schedule distribution strategies
- Add persistent schedule storage
- Integrate with cloud schedulers (AWS EventBridge, Azure Logic Apps, etc.)
- Add comprehensive monitoring and alerting
