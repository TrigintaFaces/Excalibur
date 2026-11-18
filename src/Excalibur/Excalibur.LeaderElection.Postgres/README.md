# Excalibur.LeaderElection.Postgres

PostgreSQL implementation of leader election for the Excalibur framework. Uses advisory locks (`pg_try_advisory_lock`) for distributed coordination with automatic failover and session-based locking.

## Part Of

This package is included in the following metapackages:

| Metapackage | Tier | What It Adds |
|---|---|---|
| `Excalibur.Postgres` | Complete | Everything for PostgreSQL: ES + Outbox + Inbox + Saga + LE + Audit + Compliance + Data |

> **Tip:** Install `Excalibur.Postgres` for a production-ready PostgreSQL stack with a single package reference.

## Features

- **Advisory lock-based leader election** -- lightweight, session-scoped locks that auto-release on connection loss
- **Health-based leader election** -- extends standard LE with health-aware candidate tracking and voluntary step-down
- **Factory pattern** -- create multiple independent leader elections with different lock keys
- **Telemetry integration** -- OpenTelemetry metrics and traces via `TelemetryLeaderElection` decorator
- **Health checks** -- ASP.NET Core health check integration

## Quick Start

```csharp
services.AddPostgresLeaderElection(options =>
{
    options.ConnectionString = "Host=localhost;Database=myapp;";
    options.LockKey = 12345;
});
```

Or using the builder pattern:

```csharp
using Microsoft.Extensions.DependencyInjection;

public static class PostgresLeaderElectionRegistration
{
    public static IServiceCollection Configure(IServiceCollection services, string connectionString)
    {
        services.AddExcalibur(excalibur => excalibur.AddLeaderElection(le =>
            le.UsePostgres(postgres => postgres
                .ConnectionString(connectionString)
                .LockKey(12345))));
        return services;
    }
}
```

## Schema

Advisory-lock leader election needs **no table** — the lock lives in the PostgreSQL lock manager.

The health-based variant records candidate health in a table, which it creates automatically on
first use. For a deployment that provisions schema separately, or runs without table-creation
rights, the canonical DDL ships in the package as
`scripts/001_CreateLeaderElectionHealthSchema.sql`. It is derived from the statement the store
issues at runtime, so a database provisioned either way has the same shape. Defaults: schema
`public`, table `leader_election_health` (both configurable via
`PostgresHealthBasedLeaderElectionOptions`).

The script only ever creates the table if it is missing; it does not alter an existing one.


## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.


### Dedicated tenure ownership and renewal deadlines

Each tenure owns a new non-pooled, non-multiplexed PostgreSQL backend with ambient transaction enlistment disabled. Acquisition and every ownership probe use that same session; renewal never reconnects. A probe confirms the exact configured bigint advisory-lock key, its database, backend PID, exclusive mode and single-bigint namespace. Holding a different advisory lock on the same backend is insufficient.

Local leadership expires at RenewInterval + GracePeriod from the conservative start of the most recent successful ownership request. Normal requests start on a serialized request-start cadence; indeterminate failures retry from the failed request's start using RetryInterval. Response latency does not extend the proof deadline. A hung operation cannot postpone local demotion, and a late success cannot revive an expired/stopped tenure. A closed/broken backend causes fail-closed local retirement; a false exact-key probe proves that ownership is absent.

PostgreSQL session advisory locks have no server LeaseDuration TTL. Local demotion and server-side session release are separate events. Retirement joins the registered operation before unlocking the exact key and disposing that same old backend; it never closes a newer tenure's session. A stuck driver can leave retirement pending and block restart, while local leadership is already false. Fencing remains required to reject stale protected writes.

Exceptions remain ambiguous by default. To accelerate deliberate permanent verification failures, register the framework's IMessageFailureClassifier or supply PostgresLeaderElectionContext with a FailureClassifier. The service registrations, factory and health wrapper forward that context. An unrelated cancellation exception is not automatically a request to stop; only owned shutdown cancellation stops the actor. Permanent classification describes an unusable verification path, not proof that the server lock disappeared.

Stop waits for owned session work, cleanup and timer disposal. A canceled wait reports cancellation and retains unfinished ownership; restart requires successful quiescence. Consumer notifications are ordered immutable transition history delivered outside state locks. Stop does not join consumer callbacks, allowing callbacks to stop/dispose their candidate. Start inside an active callback is refused; an ordinary external restart waits prior delivery before allocating another run. Throwing subscribers cannot suppress owned cleanup or the remaining subscribers. The health wrapper memoizes its underlying disposal result, including pending and failed results.

The historical five-argument constructor remains available. The additional context overload preserves explicit failure-classification policy without introducing reflection-based activation or serialization.

### Fencing and caller transactions

The PostgreSQL fencing provider disables ambient transaction enlistment on its own connections for issuance, reads and validation. Issued fencing tokens must remain durable even when the caller's business transaction rolls back. This includes the first issuance: sequence creation is transactional, so allowing it to enlist could remove the sequence on rollback and permit a later provider to issue the same token again. The caller's transaction remains active and its business changes retain their normal rollback behavior. Fencing tokens may contain gaps; they must never be reused for the same resource.


## Health wrapper lifecycle and transaction boundaries

Construction snapshots validated health options and provider binding. Update snapshots metadata.
Health DML retains caller transaction/diagnostic context. Only automatic DDL suppresses the ambient
transaction; single-flight creation caches success after native command and connection disposal.
Stop promptly retires inner leadership, joins pending starts and mutations, and removes health last.
A fresh Update during Stop waits and then executes as nonleader intent. Get and Update remain usable
while stopped. Get does not delay Stop. Dispose seals and joins all actual SDK work and retains its
pending or faulted outcome. Caller cancellation cannot abandon owned work. Uncooperative SDK work
can delay quiescence after local leadership is already false. Final removal uses a fresh bounded
cleanup token; an outer business rollback can still roll back health DML, including removal.
Normal leadership callbacks can Stop/Dispose. Lifecycle reentry from inside its own active native
health SDK interceptor throws InvalidOperationException to prevent self-join; this guard expires
when that SDK operation exits. Resource disposal does not join consumer notification tails.

The native self-join guard is specific to the target wrapper and scans active scope ancestors.
An independent wrapper may be stopped from another wrapper's SDK callback. Cyclic dependencies
across independent asynchronous callback contexts remain ordinary application deadlocks; this
mechanism does not implement a global wait graph. Finished captured execution contexts cannot
register new permanently active native scopes.

## Fencing authority consistency

Fencing observations reject recovery/read-only sessions and distinguish insufficient sequence USAGE/SELECT privileges from an absent/unadvanced mark. Observers need not have mint/CREATE/UPDATE privileges. All contenders must select the same database/schema counter authority. Preserve the sequence history; restart/restore durability is UNVERIFIED here.

Validation I/O/consistency faults and cancellation propagate without invoking downstream middleware. Discard a pinned token on leadership loss; in-flight writes still need atomic resource-side fencing. These bounds are not real-provider Native or full-release certification.
