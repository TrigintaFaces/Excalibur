# Excalibur.LeaderElection

Distributed leader election infrastructure for the Excalibur framework.

## Installation

```bash
dotnet add package Excalibur.LeaderElection
```

## Features

- `ILeaderElection` - Leader election abstraction
- `ILeaderElectionFactory` - Factory for multi-resource leadership
- `IHealthBasedLeaderElection` - Health-aware leader election
- `InMemoryLeaderElection` - In-memory implementation for testing
- TypeForwarders for backward compatibility
- AOT-compatible with full Native AOT support

## Usage

```csharp
// Register leader election with in-memory (for testing)
services.AddInMemoryLeaderElection();

// Subscribe to leadership changes
leaderElection.LeaderChanged += (sender, args) =>
{
    if (args.IsLeader)
        Console.WriteLine("I am now the leader!");
};

// Acquire leadership
await leaderElection.AcquireLeadershipAsync(cancellationToken);
```

## Provider Packages

Choose the provider that matches your infrastructure:

| Package | Backend | Use Case |
|---------|---------|----------|
| `Excalibur.LeaderElection.SqlServer` | SQL Server | On-premises, Azure SQL |
| `Excalibur.LeaderElection.Redis` | Redis | High-performance, distributed cache |
| `Excalibur.LeaderElection.Consul` | HashiCorp Consul | Service mesh, multi-datacenter |
| `Excalibur.LeaderElection.Kubernetes` | Kubernetes Lease API | Cloud-native Kubernetes deployments |

## Related Packages

- `Excalibur.Dispatch.LeaderElection.Abstractions` - Canonical interfaces

## License

This project is multi-licensed under:
- [Excalibur License 1.0](..\..\..\licenses\LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](..\..\..\licenses\LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](..\..\..\licenses\LICENSE-SSPL-1.0.txt)
- [Apache-2.0](..\..\..\licenses\LICENSE-APACHE-2.0.txt)

See [LICENSE](..\..\..\LICENSE) for details.
