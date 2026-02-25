# Excalibur.Dispatch.AuditLogging.SqlServer

SQL Server implementation of IAuditStore for the Dispatch compliance framework. Provides tamper-evident hash-chain audit logging with Dapper, retention policy enforcement, and optimized query indexes.

## Installation

```bash
dotnet add package Excalibur.Dispatch.AuditLogging.SqlServer
```

## Quick Start

```csharp
// Add Excalibur.Dispatch.AuditLogging.SqlServer to your service configuration
services.AddAuditLoggingSqlServer();
```

## Documentation

See the [main documentation](https://github.com/TrigintaFaces/Excalibur) for detailed guides and API reference.

## License

This package is part of the Excalibur framework. See [LICENSE](..\..\..\LICENSE) for license details.
