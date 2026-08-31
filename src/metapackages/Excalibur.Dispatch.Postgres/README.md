# Excalibur.Dispatch.Postgres

Experience metapackage that bundles Excalibur.Dispatch with PostgreSQL event sourcing, outbox, and hosting for a single-package setup.

## Quick Start

```csharp
// The metapackage bundles the dependencies; registration uses each package's own entry point.
services.AddDispatch();
services.AddExcalibur(excalibur => excalibur
    .AddEventSourcing(es => es.UsePostgres(pg => pg.ConnectionString("Host=...")))
    .AddOutbox(outbox => outbox.UsePostgres(pg => pg.ConnectionString("Host=..."))));
```

This registers: Dispatch core, PostgreSQL event store, snapshot store, outbox, and web hosting.

## Included Packages

- `Excalibur.Dispatch`
- `Excalibur.EventSourcing.Postgres`
- `Excalibur.Dispatch.Hosting.Web`
