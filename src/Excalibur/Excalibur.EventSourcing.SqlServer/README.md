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
| `scripts/003_CreateCursorMapSchema.sql` | `dbo.ProjectionCursorMaps` | Only if a projection resumes from per-stream positions |
| `scripts/004_CreateSubscriptionCheckpointSchema.sql` | `dbo.SubscriptionCheckpoints` | Only if you run catch-up subscriptions |

Every script is guarded, so re-running one against an existing database is a no-op.

`EventStoreEvents.TenantId` is created `NOT NULL`, and an untenanted event stores the reserved
`__untenanted__` sentinel rather than `NULL`, so an untenanted event is a value rather than a missing
one and the stream-identity constraint binds it like any other.

A database provisioned by an earlier prerelease has no in-place upgrade path; re-provision it from
these scripts.

## Related Packages

- `Excalibur.EventSourcing` - Core event sourcing abstractions
- `Excalibur.Data.Abstractions` - Data access patterns

## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.
