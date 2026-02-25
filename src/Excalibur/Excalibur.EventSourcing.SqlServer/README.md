# Excalibur.EventSourcing.SqlServer

SQL Server implementation of event sourcing infrastructure for the Excalibur framework.

## Installation

```bash
dotnet add package Excalibur.EventSourcing.SqlServer
```

## Features

- `SqlServerEventStore` - Dapper-based event store implementation
- `SqlServerSnapshotStore` - SQL Server snapshot persistence
- Optimized for high-throughput event streaming
- Connection factory pattern for multi-database scenarios
- AOT-compatible with full Native AOT support
- NO Entity Framework Core dependency

## Usage

```csharp
// Register SQL Server event store
services.AddSqlServerEventSourcing(connectionString);

// With configuration options
services.AddSqlServerEventSourcing(connectionString, options =>
{
    options.Schema = "events";
    options.RetryCount = 5;
});

// Or with connection factory for multi-tenant scenarios
services.AddSqlServerEventSourcing(sp =>
    () => new SqlConnection(GetTenantConnectionString(sp)));
```

## Database Schema

Run the SQL scripts in `/sql/` folder to create required tables:
- `dispatch.events` - Event stream storage
- `dispatch.snapshots` - Aggregate snapshots

## Related Packages

- `Excalibur.EventSourcing` - Core event sourcing abstractions
- `Excalibur.Data.Abstractions` - Data access patterns

## License

This project is multi-licensed under:
- [Excalibur License 1.0](..\..\..\licenses\LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](..\..\..\licenses\LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](..\..\..\licenses\LICENSE-SSPL-1.0.txt)
- [Apache-2.0](..\..\..\licenses\LICENSE-APACHE-2.0.txt)

See [LICENSE](..\..\..\LICENSE) for details.
