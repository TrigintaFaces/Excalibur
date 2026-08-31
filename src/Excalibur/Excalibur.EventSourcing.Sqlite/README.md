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

The script only ever creates missing tables; it does not alter an existing one. To bring a database
created by an earlier version up to date, run the package once with table-creation rights and let
the store reconcile it.
