# Excalibur.Dispatch.SqlServer

Experience metapackage that bundles Excalibur.Dispatch with SQL Server event sourcing, outbox, and hosting for a single-package setup.

## Quick Start

```csharp
services.AddDispatchSqlServer(options =>
{
    options.ConnectionString = "Server=...";
});
```

This registers: Dispatch core, SQL Server event store, snapshot store, outbox, and web hosting.

## Included Packages

- `Excalibur.Dispatch`
- `Excalibur.EventSourcing.SqlServer`
- `Excalibur.Dispatch.Hosting.Web`
