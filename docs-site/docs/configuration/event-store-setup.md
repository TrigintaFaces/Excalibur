---
sidebar_position: 2
title: Event Store Setup
description: Configure event stores and repositories for event sourcing
---

# Event Store Setup

This guide covers configuring event stores and registering aggregate repositories.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required packages:
  ```bash
  dotnet add package Excalibur.EventSourcing
  dotnet add package Excalibur.EventSourcing.SqlServer  # or your provider
  ```
- Familiarity with [event sourcing concepts](../event-sourcing/index.md) and [dependency injection](../core-concepts/dependency-injection.md)

## Basic Setup

```csharp
// Configure event sourcing with repositories
services.AddExcaliburEventSourcing(builder =>
{
    builder.AddRepository<OrderAggregate, Guid>(id => new OrderAggregate());
    builder.UseIntervalSnapshots(100);
});

// Add the event store provider
services.AddSqlServerEventStore(connectionString);
```

## Event Store Providers

### SQL Server

```bash
dotnet add package Excalibur.EventSourcing.SqlServer
```

```csharp
// Option 1: Simple registration with connection string
services.AddSqlServerEventStore(connectionString);

// Option 2: Full event sourcing setup with all stores
services.AddSqlServerEventSourcing(options =>
{
    options.ConnectionString = connectionString;
    options.RegisterHealthChecks = true;
});

// This registers:
// - IEventStore (SqlServerEventStore)
// - ISnapshotStore (SqlServerSnapshotStore)
// - IEventSourcedOutboxStore (SqlServerEventSourcedOutboxStore)
```

### Postgres

```bash
dotnet add package Excalibur.Data.Postgres
```

```csharp
// With connection string
services.AddPostgresEventStore(connectionString);

// Or with options
services.AddPostgresEventStore(connectionString, options =>
{
    options.SchemaName = "events";
});
```

### In-Memory (Testing)

```bash
dotnet add package Excalibur.EventSourcing.InMemory
```

```csharp
// In-memory event store for tests only
services.AddInMemoryEventStore();
```

## Repository Registration

### Basic Registration

Register repositories for your aggregates:

```csharp
services.AddExcaliburEventSourcing(builder =>
{
    builder.AddRepository<OrderAggregate, Guid>();
    builder.AddRepository<CustomerAggregate, Guid>();
    builder.AddRepository<InventoryAggregate, string>();
});
```

### Custom Factory

When your aggregate requires custom construction:

```csharp
services.AddExcaliburEventSourcing(builder =>
{
    builder.AddRepository<OrderAggregate, Guid>(
        key => new OrderAggregate(key, tenantId));
});
```

### String-Keyed Aggregates

For aggregates using string identifiers:

```csharp
services.AddExcaliburEventSourcing(builder =>
{
    builder.AddRepository<LegacyOrderAggregate>(
        key => new LegacyOrderAggregate(key));
});
```

## Event Serialization

### Default (System.Text.Json)

Events are serialized using the configured serializer:

```csharp
// Default JSON serialization
services.AddJsonSerialization();

// Or with options
services.AddJsonSerialization(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});
```

### Custom Serializer

```csharp
services.AddExcaliburEventSourcing(builder =>
{
    builder.UseEventSerializer<MessagePackEventSerializer>();
});
```

## Upcasting (Event Versioning)

Handle breaking changes in event schemas using message upcasters:

```csharp
services.AddExcaliburEventSourcing(builder =>
{
    builder.AddUpcastingPipeline(upcasting =>
    {
        // Register individual upcaster
        upcasting.RegisterUpcaster<OrderCreatedV1, OrderCreated>(
            new OrderCreatedV1ToV2Upcaster());

        // Or scan assembly for all upcasters
        upcasting.ScanAssembly(typeof(Program).Assembly);

        // Enable auto-upcasting during replay
        upcasting.EnableAutoUpcastOnReplay();
    });
});

// Define upcaster
public class OrderCreatedV1ToV2Upcaster : IMessageUpcaster<OrderCreatedV1, OrderCreated>
{
    public OrderCreated Upcast(OrderCreatedV1 source)
    {
        return new OrderCreated(source.AggregateId, source.Version)
        {
            OrderId = source.OrderId,
            CustomerId = source.CustomerName,  // Map renamed field
            CreatedAt = source.Timestamp
        };
    }
}
```

## Database Schema

### SQL Server Schema

The SQL Server provider creates these tables:

```sql
-- Events table
CREATE TABLE [EventSourcing].[Events] (
    [SequenceNumber] BIGINT IDENTITY(1,1) PRIMARY KEY,
    [StreamId] NVARCHAR(256) NOT NULL,
    [Version] INT NOT NULL,
    [EventType] NVARCHAR(512) NOT NULL,
    [Data] NVARCHAR(MAX) NOT NULL,
    [Metadata] NVARCHAR(MAX) NULL,
    [Timestamp] DATETIMEOFFSET NOT NULL,
    CONSTRAINT [UQ_Events_StreamVersion] UNIQUE ([StreamId], [Version])
);

-- Snapshots table
CREATE TABLE [EventSourcing].[Snapshots] (
    [StreamId] NVARCHAR(256) PRIMARY KEY,
    [Version] INT NOT NULL,
    [Data] NVARCHAR(MAX) NOT NULL,
    [Timestamp] DATETIMEOFFSET NOT NULL
);
```

### Migrations

## Configuration Options

| Option | Default | Description |
|--------|---------|-------------|
| `ConnectionString` | `null` | SQL Server connection string (required unless using factory) |
| `EventStoreSchema` | `dbo` | Database schema for the events table |
| `EventStoreTable` | `Events` | Name of events table |
| `SnapshotStoreSchema` | `dbo` | Database schema for the snapshots table |
| `SnapshotStoreTable` | `Snapshots` | Name of snapshots table |
| `OutboxSchema` | `dbo` | Database schema for the outbox table |
| `OutboxTable` | `EventSourcedOutbox` | Name of outbox table |
| `RegisterHealthChecks` | `true` | Whether to register health checks |

## Multiple Event Stores

For multi-tenant or sharded scenarios:

```csharp
// Register named event stores using keyed services
services.AddKeyedSingleton<IEventStore>("tenant-a",
    (sp, _) => new SqlServerEventStore(
        tenantAConnection,
        sp.GetRequiredService<ILogger<SqlServerEventStore>>()));

services.AddKeyedSingleton<IEventStore>("tenant-b",
    (sp, _) => new SqlServerEventStore(
        tenantBConnection,
        sp.GetRequiredService<ILogger<SqlServerEventStore>>()));

// Resolve by tenant
var eventStore = services.GetRequiredKeyedService<IEventStore>(tenantId);
```

## Observability

Enable OpenTelemetry tracing:

```csharp
services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddEventSourcingInstrumentation();
    });
```

This adds spans for:
- `EventStore.Append`
- `EventStore.Load`
- `Snapshot.Save`
- `Snapshot.Load`
- `Repository.Save`
- `Repository.Load`

## Best Practices

| Practice | Reason |
|----------|--------|
| Use strongly-typed IDs | Type safety, prevents mixing aggregate types |
| Configure snapshots | Prevents unbounded event replay |
| Enable migrations | Automatic schema updates |
| Add health checks | Monitor event store availability |
| Use connection pooling | Performance in high-throughput scenarios |

## Troubleshooting

### "Stream not found"

The aggregate doesn't exist. This is normal for new aggregates — create via factory method.

### "Concurrency conflict"

Another process modified the aggregate. Reload and retry:

```csharp
try
{
    await repository.SaveAsync(aggregate, ct);
}
catch (ConcurrencyException)
{
    // Reload and retry
    var fresh = await repository.GetByIdAsync(aggregate.Id, ct);
    // Re-apply changes
    await repository.SaveAsync(fresh, ct);
}
```

### Slow event replay

Configure snapshots to limit replay:

```csharp
es.UseIntervalSnapshots(100);  // Snapshot every 100 events
```

## See Also

- [Event Store](../event-sourcing/event-store.md) — Core event store concepts and API reference
- [Event Sourcing Overview](../event-sourcing/index.md) — Introduction to event sourcing patterns in Excalibur
- [Snapshot Setup](../configuration/snapshot-setup.md) — Configure snapshot strategies to optimize event replay performance
- [Aggregates](../event-sourcing/aggregates.md) — Aggregate root design and event application patterns

