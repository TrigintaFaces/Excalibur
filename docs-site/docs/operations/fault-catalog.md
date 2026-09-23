---
sidebar_position: 9
title: Fault and Degraded-Mode Catalog
description: Per-subsystem catalog of faults — the log event and metric you will see, what the framework does on its own, and what you have to do.
---

# Fault and Degraded-Mode Catalog

What each fault looks like **while it is happening**: the log event id you will see, the metric that
moves, what the framework does without being asked, and what is left for you.

Every event id and metric name on this page is emitted by the shipped packages. Log event ids are stable
identifiers — filter on the number, not on the message text, which may be reworded.

## How to read an entry

| Column | Means |
|---|---|
| **Fault** | The underlying condition. |
| **Observable** | The log event id, and the metric that moves. Log at `Information` or above to see the ones marked as warnings and errors. |
| **Framework behaviour** | What happens without operator action. |
| **Your action** | What the framework cannot do for you. |

**A note on what "handled" means here.** Most degraded modes in this catalog resolve into one of three
outcomes — *retry later*, *dead-letter*, or *refuse and stand down*. None of them is "the message was
lost", except where a row says so explicitly. Where a row says a message is discarded, that is a
configuration you can close.

## Index by fault class

| Fault class | Sections |
|---|---|
| Broker outage | [Transports and brokers](#transports-and-brokers), [Outbox](#outbox), [Inbox](#inbox) |
| Database or store outage | [Outbox](#outbox), [Event store and snapshots](#event-store-and-snapshots), [Sagas](#sagas), [Change data capture](#change-data-capture) |
| Network partition | [Leader election and fencing](#leader-election-and-fencing), [Transports and brokers](#transports-and-brokers) |
| Slow consumers | [Outbox](#outbox), [Inbox](#inbox), [Projections and materialized views](#projections-and-materialized-views) |
| Disk, quota, or capacity exhaustion | [Capacity and quota exhaustion](#capacity-and-quota-exhaustion) |
| Credentials expiring | [Credentials and key access](#credentials-and-key-access) |
| Clock skew | [Clock skew](#clock-skew) |
| Leader loss | [Leader election and fencing](#leader-election-and-fencing) |

## Transports and brokers

| Fault | Observable | Framework behaviour | Your action |
|---|---|---|---|
| Broker unreachable; subscriber's receive loop faults | `21000` *subscriber reconnecting*, with the attempt number and the backoff it will wait | The subscriber reconnects on an unbounded backoff loop. It does not give up and does not stop the host. | None while the broker is down. If the attempt counter climbs indefinitely, the broker is not coming back on its own. |
| A configured reconnect schedule returns a delay at or below the subscriber's floor | `21001` *backoff floor applied* | The floor is used instead of the configured delay, so the reconnect loop cannot spin without pause. | Fix the schedule. The floor is a guard, not a setting to rely on. |
| Broker rejecting publishes; the transport circuit breaker opens | `131220` from the outbox drain, naming the transport and the message | The message is **left for retry, not dead-lettered**. An open breaker is a statement about the transport, not about the message. | Nothing per-message. Restore the transport; the backlog drains. |
| Publish fails on the inbox dispatch path with an open breaker | `132218`, naming the message type | Same: left for retry, not dead-lettered. | As above. |

Transport health is surfaced through health checks and the transport health metrics; see
[Health Checks](../observability/health-checks.md).

## Outbox

Meters: `Excalibur.Outbox.Store` — `excalibur.outbox.store.operations` (counter, by operation type),
`excalibur.outbox.store.messages` (counter of messages *processed*, not a queue depth) and
`excalibur.outbox.store.operation_duration` (histogram, milliseconds) — and
`Excalibur.Dispatch.BackgroundServices` (`excalibur.background_service.messages_processed`,
`excalibur.background_service.messages_failed`, `excalibur.background_service.processing_cycles`,
`excalibur.background_service.processing_errors`, `excalibur.background_service.processing_duration`).

| Fault | Observable | Framework behaviour | Your action |
|---|---|---|---|
| A drain cycle throws | `131302` *background service error*; `excalibur.background_service.processing_errors` rises | The cycle ends; the next one starts on the configured polling interval. Claimed messages stay claimed until their reservation lapses, then are re-claimed. | Read the inner exception. Repeated errors with no processed messages is a store or transport problem, not an outbox one. |
| Shutdown could not drain in time | `131307` *drain timeout exceeded*, naming the configured timeout | The host stops anyway. In-flight messages stay claimed and are re-claimed by the next process after their reservation lapses. | Raise the drain timeout, or accept the redelivery. It is inside the at-least-once guarantee. |
| This instance is not the leader | `131308` *skipped — not the leader* | The cycle is skipped entirely. This is normal on every non-leader instance. | Nothing. If **every** instance logs this, no one holds leadership — see [Leader election](#leader-election-and-fencing). |
| Delivery succeeded but the store write recording it failed | `131310`, at `Error` | The delivery **stands**. The message is not dead-lettered — filing a delivered message as undeliverable is worse than the duplicate that may follow. The row is left claimed; a later drain completes the mark or redelivers. | Nothing per-message. Handlers absorb the duplicate. Investigate the store write. |
| Dead-letter routing itself failed | `131309`, at `Error` | The message stays claimed and is retried on a later cycle. The rest of the batch is unaffected. | Check the dead-letter store. |
| A message failed and **no dead-letter queue is registered** | `131226` — the message text begins `OUTBOX MESSAGE LOST` | The message is discarded permanently. | Register an `IDeadLetterQueue`. `131224` warns about this at startup, before anything is lost. |
| A failure report was declined because the claim is no longer held | `134005` at `Information` | The batch continues with the remaining messages. Another drain now owns that message. | Nothing. Frequent occurrences mean reservations are shorter than your delivery latency. |
| A failure report wrote nothing — no such message, or already terminal | `134006` / `134010` at `Information` | The batch continues. | Nothing. |
| A failure report returned an outcome this build does not recognise | `134007` at `Warning` | The failure is treated as **not** recorded; the message stays claimed until its reservation lapses. | Check for a store package at a different version from the drain. |
| The drain is running **unfenced** | `131228` at `Information` (no leader election registered) or `131227` at `Warning` (explicit `AsSingleWriter()` opt-out) | The drain runs. Safe with exactly one draining process; a genuine multi-writer deployment has an open split-brain window. | If more than one process drains this outbox, register a leader election on a store that supports fencing. |
| Backlog growing faster than the drain clears it | `excalibur.background_service.processing_cycles` keeps rising while `excalibur.background_service.messages_processed` stays flat or falls, and `excalibur.outbox.store.operation_duration` climbs. **No shipped meter reports outbox queue depth** — read it from the [operational dashboard](./dashboard.md), or count unsent rows yourself. | Nothing — this is a capacity condition, not an error. | Raise batch size or parallelism, or add capacity to the transport. See [Performance Tuning](./performance-tuning.md). |

## Inbox

Meter: `Excalibur.Inbox` (`excalibur.inbox.operations`, `excalibur.inbox.operation_duration`).

| Fault | Observable | Framework behaviour | Your action |
|---|---|---|---|
| A redelivered message is already processed | `132003` *duplicate detected*, at the level your `DuplicateBehavior` selects | The handler is **not** re-invoked. This is the inbox working. | Nothing. A rising rate means the transport is redelivering more, which is worth understanding. |
| The retry drain finds an entry another processor holds | `132223` | The drain skips it. The holder will finish it. | Nothing. |
| The drain's ownership term lapsed after the handler ran | `132224` | The handler's effect **stands**; the entry was reclaimed and will be retried by whoever now holds it. The handler may therefore run again. | Handlers must be idempotent. If this is frequent, the lease is shorter than handler duration — raise it. |
| A message failed and **no dead-letter queue is registered** | `132222` — the message text begins `INBOX MESSAGE LOST` | Discarded permanently. | Register an `IDeadLetterQueue`. `132220` warns at startup. |
| A message was dead-lettered | `132217` | The entry is terminal; the transport is acknowledged. | Replay from the dead-letter queue once the cause is fixed. |
| Shutdown could not drain in time | `132005` | The host stops. Unfinished entries are re-claimed after their lease lapses. | As for the outbox. |
| Handler slower than the lease | `132224` rising, together with repeated `132003` for the same ids | Work is re-admitted and re-run. | Raise the lease, or make the handler faster. A duplicate window bounded by the lease is the documented guarantee, not a defect. |

## Event store and snapshots

Meter: `Excalibur.EventSourcing.EventStore` and `Excalibur.EventSourcing.SnapshotStore`
(`excalibur.eventsourcing.eventstore.operations`, `…eventstore.duration`, `…eventstore.events_appended`,
`…eventstore.events_loaded`, `…snapshotstore.operations`, `…snapshotstore.duration`). Operations are
tagged with `operation.result`, whose values include `success`, `concurrency_conflict`, `failure` and
`not_found` — alert on the tag, not on a separate counter.

| Fault | Observable | Framework behaviour | Your action |
|---|---|---|---|
| Two writers append to one stream at the same version | `111104` *concurrency conflict*; `operation.result=concurrency_conflict` on the append counter | The append is rejected. Nothing is written. | Reload the aggregate and retry the command. A sustained rate means two writers own one aggregate. |
| A snapshot is missing for an aggregate that should have one | `112003` | The aggregate is reconstructed from events. Slower, not wrong. | Nothing, unless load times matter. |
| Auto-snapshotting is failing | `excalibur.eventsourcing.auto_snapshot.failed` rising against `…auto_snapshot.evaluated` | Snapshots are not created; replays lengthen over time. | Investigate the snapshot store. Load latency degrades gradually, which is why the counter matters more than the symptom. |
| A catch-up subscription's poll fails | `114603` | The subscription continues; the position is not advanced past unprocessed events. | Investigate the store. Replay on recovery is expected. |
| A subscription cannot deserialize a stored event | `114604` | That event is not delivered. | Almost always a missing or renamed event type after a deployment. Register the type or add an upcaster. |
| An event was read for an erased subject | `114508` *erased event skipped* | The event is skipped rather than surfaced. | Nothing. This is crypto-shredding working. |
| Cold-tier reads rising sharply | `excalibur.eventsourcing.archive.cold_reads`, with `…archive.errors` and `…archive.duration_seconds` | Reads fall through to cold storage transparently. | Expected after an archive run. Errors on this counter mean history is unreachable, which is a data-availability incident. |

## Projections and materialized views

Meters: `Excalibur.EventSourcing.Projections` (`excalibur.projection.error.count`, tagged with
`projection.type` and `error.type`) and `Excalibur.EventSourcing.MaterializedViews`
(`materialized_view.staleness`, `materialized_view.state`, `materialized_view.refresh.duration`,
`materialized_view.refresh.failures`).

| Fault | Observable | Framework behaviour | Your action |
|---|---|---|---|
| A projection handler throws on an event | `113103`; `excalibur.projection.error.count` rises with the `error.type` tag | The error is recorded. The projection does not silently advance past an event it did not apply. | Fix the handler, then rebuild the projection. |
| A projection falls behind the stream | `113102` *projection behind*; `materialized_view.staleness` climbs for views | Nothing — it catches up when it can. | Reads are stale meanwhile. Treat staleness as an SLI, not an error. |
| An async projection host errors | `113205`; individual event errors as `113204` | The host logs and continues; checkpoints are saved as `113203`. | A checkpoint that stops advancing while events arrive means the host is stuck, not slow. |
| A rebuild is requested while one is already running | `113108` | The second request is **rejected**, not queued. | Wait for the first. `GetStatusAsync` reports progress. |
| An event errors during a rebuild | `113105` | Recorded; the rebuild continues. | Review before trusting the rebuilt view. |
| A rebuild finds no store to persist into | `113107` | The rebuild produces nothing durable. | Register a projection store. |
| No global-stream query is available to the async host | `113206` | The host cannot read the stream it projects from. | The store in use does not offer a global stream. Choose a store that does, or use a different projection strategy. |

## Change data capture

See [CDC Troubleshooting](./cdc-troubleshooting.md) for diagnosis; this table is the behaviour contract.

| Fault | Observable | Framework behaviour | Your action |
|---|---|---|---|
| No CDC processor is registered but the background service is running | `3110` | The service does nothing. | Register a processor, or disable the service (`3100` confirms it is disabled). |
| A processing cycle throws | `3102`, then `3109` *error backoff* | Backs off and retries. No checkpoint is written for a table that failed, so the next run resumes from the last durable position and redelivers. | Duplicates on recovery are expected; the window is the whole run for that table. |
| Shutdown could not drain in time | `3105` | Stops; the next run resumes from the last checkpoint. | None. |
| The stored position is no longer available in the source | `100743` *stale position detected*, then `100744` *recovery attempt* or `100746` *recovery exhausted* | Applies the configured `StalePositionRecoveryStrategy`: `Throw` stops, `FallbackToEarliest` replays, `FallbackToLatest` **skips everything in between**. | Confirm which strategy you are running. `FallbackToLatest` is opt-in data loss. |
| A stale log sequence number is reset | `100942` | Detection resets the position for that table. | Expect a replay. |
| A checkpoint was deliberately not written | `100742` *checkpoint skipped* | The position stays where it was, so the changes are redelivered. | This is the guarantee holding. Nothing to do. |
| The in-memory idempotency filter is full | `100981` — *new events will not be tracked for deduplication* | The filter stops tracking. Deduplication degrades; delivery does not stop. | Raise the capacity, or move to a durable idempotency filter. Silent duplicate suppression ending is exactly the kind of degradation worth alerting on. |
| The replication connection drops (PostgreSQL) | `102308` processing error, `102309` fatal error | Transient errors retry; a fatal error stops the processor. If a consecutive-transient-failure bound is configured, exhausting it stops **without** writing a position. | A stop with no position written means the next run replays rather than skips. That is the safe direction. |

## Sagas

Meters: `Excalibur.Dispatch.Sagas` (`dispatch.saga.started_total`, `dispatch.saga.completed_total`,
`dispatch.saga.failed_total`, `dispatch.saga.compensated_total`, `dispatch.saga.duration`,
`dispatch.saga.handler_duration`) and the saga store's `excalibur.saga.operations` /
`excalibur.saga.operation_duration`.

| Fault | Observable | Framework behaviour | Your action |
|---|---|---|---|
| A step fails | `122203` *step failed, starting compensation* | Compensation runs for the steps already executed. | Make compensations idempotent; they can run more than once. |
| Compensation completes | `122204`; `dispatch.saga.compensated_total` rises | The saga ends compensated. | A rising compensation rate is a business-level signal, not only an operational one. |
| A saga execution fails outright | `122205`; `dispatch.saga.failed_total` rises | The saga ends failed. | Compensation may be incomplete. Reconcile by hand. |
| A timeout could not be delivered | `121203` | Logged; the timeout batch continues. | A saga waiting on an undelivered timeout waits forever. Alert on this. |
| A timeout's message type cannot be resolved or constructed | `121206`, `121208`, `121207` | That timeout is not delivered. | Almost always a type renamed or removed by a deployment while timeouts were already scheduled. |
| A timeout was superseded before firing | `121209` | Not delivered — a newer schedule replaced it. | Nothing. |
| The timeout service stops | `121205` | Scheduled timeouts stop firing until it restarts. | Alert on this. A stopped timeout service is silent: sagas simply never advance. |

## Leader election and fencing

Meter: `Excalibur.LeaderElection` — `excalibur.leaderelection.acquisitions`,
`excalibur.leaderelection.lease_duration`, `excalibur.leaderelection.is_leader`.

| Fault | Observable | Framework behaviour | Your action |
|---|---|---|---|
| An instance cannot acquire the lock | SQL Server `184002`; the instance's `excalibur.leaderelection.is_leader` stays at zero | Normal for non-leaders. | Alert when the **sum** of `is_leader` across instances is zero for longer than a lease plus grace — that is nobody leading, not one leader. |
| Renewal fails; leadership is lost | SQL Server `184006` then `184008`; Redis `183006` then `183009`; Kubernetes `182015` / `182021` then `182019` | The instance relinquishes within its configured grace period. Another instance acquires. | This is a partition healing correctly. Frequent transitions mean the lease is too short for your network. |
| The fencing-token mint fails | SQL Server `184010`; Redis `183011` | **Fail-closed** — leadership is not declared and the lock is released. A tenure without a valid token never becomes observable. | Check the mint. On SQL Server and PostgreSQL it is a `SEQUENCE`; on Redis and Consul a counter key. |
| The fencing token space is exhausted | `180008` (in-memory), `181024` (Consul), `182105` (Kubernetes) | Fail-closed: no token, no leadership. | This requires a store reset, not a restart. See [Disaster Recovery](./disaster-recovery.md#fences-and-high-water-marks). |
| A superseded tenure tries to mark a message sent | `131229` *fenced mark-sent refused* | The cycle aborts with no further store write. The message is left claimed for the current leader. | Nothing. The fence is doing its job. A **sustained** stream of these means a stale process is still running, or the mint sits below the recorded high-water after a restore. |
| A superseded tenure tries to report a failure | `134008` | Nothing is recorded. The tenure should stand down rather than keep draining. | As above. |
| A superseded tenure tries to dead-letter | `134009` | The outbox row is **not** destroyed. A newer tenure owns the message and will resolve it. | Nothing. |
| A dead-letter entry was withdrawn after its fenced mark was refused | `131311` at `Warning` | The entry is removed, so nobody redrives a message that may yet be delivered. | Nothing. |
| That withdrawal itself failed | `131312` at `Error`, naming the entry and the message | The entry remains and describes a message this tenure no longer owns. | **Do not replay that entry.** Confirm the message's fate, then remove the entry by hand. |
| A candidate cannot confirm its own leadership | Consul `181025` *grace period elapsed* | Relinquishes within the grace period. The grace period is a hard upper bound on time-to-relinquish. | Size the grace period against your worst-case network stall, not your average. |

## Capacity and quota exhaustion

| Fault | Observable | Framework behaviour | Your action |
|---|---|---|---|
| The in-memory outbox store is full of undelivered messages | `StageMessageAsync` **throws** | It **refuses rather than evicting**. Capacity is reclaimed only from messages whose delivery is over. The caller — whose own transaction has not committed — learns the message was not accepted. | Size `MaxMessages` for the drain you actually run. A persistently full store is a drain that is not keeping up. |
| The inbox processing queue is saturated | Throughput flat against `excalibur.inbox.operations` | Bounded by `QueueCapacity`, `ProducerBatchSize`, `ConsumerBatchSize` and `PerRunTotal`. Back-pressure, not loss. | Raise the bounds, or add consumers. |
| The CDC idempotency filter is full | `100981` | Stops tracking new events. Deduplication degrades; delivery continues. | Raise capacity or use a durable filter. |
| A cloud store throttles (request-unit or capacity limits) | Provider retry policies engage; `excalibur.outbox.store.operation_duration` and `excalibur.inbox.operation_duration` rise before error counts do | SDK-managed retry with backoff. | Latency rises before anything fails — alert on the duration histograms, not only on errors. See [Recovery Runbooks](./recovery-runbooks.md#cloud-provider-recovery-scenarios). |
| The database is out of space or resources | Provider-specific transient codes (PostgreSQL `53xxx`, SQL Server log-full errors) | Treated as transient and retried. Nothing is marked sent that was not sent. | Retrying will not create disk. Alert on sustained retry, not on individual errors. |

## Credentials and key access

| Fault | Observable | Framework behaviour | Your action |
|---|---|---|---|
| The key-management service is unreachable or refuses access | `92218` *key management failed*, then `92214` with the collected failures. The encryption health check reports **Unhealthy** (a *slow* key-management response reports Degraded with `92215` instead). | The health check result changes, so an orchestrator can stop routing to the instance. Nothing else stops. | Wire the health check — this path runs only while a key-management provider is registered and `VerifyKeyManagement` is on, which is the default. An expired credential is otherwise visible only as decryption failures. |
| A field cannot be decrypted | `92206` *decryption error for field* | Reported rather than silently skipped. | Usually a key version the store no longer holds — see [Disaster Recovery](./disaster-recovery.md#key-management-and-crypto-shredding). |
| A field that should have been decrypted was read still encrypted | `92974` | Reported. | A registration or configuration gap, not a key problem. |
| Scheduled key rotation fails | `92550` *rotation failed*, `92551` *rotation exception*, `92544` *rotation check error* | The existing key stays in use. Nothing breaks immediately. | Alert on this. A rotation that silently stops means key age grows without bound — `92223` reports a key past its maximum age. |
| Re-encryption after a rotation fails | `92209`, per item `92210` | Items stay under the old key and remain readable. | Re-run the migration. Do not retire the old key first. |
| Escrow backup or recovery fails | `92942` | Fails closed; no key material is released below the configured custodian threshold. | Recovery authorization, not confidentiality — see [Disaster Recovery](./disaster-recovery.md#key-management-and-crypto-shredding). |
| Erasure could not delete a key | `92708`; verification reports `92720` *key not deleted* with the current status | The erasure is **not** reported complete. | This is a compliance obligation, not a retry. Resolve before closing the request. |

## Clock skew

Clock skew is not a fault the framework reports as an event; it is a condition that changes how other
guarantees behave. What matters is which clock each decision uses.

| Decision | Clock used | What skew does |
|---|---|---|
| Outbox retry backoff floor | The **store's** clock. Each store composes the caller's schedule with the floor over durations anchored to the server clock. | No dispatcher clock reaches the persisted gate. Dispatcher skew does not shorten the floor. |
| Leader lease expiry (Kubernetes) | The **API server's** stamp on `renewTime`. | Candidate skew does not affect the comparison. |
| Leader lease expiry (MongoDB) | The **database's** clock, compared server-side. | As above. |
| Leader lease expiry (Redis) | The **Redis server's** TTL. | Expiry safety never rests on a client clock. The client-side grace governs only how fast a candidate self-demotes after a renewal fault. |
| Leader lease expiry (SQL Server, PostgreSQL) | Elapsed **monotonic** time on the candidate, against a session-scoped lock. | Monotonic time is unaffected by wall-clock adjustment. A large NTP step does not extend a lease. |
| Inbox lease and message time-to-live | The store's clock. | Skew between application hosts does not widen the duplicate window; skew between **database replicas** can. |

**The practical guidance is narrow, because the design already removed most of the exposure:** keep your
*database* and *broker* clocks disciplined, and size leader-election grace periods against the worst
stall you will tolerate rather than the average. Application-host clock skew is, by construction, not
load-bearing for any of the decisions above.

## Schema migration

Relevant during an upgrade; see the [upgrade runbook](../migration/version-upgrades.md#upgrading-across-pre-releases).

| Fault | Observable | Framework behaviour | Your action |
|---|---|---|---|
| Pending migrations found at startup | `114414` | Reported, with the list. | Apply them before the new version writes. |
| The migration lock cannot be taken | `114412` | The migration does not run. The lock serializes concurrently starting instances, so this usually means another instance is migrating. | Wait. If nothing is migrating, a lock is stranded. |
| A migration script is missing | `114418` | The migration cannot proceed. | The applied history references a script this build does not carry — usually a rollback to a version that predates it. |
| An already-applied script's checksum has changed | `114416` *checksum drift* | Reported. A script that was edited after being applied no longer describes the database you have. | Never edit an applied script. Add a new one. |
| A migration fails | `114403` | Reported. The database is in whatever state the script left it. | Restore, or fix forward, deliberately. |
| A rollback fails | `114407` | Reported. | The database is between two states. This is a restore, not a retry. |

## See Also

- [Disaster Recovery](./disaster-recovery.md) — restoring state, and what a restore does to these positions
- [Recovery Runbooks](./recovery-runbooks.md) — provider-specific recovery procedures
- [Incident Runbooks](./incident-runbooks.md) — severity model, ownership, and response flow
- [Reliability Guarantees](./reliability-guarantees.md) — the guarantees these degraded modes preserve
- [SLO, SLI, and Telemetry](./slo-sli-telemetry.md) — which of these metrics belong in an objective
- [Health Checks](../observability/health-checks.md) — turning these conditions into readiness signals
