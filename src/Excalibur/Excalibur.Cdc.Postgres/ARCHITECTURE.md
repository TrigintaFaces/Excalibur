# Architecture — Excalibur.Cdc.Postgres (logical-replication change data capture)

This document states what the PostgreSQL CDC processor guarantees, how it achieves it, and what it does
**not** yet prove. It is a contract, not a description: every claim below is meant to be falsifiable, and
where a guarantee is not enforced by a test we say so rather than assert it.

## Am I exposed?

Four questions you can answer about your own application without reading the rest of this document.

| Ask | If yes |
|---|---|
| Did you register with `AddPostgresCdcWithInMemoryState(...)`? | **The guarantees below do not apply to you.** They are stated in terms of a position that survives a restart, and the in-memory state store does not keep one. Use `AddPostgresCdc(...)` with the Postgres state store for anything you care about. |
| Can two calls to `ProcessBatchAsync` overlap **in one process** — a timer trigger that can fire again before the previous run finishes? | You are exposed to the concurrency gap below. A `SemaphoreSlim(1,1)` in your own wrapper closes it, as does a host setting that keeps the trigger to one concurrent invocation. PostgreSQL already prevents a second *process* from interfering; it cannot prevent a second *call*. |
| Are your configured table names unqualified (`orders` rather than `public.orders`) in a database with per-tenant schemas? | Changes from another schema's same-named table will reach your handler. Qualify the name. |
| Does your handler do anything besides the write itself when it sees a `Truncate` — publish, audit, count? | That side effect can run more than once. See the truncate obligation below. |

## Guarantees

### Delivery is at-least-once, and the duplicate window is a whole transaction

**Every change in a committed transaction is delivered to the handler at least once. A change may be
delivered more than once, and the duplicate window is the entire transaction — every change it carried,
not only the change that failed.**

This is the load-bearing sentence for a consumer, because it decides whether their handler is correct.
Nothing in this package deduplicates. There is no per-change marker, no "already seen" record, and
therefore nothing that could skip a change on a replay. Redelivery is complete and duplicative by
construction.

**A multi-table `TRUNCATE` makes that window wider than it looks.** PostgreSQL encodes
`TRUNCATE a, b, c` as a *single* replication message naming every relation, and the processor fans out
one change per relation. If the handler throws on the third, the transaction is not confirmed, and on
resume all three are delivered again — including the two that already succeeded.

### The checkpoint never advances past a change no handler saw

**The durable position never passes the log position of a message until every change that message carried
has been handed to the handler and the handler returned, or the change was rejected by the configured
table filter.**

The two outcomes are deliberately indistinguishable at the point of confirming, because they are not two
values of a variable — they are two different facts about whether the confirm is reachable at all:

```
filtered by configuration   ->  the loop continues     ->  the confirm is reached
the handler throws          ->  the iteration unwinds  ->  the confirm is UNREACHABLE
```

### The slot is never acknowledged ahead of the durable position

**The replication slot's confirmed position is always at or behind the position recorded in the state
store.** A crash between the two writes therefore loses no work: the stream resumes from the slot, which
is the older of the two, and redelivers.

### Both loops resume from the slot, and the state store never decides where

**The continuous loop and the batch loop both resume from the replication slot's confirmed position.** The
position in the state store is recorded for observation and never passed to the server as a start point.
This matters because PostgreSQL starts a logical stream at the requested position or the slot's confirmed
position, *whichever is greater*: a start position supplied by the client can only ever move the start
forward of the slot, never back. A client that resumed from its own record would therefore skip, for good,
any change between the slot and that record that had not been delivered.

## How it is achieved

- **The fan-out is inside one iteration.** `PostgresCdcProcessor.cs` — the continuous loop at `:473` and
  the batch loop at `:197` each drain the whole change list for a message before the iteration body
  continues.
- **The confirm sits at the bottom of a later iteration.** `ConfirmCommitAsync` is reachable from exactly
  one branch in each loop (`:500` continuous, `:219` batch), taken only for a commit message. `TRUNCATE`
  is transactional in PostgreSQL, so its commit always arrives as a strictly later message than the
  fan-out that produced the changes.
- **No `catch` sits between the handler and the confirm.** The confirm is not skipped by a flag; an
  exception makes it unreachable. **That absence is the whole mechanism**, which is why the gap below is
  about protecting it rather than about any code that exists today.
- **The confirm's write order is the invariant.** `ConfirmCommitAsync` (`:579`) writes the state store
  first (`:583-585`) and acknowledges the slot second (`:588`). **Reversing those two writes converts this
  package from at-least-once to lossy**, and no test in the repository would fail.
- **Ordering is log-sequence, never wall clock.** The commit timestamp carried on each change is the
  server's own stamp, passed through as data; no decision is made by comparing it to anything.

- **One resume source.** Both `StartReplication` calls in `PostgresCdcProcessor.cs` (the batch loop and the
  continuous loop) pass no start position, so the server resumes from the slot. The slot's confirmed
  position advances only from the flush position this processor reports, which `ConfirmCommitAsync` sets
  after the transaction's changes were handed off; nothing reports a received-but-undelivered position as
  flushed.

## Evidence

- **Multi-table `TRUNCATE`, real PostgreSQL** — `PostgresCdcMultiTableTruncateIntegrationShould`: five
  arms against a real `wal_level=logical` container, never skip-gated, because the defect they lock lives
  in the replication message encoding and a mock returns whatever it was told. They cover a multi-table
  truncate on the batch path, a relation *after the first* matching the configured tables, `CASCADE`, the
  continuous path, and the failure case below.
- **The checkpoint arm is non-vacuous, and the distinction matters.** The failure arm reads the durable
  position **from the state store itself** rather than observing redelivery, so an implementation that
  confirmed early would fail it before redelivery is ever consulted. Its assertion that two relations were
  seen before the failure is the guard that stops the arm passing against an implementation that never
  reached the second relation at all.
- **Resume authority, real PostgreSQL** — `PostgresCdcResumesFromTheSlotIntegrationShould`: two arms, one per
  loop, never skip-gated. Each records a state-store position *ahead* of a change that was not delivered and
  asserts the change is still delivered; a change inserted afterwards is the control that the stream ran.
  With the continuous loop starting from the state store, its arm fails on exactly that assertion.
- **Independent reliability review, 2026-09.** An adversarial review of the checkpoint invariant attempted
  to construct a violating interleaving for a single caller and could not.

## Consumer obligations

1. **Handlers MUST be idempotent, and here is what to key on.** At-least-once is the guarantee, so applying
   the same change twice must be safe. For an insert, update or delete, the change carries `KeyColumns`, and
   `(TableName, KeyColumns)` identifies the row. If you would rather not build that yourself, registering an
   idempotency filter deduplicates on the event's position before your handler runs.
2. **A truncate carries NO key columns, so it needs a different key.** This is the obligation most easily
   missed, and it fails in two steps. First, a truncate *looks* idempotent — truncating an empty table is
   harmless — until the handler does something else alongside it, such as publishing a notification or
   writing an audit row. Second, the obvious guard does not fit: a truncate has no row, so it is created
   without key columns and a `(table, primary key)` dedup has nothing to grip. Its identity is
   `(TransactionId, SchemaName, TableName)`.
3. **Call `ProcessBatchAsync` from one caller at a time.** The processor is registered as a singleton, so
   every resolved interface hands back the same instance; two overlapping calls are two callers of one
   object. See the first known gap — this is an obligation today rather than something the type enforces.
4. **A configured table name matches on suffix.** `orders` matches `public.orders` *and* `audit.orders`.
   This errs toward delivering more than you asked for, never less — but in a schema-per-tenant deployment
   it means changes from another tenant's schema reach your handler. Qualify the name if that matters.
5. **Batch size is a floor, not a ceiling.** A transaction is always processed whole, so a single
   transaction larger than the configured batch is delivered in full rather than split. Splitting it left a
   handled prefix unconfirmed and replaying forever, which is why it is not done.

## Known gaps

- **Two overlapping callers of the batch entry point are not prevented, and the result is worse than a
  stall.** The processor is registered as a singleton and keeps the current transaction identity in
  instance state, so a second concurrent call can overwrite the identity the first is using — stamping a
  transaction's changes with another transaction's id and commit time — and can acknowledge a transaction
  whose changes the other caller has not yet handled. The mutual exclusion this package actually relies on
  is PostgreSQL permitting one consumer per replication slot, which prevents a *second process* from
  interfering and does not prevent a *second call in the same process*, because both share one connection.
  Until this is enforced, obligation 3 above is the only thing standing between a serverless timer trigger
  with overlapping invocations and a checkpoint that advances past undelivered work.
- **By default, a handler that fails deterministically stalls the stream indefinitely and pins write-ahead
  log on the server — and the remedy is opt-in.** A fault is classified before the loop decides what to do.
  With no `IMessageFailureClassifier` registered, only a small set of definitively non-retryable framework
  exceptions counts as fatal and **anything unrecognised is treated as transient**, so an ordinary handler
  exception reconnects and retries from the un-advanced position, forever. Because the slot is never
  acknowledged, the server retains WAL for it, and a permanently poisoned handler becomes a disk-space
  outage on the database rather than a quiet backlog.

  **What bounds it now, and what still does not.** The retry backs off exponentially from
  `PollingInterval` up to `CdcFatalErrorOptions<TEvent>.MaxReconnectDelay` (one minute by default) instead
  of repeating at a fixed interval, and each consecutive failure without progress is reported to the CDC
  health check, which turns Unhealthy at `CdcHealthCheckOptions.UnhealthyConsecutiveTransientFailures`
  (three by default) when you register `AddCdcHealthCheck()`. Setting
  `CdcFatalErrorOptions<TEvent>.MaxConsecutiveTransientFailures` makes the processor stop after that many
  consecutive failures, through the same path as a fatal error, with a `CdcRetryExhaustedException`. No
  position is written on the way out. The default is no limit, so an unconfigured processor still retries
  indefinitely; it now does so visibly and with backoff.

  **You can also stop it on classification.** Register an `IMessageFailureClassifier` that returns
  `Permanent` or `Poison` for the failures your handler cannot recover from: the processor then stops on the
  first occurrence and invokes `CdcFatalErrorOptions<TEvent>.OnFatalError` if you supplied one. Either way
  there is still no skip-after-N and no dead-letter path, so a stop stops the stream rather than stepping
  over the change.

  **A replication-slot refusal (`55006 object_in_use`) is transient on purpose.** It is what a new instance
  sees while the previous one still holds the slot during a rolling deployment, and it clears when that
  connection closes. A small `MaxConsecutiveTransientFailures` can therefore stop the new instance before
  the old one releases the slot: with a one-second interval, five failures elapse in about thirty-one
  seconds. Size the limit against your termination grace period.

  Alert on replication-slot lag as well as on the health check: a stream that connects successfully and
  then fails after longer than `MaxReconnectDelay` is treated as healthy each time, so a fault that
  recurs only after a long run is visible on the server as a slot whose confirmed position stops moving,
  and not in the failure count.
- **The checkpoint property is proven on the batch path and unproven on the continuous one.** The guarantee
  rests on there being no `catch` between the handler and the confirm, so the change that would break it is
  someone adding one — a reasonable-looking edit that stops one bad row wedging a pipeline, and which
  silently converts a failed change into a filtered one. On the **batch** path that edit is caught: the
  failure arm drives the batch loop and asserts the durable position did not move. On the **continuous**
  path nothing asserts the failure case — its arm covers delivery only — so the same edit there would
  redden nothing. The two paths are equally exposed in principle and unequally covered in fact.
- **Editing the state store does not replay anything.** Because neither loop resumes from it, rewinding the
  recorded position has no effect on where processing starts. To replay, reposition or recreate the
  replication slot.
- **The recorded position only moves forward.** The state store's upsert refuses a write that is not ahead
  of the stored position, so an out-of-order confirm is a no-op rather than a regression. It is an
  observation of progress, not a guard: the slot is what prevents loss.
