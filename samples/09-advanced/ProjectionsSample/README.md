# Projections Sample

This sample demonstrates how to build **CQRS read models (projections)** from event-sourced aggregates using the Excalibur framework.

## Overview

Projections are read-optimized views of event-sourced data. They transform domain events into denormalized read models that support fast queries, without the complexity of replaying events for every read.

## Quick Start

```bash
cd samples/09-advanced/ProjectionsSample
dotnet run
```

## Key Concepts

### 1. IProjection<TKey> - Read Model Interface

```csharp
public interface IProjection<out TKey> where TKey : notnull
{
    TKey Id { get; }
    long Version { get; }
    DateTimeOffset LastModified { get; }
}
```

All projections implement this interface, providing a consistent contract for identity, versioning, and modification tracking.

### 2. IProjectionStore<T> - Storage Abstraction

```csharp
public interface IProjectionStore<TProjection> where TProjection : class
{
    Task<TProjection?> GetByIdAsync(string id, CancellationToken ct);
    Task UpsertAsync(string id, TProjection projection, CancellationToken ct);
    Task DeleteAsync(string id, CancellationToken ct);
    Task<IReadOnlyList<TProjection>> QueryAsync(
        IDictionary<string, object>? filters,
        QueryOptions? options,
        CancellationToken ct);
    Task<long> CountAsync(IDictionary<string, object>? filters, CancellationToken ct);
}
```

Multiple storage backends are available:
- `SqlServerProjectionStore` - SQL Server with JSON columns
- `PostgresProjectionStore` - PostgreSQL with JSONB
- `MongoDbProjectionStore` - MongoDB document store
- `CosmosDbProjectionStore` - Azure Cosmos DB
- `ElasticSearchProjectionStore` - Elasticsearch for full-text search

## Projection Patterns Demonstrated

### 1. Inline Projections

Synchronous updates that process events immediately:

```csharp
public class ProductCatalogProjectionHandler
{
    private readonly IProjectionStore<ProductCatalogProjection> _store;

    public async Task HandleAsync(ProductCreated @event, CancellationToken ct)
    {
        var projection = new ProductCatalogProjection
        {
            Id = @event.ProductId.ToString(),
            Name = @event.Name,
            Price = @event.Price,
            // ... other properties
        };
        await _store.UpsertAsync(projection.Id, projection, ct);
    }
}
```

**Pros:**
- Strong consistency between write and read models
- Simple to implement and reason about

**Cons:**
- Couples read and write performance
- Single point of failure

### 2. Multi-Stream Projections

Aggregate data from multiple event streams:

```csharp
public class CategorySummaryProjection : IProjection<string>
{
    public string CategoryName { get; set; }
    public int TotalProducts { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal TotalInventoryValue { get; set; }
    // Aggregates data from all products in category
}
```

This pattern is useful for:
- Dashboard summaries
- Category navigation
- Analytics and reporting

### 3. Checkpoint Tracking

Track progress for async projections:

```csharp
public record ProjectionCheckpoint
{
    public string ProjectionName { get; set; }
    public long LastPosition { get; set; }           // Global event position
    public DateTimeOffset LastProcessedAt { get; set; }
    public long TotalEventsProcessed { get; set; }
}
```

Checkpoints enable:
- Resume after restarts
- Rebuild from scratch
- Gap detection and recovery

### 4. Projection Rebuild

Reset and replay all events:

```csharp
// 1. Reset checkpoint
checkpointStore.ResetCheckpoint(projectionName);

// 2. Clear projection store (optional)
await projectionStore.DeleteAllAsync(ct);

// 3. Replay events from position 0
await foreach (var @event in eventStore.ReadAllAsync(fromPosition: 0, ct))
{
    await projectionHandler.HandleAsync(@event, ct);

    // 4. Update checkpoint periodically (not every event!)
    if (processed % batchSize == 0)
    {
        checkpointStore.SaveCheckpoint(new ProjectionCheckpoint { ... });
    }
}
```

## Querying Projections

The `IProjectionStore<T>` supports rich querying:

```csharp
// Filter by property
var electronics = await store.QueryAsync(
    new Dictionary<string, object> { ["Category"] = "Electronics" },
    null, ct);

// Pagination and sorting
var topProducts = await store.QueryAsync(
    null,
    new QueryOptions(Skip: 0, Take: 10, OrderBy: "Price", Descending: true),
    ct);

// Advanced operators (SQL Server, PostgreSQL)
var affordable = await store.QueryAsync(
    new Dictionary<string, object> { ["Price:lt"] = 100 },
    null, ct);
```

Supported filter operators:
| Operator | Example | Description |
|----------|---------|-------------|
| (default) | `["Status"] = "Active"` | Equality |
| `:gt` | `["Price:gt"] = 100` | Greater than |
| `:gte` | `["Price:gte"] = 100` | Greater than or equal |
| `:lt` | `["Price:lt"] = 1000` | Less than |
| `:lte` | `["Price:lte"] = 1000` | Less than or equal |
| `:neq` | `["Status:neq"] = "Deleted"` | Not equals |
| `:in` | `["Tags:in"] = ["A", "B"]` | In collection |
| `:contains` | `["Name:contains"] = "test"` | String contains |

## Project Structure

```
ProjectionsSample/
├── Domain/
│   ├── Events.cs              # Domain events
│   └── ProductAggregate.cs    # Event-sourced aggregate
├── Projections/
│   ├── ProductCatalogProjection.cs          # Product read model
│   ├── ProductCatalogProjectionHandler.cs   # Event handler
│   ├── CategorySummaryProjection.cs         # Multi-stream projection
│   └── CategorySummaryProjectionHandler.cs  # Aggregation handler
├── Infrastructure/
│   ├── InMemoryProjectionStore.cs   # Demo store implementation
│   └── ProjectionCheckpoint.cs      # Checkpoint tracking
├── Program.cs                 # Demo scenarios
└── README.md                  # This file
```

## Best Practices

### 1. Design for Queries
Design projections around specific query needs, not around aggregates:
```csharp
// ✓ Good: Designed for catalog listing
class ProductCatalogProjection { Name, Price, InStock, Category }

// ✗ Bad: Just mirroring the aggregate
class ProductProjection { Name, Price, Stock, IsActive, CreatedAt, ... }
```

### 2. Use Eventual Consistency
For high-throughput systems, use async projections:
```csharp
// Async projection with separate worker
public class ProjectionWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var checkpoint = await _checkpointStore.GetAsync(_projectionName, ct);

        await foreach (var @event in _eventStore.ReadFromAsync(checkpoint.Position, ct))
        {
            await _handler.HandleAsync(@event, ct);
            await _checkpointStore.SaveAsync(checkpoint with { Position = @event.Position }, ct);
        }
    }
}
```

### 3. Handle Idempotency
Projections should handle duplicate events gracefully:
```csharp
public async Task HandleAsync(ProductCreated @event, CancellationToken ct)
{
    var existing = await _store.GetByIdAsync(@event.ProductId.ToString(), ct);
    if (existing?.Version >= @event.Version)
    {
        return; // Already processed
    }
    // ... process event
}
```

### 4. Batch Checkpoint Updates
Don't checkpoint after every event:
```csharp
const int BatchSize = 100;
var count = 0;

await foreach (var @event in events)
{
    await handler.HandleAsync(@event, ct);

    if (++count % BatchSize == 0)
    {
        await checkpointStore.SaveAsync(checkpoint, ct);
    }
}

await checkpointStore.SaveAsync(checkpoint, ct); // Final save
```

## Dependencies

- `Excalibur.Dispatch.Abstractions` - Core interfaces
- `Excalibur.EventSourcing.Abstractions` - `IProjectionStore<T>`, `QueryOptions`
- `Excalibur.Domain` - `AggregateRoot<T>`
- `Microsoft.Extensions.Logging` - Logging

## Production Considerations

1. **Choose the right store** - Use SQL Server/PostgreSQL for transactional consistency, MongoDB/Cosmos for scalability, Elasticsearch for search
2. **Monitor checkpoint lag** - Track the gap between event store position and projection position
3. **Plan for rebuilds** - Design projection handlers to support full replay
4. **Consider snapshots** - For large projections, snapshot periodically to speed up rebuilds
5. **Use transactions** - When updating multiple projections from the same event, use transactions if available
