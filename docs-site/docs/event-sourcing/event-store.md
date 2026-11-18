---
sidebar_position: 4
title: Event Store
description: Persist and load events from the event store
---

# Event Store

The event store is the persistence layer for event-sourced aggregates. It stores events immutably with optimistic concurrency control.

## Before You Start

- **.NET 10.0**
- Install the required packages:
  ```bash
  dotnet add package Excalibur.EventSourcing
  dotnet add package Excalibur.EventSourcing.SqlServer  # or your provider
  ```
- Familiarity with [event sourcing concepts](./index.md) and [domain events](./domain-events.md)

## Core Interface

```csharp
public interface IEventStore
{
    // Load all events for an aggregate
    ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
        string aggregateId,
        string aggregateType,
        CancellationToken cancellationToken);

    // Load events starting from a version (used with snapshots)
    ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
        string aggregateId,
        string aggregateType,
        long fromVersion,
        CancellationToken cancellationToken);

    // Append events with optimistic concurrency
    ValueTask<AppendResult> AppendAsync(
        string aggregateId,
        string aggregateType,
        IEnumerable<IDomainEvent> events,
        long expectedVersion,
        CancellationToken cancellationToken);
}
```

> **Note:** Methods return `ValueTask` to avoid allocations for synchronous completions (e.g., cache hits). `StoredEvent` is a wrapper that contains the deserialized event plus metadata like version and timestamp.

## Configuration

### SQL Server

```csharp
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(builder =>
{
    builder.UseEventStore<SqlServerEventStore>();
}));

// SQL Server event store is typically added via Excalibur.Hosting
services.AddSqlServerEventStore(opts => opts.ConnectionString = connectionString);
```

### PostgreSQL

```csharp
// Fluent builder registration (5 canonical connection overloads)
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(es =>
{
    es.UsePostgres(pg =>
    {
        pg.ConnectionString(connectionString)
          .EventStoreSchema("events");
    });
}));

// With pre-configured NpgsqlDataSource
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(es =>
{
    es.UsePostgres(pg => pg.DataSource(npgsqlDataSource));
}));
```

See [Event Store Providers](providers.md) for full PostgreSQL setup details including all 5 connection overloads.

### In-Memory (Testing)

```csharp
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(builder =>
{
    builder.UseEventStore<InMemoryEventStore>();
}));
```

## Database Schema

### SQL Server Schema

```sql
CREATE TABLE [events].[Events] (
    -- Assigned by the STORE, not the database: allocated from the [events].[EventsPosition] counter
    -- row inside the appending transaction. See that table below for why.
    [Position] BIGINT NOT NULL,
    [EventId] NVARCHAR(256) NOT NULL,
    [AggregateId] NVARCHAR(256) NOT NULL,
    [AggregateType] NVARCHAR(256) NOT NULL,
    [Version] BIGINT NOT NULL,
    [EventType] NVARCHAR(512) NOT NULL,
    -- Binary, and MUST be nullable: erasure sets EventData to NULL to tombstone an
    -- event while preserving its position. A NOT NULL column makes erasure fail.
    [EventData] VARBINARY(MAX) NULL,
    [Metadata] VARBINARY(MAX) NULL,
    [Timestamp] DATETIMEOFFSET NOT NULL,
    -- Set when the payload has been moved to cold storage; the row and its position stay.
    [ArchivedAt] DATETIMEOFFSET NULL,
    -- NOT NULL, and part of the stream key below. Every write binds a tenant term: a
    -- single-tenant (unscoped) host stores the reserved '__untenanted__' sentinel here rather
    -- than omitting the column, so there is exactly one way to say "this event has no tenant"
    -- and it is a value. The sentinel is concrete because a NULL discriminator cannot
    -- participate in a unique constraint on any provider.
    -- Binary collation: the server default is typically case-insensitive, so without it a
    -- scoped read matches another tenant whose identifier differs only by case.
    [TenantId] NVARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL,

    CONSTRAINT [PK_Events_Position] PRIMARY KEY CLUSTERED ([Position]),
    CONSTRAINT [UQ_Events_EventId] UNIQUE ([EventId]),
    -- The tenant participates in stream IDENTITY, not merely in read filters, which makes
    -- optimistic concurrency per-tenant rather than global. Leaving it out of this key while
    -- the version probe is tenant-scoped makes the two disagree: the probe reports "no such
    -- aggregate" for a tenant that has never used the id, while the insert collides with
    -- another tenant's row. The caller sees a retryable concurrency conflict whose retry
    -- re-probes and collides again, so it never converges.
    CONSTRAINT [UQ_Events_Aggregate_Version]
        UNIQUE ([AggregateId], [AggregateType], [Version], [TenantId])
);

-- The global position counter. The store allocates each append's position by UPDATEing this row
-- inside the appending transaction: the row lock is released only at COMMIT and the increment rolls
-- back with the transaction, so an aborted append burns no position. The committed positions are
-- therefore a contiguous prefix at each instant. Safe checkpoint advancement ALSO requires a reader
-- that cannot skip a lower position while concurrent commits occur. The counter alone does not make
-- a scan atomic. An IDENTITY alone cannot provide these guarantees: rollback can leave a permanent
-- hole and concurrent appends can commit in the opposite order to their allocated positions.
CREATE TABLE [events].[EventsPosition] (
    -- Singleton by construction: a second counter row would reintroduce gaps.
    [Id]    TINYINT NOT NULL CONSTRAINT [PK_EventsPosition] PRIMARY KEY,
    [Value] BIGINT  NOT NULL,
    CONSTRAINT [CK_EventsPosition_Singleton] CHECK ([Id] = 1)
);
GO

-- Seeded from the table's own high-water mark rather than 0: Position is the PRIMARY KEY, so a
-- counter seeded at 0 against a table that already holds events would reissue existing values.
INSERT INTO [events].[EventsPosition] ([Id], [Value])
SELECT 1, ISNULL((SELECT MAX([Position]) FROM [events].[Events]), 0);
GO


CREATE INDEX [IX_Events_Aggregate] ON [events].[Events] ([AggregateId], [AggregateType], [Version]);
CREATE INDEX [IX_Events_EventType] ON [events].[Events] ([EventType], [Timestamp]);
```

:::caution Ordering and performance

The shared counter row provides contiguous positions. Writers hold its lock until
commit, so competing append transactions can wait for that lock. Preserve this
ordering contract when considering alternative allocation schemes: an `IDENTITY`
allocation alone does not establish contiguous committed visibility.

**Historical SQL Server observation.** These retained figures came from an
interleaved insert/counter experiment on one machine. The source revision,
complete environment, repetitions and uncertainty are not recorded here. They do
not establish current capacity, a speedup or zero counter-allocation cost.

| | ms per operation |
|---|---:|
| a plain insert in its own transaction | 2.00 |
| **the same insert plus the counter allocation, one transaction** | **1.91** |
| the counter allocation and insert, with the commit removed from the measurement | 0.020 |

The 0.020 ms row has a different measurement boundary from the transaction rows.
The 2.00 versus 1.91 ms observations do not establish that adding the counter is
faster or free. Throughput depends on the full append operation, transaction size,
storage, contention and database behavior; commit latency alone cannot predict it.
Faster storage can change those costs without removing the serialization requirement.

The following serial insert probe observes one transaction workload. It does not
measure the event store's append ceiling. Use a disposable probe table.

```sql
-- Elapsed milliseconds per serial insert transaction, including loop overhead.
DECLARE @i INT = 0, @t0 DATETIME2(7) = SYSUTCDATETIME();
WHILE @i < 1000
BEGIN
    BEGIN TRAN;
    INSERT dbo.YourProbeTable (Payload) VALUES ('x');
    COMMIT;
    SET @i += 1;
END
SELECT CAST(DATEDIFF_BIG(microsecond, @t0, SYSUTCDATETIME()) / 1000.0 / 1000 AS DECIMAL(10, 3)) AS ms_per_commit;
```

Repeat under controlled conditions. Variation warrants investigating the complete
workload and environment; it does not identify storage as the sole cause. An earlier
container-volume observation ranged from 1.9 to 4.2 ms within half an hour; its
incomplete provenance makes it historical context, not a current capacity figure.

A multi-event append shares one counter-block allocation and transaction. Measure
both per-append and per-event costs; cost is not necessarily independent of event count.

These observations are neither a performance floor nor a specification. Multimodal
distributions make a single mean an incomplete summary. Preserve distributions and
process-level variation before drawing comparisons.

A contiguous committed prefix at one instant is not enough to advance a subscriber
checkpoint safely: a scan can span concurrent commits. The registered SQL Server
`IGlobalStreamQuery.ReadAllAsync` uses a contiguity guard and stops before a gap;
resume from the last delivered position. A type-filtered page is legitimately sparse
and does not supply that same protection, so do not use its maximum position to
advance a shared unfiltered checkpoint. Keep the registered reader and its source
binding when composing projections. Performance changes must preserve both allocation
and reader visibility guarantees; these observations establish no competitor's
correctness or speed.

:::

:::note Upgrading an existing event-store schema

The `TenantId` column persists tenant isolation across load, append, and erasure. If you already run an
earlier `Events` schema, add it before deploying — otherwise, once an ambient tenant is present, every load
and every append fails with `Invalid column name 'TenantId'`:

```sql
ALTER TABLE [events].[Events] ADD [TenantId] NVARCHAR(64) COLLATE Latin1_General_BIN2 NULL;
```

On PostgreSQL the column is `tenant_id`:

```sql
ALTER TABLE events ADD COLUMN IF NOT EXISTS tenant_id VARCHAR(64);
```

:::

### Schema Setup

Create the required tables using the SQL scripts above, or use database migration tools like:
- EF Core migrations (for schema management only)
- DbUp
- Flyway
- Custom SQL deployment scripts

## Using the Event Store

### Through Repository (Recommended)

```csharp
public class OrderHandler : IActionHandler<CreateOrderAction>
{
    private readonly IEventSourcedRepository<Order, Guid> _repository;

    public async Task HandleAsync(CreateOrderAction action, CancellationToken ct)
    {
        var order = Order.Create(Guid.NewGuid(), action.CustomerId);
        await _repository.SaveAsync(order, ct);
    }
}
```

### Direct Access

```csharp
public class EventExplorer
{
    private readonly IEventStore _eventStore;

    public async Task<IReadOnlyList<StoredEvent>> GetOrderHistory(
        Guid orderId, CancellationToken ct)
    {
        return await _eventStore.LoadAsync(
            orderId.ToString(), "Order", ct);
    }

    public async Task<IReadOnlyList<StoredEvent>> GetEventsSince(
        Guid orderId, long version, CancellationToken ct)
    {
        return await _eventStore.LoadAsync(
            orderId.ToString(), "Order", version, ct);
    }
}
```

## Optimistic Concurrency

The event store uses version numbers for optimistic concurrency:

```csharp
// Append expects specific version
var result = await eventStore.AppendAsync(
    aggregateId: "order-123",
    aggregateType: "Order",
    events: newEvents,
    expectedVersion: 5,  // Must match current version
    ct);

if (!result.Success)
{
    // Not necessarily a conflict: !Success covers BOTH a concurrency conflict and an
    // outright failure, and they call for different responses. Read result.Outcome to
    // tell them apart -- see "Reading the outcome" below.
    throw new ConcurrencyException(result.ErrorMessage!);
}
```

### Reading the outcome

`AppendResult.Outcome` states which of four things happened, rather than leaving you to infer it from a
boolean and an error string:

| `AppendOutcome` | meaning | `Success` | `NextExpectedVersion` |
| --- | --- | --- | --- |
| `Committed` | written by this call | `true` | version of the last event written |
| `AlreadyCommitted` | written by an **earlier** call whose acknowledgement was lost | `true` | the version that call landed at |
| `ConcurrencyConflict` | another writer holds the version you expected | `false` | the **measured** actual version; `null` only when no version read succeeded |
| `Failed` | nothing was written | `false` | always `null` |

`Success` is true for `Committed` and `AlreadyCommitted` alike — in both, the events you asked to append are
durable — so a host that only asks *did it work* is answered correctly without change.

**`NextExpectedVersion` is `long?`.** Every non-null value is one the store *measured*, never your own
expected version echoed back. Versions are zero-based, so a `-1` is the ordinary value meaning *this stream
does not exist* rather than a failure sentinel; *not measured* is `null` instead. A non-null value may be
passed straight back as the next expected version; on `null` you must reload.

**`AlreadyCommitted` carries one obligation.** The rows are durable but were written earlier, so they may
have been acted on in between — present does not imply retrievable. A caller still holding the live payloads
must not assume they are what the store would now return; read them back if your next step depends on the
stored form.

The cloud-native stores return `CloudAppendResult` with the same discriminator as `CloudAppendOutcome`.
Full details, including the factory signatures for anyone implementing a store:
[an append result states its outcome](../migration/append-result-outcome.md).

### Handling Conflicts

```csharp
public async Task HandleWithRetry(UpdateOrderAction action, CancellationToken ct)
{
    const int maxRetries = 3;
    var attempt = 0;

    while (attempt < maxRetries)
    {
        try
        {
            var order = await _repository.GetByIdAsync(action.OrderId, ct);
            order.UpdateShippingAddress(action.Address);
            await _repository.SaveAsync(order, ct);
            return;
        }
        catch (ConcurrencyException)
        {
            attempt++;
            if (attempt >= maxRetries)
                throw;

            // Small delay before retry
            await Task.Delay(100 * attempt, ct);
        }
    }
}
```

### Two different failures, two different retries

The pattern above is the right response to a **genuine** conflict: another writer really did take your
version, so you reload, re-apply the command against the new state, and save. That produces new events with
new identifiers, which is correct — they are genuinely new events.

It is **not** the right response to an **ambiguous** outcome: a timeout, a dropped connection, or any
failure where you never learned whether the append committed. There the events may already be in the
stream, and re-deriving them from a reload creates a second copy of the same business event at the next
version, where nothing can detect it.

For that case, re-present **the same events**:

```csharp
// The events were built once, before the first attempt. On an ambiguous failure, present THE SAME
// instances again at THE SAME expected version -- do NOT rebuild them from a reload.
var events = order.DequeueUncommittedEvents();

for (var attempt = 0; attempt < 3; attempt++)
{
    try
    {
        var result = await _eventStore.AppendAsync(
            order.Id, nameof(Order), events, expectedVersion, ct);

        if (result.Success)
        {
            return;
        }

        if (result.IsConcurrencyConflict)
        {
            // A real conflict: somebody else took the version. Reload and re-apply instead.
            break;
        }
    }
    catch (Exception ex) when (ex is TimeoutException or IOException)
    {
        // Ambiguous. The append may have committed. Retrying with the SAME events is safe.
    }
}
```

The store recognises the retry because the events carry the same identifiers, finds them already present,
and reports **success** rather than a conflict — specifically `AppendOutcome.AlreadyCommitted`, which is a
success you can distinguish from a fresh write. Read it when you need to know that the rows predate this
call and may have been acted on since.

### The identifier contract

An event identifier names a **business event**, not an attempt at writing one.

- **Retrying the same command must present the same `EventId`.** That is what lets the store recognise its
  own earlier write and answer honestly.
- **Minting a fresh identifier per attempt is safe but weaker.** The store cannot recognise the retry, so
  you get at-least-once delivery into the stream and your handlers must be idempotent.
- **Never reuse an identifier for a different business event.** This is the one direction the store cannot
  defend against: it will recognise the identifier, report success, and your second event will never be
  written.

## Event Streams

For global stream reading and projections, see the [Projections](projections.md) documentation.

SQL Server and PostgreSQL global queries span tenants. Both `ReadAllAsync` and
`ReadByEventTypeAsync` return each row's stored tenant term in `StoredEvent.TenantId`.
Tenant identifiers retain their casing; `Acme` and `acme` remain distinct. Legacy database rows
with a null tenant column are reported with the explicit `__untenanted__` term.

A null `StoredEvent.TenantId` means the provider or serialized representation did not supply
tenant provenance. It does not mean the event is untenanted. Do not substitute an ambient tenant
or use an unknown value to select a tenant's cold-storage partition. Older archive records without
this property remain readable with an unknown value. This property describes provenance; it does
not add authorization or tenant filtering to the estate-wide global query.

> **Note:** The base `IEventStore` interface focuses on aggregate-level operations. Global stream reading is typically handled by projection infrastructure or CDC (Change Data Capture) patterns.

## Event Serialization

### Configure Serializer

```csharp
// Register event sourcing with SQL Server
services.AddExcalibur(excalibur => excalibur.AddEventSourcing());

// Configure serialization via DI
services.AddJsonSerialization(options =>
{
    options.ConfigureSerializer = json =>
    {
        json.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    };
});
```

### Type Discovery

The name an event is stored under is declared on the event type with `[MessageName]`. Nothing is derived from the CLR type, so the type is free to move namespace or assembly without changing what is already written. The `[EventType]` column above holds exactly this declared name:

```csharp
[MessageName("Contoso.Orders.OrderCreated")]
public sealed record OrderCreated(Guid OrderId, string CustomerId) : DomainEvent;
```

The attribute is required — registering an event type that declares no name throws. To change the name later, declare the new one and keep the old as a `[MessageNameAlias]`. See [Stable Message Names](domain-events.md#stable-message-names).

## Archiving and Retention

Archiving is typically handled at the database level. Consider:

- **Table partitioning** by date for efficient archival
- **Database maintenance jobs** to move old events to archive tables
- **Backup strategies** that preserve event history

### GDPR Compliance (Right to Erasure)

:::danger Logical Delete is NOT GDPR Compliant

Simply marking events as "deleted" in metadata does **not** satisfy GDPR Article 17 (Right to Erasure). The personal data still exists in your database and is technically accessible.
:::

**Compliant approaches for event sourcing:**

| Approach | Description |
|----------|-------------|
| **Crypto-shredding** | Encrypt PII with per-user keys; delete key to make data permanently unreadable |
| **Event replacement** | Replace events containing PII with sanitized versions |
| **Physical deletion** | Delete events entirely (controversial, breaks immutability) |

**Recommended: Crypto-shredding**

Store PII encrypted with user-specific encryption keys. When a GDPR erasure request is received, delete the encryption key - the data becomes permanently unreadable.

```csharp
// Configure GDPR erasure with crypto-shredding
services.AddGdprErasure(options =>
{
    options.DefaultGracePeriod = TimeSpan.FromHours(72);
    options.RequireVerification = true;
});
```

See [GDPR Erasure](../compliance/gdpr-erasure.md) for complete implementation including:
- Erasure request workflow
- Legal hold management
- Compliance certificates
- Data inventory tracking

## Health Checks

```csharp
// SQL Server event sourcing automatically registers health checks
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(es =>
{
    es.UseSqlServer(sql =>
    {
        sql.ConnectionString(connectionString);
    });
}));
```

## Observability

### Metrics

```csharp
services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddExcaliburInstrumentation();
        // Emits:
        // - excalibur.eventstore.events.appended
        // - excalibur.eventstore.events.loaded
        // - excalibur.eventstore.concurrency.conflicts
    });
```

### Tracing

```csharp
services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddExcaliburInstrumentation();
        // Creates spans for:
        // - Load events
        // - Append events
        // - Stream reads
    });
```

## Best Practices

| Practice | Recommendation |
|----------|----------------|
| Indexing | Index on AggregateId + Version |
| Partitioning | Consider partitioning by AggregateId for large stores |
| Compression | Enable for EventData in large deployments |
| Backup | Regular backups - events are your source of truth |
| Monitoring | Alert on high concurrency conflict rates |

## Next Steps

- [Snapshots](snapshots.md) — Optimize loading with snapshots
- [Projections](projections.md) — Build read models from events
- [Aggregates](aggregates.md) — Use event store with aggregates

## See Also

- [Repositories](./repositories.md) — High-level API for loading and saving aggregates via the event store
- [Domain Events](./domain-events.md) — Define the events that get persisted to the store
- [Event Store Setup](../configuration/event-store-setup.md) — Step-by-step configuration guide for event store providers
- [Event Store Providers](./providers.md) — Provider-specific setup for SQL Server, PostgreSQL, and in-memory


## Saving events with transactional projections

TransactionalInline is available starting in `10.0.0-alpha.21`.

SQL Server, PostgreSQL, SQLite, and Oracle repositories can opt into
[TransactionalInline projections](./projections.md#transactionalinline-in-one-sql-server-database).
`SaveAsync` then commits the aggregate's events and its aggregate-local models in one database
transaction. A projection failure rolls back the append. Both stores must use the same relational database
and tenant partition, with a dedicated transactional model table.

This behavior belongs to the coordinated repository save, not ordinary `IEventStore.AppendAsync`.
Existing Inline projections still run after the event commit with a bounded wait; Async projections
still run independently. Neither mode becomes transactional simply by sharing a database.
