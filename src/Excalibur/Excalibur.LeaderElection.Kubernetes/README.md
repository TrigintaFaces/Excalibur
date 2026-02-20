# Excalibur.LeaderElection.Kubernetes

Kubernetes implementation of distributed leader election using the Lease API.

## Installation

```bash
dotnet add package Excalibur.LeaderElection.Kubernetes
```

## Features

- Native Kubernetes Lease API for cloud-native leader election
- Automatic in-cluster configuration detection
- Local development support via kubeconfig
- Background hosted service for automatic leadership management
- Graceful leadership transitions
- Pod identity auto-detection
- AOT-compatible with full Native AOT support

## Usage

```csharp
// Register Kubernetes leader election
services.AddExcaliburKubernetesLeaderElection(options =>
{
    options.Namespace = "my-namespace";
    options.LeaseName = "my-app-leader";
    options.LeaseDurationSeconds = 15;
    options.RenewIntervalMilliseconds = 5000;
});

// Or with hosted service for automatic management
services.AddExcaliburKubernetesLeaderElectionHostedService("order-processor");
```

## Configuration Options

| Option | Default | Description |
|--------|---------|-------------|
| `Namespace` | auto-detect | Kubernetes namespace |
| `LeaseName` | `{resource}-leader-election` | Lease resource name |
| `CandidateId` | pod name | Unique candidate identifier |
| `LeaseDurationSeconds` | 15 | Lease duration |
| `RenewIntervalMilliseconds` | 5000 | Lease renewal interval |
| `RetryIntervalMilliseconds` | 2000 | Acquisition retry interval |
| `GracePeriodSeconds` | 5 | Grace period before leader considered dead |
| `StepDownWhenUnhealthy` | true | Auto-release leadership when unhealthy |

## How It Works

Kubernetes Lease API provides native distributed locking:
1. Create or update a Lease resource in the target namespace
2. Holder identity and timestamps determine current leader
3. Lease must be renewed before duration expires
4. Failed renewal triggers leadership transition

## RBAC Requirements

Your pod's service account needs Lease permissions:

```yaml
apiVersion: rbac.authorization.k8s.io/v1
kind: Role
metadata:
  name: leader-election-role
rules:
- apiGroups: ["coordination.k8s.io"]
  resources: ["leases"]
  verbs: ["get", "watch", "list", "create", "update", "patch", "delete"]
```

## Related Packages

- `Excalibur.LeaderElection` - Core abstractions and InMemory implementation

## License

This project is multi-licensed under:
- [Excalibur License 1.0](..\..\..\licenses\LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](..\..\..\licenses\LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](..\..\..\licenses\LICENSE-SSPL-1.0.txt)
- [Apache-2.0](..\..\..\licenses\LICENSE-APACHE-2.0.txt)

See [LICENSE](..\..\..\LICENSE) for details.
