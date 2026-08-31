# Excalibur.AuditLogging.SqlServer

SQL Server implementation of IAuditStore for the Dispatch compliance framework. Provides tamper-evident hash-chain audit logging with Dapper, retention policy enforcement, and optimized query indexes.

## Part Of

This package is included in the following metapackages:

| Metapackage | Tier | What It Adds |
|---|---|---|
| `Excalibur.SqlServer` | Complete | Everything for SQL Server: ES + Outbox + Inbox + Saga + LE + Audit + Compliance + Data |

> **Tip:** Install `Excalibur.SqlServer` for a production-ready SQL Server stack with a single package reference.

## Installation

```bash
dotnet add package Excalibur.AuditLogging.SqlServer
```

## Quick Start

```csharp
services.AddAuditLogging();
services.AddSqlServerAuditStore(new SqlServerAuditOptions
{
    ConnectionString = connectionString,
});
```

## Documentation

See the [main documentation](https://github.com/TrigintaFaces/Excalibur) for detailed guides and API reference.

## License

This package is part of the Excalibur framework. See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for license details.
