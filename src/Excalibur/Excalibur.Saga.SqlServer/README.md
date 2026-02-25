# Excalibur.Saga.SqlServer

SQL Server implementation of saga state persistence for the Excalibur framework.

## Installation

```bash
dotnet add package Excalibur.Saga.SqlServer
```

## Features

- `SqlServerSagaStore` - Dapper-based saga state persistence
- MERGE-based upsert for atomic save operations
- Connection factory pattern for multi-database scenarios
- Optimistic concurrency with ROWVERSION
- AOT-compatible with full Native AOT support
- NO Entity Framework Core dependency

## Usage

```csharp
// Register SQL Server saga store
services.AddSqlServerSagaStore(connectionString);

// Or with connection factory
services.AddSqlServerSagaStore(sp =>
    () => new SqlConnection(GetConnectionString(sp)));

// Or use with IDispatchBuilder
builder.UseSqlServerSagaStore(connectionString);
```

## Configuration

```csharp
services.AddSqlServerSagaStore(connectionString, options =>
{
    options.SchemaName = "dispatch";
    options.TableName = "sagas";
});

services.AddSqlServerSagaTimeoutStore(connectionString, options =>
{
    options.SchemaName = "dbo";
    options.TableName = "SagaTimeouts";
});
```

## Database Schema

Run the SQL scripts in `/sql/` folder to create required tables (defaults):
- `dispatch.sagas` - Saga state storage with concurrency control
- `dbo.SagaTimeouts` - Saga timeouts for delayed execution

## Related Packages

- `Excalibur.Saga` - Core saga abstractions
- `Excalibur.Data.Abstractions` - Data access patterns

## License

This project is multi-licensed under:
- [Excalibur License 1.0](..\..\..\licenses\LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](..\..\..\licenses\LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](..\..\..\licenses\LICENSE-SSPL-1.0.txt)
- [Apache-2.0](..\..\..\licenses\LICENSE-APACHE-2.0.txt)

See [LICENSE](..\..\..\LICENSE) for details.
