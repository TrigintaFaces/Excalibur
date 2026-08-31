# Excalibur.LeaderElection.SqlServer

SQL Server implementation of distributed leader election using application locks.

## Part Of

This package is included in the following metapackages:

| Metapackage | Tier | What It Adds |
|---|---|---|
| `Excalibur.SqlServer` | Complete | Everything for SQL Server: ES + Outbox + Inbox + Saga + LE + Audit + Compliance + Data |

> **Tip:** Install `Excalibur.SqlServer` for a production-ready SQL Server stack with a single package reference.

## Installation

```bash
dotnet add package Excalibur.LeaderElection.SqlServer
```

## Features

- Uses `sp_getapplock` and `sp_releaseapplock` for distributed locking
- Connection factory pattern for multi-database scenarios
- Automatic lock renewal and heartbeat
- Graceful leadership handoff
- AOT-compatible with full Native AOT support
- NO Entity Framework Core dependency

## Usage

```csharp
// Register SQL Server leader election
services.AddSqlServerLeaderElection(connectionString);

// Or with connection factory
services.AddSqlServerLeaderElection(sp =>
    () => new SqlConnection(GetConnectionString(sp)));
```

## How It Works

SQL Server application locks provide exclusive access to a named resource:
- Lock acquired = leadership granted
- Lock released = leadership relinquished
- Lock timeout = leadership lost (failover)

## Schema

`sp_getapplock` leader election needs **no table** — the lock lives in the SQL Server lock manager.

The health-based variant records candidate health in a table, which it creates automatically on
first use. For a deployment that provisions schema separately, or runs without table-creation
rights, the canonical DDL ships in the package as
`scripts/001_CreateLeaderElectionHealthSchema.sql`. It is derived from the statement the store
issues at runtime, so a database provisioned either way has the same shape. Defaults: schema
`dbo`, table `LeaderElectionHealth` (both configurable via
`SqlServerHealthBasedLeaderElectionOptions`).

The script is guarded and re-runnable, and only ever creates the table if it is missing; it does
not alter an existing one.

## Related Packages

- `Excalibur.LeaderElection` - Core abstractions and InMemory implementation

## License

This project is multi-licensed under:
- [Excalibur License 1.0](..\..\..\licenses\LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](..\..\..\licenses\LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](..\..\..\licenses\LICENSE-SSPL-1.0.txt)
- [Apache-2.0](..\..\..\licenses\LICENSE-APACHE-2.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.
