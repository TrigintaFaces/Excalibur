# Two processes, one database, two shapes of read model

A web API and a background jobs service deployed side by side against one SQL Server — the shape of
most on-premises deployments, and the shape that makes projection composition a real decision rather
than a default.

It is **one project with two roles** so that the two processes share a domain without a third project:

```bash
dotnet run -- jobs     # the jobs service: runs the projection host
dotnet run -- api      # the web API: drives projections itself, no background poller
```

They differ in exactly one registration. Everything else is shared, because what is being
demonstrated is the topology, not the domain.

## Setup

1. Apply the provider's shipped migrations, `src/Excalibur/Excalibur.EventSourcing.SqlServer/Scripts/001`
   through `004` (event store, snapshots, cursor map, subscription checkpoints). **Do not hand-write
   these** — a local copy drifts from the shipped schema and the sample stops proving anything about
   the real provider.
2. Run `scripts/schema.sql`, which adds the one table the sample itself owns (the effect log).
   **The projection table is not in there**: its columns are dictated by the store, not by the
   projection type, so the sample creates it at startup from the shipped script
   (`SqlServerProjectionSchema.CreateTableScript`). Hand-writing it is how this sample first failed —
   it declared columns named after `OrderSummary`'s properties, deployed cleanly, and then threw
   `Invalid column name 'Data'` at the first apply.
3. Set `ConnectionStrings:EventStore`.

### Required startup configuration

Each of these is a startup refusal naming its own remedy, not a runtime surprise — which is the
behaviour to expect and the reason the sample is worth reading:

| missing | what the host says |
|---|---|
| `SubscriptionScope` or a matching subscription definition | configure the same source/destination scope, revision and projection set in both roles |
| `RegisterEventTypesFromAssembly(...)` | the default serializer rejects event types it was not told about, so every load would fail |
| `[MessageName("...")]` on each event | a stored identity derived from the type breaks when the namespace or assembly changes, orphaning history |
| `AddSqlServerSubscriptionCheckpointStore(...)` | without it the checkpoint resets on every restart and the whole stream re-applies — not idempotent for an accumulating handler |
| `AddRepository<Order>(...)` | the aggregate's repository is registered per type, so an unregistered one is a DI failure at the call site |

`UseSqlServer(...)` does **not** register checkpoint durability. It is a separate call, deliberately,
so a host has to state whether its checkpoints survive a restart.

## The decision this sample exists to make concrete

| read model | shape | who applies it | can it be stale? |
|---|---|---|---|
| `OrderDetail` — one order, by id | `Ephemeral` | nobody; it is computed on read | **no** |
| `OrderSummary` — the list screen | `Async`, stored | the host, or a manual drive | yes, until applied |

**A read-by-id model does not need any of this.** `GET /orders/{id}` folds that order's own events
when you ask for it: no store, no checkpoint, no host, no session token, and no staleness to reason
about. That is the right shape for a detail screen and it is unavailable to a list screen, which is
why the sample has both.

**A queryable model has to be stored, so something must write it**, and that is where the topology
starts to matter.

## Why the API drives projections instead of hosting them

`POST /orders` saves and then calls `IProjectionProcessor.ProcessPendingAsync` before returning, so
the list screen reflects the write by the time the caller's next request arrives — with no session
token on the read.

It registers `AddProjectionProcessor()`, which supplies the processor and **no** background worker.
That matters on IIS: an application pool idles out and recycles, and a `BackgroundService` dies with
it — silently, while the site keeps answering requests. Microsoft's guidance for a background-only
workload is a Windows Service or a worker, which is what the `jobs` role is.

The drive is **not** atomic with the append and does not need to be. The checkpoint is durable, so a
crash between the two leaves the read model *behind*, never missing a fold, and whichever process
next drives the processor catches it up.

## What to run, and what each run shows

`GET /effects` is the instrument. The handler on `OrderSummary` records one row per **delivery** —
before it decides whether to perform its effect — with both the classification it arrived with and
whether the effect was skipped. So an effect skipped when it should have fired, or fired twice when it
should have fired once, is a number rather than something nobody can see.

**The ordering is the whole instrument.** An earlier version returned early on `IsReplay` and recorded
nothing in that case, so the log could only ever contain deliveries where `IsReplay` was false: the
reported replay count was necessarily zero whatever the framework did, and a wrongly suppressed
delivery vanished from the record instead of appearing as the suppression it was. A number that cannot
be anything but zero is not evidence.

A real handler would also put a unique constraint on that table and be idempotent; this one records
duplicates deliberately so they can be counted.

### 1. Read-your-writes without a session token

Run `api` alone. `POST /orders`, then `GET /orders`. The order is present, because the write drove
the projection before returning. `projectionsApplied` in the response says how many events that
drive applied.

### 2. A first delivery is not a replay

Still `api` alone, with a stream that already has history: `POST /orders` a few times, stop the
process, start it again, and `POST /projections/drive`.

`GET /effects` must show, for every event, `deliveries >= 1` with `reportedAsReplay: 0` and
`performed` equal to `deliveries`. Each one is a **first** delivery — no effect has been performed for
it — so reporting a replay would tell a handler to skip an effect that never happened, and `suppressed`
would be non-zero.

The `performed` column is what makes this falsifiable: a zero `reportedAsReplay` on its own was
guaranteed by the old instrument's ordering and proved nothing. Classification is against this subscription's own checkpoint, never against the
stream head: the head says an event was committed before this run began, which says nothing about
whether this subscription ever applied it.

### 3. A crash can redeliver a page

The built-in processor checkpoints after every finite page, once every required projection fold has
settled. Kill `jobs` after an effect but before its checkpoint commits, then restart. `GET /effects`
can show repeated delivery attempts for that in-flight page. One attempt reads at most `BatchSize`
events; repeated failures can repeat effects again. `CheckpointInterval` does not control this processor.
Use an event-identifier idempotency key for real external effects.

### 4. API and Jobs cooperate on the same subscription

Both roles use `ProjectionName = "OrderSummaries"`, scope
`"two-process-orders/sql-events/sql-order-summary"`, revision `"1"`, and the same projection registrations.
The stable name is bound atomically to that definition. Starting another processor with different
projections, scope or revision under that name is refused before effects instead of silently skipping
another process's read models. To change semantics, choose a new subscription name and deliberately
rebuild or cut over its destination.

Manual calls and the background worker may also coexist in one process: a fair local gate protects one
finite page and is released before polling sleeps. Across processes, positioned writes and checkpoint
CAS reconcile contention. External effects are still at-least-once.

### 5. A stopped projection host is visible

Run `api` alone, post orders, and do **not** run `jobs`. The projection health check reports
`Degraded` naming `EnableProjectionProcessing`, rather than reporting healthy — because a host with
no projection processing at all otherwise looks identical to one that is fully caught up.

## What has actually been run

Historical checks against SQL Server 2022, before the cooperative-processing redesign (not validation of this version):

- **scenario 1** — read-your-writes with no session token. `projectionsApplied` reports the drive;
  the order is in `GET /orders` immediately.
- **scenario 2** — an event committed in an *earlier process lifetime*, and therefore below the
  current stream head, reports `reportedAsReplay: 0`. Under a head-based classification it would
  report `1` and its notification would be silently skipped. This is the one worth re-running if the
  classifier is ever touched.
- **both read models agree** — ephemeral `250 → 300` after a line is added; the stored summary shows
  `total 300, eventCount 2`.
- **durable checkpoint and placeable rows** — `SubscriptionCheckpoints` advances, and each
  `SampleOrderSummary` row carries the `LastAppliedPosition` a compare-and-set needs.
- **two concurrent readers, no duplicate effects** — 8 writes through the api while the jobs host
  polled the same subscription: 13 effects, 0 with more than one attempt.

**Withdrawn from the verified list:** the "0 ever reported as a replay" figure from that run. It was
produced by the earlier instrument, whose ordering made zero the only possible answer. The
classification claim is unmeasured until these scenarios are re-run against the corrected handler.

**Not yet run, and therefore unproven:** scenario 3's crash-induced duplicate window, scenario 4's
differing work sets, and the degraded health report in scenario 5. The health endpoint is wired at
`/health` and reports `Healthy` for both roles; the degraded and halted paths have unit coverage but
have not been exercised here.

## Limits

This revision's concurrent API/Jobs and crash scenarios must be rerun against real SQL Server before
claiming operational verification. The separate finite cooperative-processing model checks its stated
protocol and fault bounds; it does not prove the providers or this sample correct.

Projection processing does not inherit ambient leader election or the outbox lease. The sample's effect
log deliberately counts duplicate attempts; it is instrumentation, not an exactly-once delivery mechanism.
