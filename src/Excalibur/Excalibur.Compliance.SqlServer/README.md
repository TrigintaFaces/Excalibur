# Excalibur.Compliance.SqlServer

SQL Server implementation of IKeyEscrowService for the Excalibur framework. Provides key escrow storage with Shamir's Secret Sharing for split-knowledge key recovery using Dapper.

## Part Of

This package is included in the following metapackages:

| Metapackage | Tier | What It Adds |
|---|---|---|
| `Excalibur.SqlServer` | Complete | Everything for SQL Server: ES + Outbox + Inbox + Saga + LE + Audit + Compliance + Data |

> **Tip:** Install `Excalibur.SqlServer` for a production-ready SQL Server stack with a single package reference.

## Installation

```bash
dotnet add package Excalibur.Compliance.SqlServer
```

## Quick Start

```csharp
// Add Excalibur.Compliance.SqlServer to your service configuration
services.AddComplianceSqlServer();
```

## Documentation

See the [main documentation](https://github.com/TrigintaFaces/Excalibur) for detailed guides and API reference.

## License

This package is part of the Excalibur framework. See [LICENSE](..\..\..\LICENSE) for license details.
