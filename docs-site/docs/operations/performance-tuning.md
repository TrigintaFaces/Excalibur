---
sidebar_position: 3
title: Performance Tuning
description: Optimize Excalibur application performance
---

# Performance Tuning

This guide covers performance optimization for event sourcing, outbox processing, and projections.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- A deployed Excalibur application with observability enabled
- Familiarity with [metrics reference](../observability/metrics-reference.md) and [caching](../performance/caching.md)

## Performance Benchmarks

### Baseline Expectations

| Operation | Target Latency | Target Throughput |
|-----------|----------------|-------------------|
| Aggregate load (with snapshot) | `<10ms` | 1,000/s |
| Aggregate load (100 events) | `<50ms` | 200/s |
| Aggregate save (single event) | `<20ms` | 500/s |
| Outbox message processing | `<5ms/message` | 10,000/s |
| Projection update | `<10ms` | 5,000/s |

## Event Store Optimization

### Snapshot Tuning

The most impactful optimization for read performance:

```csharp
es.UseCompositeSnapshotStrategy(composite =>
{
    // Snapshot every 100 events OR every hour
    composite.AddInterval(100)
             .AddTimeInterval(TimeSpan.FromHours(1));
});
```

#### Measuring Snapshot Effectiveness

Monitor snapshot effectiveness by querying the event store database directly:

```sql
-- SQL Server: Find aggregates with many events since last snapshot
SELECT e.StreamId, COUNT(*) AS EventsSinceSnapshot
FROM [dbo].[Events] e
LEFT JOIN [dbo].[Snapshots] s ON e.StreamId = s.StreamId
WHERE e.Version > ISNULL(s.Version, 0)
GROUP BY e.StreamId
HAVING COUNT(*) > 100
ORDER BY COUNT(*) DESC;
```

### Index Optimization

#### SQL Server

```sql
-- Most important: stream lookup
CREATE NONCLUSTERED INDEX IX_Events_StreamId_Version
ON EventSourcing.Events (StreamId, Version)
INCLUDE (EventType, Data, Timestamp);

-- For global sequence queries (CDC, projections)
CREATE NONCLUSTERED INDEX IX_Events_SequenceNumber
ON EventSourcing.Events (SequenceNumber);

-- For time-based queries
CREATE NONCLUSTERED INDEX IX_Events_Timestamp
ON EventSourcing.Events (Timestamp)
WHERE Timestamp > DATEADD(DAY, -30, GETDATE());  -- Filtered for recent data
```

#### PostgreSQL

```sql
-- Stream lookup with covering index
CREATE INDEX idx_events_stream ON event_sourcing.events (stream_id, version)
INCLUDE (event_type, data, timestamp);

-- BRIN index for time-series (space efficient)
CREATE INDEX idx_events_timestamp_brin
ON event_sourcing.events USING BRIN (timestamp);

-- Partial index for recent events
CREATE INDEX idx_events_recent
ON event_sourcing.events (stream_id, version)
WHERE timestamp > NOW() - INTERVAL '30 days';
```

### Query Optimization

#### Batch Loading

```csharp
// DON'T: Load aggregates one by one
foreach (var id in orderIds)
{
    var order = await repository.GetByIdAsync(id, ct);  // N queries
}

// DO: Use projections for read-heavy scenarios
var orders = await orderProjection.GetByIdsAsync(orderIds, ct);  // Single query
```

#### Projection Queries

```csharp
// DON'T: Load from event store for read-heavy scenarios
var events = await _eventStore.LoadAsync("customer-123", "Order", ct);

// DO: Query projections directly
var orders = await _orderProjection.GetByCustomerAsync("customer-123", ct);
```

## Outbox Optimization

### Batch Size Tuning

```csharp
outbox.WithProcessing(p =>
{
    p.BatchSize(batchSize);  // Tune based on workload
});
```

| Scenario | Recommended Batch Size |
|----------|------------------------|
| Low latency | 10-50 |
| Balanced | 100-200 |
| High throughput | 500-1000 |
| Bulk processing | 1000+ |

### Parallel Processing

```csharp
outbox.WithProcessing(p =>
{
    p.EnableParallelProcessing(parallelism);
});
```

| CPU Cores | Recommended Parallelism |
|-----------|------------------------|
| 2 | 2 |
| 4 | 4 |
| 8+ | 6-8 (leave headroom) |

### Polling Interval

```csharp
outbox.WithProcessing(p =>
{
    p.PollingInterval(TimeSpan.FromMilliseconds(interval));
});
```

| Latency Requirement | Polling Interval |
|---------------------|------------------|
| Real-time (`<100ms`) | 50-100ms |
| Near real-time (`<1s`) | 200-500ms |
| Standard | 1-5s |
| Relaxed | 10-30s |

### Message Size

Keep messages small for better throughput:

```csharp
// DON'T: Include full aggregate state
public record OrderCreated(
    Guid OrderId,
    string CustomerId,
    List<OrderItem> Items,    // Avoid large payloads
    string FullJsonState      // Never do this
);

// DO: Include only necessary data
public record OrderCreated(
    Guid OrderId,
    string CustomerId
);
```

## Projection Optimization

### Using IProjectionEventProcessor

Excalibur provides `IProjectionEventProcessor` for projection processing. Implement this interface for your handlers:

```csharp
public class OrderProjectionHandler : IProjectionEventProcessor
{
    private readonly IProjectionStore<OrderProjection> _store;

    public OrderProjectionHandler(IProjectionStore<OrderProjection> store)
        => _store = store;

    public async Task HandleAsync(object eventData, CancellationToken ct)
    {
        // Handle events and update projections
        if (eventData is OrderCreated e)
        {
            await _store.UpsertAsync(e.OrderId.ToString(), new OrderProjection
            {
                OrderId = e.OrderId,
                CustomerId = e.CustomerId,
                Status = "Created"
            }, ct);
        }
    }
}

// Register projection handler via DI
builder.Services.AddSingleton<IProjectionEventProcessor, OrderProjectionHandler>();
```

### Batch Projection Updates

For high-throughput scenarios, batch your projection writes:

```csharp
public class BatchingOrderProjectionHandler : IProjectionEventProcessor
{
    private readonly IProjectionStore<OrderProjection> _store;
    private readonly List<OrderProjection> _batch = new();
    private const int BatchSize = 100;

    public async Task HandleAsync(object eventData, CancellationToken ct)
    {
        if (eventData is OrderCreated e)
        {
            _batch.Add(new OrderProjection { OrderId = e.OrderId });

            if (_batch.Count >= BatchSize)
            {
                await _store.UpsertManyAsync(_batch, ct);
                _batch.Clear();
            }
        }
    }
}
```

### Denormalization

Trade storage for query speed:

```csharp
// Denormalized projection for fast queries
public record OrderProjection
{
    public Guid OrderId { get; init; }
    public string CustomerId { get; init; }
    public string CustomerName { get; init; }     // Denormalized
    public string CustomerEmail { get; init; }    // Denormalized
    public List<OrderItemProjection> Items { get; init; }
    public decimal TotalAmount { get; init; }     // Pre-computed
}
```

### Indexing Projections

```sql
-- Query patterns drive index design
CREATE INDEX idx_orders_customer ON OrderProjections (CustomerId);
CREATE INDEX idx_orders_status_date ON OrderProjections (Status, CreatedAt DESC);
CREATE INDEX idx_orders_total ON OrderProjections (TotalAmount DESC);
```

## Connection Pool Tuning

### SQL Server

```csharp
var connectionString = "...;Max Pool Size=100;Min Pool Size=10;";
```

### PostgreSQL with PgBouncer

```
# pgbouncer.ini
[pgbouncer]
pool_mode = transaction
max_client_conn = 1000
default_pool_size = 50
```

### Connection Health

```csharp
services.AddHealthChecks()
    .AddCheck("connection-pool", () =>
    {
        var stats = SqlConnection.GetPoolStatistics(connectionString);
        var utilization = (double)stats.CurrentFree / stats.MaxPoolSize;

        return utilization > 0.1
            ? HealthCheckResult.Healthy($"Pool {utilization:P0} free")
            : HealthCheckResult.Degraded($"Pool exhausted: {utilization:P0} free");
    });
```

## Caching

### Aggregate Caching

Use `IDistributedCache` or `IMemoryCache` to cache loaded aggregates and reduce event store reads:

```csharp
services.AddMemoryCache();
services.AddDistributedMemoryCache();

// Register a caching decorator around your repository
services.Decorate<IEventSourcedRepository<OrderAggregate, Guid>>(
    (inner, sp) => new CachingRepository<OrderAggregate, Guid>(
        inner,
        sp.GetRequiredService<IMemoryCache>(),
        slidingExpiration: TimeSpan.FromMinutes(5)));
```

### Snapshot Caching

Snapshot stores benefit from caching since snapshots change infrequently:

```csharp
services.AddMemoryCache();

// Cache snapshots with absolute expiration to ensure freshness
services.Decorate<ISnapshotStore>(
    (inner, sp) => new CachingSnapshotStore(
        inner,
        sp.GetRequiredService<IMemoryCache>(),
        absoluteExpiration: TimeSpan.FromMinutes(10)));
```

## Profiling

### Enable Detailed Metrics

```csharp
services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddMeter("Excalibur.EventSourcing.*");
        metrics.AddMeter("Excalibur.Dispatch.*");
    });
```

### SQL Query Analysis

```sql
-- SQL Server: Enable Query Store
ALTER DATABASE EventStore SET QUERY_STORE = ON;

-- Find slow queries
SELECT TOP 10
    qsq.query_id,
    qsqt.query_sql_text,
    qsp.avg_duration / 1000.0 AS avg_ms
FROM sys.query_store_query qsq
JOIN sys.query_store_query_text qsqt ON qsq.query_text_id = qsqt.query_text_id
JOIN sys.query_store_plan qsp ON qsq.query_id = qsp.query_id
ORDER BY avg_duration DESC;
```

## Performance Checklist

### Event Store

- [ ] Snapshots configured with appropriate interval
- [ ] Indexes created for common access patterns
- [ ] Connection pool sized appropriately
- [ ] Batch operations used where possible

### Outbox

- [ ] Batch size optimized for workload
- [ ] Parallel processing enabled
- [ ] Polling interval matches latency needs
- [ ] Message payloads kept small

### Projections

- [ ] Batch processing implemented
- [ ] Appropriate indexes on projection stores
- [ ] Denormalization used where beneficial
- [ ] Caching enabled for hot data

### General

- [ ] OpenTelemetry metrics enabled
- [ ] Query profiling active
- [ ] Health checks monitoring resources
- [ ] Load testing performed

## Quick Wins

| Optimization | Impact | Effort |
|--------------|--------|--------|
| Enable snapshots | High | Low |
| Add missing indexes | High | Low |
| Increase batch sizes | Medium | Low |
| Enable caching | Medium | Low |
| Optimize message size | Medium | Medium |
| Add parallelism | Medium | Low |
| Denormalize projections | High | High |
| Connection pool tuning | Medium | Low |

## See Also

- [Caching](../performance/caching.md) — Distributed and in-memory caching strategies
- [Auto-Freeze](../performance/auto-freeze.md) — Automatic aggregate freezing for performance optimization
- [Metrics Reference](../observability/metrics-reference.md) — Complete list of available performance metrics
- [Resilience with Polly](resilience-polly.md) — Retry policies and circuit breakers for resilient operations
