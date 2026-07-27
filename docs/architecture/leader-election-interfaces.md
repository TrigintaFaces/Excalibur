# LeaderElection Interface Consistency

## Interface Hierarchy

| Interface | Package | Purpose |
|-----------|---------|---------|
| `ILeaderElection` | LeaderElection | Core abstraction: AcquireLeaseAsync, RenewLeaseAsync, ReleaseLeaseAsync |
| `ILeaderElectionFactory` | LeaderElection | Creates named instances |

## Provider Differences

| Provider | Connection | Health | Specifics |
|----------|-----------|--------|-----------|
| InMemory | N/A | Always healthy | Single-process testing |
| Redis | RedisConnection | Redis PING | Distributed lock via SET NX EX |
| SqlServer | SqlConnection factory | DB connectivity | Application lock pattern |
| Kubernetes | K8s client | Lease API | K8s Lease resources |

All providers implement `ILeaderElection` consistently. Provider-specific options control connection/lease behavior.
