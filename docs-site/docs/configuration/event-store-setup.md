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
        return new OrderCreated
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

The SQL Server provider does **not** create these tables. You must create them before
first use — the schema below is the one the provider reads and writes.

The names below are the **defaults** (`EventStoreSchema` = `dbo`, `EventStoreTable` = `EventStoreEvents`,
`SnapshotStoreTable` = `EventStoreSnapshots`). If you override any of them in
`SqlServerEventSourcingOptions`, rename the corresponding object here to match — the provider does not
discover a differently-named table, it fails the read.

```sql
-- Events table
CREATE TABLE [dbo].[EventStoreEvents] (
    -- Assigned by the database and read back via OUTPUT INSERTED.Position.
    -- Must be IDENTITY: the insert never supplies a value for it.
    [Position]       BIGINT IDENTITY(1,1) NOT NULL,
    [EventId]        NVARCHAR(256)  NOT NULL,
    [AggregateId]    NVARCHAR(256)  NOT NULL,
    [AggregateType]  NVARCHAR(256)  NOT NULL,
    [EventType]      NVARCHAR(512)  NOT NULL,
    -- MUST be nullable. Erasure sets EventData to NULL to tombstone an event while
    -- preserving its position in the stream; a NOT NULL column makes erasure fail.
    [EventData]      VARBINARY(MAX) NULL,
    [Metadata]       VARBINARY(MAX) NULL,
    [Version]        BIGINT         NOT NULL,
    [Timestamp]      DATETIMEOFFSET NOT NULL,
    -- The event store is a keyed multi-tenant table: every row carries a tenant term, so this column is
    -- NOT NULL and part of the tenant-discriminated unique key. A single-tenant (unscoped) host stores the
    -- reserved '__untenanted__' sentinel here — never NULL — so an un-partitioned, all-tenants read or
    -- erase is unrepresentable. The sentinel is a concrete value because a NULL discriminator cannot
    -- participate in a unique constraint on any provider.
    [TenantId]       NVARCHAR(255) COLLATE Latin1_General_BIN2  NOT NULL,
    CONSTRAINT [PK_Events_Position] PRIMARY KEY CLUSTERED ([Position]),
    CONSTRAINT [UQ_Events_AggregateVersion] UNIQUE ([AggregateId], [AggregateType], [Version], [TenantId])
);

-- Stream reads order by Version within an aggregate.
CREATE INDEX [IX_EventStoreEvents_Aggregate]
    ON [dbo].[EventStoreEvents] ([AggregateId], [AggregateType], [Version]);

-- Migrating an existing Events table to tenant keying: backfill any legacy NULL tenant to the sentinel
-- BEFORE adding the NOT NULL and unique-key constraints, or existing untenanted rows become unreadable
-- (a tenant predicate binding the sentinel will not match a NULL row):
--     UPDATE [dbo].[EventStoreEvents] SET [TenantId] = '__untenanted__' WHERE [TenantId] IS NULL;

-- Snapshots table
CREATE TABLE [dbo].[EventStoreSnapshots] (
    [SnapshotId]     NVARCHAR(256)  NULL,
    [AggregateId]    NVARCHAR(256)  NOT NULL,
    [AggregateType]  NVARCHAR(256)  NOT NULL,
    [Version]        BIGINT         NOT NULL,
    [Data]           VARBINARY(MAX) NOT NULL,
    -- DATETIME2, not DATETIMEOFFSET: the read path maps this to DateTime and re-stamps
    -- UTC kind, which a DATETIMEOFFSET column does not round-trip through.
    [CreatedAt]      DATETIME2      NOT NULL,
    [Metadata]       VARBINARY(MAX) NULL,
    -- The reserved '__untenanted__' sentinel in a single-tenant host -- never NULL and never
    -- an empty string. NOT NULL because SQL Server does not allow a nullable column in a
    -- primary key.
    --
    -- Deliberately NO DEFAULT. The store always supplies this column -- a single-tenant
    -- save writes the '__untenanted__' sentinel explicitly, not by omission. A DEFAULT here would be
    -- unreachable in normal operation and harmful in abnormal operation: it would let an
    -- INSERT that omitted the tenant succeed silently, taking the default and colliding
    -- every tenant onto one row. Without it, such a statement fails outright.
    [TenantId]       NVARCHAR(256) COLLATE Latin1_General_BIN2  NOT NULL,
    -- One row per aggregate PER TENANT. Saves MERGE on these columns, and the read path
    -- issues a single-row query with no TOP 1 -- a second row makes it throw. Without
    -- TenantId in the key, two tenants holding the same aggregate id ARE that second row.
    CONSTRAINT [PK_EventStoreSnapshots_Aggregate] PRIMARY KEY CLUSTERED ([AggregateId], [AggregateType], [TenantId])
);
```

### Migrations

The SQL Server provider ships a migration **runner**, not migration **scripts**. It discovers `.sql` files
embedded as manifest resources in an assembly *you* nominate, records what it has applied in a
`__MigrationHistory` table it creates itself, and applies anything pending at startup before the
application begins serving.

Both settings are **required** when the migrator is registered — it throws at startup if either is unset:

```csharp
options.MigrationAssembly = typeof(Program).Assembly;   // assembly holding your embedded .sql resources
options.MigrationNamespace = "MyApp.Migrations";        // resource-name prefix to scan
```

Scripts are applied in ascending order of the portion of the resource name after the namespace prefix, so
name them to sort correctly (`001_...`, `002_...`). A checksum is recorded per applied migration.

**This does not create the event-store tables for you.** Nothing in the package embeds the schema above —
provision it yourself (the DDL in this section), or supply it as your own first migration script.

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

### Multi-tenancy (recommended)

For **multi-tenant** scenarios, do not register one event store per tenant by hand. Use first-class
multi-tenancy, which scopes a **declared set of tenant-owned contracts** — the event store, projections,
sagas, the inbox, the outbox, and event-store erasure — through a single ambient tenant context, and is
**fail-closed** for those contracts (an operation with no ambient tenant throws rather than running
unscoped). It does **not** scope every store you register: anything outside that set — including the audit
store, the compliance/data-inventory stores, and the dead-letter queue — is left unscoped, with no error
raised. See [multi-tenancy](../multi-tenancy.md) for the full set and what it excludes:

```csharp
// Ambient tenant context (resolved from the message pipeline)
services.AddTenantContext();

// One call selects the isolation strategy and wires fail-closed tenant scoping
services.AddMultiTenancy(o => o.Strategy = TenantIsolationStrategy.RowDiscriminator);
```

- **`RowDiscriminator`** — a single shared store with a `TenantId` predicate applied inside every query.
  `AddMultiTenancy` wraps the registered tenant-owned contracts — `IEventStore`, `IProjectionStore<T>`,
  `ISagaStore`, `IInboxStore`, `IOutboxStore` and `IEventStoreErasure` — with its tenant-scoping decorator.
  Stores outside that set are not wrapped.
- **`Sharding`** — a dedicated store per tenant, selected per operation by tenant-aware routing. Register
  the shard map and provider store resolvers, then select `TenantIsolationStrategy.Sharding` (which wires
  the same routing as `EnableTenantSharding(...)`).

See [Multi-Tenancy](../multi-tenancy.md) for the full setup.

### Keyed stores (advanced)

If you genuinely need physically distinct stores resolved by a key **you** control (e.g. a manual shard
key that is not the tenant), register them keyed and resolve from a built `IServiceProvider` — keyed
resolution is an extension on `IServiceProvider`, not on `IServiceCollection`:

```csharp
// Register named event stores using keyed services
services.AddKeyedSingleton<IEventStore>("shard-a",
    (sp, _) => new SqlServerEventStore(
        shardAConnection,
        sp.GetRequiredService<ILogger<SqlServerEventStore>>()));

services.AddKeyedSingleton<IEventStore>("shard-b",
    (sp, _) => new SqlServerEventStore(
        shardBConnection,
        sp.GetRequiredService<ILogger<SqlServerEventStore>>()));

// Resolve from the built provider (or inject IServiceProvider / use [FromKeyedServices])
var provider = services.BuildServiceProvider();
var eventStore = provider.GetRequiredKeyedService<IEventStore>(shardKey);
```

:::warning Keyed stores are not automatically tenant-scoped
`AddMultiTenancy(RowDiscriminator)` decorates exactly **one** registered `IEventStore` descriptor. If you
register N keyed stores by hand, the other stores are **not** wrapped with tenant scoping and will read
and write across tenant boundaries. For tenant isolation use `TenantIsolationStrategy.Sharding` (or the
`RowDiscriminator` strategy over a single store), never a set of hand-keyed stores.
:::

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

