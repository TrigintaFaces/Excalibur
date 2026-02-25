# Excalibur.LeaderElection.Consul

HashiCorp Consul implementation of distributed leader election using sessions and KV store.

## Installation

```bash
dotnet add package Excalibur.LeaderElection.Consul
```

## Features

- Uses Consul sessions with KV store for distributed locking
- Session TTL with automatic renewal
- Lock delay for graceful failover
- ACL token support for secure environments
- Multi-datacenter support
- Health check integration
- AOT-compatible with full Native AOT support

## Usage

```csharp
// Register Consul leader election
services.AddConsulLeaderElection(options =>
{
    options.ConsulAddress = "http://localhost:8500";
    options.KeyPrefix = "myapp/leader-election";
    options.SessionTTL = TimeSpan.FromSeconds(30);
    options.LockDelay = TimeSpan.FromSeconds(15);
});

// Or for a specific resource
services.AddConsulLeaderElectionForResource("order-processor");
```

## Configuration Options

| Option | Default | Description |
|--------|---------|-------------|
| `ConsulAddress` | `http://localhost:8500` | Consul server address |
| `Datacenter` | `null` | Target datacenter |
| `Token` | `null` | ACL token for authentication |
| `KeyPrefix` | `excalibur/leader-election` | KV store key prefix |
| `SessionTTL` | 30 seconds | Session time-to-live |
| `LockDelay` | 15 seconds | Delay before lock reacquisition |
| `HealthCheckId` | `null` | Health check to bind session |

## How It Works

Consul provides distributed locking via sessions:
1. Create session with TTL and optional health check binding
2. Acquire lock via `PUT ?acquire=<session>` on KV key
3. Only one session can hold the lock at a time
4. Session invalidation releases the lock after lock delay

## Related Packages

- `Excalibur.LeaderElection` - Core abstractions and InMemory implementation

## License

This project is multi-licensed under:
- [Excalibur License 1.0](..\..\..\licenses\LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](..\..\..\licenses\LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](..\..\..\licenses\LICENSE-SSPL-1.0.txt)
- [Apache-2.0](..\..\..\licenses\LICENSE-APACHE-2.0.txt)

See [LICENSE](..\..\..\LICENSE) for details.
