# Excalibur.EventSourcing.Sqlite

Lightweight SQLite event store and snapshot store for Excalibur event sourcing.

## When to Use

- **Local development** -- no Docker or database server needed
- **Testing** -- fast, in-process event sourcing with zero infrastructure
- **Embedded scenarios** -- single-file database for desktop/CLI applications
- **Prototyping** -- quick iteration without database setup

## Usage

```csharp
services.AddExcalibur(x => x.AddEventSourcing(es =>
{
    es.UseSqlite(options =>
    {
        options.ConnectionString = "Data Source=events.db";
    });
}));
```

Tables are auto-created on first use.

## Not For Production

This package is designed for development and testing.
For production workloads, use `Excalibur.EventSourcing.SqlServer` or `Excalibur.EventSourcing.Postgres`.

## Schema

Both tables are created automatically on first use, so nothing is required to get started.

For a database you provision yourself — one built ahead of time, shipped read-only, or managed by a
migration tool — the canonical DDL ships in the package as `scripts/001_CreateEventStoreSchema.sql`.
It is derived from the same statements the store issues at runtime, so a database provisioned either
way has the same shape. Defaults: tables `Events` and `Snapshots`, both settable through the
`SqliteEventStore` and `SqliteSnapshotStore` constructors.

If you run catch-up subscriptions, `scripts/002_CreateSubscriptionCheckpointSchema.sql` creates the
`SubscriptionCheckpoints` table they record their position in.

The scripts only ever create missing tables; they do not alter an existing one. A database
provisioned by an earlier prerelease has no in-place upgrade path; re-provision it from these
scripts.

Both sit under `scripts/` in the package; a restore puts them at
`~/.nuget/packages/excalibur.eventsourcing.sqlite/<version>/scripts/`.

A single-tenant host's convergence of untenanted rows onto its own tenant identity depends on how the
host is configured, which SQL cannot see, so the store does that step at startup rather than in the
script.


## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.
