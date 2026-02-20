---
sidebar_position: 4
title: Snapshot Setup
description: Configure snapshot strategies to optimize event replay performance
---

# Snapshot Setup

Snapshots store the current state of an aggregate, avoiding the need to replay all historical events. This is essential for aggregates with many events.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required packages:
  ```bash
  dotnet add package Excalibur.EventSourcing
  dotnet add package Excalibur.EventSourcing.SqlServer  # or your provider
  ```
- Familiarity with [event stores](../event-sourcing/event-store.md) and [event store setup](./event-store-setup.md)

## Why Use Snapshots?

Without snapshots:
```
Load aggregate with 10,000 events
→ Replay all 10,000 events
→ Slow load time, high memory usage
```

With snapshots (every 100 events):
```
Load snapshot at version 9,900
→ Replay only 100 events
→ Fast load time, consistent performance
```

## Basic Setup

```csharp
// Configure event sourcing with snapshot strategy
services.AddExcaliburEventSourcing(builder =>
{
    builder.AddRepository<OrderAggregate, Guid>();
    builder.UseIntervalSnapshots(100);  // Snapshot every 100 events
});

// Add the SQL Server event store and snapshot store
services.AddSqlServerEventSourcing(connectionString);
```

## Snapshot Strategies

### Interval-Based (Recommended)

Create a snapshot every N events:

```csharp
builder.UseIntervalSnapshots(100);
```

| Event Count | Recommended Interval |
|-------------|---------------------|
| < 1,000 | 50-100 |
| 1,000 - 10,000 | 100-500 |
| > 10,000 | 500-1,000 |

### Time-Based

Create snapshots after a time interval:

```csharp
builder.UseTimeBasedSnapshots(TimeSpan.FromHours(1));
```

Useful when:
- Event frequency varies significantly
- You want predictable snapshot timing
- Aggregates receive bursts of events

### Size-Based

Create snapshots when event data exceeds a size threshold:

```csharp
builder.UseSizeBasedSnapshots(maxSizeInBytes: 1_000_000);  // 1 MB
```

Useful when:
- Events vary significantly in size
- Memory usage is a primary concern

### Composite Strategy

Combine multiple strategies with OR logic:

```csharp
builder.UseCompositeSnapshotStrategy(composite =>
{
    composite.AddIntervalStrategy(100)      // Every 100 events
             .AddTimeBasedStrategy(TimeSpan.FromHours(1))  // OR every hour
             .AddSizeBasedStrategy(500_000);  // OR when > 500 KB
});
```

### No Snapshots

For aggregates with few events:

```csharp
builder.UseNoSnapshots();
```

## Snapshot Storage

### Inline with Events

Store snapshots in the same database as events (default):

```csharp
// SQL Server stores snapshots in the Snapshots table alongside events
services.AddSqlServerEventSourcing(connectionString);
```

### Separate Store

Use a different storage backend for snapshots:

```csharp
// Register event store and a custom snapshot store
services.AddSqlServerEventStore(connectionString);

// Use a custom snapshot manager
services.AddExcaliburEventSourcing(builder =>
{
    builder.UseSnapshotManager<RedisSnapshotManager>();
});
```

## Implementing Snapshot Methods

Your aggregate must override the snapshot methods from `AggregateRoot`. The `ISnapshot` interface uses `byte[] Data` for serialized state — you define a domain state type and serialize it:

```csharp
public class Order : AggregateRoot<OrderId>
{
    public string CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public decimal TotalAmount { get; private set; }
    private readonly List<OrderItem> _items = new();

    // Override to create snapshot from current state
    public override ISnapshot CreateSnapshot()
    {
        // Create domain state object (NOT ISnapshot)
        var state = new OrderSnapshotState
        {
            CustomerId = CustomerId,
            Status = Status,
            TotalAmount = TotalAmount,
            Items = _items.Select(i => new OrderItemState
            {
                Sku = i.Sku,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice
            }).ToList()
        };

        // Serialize to bytes and wrap in Snapshot using factory method
        return Snapshot.Create(
            aggregateId: Id.ToString(),
            version: Version,
            data: JsonSerializer.SerializeToUtf8Bytes(state),
            aggregateType: nameof(Order));
    }

    // Override to restore state from snapshot
    protected override void ApplySnapshot(ISnapshot snapshot)
    {
        // Deserialize state from snapshot.Data
        var state = JsonSerializer.Deserialize<OrderSnapshotState>(snapshot.Data)
            ?? throw new InvalidOperationException("Failed to deserialize snapshot");

        CustomerId = state.CustomerId;
        Status = state.Status;
        TotalAmount = state.TotalAmount;
        _items.Clear();
        _items.AddRange(state.Items.Select(i => new OrderItem
        {
            Sku = i.Sku,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice
        }));
    }
}

// Domain state to be serialized — NOT ISnapshot
public record OrderSnapshotState
{
    public string CustomerId { get; init; } = string.Empty;
    public OrderStatus Status { get; init; }
    public decimal TotalAmount { get; init; }
    public List<OrderItemState> Items { get; init; } = new();
}

public record OrderItemState
{
    public string Sku { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
}
```

## Custom Snapshot Strategy

Implement `ISnapshotStrategy` for custom logic:

```csharp
public class BusinessHoursSnapshotStrategy : ISnapshotStrategy
{
    public bool ShouldCreateSnapshot(IAggregateRoot aggregate)
    {
        // Only snapshot during off-peak hours
        var hour = DateTime.UtcNow.Hour;
        var isOffPeak = hour < 6 || hour > 22;

        return isOffPeak && aggregate.Version % 50 == 0;
    }
}

// Register
builder.AddSnapshotStrategy<BusinessHoursSnapshotStrategy>();
```

## Snapshot Manager

Control when snapshots are created:

```csharp
public interface ISnapshotManager
{
    Task SaveSnapshotAsync<TAggregate>(TAggregate aggregate, CancellationToken ct)
        where TAggregate : IAggregateRoot;

    Task<TSnapshot?> LoadSnapshotAsync<TSnapshot>(string streamId, CancellationToken ct);
}
```

### Manual Snapshots

Force a snapshot outside normal strategy:

```csharp
var manager = services.GetRequiredService<ISnapshotManager>();
await manager.SaveSnapshotAsync(aggregate, ct);
```

### Bulk Snapshot Creation

For existing aggregates without snapshots:

```csharp
public class SnapshotMigrationJob
{
    public async Task MigrateAsync(CancellationToken ct)
    {
        var aggregateIds = await _eventStore.GetAllStreamIdsAsync(ct);

        foreach (var id in aggregateIds)
        {
            var aggregate = await _repository.GetByIdAsync(id, ct);
            if (aggregate is not null)
            {
                var snapshot = aggregate.CreateSnapshot();
                await _snapshotStore.SaveSnapshotAsync(snapshot, ct);
            }
        }
    }
}
```

## Performance Considerations

### Snapshot Serialization

Choose efficient serialization:

```csharp
builder.UseEventSerializer<MessagePackSnapshotSerializer>();
```

| Serializer | Size | Speed | Human-Readable |
|------------|------|-------|----------------|
| JSON | Large | Medium | Yes |
| MessagePack | Small | Fast | No |
| MemoryPack | Smallest | Fastest | No |

### Snapshot Size

Keep snapshots small:

```csharp
// DON'T: Include derived data
public record BadSnapshot
{
    public List<OrderItem> Items { get; init; }
    public decimal TotalAmount { get; init; }  // Can be computed from Items
}

// DO: Only include essential state
public record GoodSnapshot
{
    public List<OrderItem> Items { get; init; }
    // TotalAmount computed when needed
}
```

## Monitoring

### Snapshot Metrics

```csharp
services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddEventSourcingInstrumentation();
    });
```

Metrics:
- `excalibur.snapshots.created` — Snapshots created
- `excalibur.snapshots.loaded` — Snapshots loaded
- `excalibur.snapshots.size_bytes` — Snapshot size distribution
- `excalibur.events.replayed` — Events replayed after snapshot

### Health Check

Snapshot store health is monitored automatically when `RegisterHealthChecks = true` in `SqlServerEventSourcingOptions` (default). See [Event Store Setup](./event-store-setup.md) for details.

## Best Practices

| Practice | Reason |
|----------|--------|
| Choose appropriate interval | Balance storage vs. replay time |
| Override snapshot methods | Required for snapshot support |
| Keep snapshots small | Faster serialization/deserialization |
| Monitor replay counts | Detect missing snapshot coverage |
| Test snapshot restoration | Ensure state is correctly restored |

## Troubleshooting

### Snapshot not being created

1. Verify aggregate overrides `CreateSnapshot()` and `ApplySnapshot()`
2. Check strategy threshold is being reached
3. Verify snapshot store is configured

### State mismatch after restore

1. Verify `CreateSnapshot()` captures all state
2. Verify `ApplySnapshot()` restores all state
3. Check for missing private fields

### Large snapshot size

1. Review what's included in snapshot
2. Remove derived/computed data
3. Consider more compact serialization

## See Also

- [Snapshots](../event-sourcing/snapshots.md) — Snapshot concepts and how they integrate with event sourcing
- [Event Store Setup](../configuration/event-store-setup.md) — Configure event stores and aggregate repositories
- [Aggregates](../event-sourcing/aggregates.md) — Aggregate root design, including snapshot method overrides
- [Event Sourcing Overview](../event-sourcing/index.md) — Introduction to event sourcing patterns in Excalibur
