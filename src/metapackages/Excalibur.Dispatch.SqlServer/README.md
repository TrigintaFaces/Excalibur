# Excalibur.Dispatch.SqlServer

Experience metapackage that bundles Excalibur.Dispatch with SQL Server event sourcing, outbox, and hosting for a single-package setup.

## Quick Start

```csharp
// The metapackage bundles the dependencies; registration uses each package's own entry point.
services.AddDispatch();
services.AddExcalibur(excalibur => excalibur
    .AddEventSourcing(es => es.UseSqlServer(sql => sql.ConnectionString("Server=...")))
    .AddOutbox(outbox => outbox.UseSqlServer(sql => sql.ConnectionString("Server=..."))));
```

This registers: Dispatch core, SQL Server event store, snapshot store, outbox, and web hosting.

## Included Packages

- `Excalibur.Dispatch`
- `Excalibur.EventSourcing.SqlServer`
- `Excalibur.Dispatch.Hosting.Web`
