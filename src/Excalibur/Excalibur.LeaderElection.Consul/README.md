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
- Not Native AOT compatible - the Consul client this provider builds on resolves types by runtime reflection

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
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.

## Ownership proof and local deadlines

The server session TTL is `SessionTTL` (30 seconds by default), independently of
the inherited nominal `LeaseDuration`. The local leadership budget is
`RenewInterval + GracePeriod`. Configuration requires that budget plus a one-second
margin be strictly below the requested TTL and each effective TTL returned by
Consul. A missing effective TTL fails closed. Consul's TTL is a lower bound on
invalidation, not a guaranteed expiration upper bound; health checks can invalidate
sessions earlier. Protect external writes with a fencing-aware resource.

Each tenure has a private session and nonce. Publication and renewal require both
a successful session renewal and a consistent KV read matching the captured
session, nonce and candidate. Their deadline starts before the renewal request,
not when its response arrives. Renewal and retry cadence also starts at the actual
request start. Read-only discovery never extends leadership.

While holding leadership, a separate bounded monitor checks ownership every two
seconds. Slow discovery cannot block renewal. Its responses cannot retire a newer
completed proof. During acquisition retries, discovery follows the retry cadence.
Native `SessionExpiredException` is definitive loss without an optional classifier.
Definitive ownership loss, local health ineligibility and classified permanent
failures accelerate retirement; ambiguous errors remain bounded by the independent
local deadline. Grace does not postpone discovery of definitive ownership loss.

Stop clears local authority before remote cleanup. It retains and joins actual
SDK operations, including operations that ignore cancellation, before conditionally
releasing and destroying the captured old session. Caller cancellation cancels the
wait, not cleanup ownership. Restart waits for old cleanup and notification drain.
Synchronous disposal joins the same cleanup; asynchronous disposal is preferred.
Injected Consul clients are borrowed; internally constructed clients are disposed
only after owned work and public health operations finish.

Session delete behavior, health checks, lock delay, datacenter, ACL token,
candidate health metadata and fencing tokens on leadership events remain supported.
This provider is not NativeAOT compatible because its vendor client uses reflective
serialization; these protocol changes do not change that limitation.

## Fencing authority consistency

Fencing counter reads use Consistent queries and both reads and CAS writes use the constructor-captured datacenter/token authority. An unavailable consistency quorum propagates failure; it is not UNSET or proof of staleness. Preserve the counter KV history; deletion or old snapshot restore is not detected. Restart/restore durability is UNVERIFIED here.

Validation I/O/consistency faults and cancellation propagate without invoking downstream middleware. Discard a pinned token on leadership loss; in-flight writes still need atomic resource-side fencing. These bounds are not real-provider Native or full-release certification.
