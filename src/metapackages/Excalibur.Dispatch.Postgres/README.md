# Excalibur.Dispatch.Postgres

Experience metapackage that bundles Excalibur.Dispatch with PostgreSQL event sourcing, outbox, and hosting for a single-package setup.

## Quick Start

```csharp
services.AddDispatchPostgres(options =>
{
    options.ConnectionString = "Host=...";
});
```

This registers: Dispatch core, PostgreSQL event store, snapshot store, outbox, and web hosting.

## Included Packages

- `Excalibur.Dispatch`
- `Excalibur.EventSourcing.Postgres`
- `Excalibur.Dispatch.Hosting.Web`
