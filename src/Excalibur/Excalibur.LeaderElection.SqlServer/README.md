# Excalibur.LeaderElection.SqlServer

SQL Server implementation of distributed leader election using application locks.

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

## Related Packages

- `Excalibur.LeaderElection` - Core abstractions and InMemory implementation

## License

This project is multi-licensed under:
- [Excalibur License 1.0](..\..\..\licenses\LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](..\..\..\licenses\LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](..\..\..\licenses\LICENSE-SSPL-1.0.txt)
- [Apache-2.0](..\..\..\licenses\LICENSE-APACHE-2.0.txt)

See [LICENSE](..\..\..\LICENSE) for details.
