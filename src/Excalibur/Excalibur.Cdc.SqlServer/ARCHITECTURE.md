# Architecture — Excalibur.Cdc.SqlServer (SQL Server change data capture)

This document states what the SQL Server CDC processor guarantees, how it achieves it, and what it does
**not** prove. It is a contract, not a description: every claim below is meant to be falsifiable, and
where a guarantee is not enforced by a test we say so rather than assert it.

**The guarantee here is not the same as the PostgreSQL package's.** Both deliver at-least-once, but the
mechanism, the unit the duplicate window is measured in, and what happens to a *sibling* table on failure
all differ. Read this document rather than assuming the two providers behave alike.

## Am I exposed?

Five questions you can answer about your own application without reading the rest of this document.

| Ask | If yes |
|---|---|
| Do you run more than one instance against the same capture instances? | You need leader election **with fencing tokens**. Without it, nothing stops two instances processing the same tables and advancing each other's checkpoints. With it, a demoted instance's checkpoint write is rejected rather than applied. |
| Did you set `RecoveryStrategy` to `FallbackToLatest`? | **You have opted into data loss.** On a stale position the processor jumps to the newest available position and every change in between is never delivered. The default, `FallbackToEarliest`, replays instead. |
| Do you supply an `OnFatalError` callback? | A change your handler throws on is **swallowed** and not retried inside that run. Its table is then frozen for the rest of the run and redelivered on the next run — including changes after it that already succeeded. |
| Is your SQL Server Agent CDC cleanup (retention) shorter than your worst-case outage? | Changes can be purged before the processor reaches them. That is loss outside this package's control; the processor detects the resulting stale position and applies your recovery strategy. |
| Does your handler do anything besides the write itself — publish, audit, count? | That side effect can run more than once. Delivery is at-least-once and nothing in this package deduplicates unless you register an idempotency filter. |

## Guarantees

### Delivery is at-least-once per tracked table, and the duplicate window is the table's whole run

**Every change captured for a tracked table is delivered to the handler at least once. A change may be
delivered more than once, and on a failure the duplicate window is every change that table saw since its
last durable checkpoint — not only the change that failed.**

This is the load-bearing sentence for a consumer, because it decides whether their handler is correct.
The checkpoint is written per table, once, after a batch completes, and only for tables whose every change
in that run succeeded. A table that fails anywhere in a run writes no checkpoint for that run, so the next
run resumes from the same durable position and redelivers everything after it.

**Failure is contained to the table that failed.** Sibling tables in the same batch still checkpoint, so
one poisoned table does not force replay of the others.

### A checkpoint never advances past a change the handler did not complete

**A table's durable position moves only to a change the handler returned successfully for, and a change
whose handler threw freezes that table's position for the remainder of the run.**

The freeze is run-scoped, not batch-scoped, and the distinction is the whole guarantee: with a consumer
batch size of one, a batch-scoped barrier would be discarded with the failing change, and the next change
for that table — succeeding, or merely skipped as already processed — would write a checkpoint past the
change that never succeeded. That change would then never be redelivered.

A change skipped by the idempotency filter is treated as successful for checkpoint purposes, **except**
on a table already frozen in this run.

### A demoted leader cannot move a checkpoint

**When leader election supplies a fencing token, a checkpoint write from a superseded tenure is rejected
by the state store rather than applied, and the position is left unchanged — never lowered, never skipped.**

The token is read **once** at the start of a batch and pinned for the whole batch. A demotion mid-batch
therefore does not blank the token: the now-stale token loses the compare-and-set, the write reports zero
rows, and the processor stops the run with a leadership-superseded error instead of continuing under an
identity it no longer holds. With no leader election configured the write is unfenced, which is correct
only for a single instance.

### One invocation at a time, and both halves of an invocation end together

**Two overlapping calls to `ProcessBatchAsync` on the same processor cannot both run: the second waits,
and a call that finds a run already in progress fails rather than interleaving.** Each invocation gets a
fresh queue and a fresh stop flag, so a second poll of the same processor instance delivers changes rather
than finding its predecessor's terminal state.

**The producer and consumer are one invocation and settle together.** They share a linked cancellation
source, so a fault in either stops the other; neither is left running after the call returns. Without that,
a faulted consumer strands the producer forever, because a bounded channel in wait mode blocks a writer
until a reader takes an item.

## How it is achieved

- **The per-table barrier belongs to the run.** `CdcChangeApplier.cs:105` creates the failed-table set once
  per run and passes it into every batch; `:288-307` refuses to track a position for a frozen table and
  removes any position already tracked for it.
- **The checkpoint write happens after the batch, per table.** `CdcChangeApplier.cs:310-321` writes one
  position per table from the last successful change, so a table absent from that map writes nothing.
- **The handler decides reachability, not a flag.** `CdcChangeApplier.cs:226-292` sets the success marker
  only after the handler returns. With no fatal-error callback the exception propagates out of the consumer
  loop (`:276`) and the run ends; with one, the change is swallowed and its table is frozen (`:301-302`).
- **The fencing token is pinned once per batch.** `CdcProcessor.cs:282-294` reads current leadership a
  single time, stands by when it is not the leader, and pins the token for the batch;
  `CdcCheckpointManager.cs:180-199` passes it to the state store's compare-and-set and raises a
  leadership-superseded error when zero rows are written.
- **A fatal checkpoint fault stops the loop instead of retrying.** `CdcChangeApplier.cs:323-333` routes the
  decision through the shared fatal guard: retrying a stale-token write is rejected identically and would
  spin a demoted instance.
- **Single-invocation is a lock, not a convention.** `CdcProcessor.cs:260` acquires the execution lock and
  `:353` releases it; `:267-270` rejects a re-entrant run. `:316-317` allocates a fresh queue and clears the
  stop flag per invocation, and `:303-338` links the fault source and joins both halves.
- **Ordering is the log sequence number, never wall clock.** Positions are compared as LSN/sequence byte
  values; commit time is carried as data only.

## Evidence

- **Every captured change survives a faulting handler, real SQL Server** —
  `SqlServerCdcSecondPollIntegrationShould.DeliverEveryCapturedChangeEvenWhenAHandlerFaultsTheFirstPoll`
  and `.DeliverEveryCapturedChangeWhenABlockedProducerMeetsAFaultingHandler`. Both run against a real
  Agent-enabled SQL Server container and are **never skip-gated**: a skipped infrastructure arm proves
  nothing, and the defect these lock lives in the interaction between the checkpoint and the change table,
  which a mock returns whatever it was told about.
- **A second poll of the same processor instance still delivers and checkpoints** —
  `SqlServerCdcSecondPollIntegrationShould.DeliverAndCheckpointOnASecondPollOfTheSameProcessorInstance`,
  plus arms for a poll following an empty one and a poll following a cancelled one.
- **The fencing invariant, real SQL Server** — `SqlServerCdcFencingCheckpointShould`: a demoted leader's
  stale-token write leaves the checkpoint **unchanged**, the current leader's equal token advances it, and a
  new leader's higher token is admitted after fencing out the zombie. Never skip-gated.
- **The checkpoint advance is gated structurally** — `CdcCheckpointAdvanceGateShould` asserts every durable
  advance goes through the single guarded field, so a new call site that bypasses it is caught.

## Consumer obligations

1. **Handlers MUST be idempotent, and the key is `(TableName, Lsn, SeqVal)`.** At-least-once is the
   guarantee, so applying the same change twice must be safe. Registering an idempotency filter
   deduplicates on that identity before your handler runs; the filter is optional and nothing deduplicates
   without it.
2. **An idempotency filter narrows duplicates, it does not remove them.** The filter is marked *after* the
   handler returns, so a crash between the two redelivers the change. That ordering is deliberate: marking
   first would let a change be skipped that was never handled.
3. **Run leader election with fencing tokens for more than one instance.** Without a token every write is
   unfenced, and two instances will advance each other's checkpoints. The processor stands by when it is not
   the leader rather than processing.
4. **Size CDC retention against your worst-case outage.** The SQL Server Agent cleanup job purges change
   rows on its own schedule. If it purges a position the processor has not reached, those changes are gone
   before this package sees them.
5. **Choose the recovery strategy deliberately.** `FallbackToEarliest` (the default) replays from the
   earliest available position — duplicates, no loss. `FallbackToLatest` skips to the newest — loss, no
   duplicates. `Throw` stops. `InvokeCallback` reports and resumes from the earliest.
6. **Decide what `OnFatalError` means for you.** Supplying it converts a handler throw from "stop the run"
   into "swallow, freeze this table, redeliver next run". Without it, the run stops on the first unhandled
   handler exception.

## Known gaps

- **A permanently failing handler stalls its table indefinitely, and nothing steps over the change.** With
  no fatal-error callback the run stops; with one, the table is frozen each run and redelivered the next.
  Either way there is no skip-after-N and no dead-letter path, so a change your handler can never process
  blocks that table until you intervene.
- **`FallbackToLatest` is a documented, opt-in data-loss path.** It exists because some deployments prefer a
  gap to a replay. Nothing warns at startup that the option discards changes.
- **The swallow path redelivers work that already succeeded.** Because the barrier freezes the whole table
  for the run, changes after the failed one that the handler completed are delivered again on the next run.
  That is the price of never skipping the failed change, and it is why obligation 1 is not optional.
- **Per-table checkpoints give no cross-table ordering.** Tables advance independently, so a consumer that
  infers an order between two tables from delivery order is relying on something this package does not
  promise.
- **Concurrency beyond one process is enforced only by fencing.** The execution lock covers one processor
  instance in one process. Two processes without leader election are not prevented from processing the same
  capture instances; they are simply both unfenced.
- **The duplicate-window claim is proven for a handler fault and UNVERIFIED for a process crash.** The arms
  above kill the handler, not the host. No test in this repository crashes the process between a successful
  handler and its checkpoint write, so the redelivery that follows a crash is argued from the write order
  rather than demonstrated.
