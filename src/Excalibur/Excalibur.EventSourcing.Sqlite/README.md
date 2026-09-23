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

The script only ever creates missing tables; it does not alter an existing one — against a database
created by an earlier version it runs clean and changes nothing, and the first append then fails with
`no such column: TenantId`. There are two upgrade paths, and which one applies is a deployment question:

- **The application runs this package with table-creation rights.** It reconciles both tables at
  startup — rebuilding onto the tenant-scoped shape and stamping carried-over rows as untenanted.
  Nothing is required of you.
- **Schema is owned elsewhere** — a migration tool, or a database provisioned and reviewed before the
  application connects. That deployment never reaches the startup reconciliation, so run
  `scripts/002_MakeEventAndSnapshotIdentityTenantScoped.sql`, which ships in this package and performs
  the same rebuild for both tables.

Both scripts sit under `scripts/` in the package; a restore puts them at
`~/.nuget/packages/excalibur.eventsourcing.sqlite/<version>/scripts/`.

Run `002` with the application stopped, against a backup, and **with a runner that stops on the first
error**. The script is one transaction whose guards roll it back, but the `sqlite3` shell continues past
a failed statement unless you pass `-bail`:

```bash
sqlite3 -bail app.db < 002_MakeEventAndSnapshotIdentityTenantScoped.sql
```

It brings the tables to the current shape and stops there. A single-tenant host's convergence of
untenanted rows onto its own tenant identity depends on how the host is configured, which SQL cannot
see, so the store does that step at startup instead.


## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.
