# Leader Election Sample

This sample uses the framework's Redis registration, acquisition fencing tokens, leadership notifications and joined shutdown. Its three work batches are simulations; they do not mutate a protected shared resource.

## Run the sample

Use the repository's .NET 10 SDK and Docker for Redis:

```bash
docker compose up -d
dotnet run
```

Run the sample in several terminals to see candidates contend for the same lock key. Each process prints its identity, acquisition/loss events, exact acquisition fence and current status. The demo stops its election after the simulated batches, then keeps the host running until Ctrl+C. Stop a leader while the demonstrations are running to observe another candidate acquire leadership.

For a single complete demonstration followed by host shutdown, run `dotnet run -- --Once true`. This mode performs the same election, monitoring, fenced batch simulations and joined shutdown without waiting for Ctrl+C.

## Registration and connection ownership

The connection string is selected once, in this order:

1. `ConnectionStrings:Redis` (`ConnectionStrings__Redis` in environment configuration).
2. `Redis:ConnectionString` (`Redis__ConnectionString`), as supplied by this sample's `appsettings.json`.
3. `localhost:6379`.

The Generic Host already loads JSON, environment and command-line providers in their standard precedence, so the sample does not append JSON again after environment overrides. See [Microsoft configuration guidance](https://learn.microsoft.com/dotnet/core/extensions/configuration).

The sample passes that same value into `UseRedis`. The framework registers and owns the multiplexer created from this connection string, so the sample does not open a second client or hand-register one. Connection strings are not logged because they can contain credentials. Applications supplying their own multiplexer should follow the provider's connection-ownership contract.

```csharp
using Microsoft.Extensions.DependencyInjection;

public static class RedisLeaderElectionRegistration
{
    public static IServiceCollection Configure(IServiceCollection services, string redisConnectionString)
    {
        services.AddExcalibur(excalibur =>
            excalibur.AddLeaderElection(le => le
                .UseRedis(redis => redis
                    .ConnectionString(redisConnectionString)
                    .LockKey("myapp:leader"))
                .WithOptions(options =>
                {
                    options.LeaseDuration = TimeSpan.FromSeconds(30);
                    options.RenewInterval = TimeSpan.FromSeconds(10);
                    options.GracePeriod = TimeSpan.FromSeconds(15);
                    options.InstanceId = $"{Environment.MachineName}-{Environment.ProcessId}";
                })));
        return services;
    }
}
```

Built-in registration enables the matching fencing provider. The sample does not opt out.

## Timing and ownership

| Option | Framework default | Sample value | Meaning for Redis |
|--------|-------------------|--------------|-------------------|
| `LeaseDuration` | 15s | 30s | TTL used by the Redis key |
| `RenewInterval` | 5s | 10s | Normal renewal request-start interval |
| `GracePeriod` | 5s | 15s | Added to renewal interval for the local proof budget |
| `InstanceId` | Unset | Machine name and process ID | Public candidate identity; private tenure ownership is separate |

The sample's local proof budget is `RenewInterval + GracePeriod`, or 25 seconds. It starts at the conservative monotonic request-start timestamp of the latest successful acquisition or ownership renewal. Response latency does not extend it. Normal renewals and ambiguous-failure retries use serialized request-start cadence. Ambiguous failures retain the current proof only until its existing deadline; they do not refresh that deadline. A definite owner mismatch or a classified permanent failure retires the local tenure earlier.

An independent timer clears local authority even while a renewal reply remains pending. Process suspension can delay timer execution, so this is not a hard real-time guarantee or proof that stale work cannot run. `GracePeriod` is not a timeout for deciding whether a remote candidate is dead.

Configuration validation requires `RenewInterval + GracePeriod + 1s < LeaseDuration`. Here, `10 + 15 + 1 = 26 < 30`. That reserves configuration headroom; it does not bound network calls, machine pauses, Redis failover or protected writes.

Redis uses a private, opaque owner value that changes on every acquisition, even when the public candidate ID remains the same. Renewal compares the complete value before extending TTL; release compares it before deletion. Old cleanup therefore cannot delete a successor merely because both tenures have the same public ID. Do not construct or overwrite that private value yourself.

## Pin the acquisition fence for work

`CurrentLeadership` is one complete snapshot containing the acquisition's fence and start time. Pin it once for a work batch:

```csharp
using Excalibur.Dispatch.LeaderElection;

public static class LeaderProtectedWork
{
    public static async Task ApplyBatchAsync(
        ILeaderElection leaderElection,
        Func<long, CancellationToken, Task> applyFencedBatch,
        CancellationToken cancellationToken)
    {
        if (leaderElection.CurrentLeadership is { } leadership &&
            leadership.FencingToken is { } token)
        {
            // Optional local cancellation check; this cannot make a remote write atomic.
            if (leaderElection.CurrentLeadership != leadership)
                return;

            // The application delegate must reject stale tokens and commit the batch
            // atomically at the protected resource, using this pinned token.
            await applyFencedBatch(token, cancellationToken);
        }
    }
}
```

The application supplies the delegate implementing the atomic protected-resource operation; this sample does not implement that resource. Never fetch a newer token halfway through old work. `IsLeader` alone, or `ValidateTokenAsync` followed by an independent write, leaves a check/write race. The resource must enforce its fencing high-water mark atomically with the write. Leadership election alone is not exactly-once execution; retries may also require a durable idempotency key.

`LeaderChanged.FencingToken` is the exact token for that acquisition. A relinquishment notification carries no acquisition token. Notifications describe ordered transition history; a delayed callback is not a current authorization to write. Read and pin `CurrentLeadership` when starting work.

## Shutdown and restart

Stop clears local authority, joins admitted Redis operations, and conditionally releases the captured tenure's owner value. It retains cleanup ownership if the caller cancels its wait. A canceled `StopAsync` wait does not mean cleanup finished; a subsequent wait or disposal must observe the retained result. A hung driver can keep cleanup pending even though local leadership is already false.

This demonstration awaits `StopAsync(CancellationToken.None)` so it does not report successful shutdown before owned cleanup finishes. Restart requires successful prior cleanup and notification drain. Subscriber exceptions cannot skip owned cleanup; an active notification callback can stop its election but cannot restart it inline.

### Permanent completion and application lifetime

`NotificationsCompleted` is one stable task from construction through permanent disposal. `StopAsync` is restartable and does not complete it. Resource disposal seals further work and joins admitted Redis operations; its task is independent of the terminal notification task, so a synchronous event callback may dispose its own election without joining itself.

The sample runs every demo inside `RunAndShutdownAsync`, then joins both resource disposal and `NotificationsCompleted` while the host, logger and instrumentation owners remain alive. It waits for both even when one fails, then captures host shutdown and final host disposal failures before rethrowing one original failure or aggregating distinct failures. Final callbacks finish before host shutdown or logger teardown. DI may dispose the already-disposed singleton during host teardown; the application owner records that outcome instead of letting a hidden `using` finally replace earlier failures. Repeated reports of the same memoized exception are retained once by reference.

Callers must not await their own `NotificationsCompleted` from an election callback: that task includes the callback currently executing. It covers synchronous delegate invocations only; `async void` continuations and tasks started by consumers require their own application-level joining. Keep borrowed logging and instrumentation owners alive until terminal delivery if their final output is needed.

## Provider differences

| Provider | Server-side ownership | Separate considerations |
|----------|-----------------------|-------------------------|
| Redis | Key TTL plus conditional owner-value scripts | Private owner value changes per acquisition |
| SQL Server | Session-owned application lock | `LeaseDuration` is not a server lock TTL; proof requires the original session's exact exclusive lock |
| PostgreSQL | Session-owned advisory lock | Exact bigint key, namespace, database and backend matter; renewal does not reconnect |
| Consul | Session TTL and KV session ownership | Uses its separate `SessionTTL`, private session/nonce proof and a bounded read-only monitor |

Failover timing depends on the backend, configured cadence, remote cleanup and scheduling. This sample does not publish fixed failover latency claims. Stale protected writes still require resource-side fencing for every provider.

## Inspect Redis

```bash
docker exec -it leader-election-redis redis-cli
```

```redis
SCAN 0 MATCH *leader*
GET myapp:leader
TTL myapp:leader
```

`GET` returns the framework's opaque ownership value, not just the public candidate ID. Use framework status and events for candidate display. Inspect the key without replacing or deleting another live candidate's ownership.

## Configuration

```json
{
  "Redis": {
    "ConnectionString": "localhost:6379"
  },
  "LeaderElection": {
    "LockKey": "myapp:leader",
    "LeaseDurationSeconds": 30,
    "RenewIntervalSeconds": 10,
    "GracePeriodSeconds": 15
  }
}
```

## Troubleshooting

- **Connection fails:** confirm Redis is healthy and the selected configuration points to it. Check both supported configuration keys in their stated precedence; avoid pasting credentials into logs.
- **Local leadership disappears while Redis still has the key:** local proof expiry and remote TTL/cleanup are different events. Inspect renewal failures and the pinned fence; do not continue writes merely because the key exists.
- **A paused candidate resumes after takeover:** abandon its old work when possible and rely on the protected resource to reject its old fence. Clock synchronization alone cannot prevent stale writes.
- **Takeover is slow:** distinguish graceful joined release from waiting for Redis expiry after a crash, then consider retry cadence and infrastructure behavior. A pending Stop wait is not successful cleanup.

The related packages are `Excalibur.Dispatch.LeaderElection.Abstractions`, `Excalibur.LeaderElection` and `Excalibur.LeaderElection.Redis`. SQL Server, PostgreSQL, Consul and Kubernetes have their own provider packages and deployment requirements.

The application lifetime owner takes responsibility immediately after building the host, including service resolution and startup logging. A known demo or cleanup failure skips the interactive shutdown wait. Cancellation retains the actual exception/token, and an empty aggregate remains an observable failure.
