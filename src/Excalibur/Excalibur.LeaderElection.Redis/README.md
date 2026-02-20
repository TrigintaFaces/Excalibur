# Excalibur.LeaderElection.Redis

Redis implementation of distributed leader election using SET NX with Lua scripts.

## Installation

```bash
dotnet add package Excalibur.LeaderElection.Redis
```

## Features

- Uses Redis `SET NX` with TTL for distributed locking
- Lua scripts for atomic lock operations
- StackExchange.Redis integration
- Automatic lock renewal via background timer
- High-performance with minimal latency
- AOT-compatible with full Native AOT support

## Usage

```csharp
// Register Redis leader election
services.AddRedisLeaderElection(options =>
{
    options.ConnectionString = "localhost:6379";
    options.LockKey = "myapp:leader";
    options.LockTimeout = TimeSpan.FromSeconds(30);
});
```

## How It Works

Redis provides fast, distributed locking:
- `SET key value NX PX timeout` - Acquire lock atomically
- Lua script for safe lock release (only if still owner)
- Background renewal prevents premature expiry

## Related Packages

- `Excalibur.LeaderElection` - Core abstractions and InMemory implementation

## License

This project is multi-licensed under:
- [Excalibur License 1.0](..\..\..\licenses\LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](..\..\..\licenses\LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](..\..\..\licenses\LICENSE-SSPL-1.0.txt)
- [Apache-2.0](..\..\..\licenses\LICENSE-APACHE-2.0.txt)

See [LICENSE](..\..\..\LICENSE) for details.
