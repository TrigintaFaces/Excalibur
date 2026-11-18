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

- `SqlServerOutboxStore` - Dapper-based outbox implementation
- `SqlServerDeadLetterQueue` - Dead letter handling for failed messages
- Batch message retrieval with ordering guarantees
- Status transitions (Pending → Processing → Published → Failed)
- Connection factory pattern for multi-database scenarios
- NativeAOT compatibility depends on configured providers, payload metadata and dependencies; validate by publishing and executing the application’s native binary.
- NO Entity Framework Core dependency

## Usage

```csharp
// Register via IOutboxBuilder (recommended)
services.AddExcalibur(x => x.AddOutbox(outbox =>
{
    outbox.UseSqlServer(sql =>
    {
        sql.ConnectionString("Server=.;Database=MyDb;Trusted_Connection=True;")
           .SchemaName("Messaging")
           .TableName("OutboxMessages");
    });
}));

// Or use with IDispatchBuilder
builder.UseSqlServerOutboxStore(options =>
{
    options.ConnectionString = "Server=.;Database=MyDb;Trusted_Connection=True;";
});
```

## Enqueue payload transformation

SQL Server exposes `IOutboxPayloadTransformStore` through the owning `IOutboxStore` capability
lookup. This lets the encryption decorator protect legacy `EnqueueAsync` without replacing SQL
Server's configured payload codec, envelope identity, routing or metadata. Ordinary enqueue keeps
its existing behavior; the transformed path prepares the envelope before invoking the callback.

The callback receives stable borrowed read-only serialized bytes, the prepared row tenant and the
caller token. Its non-null result is copied before the existing SQL staging transaction begins.
Callback failure, null output or cancellation before staging accepts no row. Once SQL persistence
starts, its existing transaction and cancellation semantics apply. This facet grants no claim,
fencing or worker-admission capability, and encryption does not expose an unmediated transform writer.

Legacy enqueue retains its reflection/dynamic-code annotations; this capability does not certify
that convenience path for NativeAOT. Use the explicit metadata/payload configuration and validate
actual native publishing and execution for the application paths you use.

## Database Schema

Run the SQL scripts in `/sql/` folder to create required tables:
- `dispatch.outbox` - Message queue storage
- `dispatch.deadletter` - Failed message storage

## Related Packages

- `Excalibur.Outbox` - Core outbox abstractions
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
| `scripts/001_CreateOutboxSchema.sql` |

Apply them in filename order before starting the application, and again — for any new ones — before
starting a new version after an upgrade.

The `scripts/` folder lives inside the package, not in your build output. To read it:

```bash
# the package folder, then the scripts inside it
dotnet nuget locals global-packages --list
ls ~/.nuget/packages/excalibur.outbox.sqlserver/<version>/scripts/
```

**The folder is authoritative.** If any list of scripts — including this one — disagrees with what is
actually in the package you installed, the package is right.

## Request parameter metadata

Framework requests bind their declared primitive input/output schema explicitly. Known parameter values
can be replaced before execution; custom Dapper metadata, templates and callbacks belong in custom
requests. Scheduled timestamps use `DbType.DateTimeOffset` for both values and database nulls. Earlier
Dapper inference used a string type for a null schedule; the explicit type now matches the SQL schema
and remains stable when scheduling is enabled. This does not change the stored timestamp or due-time
selection.
