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
| `Excalibur.AuditLogging.SqlServer` | Audit trail persistence |
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

## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.
