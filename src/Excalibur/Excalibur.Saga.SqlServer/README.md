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
services.AddExcalibur(x => x.AddSagas(saga =>
{
    saga.UseSqlServer(sql =>
    {
        sql.ConnectionString = connectionString;
    });
}));

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
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.

## Database schema

**This package does not create its tables.** The DDL ships inside the `.nupkg` under `scripts/`, and
you apply it — so a schema change to your database is always something you did deliberately, never
something a version upgrade did to you while you were not looking.

| Script |
|---|
| `scripts/01-SagaSchema.sql` |
| `scripts/02-SagaCorrelationIndex.sql` |
| `scripts/02-SagaMonitoringSchema.sql` |
| `scripts/SagaTimeouts.sql` |

Apply them in filename order before starting the application, and again — for any new ones — before
starting a new version after an upgrade.

The `scripts/` folder lives inside the package, not in your build output. To read it:

```bash
# the package folder, then the scripts inside it
dotnet nuget locals global-packages --list
ls ~/.nuget/packages/excalibur.saga.sqlserver/<version>/scripts/
```

**The folder is authoritative.** If any list of scripts — including this one — disagrees with what is
actually in the package you installed, the package is right.
