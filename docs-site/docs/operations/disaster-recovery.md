---
sidebar_position: 8
title: Disaster Recovery
description: Backup, restore, and recovery-ordering procedures for every stateful Excalibur subsystem, and what a restore does to fences, checkpoints, and deduplication windows.
---

# Disaster Recovery

[Recovery Runbooks](./recovery-runbooks.md) covers a *component* failing while the rest of the system
keeps running. This page covers the case where you have to **put state back** — a restored database, a
rebuilt region, a rolled-back cluster — and the derived positions that state carries no longer agree with
the world around them.

The framework stores nothing outside the stores you configure. Backup and restore are therefore your
provider's procedures, not ours. **What this page adds is the part your provider cannot tell you: what a
restore means to a fence, a checkpoint, a lease, or a deduplication window, and in what order to bring
things back.**

## RPO and RTO are your obligations, not the framework's

Excalibur does not set a recovery point objective and cannot meet one on your behalf. Every durable
position it keeps lives in a store you chose, under a backup policy you chose.

| You choose | The framework's behaviour depends on it |
|---|---|
| Backup frequency of the **outbox** store | Messages staged after the last backup are gone. The business transaction that staged them is gone too if they share a database — which is the point of the pattern. If they do *not* share a database, a restore can leave committed work with no message. |
| Backup frequency of the **event store** | This is your system of record. Everything else — snapshots, projections, materialized views — is derivable from it and should be rebuilt rather than restored. |
| Backup frequency of the **inbox** store | The inbox is the deduplication record. Restoring it to an earlier point re-opens the window in which an already-processed message is admitted again. |
| Retention on the **source** of change data capture | The framework detects a position it can no longer resume from, but cannot recover changes the database has already purged. |
| Whether the **leader-election** mint and the **outbox fence** live in the same database | If they do, one restore moves both consistently. If they do not, a partial restore desynchronizes them — see [Fences and high-water marks](#fences-and-high-water-marks). |

**RTO** is dominated by whichever of these is slowest for your data volume: restoring the event store,
rebuilding projections, and draining whatever backlog accumulated while you were down. Measure all three
in a drill; only the first is a database operation.

## What a restore does to derived state

A restore moves a store backwards in time. The framework's durable positions are **monotonic by design** —
they only ever advance — so moving one backwards is not neutral. Each row below states what the position
is, and what changes when it moves back.

| Position | Where it lives | Moving it backwards means |
|---|---|---|
| Outbox message rows | The outbox table in your write database | Messages already delivered become claimable again. Delivery is at-least-once, so this is permitted — but the duplicate volume is the whole window you rewound, not a handful. |
| Outbox **fence high-water** | A table separate from the messages: `[dbo].[OutboxFence]` (SQL Server), `public.outbox_fence` (PostgreSQL), `OUTBOX_FENCE` (Oracle). The table name is configurable through each store's `FenceTableName` option. | A leadership token that was superseded is accepted again. If a process from before the outage is still running and still holds that token, the split-brain window the fence exists to close is re-opened. |
| Leadership **fencing token mint** | Not in the outbox. A dedicated `SEQUENCE` named `fencing_` + the hex SHA-256 of the resource id (SQL Server, PostgreSQL); a counter key (Redis, Consul); a counter document (MongoDB); the `Lease` object's own transition counter (Kubernetes); an in-process counter (in-memory). | New tenures mint tokens *below* the outbox's high-water and every fenced write is refused. The drain stops completing work and says so — see the fenced-refusal entries in the [fault catalog](./fault-catalog.md#leader-election-and-fencing). This direction fails closed and is loud; the previous row fails open and is silent. |
| Inbox entries | The inbox table | A message whose `(MessageId, HandlerType)` mark you rewound past is admitted to its handler a second time. Handlers must be idempotent regardless; this is the scenario that actually exercises that obligation. |
| Deduplication entries held in memory | Process memory, governed by `DeduplicationOptions` (`DefaultExpiry`, `DeduplicationWindow`, `MaxMemoryEntries`) | Nothing to restore. In-memory deduplication does not survive a restart and is not a substitute for the inbox. |
| Event stream versions | The event store | This is the system of record. Rewinding it discards events; nothing downstream can recover them. |
| Snapshot rows | The snapshot store | Harmless in one direction: a snapshot older than the stream is simply a longer replay. A snapshot **newer** than the restored event stream is not harmless — it asserts a version the stream no longer has. Delete snapshots at or after the restore point. |
| Projection and materialized-view checkpoints | The projection store, and the position table beside each materialized view | A checkpoint ahead of the restored event stream skips events. A checkpoint behind it replays them. Rebuild rather than reason about it. |
| Change-data-capture positions | The CDC state store for the provider | A position the source database can no longer serve is detected, and your configured `StalePositionRecoveryStrategy` decides what happens next. See [Change data capture](#change-data-capture-positions). |

## Order of operations

Restore in dependency order. Each step's state is derived from the step above it, so restoring out of
order means redoing work.

1. **Stop every writer first.** Scale processing instances to zero, or stop the host. A running drain
   against a half-restored store produces duplicates you did not need and fence refusals you cannot
   interpret.
2. **Restore the event store** — your system of record — and nothing downstream of it yet.
3. **Restore the write database**, including the outbox and inbox tables, to the *same* point. If the
   outbox shares the database with your business data, this is one operation and the transactional
   guarantee holds across it. If it does not, reconcile: committed business state with no corresponding
   outbox row is work that will never be published.
4. **Reconcile the fence before restarting any drain.** Read the high-water value from the fence table and
   the current value of the leadership mint. If the mint is below the high-water, advance the mint past
   it — otherwise every fenced write is refused and the drain makes no progress. If the high-water is
   below tokens that live processes may still hold, confirm those processes are gone before starting new
   ones.
5. **Delete snapshots at or after the restore point.** `ISnapshotStore.DeleteSnapshotsOlderThanAsync`
   trims *old* snapshots and is the wrong direction here; use `DeleteSnapshotsAsync` for the affected
   aggregates. A snapshot claiming a version the stream does not have will be loaded and trusted.
6. **Rebuild projections and materialized views** rather than restoring them. See
   [Projection rebuild](#projection-rebuild).
7. **Reset change-data-capture positions deliberately**, before the processors start. See
   [Change data capture](#change-data-capture-positions).
8. **Start one instance.** Confirm it acquires leadership, that fenced writes are accepted, and that the
   outbox drains, before scaling out.
9. **Drain the dead-letter queue last**, once the rest of the system is known good. See
   [Dead-letter replay](#dead-letter-replay).

## Per-subsystem procedures

### Outbox

**What to back up.** The outbox table, the dead-letter table, and the fence table, as one consistent
snapshot with your business data. The three are related: the fence governs which tenure may complete a
message, and the dead-letter table holds the messages that will not be retried.

**What a restore does.** Messages return to whatever state they were in at the restore point. A message
that was claimed at that moment returns claimed; it becomes claimable again once its reservation lapses.
A message that had been delivered but not yet marked sent is delivered again. This is inside the
at-least-once guarantee, so handlers absorb it — but size the reservation timeout knowing that the entire
rewound window replays at once.

**Cleanup never lowers the fence.** The fence table is deliberately separate from the message table so
that routine purging of sent messages cannot lower the recorded high-water. A restore of the *messages*
alone therefore leaves the fence intact, which is the safe half. A restore of the whole database moves
both.

**Running without a fence.** If no leader election is registered the drain runs unfenced and logs that it
is doing so. That is correct for exactly one draining process and unsafe for more than one. After a
recovery in which instance counts changed, re-check this.

### Inbox

**What to back up.** The inbox table, with the same frequency as the work its handlers commit. On the
transactional path the handler's writes and the processed mark commit together, so a backup that captures
one captures the other.

**What a restore does.** The inbox is the record of what has already been processed. Rewinding it converts
effectively-once processing back into at-least-once for every message in the rewound window. Handlers are
already obliged to be idempotent; this is the event that tests whether they are.

**Do not truncate the inbox to "clean up" after a restore.** An empty inbox is not a clean one — it is one
that has forgotten every message it has seen, and the redelivery that follows will re-invoke handlers for
all of them.

**Failed entries are estate-wide.** The retry drain reads failed entries across all tenants and
re-establishes each entry's own tenant before acting on it. A per-tenant recovery that restores one
tenant's rows in isolation leaves the drain reading a mixed population; prefer restoring the whole table.

### Event store and snapshots

**The event store is the only thing on this page that must be restored rather than rebuilt.** Everything
else derives from it.

**Snapshots are a cache, and the restore order matters.** Load a snapshot newer than the stream and the
aggregate is reconstructed at a version the events cannot support. After restoring the event store to a
point in the past, delete snapshots for the affected aggregates and let them be recreated. Reconstruction
from events is slower, not wrong.

**Archived (tiered) events.** If cold storage is in use, events deleted from hot storage after being
copied to cold are only reachable through the tiered read path. Restoring hot storage alone does not
restore them; restoring hot storage to a point *before* the archive ran can leave the same events present
in both tiers. Restore both tiers to one point.

### Sagas

**What to back up.** The saga state table and, where the provider ships one, the saga timeout table.

**What a restore does.** A saga is a state machine with committed side effects. Rewinding its state does
not rewind the steps it already executed, so a restored saga will re-run steps whose compensations were
never triggered. Two consequences:

- Saga steps and their compensations must be idempotent. This is the same obligation as for handlers, and
  for the same reason.
- A scheduled timeout restored to an earlier state fires again. The timeout delivery path records
  superseded retirements, so a timeout that was already retired is not re-delivered by that path — but a
  timeout restored to a pre-scheduled state is a new schedule.

**Correlation.** After a restore, a saga instance and the messages correlated to it can disagree about
which step is current. Prefer completing in-flight sagas before a planned cutover to restoring them
afterwards.

### Projections and materialized views

**Do not back these up. Rebuild them.** A projection is a fold over the event store; restoring one couples
its checkpoint to a backup schedule it has no reason to share with the stream it reads.

The exceptions are projections whose rebuild cost is genuinely prohibitive — very large views over very
long streams. For those, back up the view **and its position row together**; they are meaningless apart.
The SQL Server materialized-view store keeps them in two tables and provisions both together, so back up
both.

#### Projection rebuild

```csharp
public sealed class RebuildEndpoint(IProjectionRebuildService rebuilds)
{
    public async Task RebuildAsync(CancellationToken cancellationToken)
    {
        await rebuilds.RebuildAsync<OrderSummaryProjection>(cancellationToken);

        var status = await rebuilds.GetStatusAsync<OrderSummaryProjection>(cancellationToken);
        // status.State is Idle, Rebuilding or Completed; status.Progress is 0-100.
        // Idle means no rebuild has been initiated for this projection type.
    }
}
```

`GetAllStatusesAsync` reports every projection that has been rebuilt or is rebuilding, which is what you
want on a recovery dashboard. A second rebuild request for a projection already rebuilding is rejected
rather than queued, and says so in the log.

Materialized views expose `RebuildAsync` on `IMaterializedViewProcessor` and track staleness through the
`materialized_view.staleness` gauge on the `Excalibur.EventSourcing.MaterializedViews` meter — watch that
gauge fall rather than guessing when the rebuild has caught up.

**Rebuild before you resume traffic, or accept that reads are stale while it runs.** The framework does
not hold reads back during a rebuild.

### Change data capture positions

A CDC position is a pointer into the *source database's* change log, not into anything the framework
owns. Two failure modes follow from that:

- **The position is behind what the source still has.** The processor resumes and replays. Delivery is
  at-least-once per tracked table, and on a failure the duplicate window is every change that table saw
  since its last durable checkpoint — not only the change that failed.
- **The position is behind what the source has *purged*.** The change data is gone. The framework detects
  the stale position and applies your `StalePositionRecoveryStrategy`.

| `StalePositionRecoveryStrategy` | On a stale position |
|---|---|
| `Throw` | The processor stops and propagates. You decide. |
| `FallbackToEarliest` (default) | Resume from the earliest available position and replay. Duplicates, no loss. |
| `FallbackToLatest` | Jump to the newest available position. **Every change in between is never delivered.** |

```csharp
services.AddCdcProcessor(cdc => cdc
    .UseSqlServer(sql => sql.ConnectionString(connectionString))
    .WithRecovery(recovery => recovery.Strategy(StalePositionRecoveryStrategy.FallbackToEarliest)));
```

**Set the retention on the source longer than your worst-case recovery time.** If the database's own CDC
cleanup is shorter than how long you can be down, changes are purged before the processor reaches them,
and no strategy recovers them.

After restoring a database that carries CDC positions, decide the position *before* starting the
processors: a restored position pointing into a log the restored database no longer has will be detected
as stale and resolved by whichever strategy is configured, which may not be the one you want for this
particular recovery.

### Leader election and fences

#### Fences and high-water marks

A fenced write carries a leadership token. The store records the highest token it has ever accepted and
refuses any write presenting a lower one. Two values therefore have to stay in step:

- the **mint** — the monotonic source that issues tokens, owned by the leader-election provider;
- the **high-water** — the highest token the outbox has accepted, owned by the outbox store.

When both live in the same database, one restore moves both and they stay consistent. When they do not —
a Redis or Kubernetes election in front of a SQL Server or PostgreSQL outbox, for example — a partial
restore breaks the relationship in one of two directions:

| After the restore | Symptom | What to do |
|---|---|---|
| **Mint below high-water** | Every fenced write is refused. The drain stops completing messages and logs the refusal. Fails closed. | Advance the mint past the recorded high-water before restarting drains. |
| **High-water below issued tokens** | Nothing refuses. A superseded tenure's token is accepted again. Fails open, silently. | Confirm no process from before the outage is still running, then start exactly one instance and let it advance the high-water. |

The second is the dangerous one precisely because it produces no signal. Treat "no fence refusals after a
restore" as the absence of evidence, not evidence of absence.

#### Which stores can be fenced

Fenced outbox drains require the store to implement the fenced claim, the fenced completion, and the
fenced dead-letter capability. Oracle, PostgreSQL and SQL Server do. MongoDB and Redis do not, and the
host refuses to start when a leader election is registered alongside one of them, unless you opt out with
`AsSingleWriter()` and take responsibility for there being exactly one active writer. **A recovery that
changes which store backs the outbox changes this answer.**

### Key management and crypto-shredding

Encryption keys are the one category of state where a restore can *undo* a compliance action.

**Crypto-shredding is irreversible by construction.** Erasure deletes the key that decrypts a subject's
data; the ciphertext remains and is no longer readable. Restoring a key store to a point before an
erasure restores the key and makes that data readable again — which is the outcome the erasure existed to
prevent.

So:

- **Key material and the data it protects have different restore rules.** Restore the data freely.
  Restoring the key store is a decision with a compliance consequence, not an operational detail.
- **Record erasures outside the key store.** If your recovery plan can restore the key store, you need an
  independent record of which keys were deliberately destroyed so they can be destroyed again.
- **A key store restored to an older point can hold a superseded key version.** Data written under a newer
  key version will not decrypt. Field decryption failures after a restore are reported rather than
  silently skipped — see the key-management entries in the [fault catalog](./fault-catalog.md#credentials-and-key-access).
- **Escrow is the recovery mechanism, not a backup.** Where key escrow is configured, recovery runs
  through the escrow path, which fails closed below its configured custodian threshold. Note what that
  threshold is and is not: it is an **authorization** quorum, not the confidentiality boundary. The
  escrowed key is encrypted with the escrow master key, so a holder of that master key can recover
  without assembling a quorum — and so can anyone who restores the escrow store and the master key
  together. Protect the master key at least as well as the escrow store.

### Dead-letter replay

Replay is the last step of a recovery, not part of it. Replaying while the system is still unstable
produces a second dead-letter population from the first.

```csharp
public sealed class Redrive(IDeadLetterQueue deadLetters)
{
    public async Task ReplayOneAsync(Guid entryId, CancellationToken cancellationToken)
    {
        var entry = await deadLetters.GetEntryAsync(entryId, cancellationToken);
        if (entry is null)
        {
            return;
        }

        _ = await deadLetters.ReplayAsync(entryId, cancellationToken);
    }

    public async Task<ReplayBatchResult> ReplayManyAsync(
        DeadLetterQueryFilter filter,
        int limit,
        CancellationToken cancellationToken)
        => await deadLetters.ReplayBatchAsync(filter, limit, cancellationToken);
}
```

Replay in bounded batches and check the result before the next one. `GetCountAsync` takes the same filter
and tells you how much is left.

**One entry class needs a human before it is replayed.** When a fenced dead-letter transition is refused —
because a newer leadership tenure owns the message — the framework withdraws the entry it had written,
precisely so that nobody redrives a message that was in fact delivered. If that withdrawal itself fails it
is logged as an error naming the entry, and the entry stays. Replaying it would duplicate a successful
delivery. Confirm the message's fate before acting on any entry named in such a log line.

## Recovery drill checklist

Run these against a restored copy, not against production, and time each one — the timings are your real
RTO.

| Exercise | Pass condition |
|---|---|
| **Restore and reconcile** | The write database restores, the fence and mint are reconciled, and one instance starts and drains without fence refusals. |
| **Broker reconciliation** | With the broker restored to an earlier point, the outbox redelivers and consumers deduplicate. Duplicate count is bounded by the rewound window, and no message is lost. |
| **Store reconciliation** | Committed business state with no outbox row, and outbox rows with no business state, are both enumerated and dispositioned. Zero of either is the goal; knowing the number is the requirement. |
| **Schema migration** | The [upgrade runbook](../migration/version-upgrades.md#upgrading-across-pre-releases) runs end to end on the restored copy, including the checksum check on already-applied scripts. |
| **Rolling upgrade and rollback** | One instance upgrades and rejoins while the others serve. Then it rolls back. Both directions drain without fence refusals. |
| **Projection rebuild** | Every projection and materialized view rebuilds from the event store alone, and the staleness gauge returns to zero. Record the wall-clock time. |
| **Dead-letter replay** | A batch replays, the count falls by exactly the number replayed, and no entry whose withdrawal failed is replayed unreviewed. |
| **Leadership loss** | Killing the leader transfers leadership within the configured grace period, and the new leader's writes are accepted while the old leader's are refused. |

## See Also

- [Fault and Degraded-Mode Catalog](./fault-catalog.md) — what each fault looks like in logs and metrics while it is happening
- [Recovery Runbooks](./recovery-runbooks.md) — component-level recovery for a system that is still running
- [Incident Runbooks](./incident-runbooks.md) — severity model and response flow
- [Reliability Guarantees](./reliability-guarantees.md) — the delivery and deduplication guarantees this page assumes
- [Versioning Strategy](../migration/version-upgrades.md) — the upgrade runbook and the packaged schema scripts
