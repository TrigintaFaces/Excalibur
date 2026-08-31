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

The SQL Server and Postgres providers **never issue `CREATE TABLE` at run time**. Provision the
tables before the first append, or the first write fails — `Invalid object name 'EventStoreEvents'`
on SQL Server, `42P01: relation "events" does not exist` on Postgres.

### Where the schema lives

Each provider package ships its DDL as numbered scripts, packed into the NuGet package under
`scripts/`. Those files are the authoritative definition of the tables the provider reads and
writes — they are derived from the store's own statements, column for column, and each carries a
header explaining why every column is shaped the way it is.

| Package | Create scripts | Migrations |
|---------|----------------|------------|
| `Excalibur.EventSourcing.SqlServer` | `001_CreateEventStoreSchema.sql`, `002_CreateSnapshotSchema.sql`, `008_CreateCursorMapSchema.sql` | `003`–`007`, `009` |
| `Excalibur.EventSourcing.Postgres` | `001_CreateSnapshotSchema.sql`, `004_CreateEventStoreSchema.sql`, `008_CreateCursorMapSchema.sql` | `002`, `003`, `005`–`007` |

To read them from a restored package:

```bash
# The scripts sit alongside lib/ in the package folder your restore populated.
ls ~/.nuget/packages/excalibur.eventsourcing.sqlserver/<version>/scripts/
```

Run the create scripts once against a new database. The remaining numbered scripts are **migrations** —
apply the ones later than the version you provisioned at, in ascending order.

:::caution Provision from the scripts, not from a transcription of them
This page used to restate the `CREATE TABLE` statements inline. A reader who copied that block got a
snapshot table with a `DATETIME2` column where the store binds `DateTimeOffset`, and every snapshot read
on that database failed. The shipped scripts are the single copy kept in step with the store, and they
are the only copy with an upgrade path — provision from them rather than from any restatement, this
page's included.
:::

### Object names

The scripts target the **defaults** — on SQL Server, `EventStoreSchema` = `dbo`,
`EventStoreTable` = `EventStoreEvents`, `SnapshotStoreTable` = `EventStoreSnapshots`. If you override any
of them in `SqlServerEventSourcingOptions`, rename the corresponding object in the script to match: the
provider does not discover a differently-named table, it fails the read.

### Migrating a table you provisioned before tenant keying

The event store is a keyed multi-tenant table: every row carries a tenant term, and an unscoped host
stores the reserved `__untenanted__` sentinel rather than `NULL`. Legacy `NULL` tenants must be
backfilled to the sentinel **before** the `NOT NULL` and unique-key constraints are added — a tenant
predicate binding the sentinel does not match a `NULL` row, so those rows become unreadable otherwise.
The shipped migration scripts do this in the correct order; use them rather than hand-writing the
`ALTER`.


### Migrations

The SQL Server provider ships a migration **runner** in addition to the schema scripts above. The runner
discovers `.sql` files
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

**This runner does not create the event-store tables for you.** It applies only the migrations it finds in
the assembly you nominate, and embeds none of its own. Provision the tables from the shipped `scripts/`
described above, or embed those scripts in your migration assembly as its first migrations.

## Configuration Options

### SqlServerEventSourcingOptions (All-in-One)

| Option | Default | Description |
|--------|---------|-------------|
| `ConnectionString` | `null` | SQL Server connection string (required unless using factory) |
| `EventStoreSchema` | `"dbo"` | Database schema for the events table |
| `EventStoreTable` | `"EventStoreEvents"` | Name of events table |
| `SnapshotStoreSchema` | `"dbo"` | Database schema for the snapshots table |
| `SnapshotStoreTable` | `"EventStoreSnapshots"` | Name of snapshots table |
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
unscoped). It does **not** write a tenant predicate into every store you register — but a store outside that
set is no longer ignored either: any contract declared tenant-owned, including the audit store, the
compliance and data-inventory stores, the snapshot store and the dead-letter queue, must present a tenant
capability from its provider's registration, and one that presents none is **refused at startup by name**.
See [multi-tenancy](../multi-tenancy.md) for what is wrapped, what is only verified, and the contracts that
have no capable provider yet:

```csharp
// Ambient tenant context (resolved from the message pipeline)
services.AddTenantContext();

// One call selects the isolation strategy and wires fail-closed tenant scoping
services.AddMultiTenancy(o => o.Strategy = TenantIsolationStrategy.RowDiscriminator);
```

- **`RowDiscriminator`** — a single shared store with a `TenantId` predicate applied inside every query.
  `AddMultiTenancy` reads which contracts are tenant-owned from the contracts themselves rather than from a
  fixed list, so one added later — including one you declare — is covered when it is declared. It does
  **three different things**, and which applies decides who is responsible for the tenant predicate:

  | Contract | What `AddMultiTenancy` does |
  |---|---|
  | `IEventStore`, `IProjectionStore<T>`, `ISagaStore` | **Decorated** — wrapped in a fail-closed tenant-scoping decorator. |
  | `IInboxStore`, `IOutboxStore`, `IEventStoreErasure`, `IErasureStore`, `ILegalHoldStore` | **Gated only** — verified at registration, **not** wrapped. |
  | Any other tenant-owned contract — e.g. `IAuditStore`, `IComplianceStore`, `IDataInventoryStore`, `ISnapshotStore`, `IDeadLetterQueue` | **Refused** unless its provider attests a tenant capability. Nothing is wrapped and no predicate is added. |

  A gated contract must apply the tenant predicate **itself**. `AddMultiTenancy` requires each one to
  present a capability emitted by its provider's registration, and **refuses to start if it is absent** — so
  an `IInboxStore` or `IOutboxStore` from a provider that never routes through a tenant-aware registration
  fails at startup rather than leaking. Writing one on the assumption that the framework will scope it is the
  mistake this table exists to prevent.

  `IOutboxStore` is gated on a **different** capability from the rest. It must prove the tenant travels *on
  the row* — stamped on enqueue, returned on drain — not that it filters on the ambient tenant. Its drain is
  **deliberately estate-wide**: one pass carries every tenant's messages and scopes each individually, so a
  store filtering on the ambient tenant would find none, claim the empty set, and stall. What the gate reads in either case is a registration-time capability, not your queries: it can
  reject a provider that never claims tenant awareness, and it cannot verify that the predicate you wrote is
  correct.

  Those five are left unwrapped deliberately. The outbox drain is cross-tenant by design: one pass carries
  every tenant's messages and scopes each one individually, so a tenant-scoped decorator would find no
  ambient tenant, claim the empty set, and stall the drain. The inbox already filters on its composite
  `(TenantId, MessageId, HandlerType)` key. Erasure, erasure-request and legal-hold sweeps run from
  background services with no ambient tenant, where a decorator would either refuse every erasure or widen
  it across tenants.

  A tiered **cold** store (`IColdEventStore`) is gated separately, and combining `RowDiscriminator` with a
  cold leg that is not tenant-aware **fails fast at startup** — that combination is refused, not degraded.

  Stores outside these eight are neither decorated nor gated, and no error is raised for them.
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

