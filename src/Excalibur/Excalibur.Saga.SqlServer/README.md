# Excalibur.Saga.SqlServer

SQL Server implementation of saga state persistence for the Excalibur framework.

## Part Of

This package is included in the following metapackages:

| Metapackage | Tier | What It Adds |
|---|---|---|
| `Excalibur.SqlServer` | Complete | Everything for SQL Server: ES + Outbox + Inbox + Saga + LE + Audit + Compliance + Data |

> **Tip:** Install `Excalibur.SqlServer` for a production-ready SQL Server stack with a single package reference.

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
// Register SQL Server saga store via ISagaBuilder
services.AddExcaliburSaga(saga =>
{
    saga.UseSqlServer(sql =>
    {
        sql.ConnectionString = connectionString;
    });
});

// Or register individually
services.AddSqlServerSagaStore(sql =>
{
    sql.ConnectionString = connectionString;
});

// Or with connection factory
services.AddSqlServerSagaStore(sp =>
    () => new SqlConnection(GetConnectionString(sp)));

// Or use with IDispatchBuilder
builder.UseSqlServerSagaStore(sql => { sql.ConnectionString = connectionString; });
```

## Configuration

```csharp
services.AddSqlServerSagaStore(sql =>
{
    sql.ConnectionString = connectionString;
    sql.SchemaName = "dispatch";
    sql.TableName = "sagas";
});

services.AddSqlServerSagaTimeoutStore(sql =>
{
    sql.ConnectionString = connectionString;
    sql.SchemaName = "dbo";
    sql.TableName = "SagaTimeouts";
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
