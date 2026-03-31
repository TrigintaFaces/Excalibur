# Excalibur.EventSourcing.Sqlite

Lightweight SQLite event store and snapshot store for Excalibur event sourcing.

## When to Use

- **Local development** -- no Docker or database server needed
- **Testing** -- fast, in-process event sourcing with zero infrastructure
- **Embedded scenarios** -- single-file database for desktop/CLI applications
- **Prototyping** -- quick iteration without database setup

## Usage

```csharp
services.AddExcaliburEventSourcing(es =>
{
    es.UseSqlite(options =>
    {
        options.ConnectionString = "Data Source=events.db";
    });
});
```

Tables are auto-created on first use.

## Not For Production

This package is designed for development and testing.
For production workloads, use `Excalibur.EventSourcing.SqlServer` or `Excalibur.EventSourcing.Postgres`.
