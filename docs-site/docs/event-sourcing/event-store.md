---
sidebar_position: 4
title: Event Store
description: Persist and load events from the event store
---

# Event Store

The event store is the persistence layer for event-sourced aggregates. It stores events immutably with optimistic concurrency control.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
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

    // Get events for outbox pattern
    ValueTask<IReadOnlyList<StoredEvent>> GetUndispatchedEventsAsync(
        int batchSize,
        CancellationToken cancellationToken);

    // Mark event as published
    ValueTask MarkEventAsDispatchedAsync(
        string eventId,
        CancellationToken cancellationToken);
}
```

> **Note:** Methods return `ValueTask` to avoid allocations for synchronous completions (e.g., cache hits). `StoredEvent` is a wrapper that contains the deserialized event plus metadata like version and timestamp.

## Configuration

### SQL Server

```csharp
services.AddExcaliburEventSourcing(builder =>
{
    builder.UseEventStore<SqlServerEventStore>();
});

// SQL Server event store is typically added via Excalibur.Hosting
services.AddSqlServerEventStore(connectionString);
```

### PostgreSQL

```csharp
// With connection string
services.AddPostgresEventStore(connectionString);

// Or with options
services.AddPostgresEventStore(connectionString, options =>
{
    options.SchemaName = "events";
});
```

See [Event Store Providers](providers.md) for full PostgreSQL setup details including connection factories.

### In-Memory (Testing)

```csharp
services.AddExcaliburEventSourcing(builder =>
{
    builder.UseEventStore<InMemoryEventStore>();
});
```

## Database Schema

### SQL Server Schema

```sql
CREATE TABLE [events].[Events] (
    [Id] BIGINT IDENTITY(1,1) NOT NULL,
    [EventId] NVARCHAR(100) NOT NULL,
    [AggregateId] NVARCHAR(100) NOT NULL,
    [AggregateType] NVARCHAR(500) NOT NULL,
    [Version] BIGINT NOT NULL,
    [EventType] NVARCHAR(500) NOT NULL,
    [EventData] NVARCHAR(MAX) NOT NULL,
    [Metadata] NVARCHAR(MAX) NULL,
    [OccurredAt] DATETIME2 NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [DispatchedAt] DATETIME2 NULL,

    CONSTRAINT [PK_Events] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UQ_Events_EventId] UNIQUE ([EventId]),
    CONSTRAINT [UQ_Events_Aggregate_Version] UNIQUE ([AggregateId], [Version])
);

CREATE INDEX [IX_Events_AggregateId] ON [events].[Events] ([AggregateId], [Version]);
CREATE INDEX [IX_Events_Undispatched] ON [events].[Events] ([DispatchedAt]) WHERE [DispatchedAt] IS NULL;
CREATE INDEX [IX_Events_EventType] ON [events].[Events] ([EventType], [OccurredAt]);
```

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
    // Concurrency conflict - another process modified the aggregate
    // result.ErrorMessage contains version mismatch details
    throw new ConcurrencyException(result.ErrorMessage!);
}
```

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

## Event Streams

For global stream reading and projections, see the [Projections](projections.md) documentation.

> **Note:** The base `IEventStore` interface focuses on aggregate-level operations. Global stream reading is typically handled by projection infrastructure or CDC (Change Data Capture) patterns.

## Event Serialization

### Configure Serializer

```csharp
// Register event sourcing with SQL Server
services.AddExcaliburEventSourcing();

// Configure serialization via DI
services.AddJsonSerialization(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});
```

### Type Discovery

Event types are discovered automatically by the serializer based on their `EventType` property (defaults to the class name). For custom type names, override the property in your event class:

```csharp
public sealed record OrderCreated(Guid OrderId, string CustomerId) : DomainEventBase
{
    public override string AggregateId => OrderId.ToString();
    public override string EventType => "order.created.v1";
}
```

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
services.AddSqlServerEventSourcing(options =>
{
    options.ConnectionString = connectionString;
    options.RegisterHealthChecks = true; // Default: true
});
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
