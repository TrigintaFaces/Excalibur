---
sidebar_position: 3
title: Performance Tuning
description: Optimize Excalibur application performance
---

# Performance Tuning

This guide covers performance optimization for event sourcing, outbox processing, and projections.

## Before You Start

- **.NET 10.0**
- A deployed Excalibur application with observability enabled
- Familiarity with [metrics reference](../observability/metrics-reference.md) and [caching](../performance/caching.md)

## Performance Benchmarks

### Dispatch Measurements by Code Epoch

The figures below are historical October 2026 measurements described in the
[performance overview](../performance/index.md); rows marked as the September 2026 epoch
have not been re-measured against the current source. None of these are production
latencies -- the command handler is a no-op and query handlers double an input ID.

The October 19 command and typed query rows each come from three
separate original-observation processes. The query uses the runtime selected for that historical run after the fixture rental-lifecycle repair. Timing increased on both peers
from the earlier isolated-marker experiment; its cause remains unresolved. These
observations do not certify the entire rebuilt checkout or NativeAOT performance.

| Operation | Mean | Allocated | Source |
|-----------|---------|-----------|-------|
| Single command dispatch | ~65 ns | 96 B | Historical observation, 19 October 2026 |
| Typed query dispatch, default pipeline | ~136 ns | 288 B | Historical observation, 19 October 2026 |
| Untyped query dispatch completion | 63.7 ns | 192 B | September 2026 epoch; returned-payload parity unverified |
| Nominal three-middleware fixture | 71.7 ns | 240 B | September 2026 epoch |

Historical pipeline stage execution and parity remain unverified. Current-source
controls found that profile selection cleared the Dispatch stages; corrected setup
is a separate workload epoch (`artifacts/benchmarks/current-refresh-20261101/pipeline-fixture-fix-evidence-r1.json`).

Only the untyped query and nominal three-middleware rows come from the WarmPath comparison epoch of 2026-09-05
(`benchmarks/baselines/net10.0/dispatch-comparative-20260905/results/`): respectively
`MediatRWarmPathComparisonBenchmarks` and `PipelineWarmPathComparisonBenchmarks`.

The untyped query result does not establish that the returned object carries the
handler response. See the separate
[October 19 observations](../performance/competitor-comparison.md#current-query-observations-19-october-2026)
for payload-verified response APIs; their results do not replace this historical epoch.

**Measure allocation in the consuming application.** Dispatch supports ambient message context
for nested causation, correlation, tenant and user propagation. Earlier diagnostic notes attributed
72 B of a 96 B command result to ambient-context publication and reported approximately 160 B
with one additional `AsyncLocal` value and 992 B with fifteen. That attribution and scaling
have not been re-established for the current source and runtime; they do not prove a fixed
number of `ExecutionContext` copies or a universal allocation floor. Measure the complete
application workload.

Allocation figures describe that benchmark workload; measure the application's configuration
and run-to-run variation before setting budgets.

A long-run allocation figure of **64.88 MB per 100K dispatches** was measured on 2026-03-11 and has
not been re-taken since. Treat it as historical rather than current: it measures a different workload
(middleware and context included), and it should not be reconciled with the per-dispatch figure above
by arithmetic.

### Setting your own budgets

:::caution These are planning placeholders, not measurements

The figures below have **not** been measured against this framework. Storage, network, handler,
and queueing costs depend on the application; the historical dispatch microbenchmarks do not
establish their relative contributions. Treat these as independent planning placeholders, replace
them with measured latency distributions and sustained completed throughput, and do not quote them
as characteristics of Excalibur. A latency budget alone does not establish throughput capacity.
:::

| Operation | Starting latency budget | Starting throughput budget |
|-----------|----------------|-------------------|
| Aggregate load (with snapshot) | `<10ms` | 1,000/s |
| Aggregate load (100 events) | `<50ms` | 200/s |
| Aggregate save (single event) | `<20ms` | 500/s |
| Outbox message processing | `<5ms/message` | 10,000/s |
| Projection update | `<10ms` | 5,000/s |

## Event Store Optimization

### Snapshot Tuning

Snapshots can reduce event replay work. Measure read savings against snapshot creation, storage,
and loading costs before choosing an interval:

```csharp
es.UseCompositeSnapshotStrategy(composite =>
{
    // Snapshot every 100 events OR every hour
    composite.AddIntervalStrategy(100)
             .AddTimeBasedStrategy(TimeSpan.FromHours(1));
});
```

#### Measuring Snapshot Effectiveness

Measure replayed event counts and load duration with and without snapshots for equivalent streams.
For database analysis, use the provider's deployed schema and complete stream identity, including tenant
and aggregate type where applicable. Select the latest applicable snapshot and handle streams without a
snapshot using the provider's initial-version convention; a join on stream ID alone may miscount events.

### Index Optimization

#### SQL Server

The index definitions below are illustrative shapes, not migration scripts for a verified provider schema.
Match deployed column names, complete stream identity, existing indexes and query plans before applying them.

```sql
-- Candidate stream lookup
CREATE NONCLUSTERED INDEX IX_Events_StreamId_Version
ON EventSourcing.Events (StreamId, Version)
INCLUDE (EventType, Data, Timestamp);

-- For global sequence queries (CDC, projections)
CREATE NONCLUSTERED INDEX IX_Events_SequenceNumber
ON EventSourcing.Events (SequenceNumber);

-- For time-based queries
CREATE NONCLUSTERED INDEX IX_Events_Timestamp
ON EventSourcing.Events (Timestamp);
```

#### PostgreSQL

```sql
-- Stream lookup with covering index
CREATE INDEX idx_events_stream ON event_sourcing.events (stream_id, version)
INCLUDE (event_type, data, timestamp);

-- BRIN index for time-series (space efficient)
CREATE INDEX idx_events_timestamp_brin
ON event_sourcing.events USING BRIN (timestamp);

```

These are alternative index candidates. PostgreSQL index predicates require immutable expressions;
a moving `NOW()` cutoff is unsuitable. See [CREATE INDEX](https://www.postgresql.org/docs/current/sql-createindex.html).

### Query Optimization

#### Batch Loading

The following illustrates different application read models, not equivalent benchmark workloads.
Use projections only when their data and consistency contract meet the request. These application-specific
APIs do not establish a query count; inspect the implementation and generated queries.

```csharp
// Load aggregate state when the operation requires it
foreach (var id in orderIds)
{
    var order = await repository.GetByIdAsync(id, ct);
}

// Illustrative application projection API for a compatible read contract
var orders = await orderProjection.GetByIdsAsync(orderIds, ct);
```

#### Projection Queries

```csharp
// Load events when event history is required
var events = await _eventStore.LoadAsync("customer-123", "Order", ct);

// Query a projection when its data and consistency meet the request
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

The following values are unverified tuning starting points. Measure completed work, latency and resource use for your workload.

| Scenario | Candidate Batch Size |
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

These are unverified starting points, not measured optima. Use parallel processing only when the workload's ordering and concurrency contracts permit it.

| CPU Cores | Candidate Parallelism |
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

These intervals are unverified starting points. Polling interval alone does not guarantee end-to-end latency; include queueing, processing and downstream delays.

| Latency Goal | Candidate Polling Interval |
|---------------------|------------------|
| Real-time (`<100ms`) | 50-100ms |
| Near real-time (`<1s`) | 200-500ms |
| Standard | 1-5s |
| Relaxed | 10-30s |

### Message Size

Include the data required by the consumer contract. Measure serialization, transport and additional
lookup costs before reducing payloads; removing required state changes the work and may break consumers.

```csharp
// A contract that carries item and state data
public record OrderCreated(
    Guid OrderId,
    string CustomerId,
    List<OrderItem> Items,
    string FullJsonState
);

// Alternative only when consumers do not require the omitted data
public record OrderCreated(
    Guid OrderId,
    string CustomerId
);
```

## Projection Optimization

### Choosing a Projection Handler Tier

The October 17 repository report
`docs/performance/projection-handlers-2026-10-17.md` measured 48 → 0 B/op for the
warmed plain synchronous `Apply` path across three matched managed pairs. That
observation does not guarantee zero allocations in user callbacks or establish a
universal ranking against DI handlers. Native functional/allocation controls were
separate from managed timing; no NativeAOT latency improvement was measured.

Excalibur provides two handler tiers. For simple cases, use `When<T>` lambdas. For complex logic needing DI, implement `IProjectionEventHandler<TProjection, TEvent>`:

```csharp
// Simple synchronous lambda; measure the complete callback workload
builder.AddProjection<OrderProjection>(p => p
    .Async()
    .When<OrderCreated>((proj, e) =>
    {
        proj.OrderId = e.OrderId;
        proj.CustomerId = e.CustomerId;
        proj.Status = "Created";
    }));

// Tier 3: DI-resolved handler (when you need logging, async, or custom IDs)
public sealed class OrderCreatedHandler
    : IProjectionEventHandler<OrderProjection, OrderCreated>
{
    private readonly ILogger<OrderCreatedHandler> _logger;

    public OrderCreatedHandler(ILogger<OrderCreatedHandler> logger)
        => _logger = logger;

    public Task HandleAsync(
        OrderProjection projection,
        OrderCreated @event,
        ProjectionHandlerContext context,
        CancellationToken cancellationToken)
    {
        projection.OrderId = @event.OrderId;
        projection.CustomerId = @event.CustomerId;
        projection.Status = "Created";
        _logger.LogDebug("Projected order {OrderId}", @event.OrderId);
        return Task.CompletedTask;
    }
}

// Registration -- handler is auto-registered in DI as Transient
builder.AddProjection<OrderProjection>(p => p
    .Async()
    .WhenHandledBy<OrderCreated, OrderCreatedHandler>());
```

Asynchronous projection handlers execute on the projection host. Slow handlers can increase projection
lag and cause session-consistent reads to report `BoundExpired`. Shared database, CPU, or connection-pool
contention can also affect write latency; measure the complete application under load.

### Batch Projection Updates

The example below registers CDC processing. It does not implement batched projection writes.
If batching is appropriate, implement it in the projection writer and verify ordering, failure recovery,
and completed throughput with equivalent work.

```csharp
// EnableProjectionProcessing() applies events per projection as the host reads the global stream.
// Illustrative CDC registration; projection-write batching requires a separate implementation.
services.AddCdcProcessor(cdc =>
{
    cdc.UseSqlServer(sql => sql.ConnectionString(connectionString))
       .TrackTable("dbo.Orders", table =>
       {
           table.MapInsert<OrderCreatedEvent>()
                .MapUpdate<OrderUpdatedEvent>();
       })
       .EnableBackgroundProcessing();
});
```

### Denormalization

Denormalization may reduce query work at the cost of update and storage work; measure the actual queries:

```csharp
// Illustrative denormalized projection; measure read and update costs
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

These are configuration examples, not measured sizing recommendations. Verify application
compatibility with transaction pooling and measure database capacity, connection waits and resource use.

### Connection Health

Use [SqlClient event counters](https://learn.microsoft.com/en-us/sql/connect/ado-net/event-counters)
to observe connection resources, including `number-of-free-connections`. Correlate these with connection
acquisition latency and timeout errors. A low free-connection count alone does not prove pool exhaustion,
and provider-wide counters should not be interpreted as a specific connection string's pool capacity.

## Caching

### Aggregate Caching

Caching may reduce repeated reads, but mutable aggregates require ownership isolation, version checks,
and invalidation consistent with the write contract. Do not share a mutable aggregate instance across
independent operations. The decorator below is an application-specific illustration, not a complete
implementation of those safeguards:

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

Snapshot caching may reduce repeated reads when hit rates justify its memory and invalidation costs.
Absolute expiration limits entry age; it does not guarantee that a cached snapshot is current after a
write. Choose versioned keys or invalidation consistent with the application's read contract. See
[Microsoft's .NET 10 memory-cache guidance](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/memory?view=aspnetcore-10.0).

```csharp
services.AddMemoryCache();

// Example expiration; provide versioning/invalidation for the required consistency
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

-- Weighted mean duration for regular executions in intervals overlapping the last hour.
-- Boundary intervals may contain executions outside that hour; this is not p99 latency.
DECLARE @since datetimeoffset = DATEADD(HOUR, -1, SYSDATETIMEOFFSET());
SELECT TOP 10
    qsq.query_id,
    qsqt.query_sql_text,
    SUM(rs.avg_duration * rs.count_executions)
        / NULLIF(SUM(rs.count_executions), 0) / 1000.0 AS avg_ms,
    SUM(rs.count_executions) AS executions
FROM sys.query_store_query qsq
JOIN sys.query_store_query_text qsqt ON qsq.query_text_id = qsqt.query_text_id
JOIN sys.query_store_plan qsp ON qsq.query_id = qsp.query_id
JOIN sys.query_store_runtime_stats rs ON qsp.plan_id = rs.plan_id
JOIN sys.query_store_runtime_stats_interval rsi
    ON rs.runtime_stats_interval_id = rsi.runtime_stats_interval_id
WHERE rs.execution_type = 0 AND rsi.end_time > @since
GROUP BY qsq.query_id, qsqt.query_sql_text
ORDER BY avg_ms DESC;
```

The query uses interval statistics described in [Microsoft's Query Store guidance](https://learn.microsoft.com/en-us/sql/relational-databases/performance/tune-performance-with-the-query-store).
It has not been executed against your database; verify permissions and Query Store availability before use.

## Performance Checklist

### Event Store

- [ ] Snapshot use and interval evaluated against replay, write and storage costs
- [ ] Indexes created for common access patterns
- [ ] Connection pool sized appropriately
- [ ] Batching evaluated while preserving ordering, consistency and recovery requirements

### Outbox

- [ ] Batch size optimized for workload
- [ ] Parallelism measured only where ordering and concurrency contracts permit it
- [ ] Polling interval matches latency needs
- [ ] Payload size evaluated while preserving required consumer data

### Projections

- [ ] Batching evaluated where ordering and recovery contracts permit it
- [ ] Appropriate indexes on projection stores
- [ ] Denormalization used where beneficial
- [ ] Caching evaluated with ownership, invalidation and measured hit rate

### General

- [ ] OpenTelemetry metrics enabled
- [ ] Query profiling active
- [ ] Health checks monitoring resources
- [ ] Load testing performed

## Candidates to Measure

These are investigation candidates, not measured impact or effort rankings. Use profiles and matched
before/after measurements to select changes; retain required ordering, consistency, and retention behavior.

| Candidate | Evidence to inspect |
|-----------|---------------------|
| Snapshots | Replay cost versus snapshot creation and loading cost |
| Indexes | Query plans, logical reads, and added write cost |
| Batch size | Completed throughput, tail latency, memory, and recovery work |
| Caching | Hit rate, memory, invalidation cost, and consistency |
| Message size | Serialization and transport costs with equivalent required data |
| Parallelism | Contention and throughput where ordering permits concurrent work |
| Denormalized projections | Read savings versus update and storage costs |
| Connection pool size | Wait time, database capacity, and resource use |

## See Also

- [Caching](../performance/caching.md) — Distributed and in-memory caching strategies
- [Auto-Freeze](../performance/auto-freeze.md) — Automatic aggregate freezing for performance optimization
- [Metrics Reference](../observability/metrics-reference.md) — Complete list of available performance metrics
- [Resilience with Polly](resilience-polly.md) — Retry policies and circuit breakers for resilient operations
