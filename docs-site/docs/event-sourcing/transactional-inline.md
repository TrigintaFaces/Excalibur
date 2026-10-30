---
sidebar_position: 5
title: TransactionalInline across SQL providers
description: Commit aggregate events and their read models together with SQL Server, PostgreSQL, SQLite, or Oracle.
---

# TransactionalInline across SQL providers

`TransactionalInline` is available starting in `10.0.0-alpha.21` for SQL Server, PostgreSQL,
SQLite, and Oracle. It commits an aggregate's events and dedicated read models in one local
database transaction. A projection failure rolls back both.

Start with the [EventStore walkthrough](walkthrough.md) for aggregate creation, event registration,
versioning, saves, reads, and upgrades. This page supplies the provider-specific registration and
schema choices for that flow.

## 1. Choose one database for the atomic unit

The event store and every transactional model must use the same database and tenant partition.
Provision new, dedicated model tables. Existing ordinary projection tables are not converted.
Ordinary `Inline()` and `Async()` retain their behavior for separate stores; sharing a database
does not change their transaction boundaries.

| Provider | Event store registration | Model registration | Model schema generator |
|---|---|---|---|
| SQL Server | `UseSqlServer(...)` | `AddSqlServerProjectionStore<T>(...)` | `SqlServerProjectionSchema` |
| PostgreSQL | `UsePostgres(...)` | `AddPostgresProjectionStore<T>(...)` | `PostgresProjectionSchema` |
| SQLite | `UseSqlite(...)` | `AddSqliteProjectionStore<T>(connectionString, table)` | `SqliteProjectionSchema` |
| Oracle | `UseOracle(...)` | `AddOracleProjectionStore<T>(connectionString, table, schemaName: schema)` | `OracleProjectionSchema` |

Each schema class exposes `CreateTransactionalTableScript`. Pass the physical table name,
including its generation suffix:

```csharp
var table = ProjectionVersionConvention.Qualify("OrderSummary", typeof(OrderSummary), '_');
var ddl = PostgresProjectionSchema.CreateTransactionalTableScript(table, "public");
// Execute through your deployment migration tooling before starting the application.
```

Use the corresponding schema class for your provider. SQL Server defaults to `dbo`, PostgreSQL
to `public`, and Oracle to `EXCALIBUR`; configure the same schema on the model store. SQLite has
no separate schema parameter. Oracle's creation script requires a new table and its DDL commits
independently, so run it outside application transactions.

## 2. Register the event and model stores

Choose one of the following pairs, then register the aggregate and projection in step 3.
Import `Microsoft.Extensions.DependencyInjection`, `Excalibur.EventSourcing.DependencyInjection`,
and the selected `Excalibur.EventSourcing.<Provider>` namespace.

```csharp
// SQL Server
services.AddSqlServerProjectionStore<OrderSummary>(o =>
{
    o.ConnectionString = connectionString;
    o.TableName = "OrderSummary";
});
// Inside AddEventSourcing:
es.UseSqlServer(sql => sql.ConnectionString(connectionString));
```

```csharp
// PostgreSQL
services.AddPostgresProjectionStore<OrderSummary>(o =>
{
    o.ConnectionString = connectionString;
    o.SchemaName = "public";
    o.TableName = "OrderSummary";
});
es.UsePostgres(pg => pg.ConnectionString(connectionString));
```

```csharp
// SQLite
services.AddSqliteProjectionStore<OrderSummary>(connectionString, "OrderSummary");
es.UseSqlite(sqlite => sqlite.ConnectionString = connectionString);
```

```csharp
// Oracle
services.AddOracleProjectionStore<OrderSummary>(connectionString, "OrderSummary", schemaName: "APP");
es.UseOracle(oracle =>
{
    oracle.ConnectionString = connectionString;
    oracle.Schema = "APP";
});
```

SQLite supports the same file or an explicitly named shared-memory database (`Mode=Memory;Cache=Shared`).
Private `:memory:` databases and URI aliases are rejected. Keep a connection open for the lifetime
of a shared-memory database. Oracle requires the same configured service and database user.
SQL Server and PostgreSQL validate the configured server and database; use matching connection
configuration rather than different aliases for one server.

The SQLite and Oracle model stores provide transactional writes and ordinary reads/query/count/delete.
They do not expose global positioned writes for Inline/Async. SQL Server and PostgreSQL retain their
existing positioned stores; use separate tables for different processing modes.

SQLite numeric query comparisons use the engine's INTEGER/REAL precision. Stored JSON can retain
more decimal digits than those comparisons distinguish. See [SQLite numeric storage and comparison rules](https://www.sqlite.org/datatype3.html).

## 3. Register a deterministic aggregate-local projection

```csharp
services.AddExcalibur(x => x.AddEventSourcing(es =>
{
    // Insert the selected Use... registration from step 2 here.
    es.AddRepository<Order>(_ => new Order());
    es.RegisterEventTypesFromAssembly(typeof(Order).Assembly);
    es.AddProjection<OrderSummary>(p => p.TransactionalInline(nameof(Order))
        .When<AmountAdded>((model, e) => model.Total += e.Amount));
}));
```

Use the registered repository for every writer that needs atomic model updates. Each save folds
the complete persisted aggregate history plus pending events into a fresh model, then writes the
models and events on the same transaction. Read through `IProjectionStore<OrderSummary>` after
the save succeeds. Direct `IEventStore.AppendAsync` bypasses this coordination.

Handlers must be synchronous and deterministic. Cross-aggregate keys, asynchronous DI handlers,
and deletion callbacks are rejected. Missing or erased history prevents the save. Work grows with
stream length; snapshots do not replace this projection replay. Transactional models do not advance
a global projection checkpoint or support session-token reads.

Do not write ordinary upserts into a transactional model table. Such a write makes the model
unplaceable, and subsequent transactional saves refuse it. Retirement is terminal within a physical
generation. Use a new table when changing modes or rebuilding a retired generation.

## 4. Handle failures and verify your configuration

A handler or model-write failure before commit leaves events and models unchanged. Reload the
aggregate before issuing another command. If commit acknowledgement is lost, reconcile the original
operation; an `Unknown` result is not permission to repeat the business command. Recognizing earlier
event IDs alone does not prove a newly configured model participated in that earlier commit.

See [save outcomes and retries](event-store.md#two-different-failures-two-different-retries) and
[transactional processing details](projections.md#transactionalinline-in-one-sql-server-database),
including outbox staging and erasure responsibilities.

The runnable `samples/02-event-sourcing/TransactionalInline` sample supports all four providers.
Set `TRANSACTIONAL_INLINE_PROVIDER` and `TRANSACTIONAL_INLINE_SQL`, then run the sample. It checks
two successful saves, immediate reads, and a rejected third save that preserves both events and model.
The shared `TransactionalProjectionConformanceTestKit` exercises provider behavior against real engines;
missing required capabilities fail the suite rather than silently skipping an arm.
