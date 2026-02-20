# Snapshot Strategies Sample

This sample demonstrates **aggregate snapshot strategies** for optimizing event sourcing performance.

## What This Sample Shows

1. **Interval-Based Snapshotting** - Create snapshot every N events
2. **Time-Based Snapshotting** - Create snapshot every N minutes
3. **Size-Based Snapshotting** - Create snapshot when aggregate exceeds N KB
4. **Composite Snapshotting** - Combine multiple strategies with Any/All logic
5. **No Snapshot Strategy** - Disable snapshotting for testing

## Why Snapshots Matter

Event sourcing stores all events for an aggregate. Rehydrating an aggregate means replaying all events, which can be slow for aggregates with many events:

```
Without Snapshots:
Load aggregate (1000 events) → ~100ms

With Snapshots (every 100 events):
Load snapshot (version 900) + replay 100 events → ~20ms
```

## Running the Sample

```bash
cd samples/09-advanced/SnapshotStrategies
dotnet run
```

## Available Strategies

### IntervalSnapshotStrategy

Creates snapshots after a specified number of events:

```csharp
// Snapshot every 100 events
var strategy = new IntervalSnapshotStrategy(interval: 100);

// ShouldCreateSnapshot returns true when Version % 100 == 0
services.AddExcaliburEventSourcing(es =>
{
    es.AddRepository<MyAggregate, Guid>(
        id => new MyAggregate(id),
        strategy);
});
```

**Best for:** High-velocity aggregates with frequent modifications.

### TimeBasedSnapshotStrategy

Creates snapshots based on time intervals:

```csharp
// Snapshot every 30 minutes
var strategy = new TimeBasedSnapshotStrategy(TimeSpan.FromMinutes(30));
```

**Best for:** Long-running aggregates that are frequently read but infrequently modified.

### SizeBasedSnapshotStrategy

Creates snapshots when aggregate state exceeds a size threshold:

```csharp
// Snapshot when aggregate exceeds 10 KB
var strategy = new SizeBasedSnapshotStrategy(maxSizeKb: 10);
```

**Best for:** Aggregates with large state (many properties, collections).

### CompositeSnapshotStrategy

Combines multiple strategies with configurable logic:

```csharp
// Snapshot when EITHER condition is met (Any mode)
var strategy = new CompositeSnapshotStrategy(
    CompositeSnapshotStrategy.CompositeMode.Any,
    new IntervalSnapshotStrategy(100),
    new TimeBasedSnapshotStrategy(TimeSpan.FromHours(1)));

// Snapshot only when BOTH conditions are met (All mode)
var strictStrategy = new CompositeSnapshotStrategy(
    CompositeSnapshotStrategy.CompositeMode.All,
    new IntervalSnapshotStrategy(100),
    new SizeBasedSnapshotStrategy(maxSizeKb: 5));
```

**Best for:** Complex scenarios requiring multiple conditions.

### NoSnapshotStrategy

Never creates snapshots:

```csharp
var strategy = new NoSnapshotStrategy();
```

**Best for:** Testing, development, or very small aggregates.

## Strategy Selection Guide

| Scenario | Recommended Strategy | Configuration |
|----------|---------------------|---------------|
| High-volume shopping carts | Interval | Every 50-100 events |
| Long-running orders | Time-based | Every 1-4 hours |
| User profiles | Interval | Every 20-50 events |
| Analytics aggregates | Size-based | Above 50 KB |
| Testing | NoSnapshot | - |
| Production default | Composite (Any) | Interval + Time |

## Expected Output

```
=================================================
  Snapshot Strategies Sample
=================================================

=== Demo 1: Interval-Based Snapshotting ===
Strategy: Create snapshot every 5 events

Created cart: a1b2c3d4-...
Added item: Laptop - Version now: 2
Added item: Mouse - Version now: 3
Added item: Keyboard - Version now: 4
Added item: Monitor - Version now: 5
  >>> Snapshot would be created at version 5
Added item: Headphones - Version now: 6
...

=== Demo 2: Time-Based Snapshotting ===
Strategy: Create snapshot every 30 seconds

Created cart: b2c3d4e5-...
Initial check: ShouldCreateSnapshot = True
After adding item: ShouldCreateSnapshot = False

Time-based strategy tracks last snapshot time per aggregate.

=== Demo 3: Composite Snapshotting ===
Strategy: Snapshot when EITHER interval OR time condition is met

Version 1: ShouldCreateSnapshot = True
Version 10: ShouldCreateSnapshot = True
Version 11: ShouldCreateSnapshot = False
...
```

## Performance Considerations

### When to Snapshot

| Event Count | Rehydration Time | Recommendation |
|-------------|------------------|----------------|
| < 50 | < 10ms | No snapshot needed |
| 50-200 | 10-50ms | Consider snapshots |
| 200-1000 | 50-200ms | Snapshots recommended |
| > 1000 | > 200ms | Snapshots required |

### Storage Trade-offs

- **More frequent snapshots** = Faster reads, more storage
- **Less frequent snapshots** = Slower reads, less storage

### Recommended Starting Points

```csharp
// Most aggregates
new IntervalSnapshotStrategy(100)

// Read-heavy, write-light
new TimeBasedSnapshotStrategy(TimeSpan.FromHours(2))

// Production belt-and-suspenders
new CompositeSnapshotStrategy(
    CompositeSnapshotStrategy.CompositeMode.Any,
    new IntervalSnapshotStrategy(100),
    new TimeBasedSnapshotStrategy(TimeSpan.FromHours(4)))
```

## Project Structure

```
SnapshotStrategies/
├── SnapshotStrategies.csproj  # Project file
├── Program.cs                  # Main sample with demos
├── Aggregates/
│   └── ShoppingCartAggregate.cs  # Demo aggregate
└── README.md                   # This file
```

## Related Samples

- [SqlServerEventStore](../SqlServerEventStore/) - SQL Server persistence
- [EventUpcasting](../EventUpcasting/) - Event schema evolution
- [ExcaliburCqrs](../../01-getting-started/ExcaliburCqrs/) - Basic event sourcing
