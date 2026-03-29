# Excalibur.SqlServer

Complete SQL Server metapackage for Excalibur. One package reference, one registration call -- everything you need to run a production Excalibur application on SQL Server.

## Quick Start

```csharp
services.AddExcaliburSqlServer(sql =>
{
    sql.ConnectionString = "Server=...";
});
```

This registers: Dispatch core, SQL Server event sourcing, outbox, hosting, inbox, sagas, leader election, audit logging, compliance, and data access.

## Included Packages

| Package | Purpose |
|---|---|
| `Excalibur.Dispatch.SqlServer` | Dispatch core + event sourcing + outbox + hosting |
| `Excalibur.Inbox.SqlServer` | Idempotent message processing |
| `Excalibur.Saga.SqlServer` | Long-running process managers |
| `Excalibur.LeaderElection.SqlServer` | Multi-instance coordination |
| `Excalibur.Dispatch.AuditLogging.SqlServer` | Audit trail persistence |
| `Excalibur.Compliance.SqlServer` | GDPR/compliance features |
| `Excalibur.Data.SqlServer` | Data access layer |

## Component Toggles

All components are enabled by default. Disable optional components via the options:

```csharp
services.AddExcaliburSqlServer(sql =>
{
    sql.ConnectionString = connectionString;
    sql.UseLeaderElection = false; // skip leader election
    sql.UseCompliance = false;     // skip GDPR/compliance
});
```

## Tier Model

This is a **Complete** tier metapackage. See also:

- `Excalibur.Dispatch.SqlServer` -- **Starter** tier (Dispatch + event sourcing + outbox + hosting only)
