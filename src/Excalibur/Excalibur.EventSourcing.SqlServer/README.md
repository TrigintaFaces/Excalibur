# Excalibur.EventSourcing.SqlServer

SQL Server implementation of event sourcing infrastructure for the Excalibur framework.

## Part Of

This package is included in the following metapackages:

| Metapackage | Tier | What It Adds |
|---|---|---|
| `Excalibur.Dispatch.SqlServer` | Starter | + Dispatch core + Outbox + Hosting |
| `Excalibur.SqlServer` | Complete | + Inbox + Saga + Leader Election + Audit + Compliance + Data |

> **Tip:** Install `Excalibur.SqlServer` for a production-ready SQL Server stack with a single package reference.

## Installation

```bash
dotnet add package Excalibur.EventSourcing.SqlServer
```

## Features

- `SqlServerEventStore` - Dapper-based event store implementation
- `SqlServerSnapshotStore` - SQL Server snapshot persistence
- Optimized for high-throughput event streaming
- Connection factory pattern for multi-database scenarios
- AOT-compatible with full Native AOT support
- NO Entity Framework Core dependency

## Usage

```csharp
// Recommended: Builder-integrated registration
services.AddExcalibur(x => x.AddEventSourcing(es =>
{
    es.UseSqlServer(options =>
    {
        options.ConnectionString = connectionString;
        options.EventStoreSchema = "events";
    });
    es.AddRepository<OrderAggregate, Guid>(id => new OrderAggregate(id));
}));

// Alternative: Direct registration
services.AddSqlServerEventSourcing(options =>
{
    options.ConnectionString = connectionString;
});
```

## Database Schema

The store does **not** create its tables at runtime. Provision them before the first append, by
running the scripts shipped inside this package under `scripts/`:

| Script | Creates | Required |
|---|---|---|
| `scripts/001_CreateEventStoreSchema.sql` | `dbo.EventStoreEvents` | Yes — this is the event store itself |
| `scripts/002_CreateSnapshotSchema.sql` | `dbo.EventStoreSnapshots` | Only if you enable snapshots |
| `scripts/003_MigrateToMultiTenant.sql` | — | Upgrade only, see below |
| `scripts/004_MakeEventTenantTotal.sql` | — | Upgrade only, see below |

Both create scripts are guarded, so re-running them against an existing database is a no-op.

### Upgrading an existing database

Run these in order; each is guarded and safe to run against a database that is already converged.

`003_MigrateToMultiTenant.sql` grows a store created before tenancy existed into the current
schema. Without it, an existing deployment fails on the first append with
`Invalid column name 'TenantId'`.

`004_MakeEventTenantTotal.sql` then backfills `EventStoreEvents.TenantId` from `NULL` to the
reserved `__untenanted__` sentinel and makes the column `NOT NULL`, so an untenanted event is a
value rather than a missing one, and an upgraded database ends up in the same shape as a fresh one.
It is not needed for a fresh install — `001` already creates the column that way. Its pre-flight
step reports any stream version that already holds both a `NULL` and a literal sentinel row;
resolve those before continuing, because only you can decide which append survives.

Both upgrade scripts rebuild a unique constraint on the system of record, so run them in a
maintenance window with the store stopped, against a backup you have restored at least once.

## Related Packages

- `Excalibur.EventSourcing` - Core event sourcing abstractions
- `Excalibur.Data.Abstractions` - Data access patterns

## License

This project is multi-licensed under:
- [Excalibur License 1.0](..\..\..\licenses\LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](..\..\..\licenses\LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](..\..\..\licenses\LICENSE-SSPL-1.0.txt)
- [Apache-2.0](..\..\..\licenses\LICENSE-APACHE-2.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.
