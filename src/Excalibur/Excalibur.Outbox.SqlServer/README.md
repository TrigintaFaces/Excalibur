# Excalibur.Outbox.SqlServer

SQL Server implementation of the transactional outbox pattern for the Excalibur framework.

## Part Of

This package is included in the following metapackages:

| Metapackage | Tier | What It Adds |
|---|---|---|
| `Excalibur.Dispatch.SqlServer` | Starter | + Dispatch core + Outbox + Hosting |
| `Excalibur.SqlServer` | Complete | + Inbox + Saga + Leader Election + Audit + Compliance + Data |

> **Tip:** Install `Excalibur.SqlServer` for a production-ready SQL Server stack with a single package reference.

## Installation

```bash
dotnet add package Excalibur.Outbox.SqlServer
```

## Features

- `SqlServerOutboxStore` - High-performance Dapper-based outbox implementation
- `SqlServerDeadLetterQueue` - Dead letter handling for failed messages
- Batch message retrieval with ordering guarantees
- Status transitions (Pending → Processing → Published → Failed)
- Connection factory pattern for multi-database scenarios
- AOT-compatible with full Native AOT support
- NO Entity Framework Core dependency

## Usage

```csharp
// Register via IOutboxBuilder (recommended)
services.AddExcaliburOutbox(outbox =>
{
    outbox.UseSqlServer(sql =>
    {
        sql.ConnectionString("Server=.;Database=MyDb;Trusted_Connection=True;")
           .SchemaName("Messaging")
           .TableName("OutboxMessages");
    });
});

// Or use with IDispatchBuilder
builder.UseSqlServerOutboxStore(options =>
{
    options.ConnectionString = "Server=.;Database=MyDb;Trusted_Connection=True;";
});
```

## Database Schema

Run the SQL scripts in `/sql/` folder to create required tables:
- `dispatch.outbox` - Message queue storage
- `dispatch.deadletter` - Failed message storage

## Related Packages

- `Excalibur.Outbox` - Core outbox abstractions
- `Excalibur.Data.Abstractions` - Data access patterns

## License

This project is multi-licensed under:
- [Excalibur License 1.0](..\..\..\licenses\LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](..\..\..\licenses\LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](..\..\..\licenses\LICENSE-SSPL-1.0.txt)
- [Apache-2.0](..\..\..\licenses\LICENSE-APACHE-2.0.txt)

See [LICENSE](..\..\..\LICENSE) for details.
