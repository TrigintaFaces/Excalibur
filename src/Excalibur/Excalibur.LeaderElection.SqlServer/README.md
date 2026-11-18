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
- NativeAOT compatibility depends on configured providers, payload metadata and dependencies; validate by publishing and executing the applicationâ€™s native binary.
- NO Entity Framework Core dependency

## Usage

Choose one registration method below and call it from your application's service setup.

```csharp
using Microsoft.Extensions.DependencyInjection;

public static class SqlServerLeaderElectionRegistration
{
    public static IServiceCollection WithConnectionString(IServiceCollection services, string connectionString)
    {
        services.AddExcalibur(excalibur => excalibur.AddLeaderElection(le =>
            le.UseSqlServer(sql => sql.ConnectionString(connectionString).LockResource("my-resource"))));
        return services;
    }

    public static IServiceCollection WithConnectionFactory(IServiceCollection services, string connectionString)
    {
        services.AddExcalibur(excalibur => excalibur.AddLeaderElection(le =>
            le.UseSqlServer(sql => sql
                .ConnectionFactory(_ => () => new Microsoft.Data.SqlClient.SqlConnection(connectionString))
                .LockResource("my-resource"))));
        return services;
    }
}
```

## How It Works

SQL Server application locks provide exclusive access to a named resource:
- Lock acquired = leadership granted
- Lock released = leadership relinquished
- Lock timeout = leadership lost (failover)

## Schema

`sp_getapplock` leader election needs **no table** â€” the lock lives in the SQL Server lock manager.

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
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.


## Application-lock tenure and shutdown

The application-lock election uses one dedicated physical SQL session per attempt. Pooling,
transparent connection retries and ambient transaction enlistment are disabled on that session.
Acquisition, verification and release address the same resource, `public` principal and `Session`
owner. A successful `SELECT 1` is insufficient: verification requires `APPLOCK_MODE` to report
`Exclusive`. A closed/broken session or a documented non-exclusive mode retires local leadership;
an ambiguous response does not refresh its proof. There is no reconnect or reacquire inside renewal.

Local leadership expires independently of the SQL operation at the last successful request-start
plus `RenewInterval + GracePeriod`. The SQL session lock has no lease TTL; `LeaseDuration` is a
configuration headroom input, not a server-expiry promise. Native commands have explicit finite
command timeouts. Acquisition allows the configured SQL lock wait plus one second of command
headroom; verification allows `GracePeriod` rounded up to whole SQL seconds (minimum one),
without moving the independent local deadline. Timeout/cancellation is not proof that a command has settled. Retirement
joins every registered acquire/verify operation before release and physical disposal. Disposal is
attempted after a release failure, all cleanup causes remain observable, and failed shutdown does
not license a fresh run. A hung SDK can therefore keep shutdown incomplete while local leadership
is already false. Fencing remains necessary for protected business writes under arbitrary pauses.

Healthy verification is scheduled from the prior request start by `RenewInterval`; ambiguous
replies retry from that request start by `RetryInterval`. Reply latency consumes the interval rather
than being added again. Overdue work causes one serial attempt, not catch-up bursts. The shared
monotonic delay rounds submillisecond scheduling up to one millisecond and slices long waits;
very small tenure budgets can expire before host scheduling permits a retry. Early timer callbacks
do not advance the proof or shorten a configured interval.

Local ownership notifications are ordered and delivered outside protocol/session cleanup. A
blocked subscriber parks new admission instead of retaining an unbounded chain of run objects.
Subscriber failures do not suppress the remaining subscribers. A callback may stop/dispose its
provider; callback-reentrant start is refused. Caller cancellation stops waiting without abandoning
owned cleanup. `CurrentLeadership` is one immutable fence/acquisition-time snapshot, and acquired
`LeaderChanged` notifications carry that exact fence rather than requiring a later getter read.


Owned admission, actor, SDK and cleanup workers execute without a business `Transaction.Current`.
This transaction-only isolation preserves diagnostic Activity/AsyncLocal flow. The dedicated SQL
session also explicitly disables enlistment, independently of how its caller was scheduled. Fencing
mint durability is infrastructure state, never an incidental participant in the business transaction;
the built-in SQL fencing provider must keep that autonomy for direct calls as well as actor calls.


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

The current fencing storage profile uses explicitly qualified dbo counter sequences for creation, existence, generation and reads, independent of login default schema. Existing dbo history is preserved. This is a pre-RC storage-profile break for old non-dbo counters: quiesce old leaders/protected work and do not mix versions; no automatic legacy migration or live authority retargeting is provided. Read-only authorities and indeterminate metadata visibility fail. Existing-object metadata-only observers need no mint UPDATE privilege; reporting genuine absence requires sufficient effective dbo-schema/database visibility without overriding DENY. Preserve counter history; restart/restore durability is UNVERIFIED here.

Validation I/O/consistency faults and cancellation propagate without invoking downstream middleware. Discard a pinned token on leadership loss; in-flight writes still need atomic resource-side fencing. These bounds are not real-provider Native or full-release certification.
