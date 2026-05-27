---
sidebar_position: 2
title: Event Store Setup
description: Configure event stores and repositories for event sourcing
---

# Event Store Setup

This guide covers configuring event stores and registering aggregate repositories.

## Before You Start

- **.NET 10.0**
- Install the required packages:
  ```bash
  dotnet add package Excalibur.EventSourcing
  dotnet add package Excalibur.EventSourcing.SqlServer  # or your provider
  ```
- Familiarity with [event sourcing concepts](../event-sourcing/index.md) and [dependency injection](../core-concepts/dependency-injection.md)

## Basic Setup

```csharp
// Configure event sourcing with provider and repositories in one builder
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(es =>
{
    es.UseSqlServer(sql => sql.ConnectionString(connectionString))
      .AddRepository<OrderAggregate, Guid>(id => new OrderAggregate())
      .UseIntervalSnapshots(100);
}));
```

## Event Store Providers

### SQL Server

```bash
dotnet add package Excalibur.EventSourcing.SqlServer
```

```csharp
// Recommended: Builder-integrated registration
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(es =>
{
    es.UseSqlServer(sql =>
    {
        sql.ConnectionString(connectionString)
           .EventStoreSchema("dbo")
           .SnapshotStoreSchema("dbo");
    })
    .AddRepository<OrderAggregate, Guid>();
}));

// This registers:
// - IEventStore + ISnapshotStore (SqlServerEventStore / SqlServerSnapshotStore)
// - Non-keyed aliases (inject IEventStore directly, no [FromKeyedServices] needed)
// - ValidateOnStart (catches missing connection at startup)
// - Prerequisite validator (fails fast if you forget to call a .UseXxx() provider)
// Outbox is registered separately via services.AddExcalibur(x => x.AddOutbox(...))
```

:::tip Connection overloads

The SQL Server builder supports 4 connection methods (last-wins if multiple are called):

```csharp
// 1. Direct connection string
sql.ConnectionString(connectionString);

// 2. Named connection string (resolved from IConfiguration)
sql.ConnectionStringName("EventStore");

// 3. Connection factory (Azure Managed Identity, Key Vault)
sql.ConnectionFactory(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var connStr = config.GetConnectionString("EventStore")!;
    return () => new SqlConnection(connStr);
});

// 4. Bind from appsettings.json section
sql.BindConfiguration("EventSourcing:SqlServer");
```
:::

### Postgres

```bash
dotnet add package Excalibur.EventSourcing.Postgres
```

```csharp
// Fluent builder registration
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(es =>
{
    es.UsePostgres(pg =>
    {
        pg.ConnectionString(connectionString)
          .EventStoreSchema("public")
          .EventStoreTable("events");
    })
    .AddRepository<OrderAggregate, Guid>();
}));
```

:::tip Postgres connection overloads

The Postgres builder supports 5 connection methods (last-wins if multiple are called):

```csharp
// 1. Direct connection string
pg.ConnectionString(connectionString);

// 2. Named connection string (resolved from IConfiguration)
pg.ConnectionStringName("EventStore");

// 3. Bind from appsettings.json section
pg.BindConfiguration("EventSourcing:Postgres");

// 4. Pre-configured NpgsqlDataSource (Azure, JSONB, custom pooling)
pg.DataSource(preBuiltDataSource);

// 5. DataSource factory (DI-aware creation)
pg.DataSourceFactory(sp => NpgsqlDataSource.Create(connStr));
```
:::
```

### In-Memory (Testing)

```bash
dotnet add package Excalibur.EventSourcing.InMemory
```

```csharp
// Builder-integrated registration
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(es =>
{
    es.UseInMemory()
      .AddRepository<OrderAggregate, Guid>();
}));
```

## Repository Registration

### Basic Registration

Register repositories for your aggregates:

```csharp
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(builder =>
{
    builder.AddRepository<OrderAggregate, Guid>();
    builder.AddRepository<CustomerAggregate, Guid>();
    builder.AddRepository<InventoryAggregate, string>();
}));
```

### Custom Factory

When your aggregate requires custom construction:

```csharp
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(builder =>
{
    builder.AddRepository<OrderAggregate, Guid>(
        key => new OrderAggregate(key, tenantId));
}));
```

### Per-Aggregate Repository Options

Configure repository behavior per aggregate type using `EventSourcedRepositoryOptions`:

```csharp
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(builder =>
{
    builder.AddRepository<OrderAggregate, Guid>(
        key => new OrderAggregate(key),
        opts =>
        {
            opts.OutboxStagingStrategy = OutboxStagingStrategy.Transactional;
            opts.EnableAutoUpcast = true;
            opts.EnableAutoSnapshotUpgrade = true;
            opts.TargetSnapshotVersion = 2;
        });
}));
```

| Option | Default | Description |
|--------|---------|-------------|
| `OutboxStagingStrategy` | `Auto` | How integration events are staged to the outbox during save (`Auto`, `Transactional`, `EventuallyConsistent`, `Deferred`) |
| `EnableAutoUpcast` | `false` | Apply upcasting pipeline during event replay |
| `EnableAutoSnapshotUpgrade` | `false` | Upgrade snapshots on load via `SnapshotVersionManager` |
| `TargetSnapshotVersion` | `1` | Target version for automatic snapshot upgrades |

### String-Keyed Aggregates

For aggregates using string identifiers:

```csharp
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(builder =>
{
    builder.AddRepository<LegacyOrderAggregate>(
        key => new LegacyOrderAggregate(key));
}));
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
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(builder =>
{
    // Register your custom IEventSerializer implementation
    builder.UseEventSerializer<MyCustomEventSerializer>();
}));
```

## Upcasting (Event Versioning)

Handle breaking changes in event schemas using message upcasters:

```csharp
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(builder =>
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
}));

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

### SqlServerEventSourcingOptions (All-in-One)

| Option | Default | Description |
|--------|---------|-------------|
| `ConnectionString` | `null` | SQL Server connection string (required unless using factory) |
| `EventStoreSchema` | `"dbo"` | Database schema for the events table |
| `EventStoreTable` | `"EventStoreEvents"` | Name of events table |
| `SnapshotStoreSchema` | `"dbo"` | Database schema for the snapshots table |
| `SnapshotStoreTable` | `"EventStoreSnapshots"` | Name of snapshots table |
| `OutboxSchema` | `"dbo"` | Database schema for the partitioned outbox table |
| `OutboxTable` | `"EventSourcedOutbox"` | Name of partitioned outbox table (unified outbox uses `services.AddExcalibur(x => x.AddOutbox(...))`) |
| `HealthChecks.RegisterHealthChecks` | `true` | Whether to register health checks |

All schema and table names are validated against SQL injection using `SqlIdentifierValidator` (alphanumeric + underscore whitelist, bracket-escaped in queries).

### Per-Store Options

When registering individual stores, use their lightweight options classes:

| Options Class | Key Property | Used By |
|---------------|-------------|---------|
| `SqlServerEventStoreOptions` | `ConnectionString` | `AddSqlServerEventStore(Action<>)` |
| `SqlServerSnapshotStoreOptions` | `ConnectionString` | `AddSqlServerSnapshotStore(Action<>)` |
| `PostgresEventSourcingOptions` | `ConnectionString` | `es.UsePostgres(pg => pg.ConnectionString(...))` |

### Custom Schema and Table Names

To use custom table names (e.g., for multi-tenant isolation or naming conventions):

```csharp
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(es =>
{
    es.UseSqlServer(opts =>
    {
        opts.ConnectionString = connectionString;
        opts.EventStoreSchema = "ordering";
        opts.EventStoreTable = "DomainEvents";
        opts.SnapshotStoreSchema = "ordering";
        opts.SnapshotStoreTable = "AggregateSnapshots";
    });
}));
```

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

