---
sidebar_position: 2
title: What's New
description: Release history, recent changes, and upgrade notes for Excalibur.
---

# What's New

Track what's changed across Excalibur releases. For upgrade guidance, see [Versioning Strategy](./migration/version-upgrades.md).

## Current Version: 3.0.0-alpha

Excalibur is in active pre-release development. The framework is functionally complete with 112,000+ automated tests across 170 packages.

---

## June 2026 — Reliability & Wiring Correctness (Sprint 850)

A focused sweep closing a class of **wiring/registration/correctness** gaps where advertised behavior did not actually fire, plus concurrency/memory hazards. Each fix carries a non-vacuous independent regression lock (red on the pre-fix code), green across the 10-shard full CI run plus Docker container shards, with both independent reviews (code + architecture/CSO) approved at zero blocking findings. These are behavioral **corrections**; as a greenfield framework there are no consumer data migrations, but **SQL Server inbox users must add the new `NextAttemptAt` column** (below), and you should review the items for behavior you may have relied on.

### Retry middleware now classifies failed *results* instead of retrying every failure

- **`RetryMiddleware` now retries a failed `IMessageResult` only when its RFC 7807 status is transient — `408`, `429`, or `5xx` — matching Polly / `HttpClientFactory` `HandleTransientHttpError` semantics.** Previously it retried *every* non-success result, which re-ran non-idempotent handlers on permanent client errors (validation / 4xx). A `4xx` result other than 408/429, and a failed result with **no** `ProblemDetails`/`Status`, are now treated as **permanent → not retried** (a deliberately returned failure with no transient signal is a handler statement that retry will not help). **Exception-based retry is unchanged** — genuine transient faults surface as exceptions and continue to be handled by the existing exception filters (`RetryableExceptions` / `NonRetryableExceptions`). If you relied on the old "retry all failures" behavior for a result that returns a non-transient status, return a transient status (or throw a retryable exception) instead. See [Retry Middleware](./middleware/built-in.md#retry-middleware).
- **Exponential backoff can no longer overflow.** Every backoff strategy (including `ExponentialWithJitter`, which previously returned an *uncapped* delay) now clamps the computed milliseconds against `MaxDelay` **before** constructing the `TimeSpan`, so a high attempt count collapses to `MaxDelay` instead of throwing `OverflowException` on a non-finite value.

### Inbox retry now honors exponential backoff

- **The inbox processor now schedules failed entries with the computed exponential backoff instead of a hardcoded 5-minute window.** On a failure it persists `NextAttemptAt = now + IBackoffCalculator.CalculateDelay(attempt)`, and the retryable-fetch predicate becomes `NextAttemptAt IS NULL OR NextAttemptAt <= now` — so the configured backoff genuinely throttles redelivery (mirroring the Sprint 849 outbox fix). This uses the new optional `IBackoffSchedulableInboxStore` capability (`MarkFailedWithBackoffAsync`); the SQL Server inbox store implements it, stores without it fall back to the existing immediate-retry path (fail-open), and the capability is forwarded transparently through the telemetry and encrypting inbox-store decorators.
- **SQL Server inbox users must add a `NextAttemptAt DATETIMEOFFSET NULL` column** to the inbox table (the store does not auto-create tables). See [Inbox → Retry Backoff Schedule](./patterns/inbox.md#retry-backoff-schedule).

### Postgres outbox now supports retry backoff

- **The Postgres outbox store now implements `IBackoffSchedulableOutboxStore`** (`MarkFailedWithBackoffAsync`), so the computed backoff is applied and the claim query excludes not-yet-due rows — signature-identical to the SQL Server store for cross-provider consistency. Other non-SQL-Server providers (Redis/Mongo/Elasticsearch/DynamoDB/Cosmos) retain the existing immediate-retry fail-open behavior and are tracked as follow-ups. See [Outbox → Ordering and Retry Scheduling](./patterns/outbox.md#ordering-and-retry-scheduling).

### Sagas persist before they dispatch, and missing sagas hit a handler

- **A saga's emitted commands and events are now buffered during `HandleAsync` and dispatched only *after* the saga state is durably persisted (save-then-dispatch).** Previously a command was dispatched immediately and `SaveAsync` ran afterward, so a persistence failure followed by replay re-dispatched the command → duplicate side effects. Now a `SaveAsync` failure dispatches nothing and the emitted messages re-buffer on the next delivery. This is internal to the coordinator — `SagaBase.SendCommandAsync`/`PublishEventAsync` remain the same `protected` helpers you call; they no longer return a dispatch result because dispatch happens later. Per-emit FIFO order is preserved.
- **`ISagaNotFoundHandler<TSaga>` is now invoked when an event arrives for a non-existent saga.** A default `LoggingNotFoundHandler<TSaga>` is registered out of the box (logs the orphaned continuation, behavior preserved). Register a custom handler with `WithNotFoundHandler<TSaga, THandler>()` to dead-letter / park / compensate instead of dropping the event. See [Sagas](./sagas/index.md).

### ASP.NET Core authorization faults return 500, not a leaky 403

- **When the ASP.NET Core authorization middleware's evaluation *throws*, it now returns HTTP 500 with a generic sanitized message and logs the full exception server-side** — instead of the previous 403 carrying the raw `ex.Message`, which both masked a server-class error as a denial and leaked internal detail across the trust boundary. An authorization **denial** (not an exception) still returns 403, unchanged.

### Internal concurrency hardening

- **Leader-election renewal timestamps** (Redis / Postgres / SQL Server) are now read/written lock-free via `Interlocked` on a `long` ticks field, eliminating a torn multi-field read that could miscompute the grace/split-brain window.
- **Event-sourcing internals** were hardened: the snapshot-tracking dictionary is now bounded (cap ≈ 1024, re-derive on miss) to prevent unbounded growth for high-cardinality aggregates; `EventVersionManager`'s upgrader map is now thread-safe (`ConcurrentDictionary` + lock, matching `SnapshotVersionManager`); and a handler-warmup-cache TOCTOU NRE on first dispatch was closed with a single local-copy read.

---

## June 2026 — Outbox/Inbox Reliability Hardening (Sprint 849)

A focused sweep closing the "advertised-but-broken" gaps on the default dispatch and outbox path: the default pipeline now actually runs, outbox ordering keys are persisted and honored, and the computed retry backoff is genuinely applied. Each fix carries a non-vacuous independent regression lock (red on the pre-fix code), green across the 10-shard full CI run plus Docker SQL Server container shards, with both independent reviews (code + architecture/CSO) approved at zero blocking findings. These are behavioral **corrections**; as a greenfield framework there are no consumer data migrations, but SQL Server outbox users must add the new ordering/backoff columns (below), and review the items for behavior you may have relied on.

### The default dispatch pipeline now runs registered middleware

- **`AddDispatch`'s default path now executes the `default` pipeline profile — so `DispatchAsync` runs the registered default middleware (notably `OutboxStagingMiddleware`) without any explicit `ConfigurePipeline`/`UseProfile` call.** Previously the default pipeline resolved to an empty profile and `DispatchAsync` bypassed all middleware, so outbox staging silently never ran on the default path. Middleware in the profile that you have not registered are skipped gracefully (fail-open) with a debug log (`InvokerMiddlewareSkipped`, event ID 10024) — only registered middleware execute, keeping the default path working out of the box while staying opt-in for heavier middleware. See [Pipeline Profiles](./pipeline/profiles.md).
- **A custom-registered `IPipelineProfileRegistry` is now preserved instead of being clobbered, and `UseProfile` on an unknown profile key throws `ArgumentException` at configuration time** (fail-loud rather than silently resolving an empty pipeline).

### Outbox messages keep their order and honor retry backoff

- **Outbox ordering keys are persisted and honored.** Each message now stores `PartitionKey`, `GroupKey`, and a monotonic `SequenceNumber`, and the SQL Server claim query selects rows in `(PartitionKey, SequenceNumber)` order — so messages sharing a `PartitionKey` are delivered in ascending sequence (per-partition FIFO). **SQL Server outbox users must add the `PartitionKey`, `GroupKey`, `SequenceNumber`, and `NextAttemptAt` columns plus the `IX_OutboxMessages_Claim` index** to the `OutboxMessages` table (the store does not auto-create tables) — see the [Outbox schema](./patterns/outbox.md#sql-server).
- **The computed exponential backoff is now actually applied.** On a delivery failure the processor records the next-attempt time on `NextAttemptAt`, and the claim predicate excludes the message until that time elapses — previously the backoff was computed but never used, so a failed message was re-claimed as soon as its lease expired. A circuit-breaker-open short-circuit is excluded from backoff (no delivery was attempted), so it stays immediately retryable. Backoff scheduling uses the new optional `IBackoffSchedulableOutboxStore` capability (`MarkFailedWithBackoffAsync`); the SQL Server store implements it, stores without it fall back to the existing immediate-retry path (fail-open), and the capability is forwarded transparently through the telemetry and encrypting store decorators. See [Outbox → Ordering and Retry Scheduling](./patterns/outbox.md#ordering-and-retry-scheduling).

### Outbox-to-transport now propagates tenant and causation

- **Outbox publishing now copies `TenantId` and `CausationId` onto the outbound transport message.** Both were dropped when the outbox handed a message to the transport, breaking multi-tenant routing and cause-effect tracing for outbox-delivered messages; they are now carried through symmetrically with the inbox restore side.

### Architecture build gate (contributor-facing)

- **Sibling `*_ENFORCE` CI flags and the package-map drift check are now wired to the `ARCH_ENFORCE` gate**, extending the Sprint 848 architecture-boundary enforcement. No runtime impact.

---

## June 2026 — Projection Correctness + the Transactional Outbox Keystone (Sprint 848)

A large-batch P1 correctness sweep across the projection stores, options validation, and the architecture build gate — plus the keystone that makes the **transactional event+outbox** path real. Each fix carries a non-vacuous independent regression lock (red on the pre-fix code), all green across the 10-shard full CI run + Docker/emulator container shards, with both independent reviews (code + architecture/CSO) approved at zero blocking findings. These are behavioral **corrections**; as a greenfield framework there are no consumer data migrations, but review the items below for behavior you may have relied on.

### Transactional event + outbox staging is now real (and SQL Server supports it)

- **Selecting `OutboxStagingStrategy.Transactional` now atomically appends events and stages outbox messages in one database transaction — for event stores that support it.** Sprint 841 made the strategy *fail fast* when its infrastructure was missing; Sprint 848 builds the path it was guarding. The optional `ITransactionalEventStore` extension of `IEventStore` is now **public** (namespace `Excalibur.EventSourcing`), and `SqlServerEventStore` implements it. Its `AppendWithOutboxStagingAsync` is a **store-owned atomic unit of work**: the store opens and owns a single connection and transaction, runs the optimistic-concurrency check, appends the events, invokes your outbox staging on that *same* transaction (only if the version check passed), then commits — rolling everything back on a concurrency conflict or any staging failure. The transaction never escapes the store, so an event append and its outbox rows can never land on two different transactions.
- **With SQL Server + an `ITransactionalOutboxWriter` registered, the default `Auto` strategy now resolves to `Transactional`** — integration events can no longer be lost in the crash window between the event append and the outbox stage. NoSQL event stores (which do not implement `ITransactionalEventStore`) continue to use eventually-consistent or deferred staging. See [Outbox Pattern → Event Sourcing Outbox Integration](./patterns/outbox.md).

### Projection queries apply your filters server-side

- **DynamoDB and Firestore projection `QueryAsync`/`CountAsync` now honor the `filters` argument.** Both stores previously **ignored filters entirely** and returned unfiltered (over-broad) result sets — a data-correctness defect. DynamoDB now AND-combines filters into a server-side `ScanRequest` `FilterExpression`; Firestore queries a write-only flat index map with real `Where(key, ==, value)` clauses (the canonical JSON blob stays the source of truth, so `decimal`/`DateTimeOffset` values keep exact round-trip fidelity). A null/empty filter returns all rows; an untranslatable (e.g. nested-key) filter throws `NotSupportedException` rather than silently returning unfiltered. If you query projections with filters on these providers, you will now get correctly filtered results. See [Data Providers](./data-providers/index.md).
- **DynamoDB cursor pagination reports a true total and fills each page.** `QueryCursorAsync` previously ran a full count scan per page and reported a truncated partial as the total; it now fills each page to the requested size by walking `LastEvaluatedKey` (DynamoDB applies its scan `Limit` *before* filtering), computes the true total once and carries it in the cursor, and returns a `null` cursor on exhaustion.

### Projection checkpoints no longer advance ahead of the cursor map

- **`GlobalStreamProjectionHost` now saves the cursor map before advancing the checkpoint.** The checkpoint (the source of truth) was previously saved first, so a crash or cursor-map save failure could leave the checkpoint ahead of a durable cursor map (restart divergence), and the pending-cursor buffer could grow unboundedly under repeated save errors. The order is inverted to **cursor-map first → checkpoint last** (on both the periodic and graceful-shutdown flush paths), and the pending buffer is now bounded on the error path. See [Projections](./event-sourcing/projections.md).

### Misconfigured Kafka DLQ and Polly options fail fast at startup

- **Kafka dead-letter options are now validated at host start across every registration path.** Invalid DLQ options (e.g. `MaxDeliveryAttempts = 0`, an empty `TopicSuffix`) previously surfaced only at first use; a new validator wired with `ValidateOnStart()` now throws `OptionsValidationException` at startup.
- **Polly resilience options now validate on the convenience overload too.** `AddPollyResilience()` without an `IConfiguration` argument previously registered its options *without* their validators, so invalid timeout / graceful-degradation / distributed-circuit-breaker values were never caught. Validation now runs unconditionally; only configuration binding stays gated on a supplied `IConfiguration`.

### Architecture boundaries are now enforced in CI

- **The Dispatch-vs-Excalibur separation and banned-dependency boundary tests now fail the build on a violation.** They were report-only because the `ARCH_ENFORCE` gate was never set in CI; it is now enabled (89/89 green). This is a contributor-facing build-gate change with no runtime impact. As part of the same lane, the duplicate dead `IMessageChannelAdapter<TMessage>` in `Excalibur.Dispatch.Channels` was removed — the `Excalibur.Dispatch` (Abstractions) `IMessageChannelAdapter` is the single canonical interface (the removed variant had no implementations).

---

## June 2026 — Reliability Seam Tail (Sprint 841)

The P1 tail of the same "advertised-but-unwired" class Sprint 840 opened (governed by ADR-336): seven seams where the framework advertised a durability or compliance guarantee it did not actually honor. With this sweep the class is **closed** — no silent degrade remains on the ADR-336 surface. These are behavioral **corrections**; as a greenfield framework there are no consumer data migrations, but review the items below for behavior you may have relied on. The Dispatch (messaging) abstractions gain three small additive members; nothing is removed.

### Credential stores persist for real — and Vault is now opt-in

- **The HashiCorp Vault and AWS Secrets Manager credential stores now read and write the real backend.** Both were configuration-fallback placeholders that read plain `IConfiguration` and **silently discarded** every `StoreCredentialAsync` call while logging success. The Vault store now round-trips against the real KV v2 HTTP API; the AWS store persists through the AWS SDK (`IAmazonSecretsManager`). A store-then-get now returns the stored secret from the backend, and a backend failure surfaces as an error instead of a logged success.
- **Behavior change — the Vault store is no longer registered by default.** `AddSecureCredentialManagement` (and `AddDispatchSecurity`) register `EnvironmentVariableCredentialStore` as the default `ICredentialStore`, and only wire the HashiCorp Vault store when a `Vault:Url` is configured. Cloud credential stores live in their packages and are wired through their security builders: `services.AddDispatchSecurityAzure(azure => …)` (`Excalibur.Security.Azure`) and `services.AddDispatchSecurityAws(aws => aws.Region("us-east-1"))` (`Excalibur.Security.Aws`).
- **Security recommendation:** use `https` for any non-loopback `Vault:Url`. A plaintext `http://` Vault endpoint transmits your token and secrets in the clear.

### Outbox: a terminal dead-letter status

- **A retry-exhausted outbox message now reaches a terminal state and is never re-claimed.** Previously an exhausted message stayed `Failed`, was re-claimed by the delivery poller after its lease expired, and was re-delivered and re-dead-lettered indefinitely — duplicate delivery plus unbounded dead-letter-queue growth. Messages now transition to the new terminal **`OutboxStatus.DeadLettered`**, which every store's claim predicate structurally excludes (an explicit allow-list of claimable statuses), so the message can never be re-claimed. See [Outbox Pattern](./patterns/outbox.md).
- **For custom outbox stores:** the new optional `IDeadLetterableOutboxStore` capability (`MarkDeadLetteredAsync`) carries the terminal transition. All shipped stores implement it; a startup `ValidateOnStart` guard fails fast if a custom polling store omits it, naming the missing capability.

### Inbox: the at-most-once guard is now live

- **The inbox `Processing` status is now durably persisted before your handler runs.** It was previously set in memory only, so the at-most-once concurrency guard and the stuck-processing timeout had no durable state to act on — effectively dead code. A concurrent delivery of the same `(messageId, handlerType)` is now durably skipped, and a message left in-flight by a crash is reclaimed after the configured timeout. See [Idempotent Consumer](./patterns/idempotent-consumer.md). Custom inbox stores opt in via the new `IProcessingTrackingInboxStore` capability (`MarkProcessingAsync`).

### Elasticsearch inbox cleanup respects the cutoff

- **`ElasticsearchInboxStore.CleanupAsync(olderThan, …)` now deletes only documents older than the cutoff.** It previously issued a `MatchAll` query and deleted **every** inbox document regardless of age. Documents at or newer than `olderThan` are now retained (same correction shape as Sprint 840's audit-archival cutoff fix).

### Transactional outbox staging fails fast instead of degrading silently

- **Selecting `OutboxStagingStrategy.Transactional` without the infrastructure it requires now fails at startup.** Without a registered `ITransactionalOutboxWriter` and a transactional event store, the strategy silently degraded to non-atomic eventually-consistent staging — integration events could be lost on a crash between the event append and the outbox stage, with no diagnostic. A `ValidateOnStart` guard now throws at startup naming exactly what is missing. Only the **explicit** `Transactional` value trips the guard; `Auto` (which documents its own graceful fallback), `EventuallyConsistent`, and `Deferred` are unaffected.

### Projection hosts no longer silently drop a poison event

- **The continuous `AsyncProjectionProcessingHost` halts on a deserialize-poison event** instead of skipping it and advancing the checkpoint past it (silent read-model drift). An event that fails to deserialize, or deserializes to `null`, now stops processing at that position without advancing the checkpoint, so it is re-attempted on the next read (transient failures self-heal) — bringing the host in line with the Sprint 840 fix to `GlobalStreamProjectionHost`.
- **The one-shot `ProjectionRebuildService` fails the rebuild on a poison event** rather than skipping it. A deserialize/`null` or apply failure now rethrows and the rebuild ends in a `Failed` state with partial state not persisted; because a rebuild is one-shot there is no checkpoint and nothing is reprocessed — fix the cause and re-run the rebuild.
- **An *apply* failure in the continuous host is recorded, not halted — and the read model stays rebuildable.** In `AsyncProjectionProcessingHost`, where many projections share one checkpoint, a failure in one projection's apply is recorded per-projection (error + health state + observability); it does **not** halt the shared checkpoint, because halting it would force the projections that *succeeded* to re-apply the event. The full boundary is captured in ADR-336 Amendment 4. See [Projections](./event-sourcing/projections.md).

### SQL Server range queries execute on the real schema

- **`SqlServerRangeQueryEventStore.ReadRangeAsync` now queries the correct column.** It referenced a non-existent `GlobalPosition` column and threw a missing-column SQL error at runtime during parallel catch-up (masked by in-memory-only tests). It now reads the actual `Position` global-ordinal column and returns the events in range ordered by the global ordinal.

---

## June 2026 — Data-Loss & Compliance Sweep (Sprint 840)

A sweep of P0 correctness defects where the framework advertised a durability or compliance guarantee it did not actually honor. Each is governed by ADR-336 (the "advertised-but-unwired reliability seam" anti-pattern + engage-test gate). These are behavioral **corrections** on the Excalibur persistence side; the Dispatch (messaging) framework is unaffected. As a greenfield framework there are no consumer data migrations — review the items below for behavior you may have relied on.

### GDPR erasure now has a structural coverage gate

- **Erasure reports `Completed` only when every discovered personal-data location is covered.** Coverage is now a **three-state** model — *Covered* (crypto-shred key deleted, or a registered `IErasureContributor` for the store kind), *Exempt* (a declared, documented retention exemption), or *Uncovered* (a gap). An **uncovered** location forces `PartiallyCompleted` **even when nothing threw** — the framework will not claim success over a store it never erased.
- **`DataLocation.StoreKind`** (`Excalibur.Compliance.DataStoreKind`) and **`IErasureContributor.CoveredStoreKinds`** are new: contributors declare which store kinds they erase, and the gate routes each location to a covering mechanism. `DataStoreKind` is an extensible string-backed kind; the unclassified default is never coverable.
- **The audit/security store is `Exempt` by default** (GDPR Art.17(3)(b) + (e)), recorded explicitly on the certificate's `Exceptions` with its legal basis — never a silent skip. Override by registering an `IErasureContributor` for `DataStoreKind.Audit`.
- **Erasure verification is non-vacuous:** the certificate records the specific `Verification.DeletedKeyIds`, and `Verification.Verified` is `false` if claimed key deletions can't be confirmed or any location was left uncovered. See [GDPR Erasure > Coverage Model](./compliance/gdpr-erasure.md#erasure-coverage-model).

### Projection hosts no longer silently skip poison events

- **`GlobalStreamProjectionHost` halts on a poison event instead of skipping it.** An event that fails to deserialize, deserializes to `null`, or throws from `ApplyAsync` now halts the batch, marks the projection unhealthy, and **never advances the checkpoint past it** — so the event is reprocessed (transient failures self-heal) rather than silently dropped from the read model. Previously such events were logged, skipped, and the checkpoint advanced past them. See [GlobalStreamProjectionHost > Error Handling](./event-sourcing/global-stream-projection-host.md#error-handling).
- **Aggregate rehydration fails loud** on an undeserializable/`null` event rather than reconstructing a silently-incomplete (corrupt) aggregate — the source-of-truth aggregate is never silently partial.

### Saga SQL store enforces optimistic concurrency

- **Concurrent saves for the same saga raise `ConcurrencyException`** instead of last-writer-wins overwriting. `SagaState.Version` is the concurrency token; the store owns the increment (EF-style — you do no version arithmetic). Applies to both `SagaManager` and `SagaCoordinator`. See [Sagas > Optimistic Concurrency](./sagas/index.md#optimistic-concurrency).

### Encryption survives key rotation

- **Audit-log and ElasticSearch field encryption are rotation-safe.** The encrypting key version is stamped into the ciphertext envelope and decryption resolves the key by that stored version against a provider that **retains prior versions** — a field encrypted before a rotation stays decryptable after it. ElasticSearch key rotation no longer renders existing ciphertext unrecoverable.
- **Envelopes carry a format-version discriminator** distinct from the key version (`EncryptedFieldResult.FormatVersion` for ES; a byte-0 discriminator for the packed audit envelope), giving future envelope-schema changes a safe forward-migration path. An unknown format version is a surfaced error, never a best-effort parse.

### Audit persistence: no silent discard

- **`Security:Auditing:StoreType=SQL` now fails fast at startup** with a clear diagnostic. Excalibur.Security ships no SQL-backed `ISecurityEventStore`; the prior placeholder accepted then silently discarded every audit event. Use `Elasticsearch`, `File`, omit the setting for the in-memory development store, or register your own SQL audit store. (This is the `Excalibur.Security` auditing subsystem, distinct from the `Excalibur.AuditLogging` package and its `SqlServerAuditStore`.)
- **Audit archival is cutoff-bound:** `ArchiveAuditEventsAsync` / `DeleteArchivedEventsAsync` restrict both the archive read and the delete to events older than `cutoffDate`, and delete **only** documents confirmed written to the (flushed/closed) archive — a failed archive write no longer deletes events.

### Outbox job no longer disposes a shared singleton

- **The outbox job no longer disposes the injected singleton `IOutboxDispatcher`.** Two consecutive job fires both succeed; a per-run disposable scope is created via `IServiceScopeFactory` when needed (the *scope* is disposed, not the shared service) — eliminating an `ObjectDisposedException` on the second fire.

---

## June 2026 — Resilience Correctness (Sprint 839)

### Graceful degradation: windowed error rate

- **Error-rate auto-degradation is now measured over a sliding window** instead of process-lifetime
  totals. Previously the error rate used cumulative counters whose ever-growing denominator meant a
  recent burst of failures could no longer move the ratio in a long-running service, so error-rate
  auto-degradation effectively stopped firing after warm-up. It now uses a Polly v8-style
  rolling-health window. Two new `GracefulDegradationOptions` properties: `ErrorRateWindow`
  (`TimeSpan`, default 1m) and `ErrorRateWindowBuckets` (`int`, default 6). A new startup validator
  (`ValidateOnStart`) rejects a non-positive window/interval or a bucket count below 1. CPU and memory
  signals are unchanged. See [Polly Resilience > Graceful Degradation](./operations/resilience-polly.md#graceful-degradation).

### Distributed circuit breaker: Half-Open → Closed recovery

- **The distributed circuit breaker now auto-recovers from Half-Open to Closed.** After `BreakDuration`
  it admits a probe call; once `SuccessThresholdToClose` **consecutive** successes are recorded while
  Half-Open it transitions back to Closed (and resets on any intervening failure). Recovery is keyed
  off the breaker's own consecutive-success metric, so long-running services no longer get stuck
  Half-Open. See [Polly Resilience > Distributed Circuit Breaker](./operations/resilience-polly.md#distributed-circuit-breaker).

### Bulkhead: hard atomic queue bound

- **`BulkheadPolicy.MaxQueueLength` is now a hard, atomic admission bound.** Queue slots are reserved
  with an interlocked increment and a caller is rejected with `BulkheadRejectedException` the instant
  the count exceeds `MaxQueueLength`, so concurrent callers can no longer overshoot the limit via a
  stale check-then-act gate. `BulkheadMetrics.QueueLength` / `HasCapacity` are now accurate under
  contention. See [Polly Resilience > Bulkhead](./operations/resilience-polly.md#bulkhead).

### Keyed message handlers are now wired correctly

- **Keyed message handlers now work on every runtime.** Previously, keyed handlers registered via keyed
  DI (e.g. `AddKeyedScoped<IActionHandler<T>, H>("key")`) were **silently never wired** on .NET 9 / .NET 10
  — not discovered for dispatch, not lifetime-promoted — so they never executed and **no error was
  raised** (`ServiceDescriptor.ImplementationType` returns `null` for keyed descriptors on
  Microsoft.Extensions.DependencyInjection 9.x/10.x). On the older 8.x runtime the same code threw
  `InvalidOperationException`. `AddDispatch()`'s handler-lifetime analysis now reads the keyed service
  accessors, so keyed handlers are correctly discovered, dispatched, and promoted **with their service
  key preserved**. See
  [Dependency Injection > Keyed Services](./core-concepts/dependency-injection.md#keyed-services).

---

## May 2026 — Backlog Clear: Zero Open Issues (Sprint 837)

### SDK Type Leakage Removal

- **ES/OpenSearch index management models no longer expose SDK types** -- All public properties on `IndexConfiguration`, `IndexTemplateConfiguration`, `ComponentTemplateConfiguration`, `AliasDefinition`, and `AliasOperation` now use `JsonElement?` instead of Elastic/OpenSearch SDK types (`IndexSettings`, `TypeMapping`, `IAlias`, `QueryContainer`, `AliasAddAction`). Consumers serialize SDK objects to `JsonElement` before assigning — see XML docs on each property for examples.

```csharp
// Before (leaked SDK types):
var config = new IndexConfiguration { Settings = new IndexSettings() };

// After (ADR-329 compliant):
var config = new IndexConfiguration
{
    SettingsJson = JsonSerializer.SerializeToElement(new IndexSettings())
};
```

### Bug Fixes

- **SecurityEventLogger dispose race fixed** -- `ObjectDisposedException` during host shutdown resolved. Uses `volatile _disposed` + `IAsyncDisposable` pattern with correct disposal ordering (channel complete → cancel CTS → wait drain → dispose CTS).

### Test Infrastructure

- **GCP PubSub SDK fakes replaced with interface seams** -- Test files now mock `ISubscriberApiClientSeam` instead of concrete `SubscriberServiceApiClient`, preventing test breakage on GCP SDK updates.

**Sprint 837 achieves zero open Beads issues** — the second time in project history (first was S746). 46,644 tests pass across all CI shards.

---

## May 2026 — xUnit v3 Migration (Sprint 836)

### Test Infrastructure

- **xUnit 2.9→3.x migration complete** -- All test projects migrated from xUnit 2.9.3 to xUnit v3 (3.2.2) via big-bang central package swap. 185 files changed, zero shipping code modifications, ~61K+ tests pass across all CI shards. Key changes: `IAsyncLifetime` now uses `ValueTask`, `Verify.XunitV3` ecosystem swap, `OutputType=Exe` for test projects. Templates updated to `xunit.v3` with `Version="3.*"`.

---

## May 2026 — CodeAnalysis Upgrade (Sprint 835)

### Dependency Upgrade

- **Microsoft.CodeAnalysis 4.14→5.3** -- Central pin bumped for Common, CSharp, and Workspaces packages. Source generators remain at 4.14.0 for consumer SDK compatibility (VS 17.14/SDK 9.0.300). Benchmark VersionOverride workaround removed. Zero new diagnostics.

---

## May 2026 — Projection Enhancements (Sprint 834)

### WithSearchText — Automatic Computed Search Field

- **New `WithSearchText` on `IProjectionBuilder<T>`** -- Dual-delegate approach computes a denormalized search text field automatically whenever a projection is updated. AOT-safe with zero overhead when not configured. See [Projections > Automatic Search Text](./event-sourcing/projections.md#automatic-search-text).

```csharp
builder.AddProjection<OrderSummary>(p => p
    .Inline()
    .WithSearchText(
        proj => $"{proj.CustomerName} {proj.OrderNumber} {proj.Status}",
        (proj, text) => proj.SearchText = text)
    .When<OrderPlaced>((proj, e) => { proj.CustomerName = e.CustomerName; }));
```

### IVersionedProjectionStore — Optimistic Concurrency on Read Path

- **New ISP sub-interface `IVersionedProjectionStore<T>`** -- Enables read-modify-write patterns with version-based optimistic concurrency. Throws `ConcurrencyException` on version mismatch. See [Projections > Optimistic Concurrency](./event-sourcing/projections.md#optimistic-concurrency-iversionedprojectionstore).
- **New `VersionedProjection<T>` class** -- Wraps a projection with its `long` version number. Version starts at 1 and increments on each update.

---

## May 2026 — Saga P2 Cleanup (Sprint 833)

### Template Fix

- **`dotnet new excalibur-saga` now produces compiling code** -- The saga template was rewritten from deleted Model B types (`ISagaDefinition`, `ISagaStep`) to Model A (`SagaBase<T>`, `ISagaTimeout<T>`), matching the framework sample at `samples/04-reliability/SagaOrchestration/`.

### API Surface Reduction

- **3 interfaces internalized** -- `ISagaReminder`, `ISagaOutboxMediator`, and `ISagaStateMigrator<TFrom, TTo>` changed from `public` to `internal`. These are implementation details not intended for direct consumer use. Consumer access is through `ISagaBuilder` extensions (`.WithReminders()`, `.WithOutbox()`).
- **`IncludeSaga` health check property removed** -- The dead `DispatchHealthCheckOptions.IncludeSaga` property (referencing deleted `ISagaMonitoringService`) was removed from the public API.

### DI Improvements

- **InMemorySagaStore auto-registered** -- `AddExcaliburOrchestration()` now registers `InMemorySagaStore` as a fallback via `TryAddSingleton`, so sagas work out-of-the-box without a persistence provider for prototyping.
- **Static ConcurrentBag eliminated** -- `SagaRegistry` pending registrations moved from a static `ConcurrentBag` to an instance-scoped `SagaPendingRegistrations` class, preventing cross-test contamination.

### ValidateOnStart

- **`SagaTimeoutOptionsValidator`** -- Validates `PollInterval` (≥100ms), `BatchSize` (>0), `ShutdownTimeout` (>0).
- **`SagaReminderOptionsValidator`** -- Validates `DefaultDelay`, `MinimumDelay`, `MaximumDelay` ranges and cross-property constraints.

---

## May 2026 — Saga Model Unification + ISagaTimeout (Sprint 832)

### Saga Model Unification (ADR-333)

- **Model B deleted** -- Removed 32,608 lines of incomplete orchestration abstractions (`ISagaDefinition`, `ISagaOrchestrator`, `ISagaStateStore`, `ISagaStep`, `ISagaContext`, `ISagaRetryPolicy`, `StepResult`, `ISagaMonitoringService`, and all related types). These had zero concrete implementations and caused runtime DI resolution failures via `AddExcaliburAdvancedSagas()`.
- **Model A is the sole saga model** -- Event-driven choreography via `SagaBase<T>`, `ISagaCoordinator`, and `ISagaStore` with 9 provider implementations (SqlServer, Postgres, MongoDB, CosmosDb, DynamoDB, Firestore, InMemory, Telemetry decorator, TenantRouting).
- **DI consolidated** -- 17 registration surfaces reduced to a single `ISagaBuilder` golden path: `services.AddExcalibur(x => x.AddSagas(saga => saga.WithCoordination().WithTimeouts()))`.
- **`WithOrchestration()` renamed to `WithCoordination()`** -- Reflects that the saga model uses event-driven coordination, not step-based orchestration.

### ISagaTimeout&lt;T&gt; — Declarative Timeout Handling

- **New `ISagaTimeout<TMessage>` interface** -- Sagas implement this to declare strongly-typed timeout handlers. When a timeout fires, the framework routes directly to `HandleTimeoutAsync` instead of the general `HandleAsync`. Follows the NServiceBus `IHandleTimeouts<T>` pattern.
- **Contravariant type parameter** -- `ISagaTimeout<in TMessage>` supports polymorphic timeout matching.
- **Bounded reflection cache** -- `SagaCoordinator.TryInvokeTimeoutHandler` uses a capped cache (1,024 entries) for timeout handler resolution.
- A saga can implement multiple `ISagaTimeout<T>` interfaces for different timeout types.

### Sample Rewrite

- **SagaOrchestration sample** rewritten to use `SagaBase<OrderSagaState>`, `ISagaTimeout<PaymentTimeout>`, `AddExcaliburOrchestration()`, `SagaRegistry.Register`, and `[LoggerMessage]` source generation throughout.

---

## May 2026 — v1.0 Readiness + Proof-of-Life Validation

### Proof-of-Life Consumer App (Sprint 831)

- **Full-stack reference sample** -- `samples/11-real-world/ProofOfLife/` validates the complete consumer DX: message dispatching, domain aggregates, event sourcing, projections, and REST API endpoints — all using only public NuGet APIs.
- **ProjectionRebuildJob sample** -- Demonstrates Quartz-scheduled full projection rebuild via `IJobConfigurator.AddJob<ProjectionRebuildJob>(cron)` and `IMaterializedViewBuilder<T>`.
- **GlobalStreamProjectionHost sample** -- Demonstrates continuous global stream tailing with `IGlobalStreamProjection<TState>` and configurable `GlobalStreamProjectionOptions`.
- **ProjectionContext.Replay guard** -- `ArgumentOutOfRangeException` now thrown for negative `globalPosition` values, preventing silent acceptance of invalid replay positions.

### Consumer DX Improvements (Sprints 829–830)

- **Inline projection consistency guarantee** -- Inline projections run synchronously during `SaveAsync`, guaranteeing read-after-write consistency within the same request.
- **Event-sourced seed data pattern** -- Documented `IHostedService` recipe for seeding initial aggregates idempotently on application startup.
- **ES builder chain integration** -- `AddExcalibur(x => x.AddEventSourcing(es => es.UseInMemory()))` composition pattern documented with provider-specific extensions.

---

## May 2026 — CDC Resilience + Projection Flat Storage

### CDC Idempotency Filtering (Sprints 825–826)

- **Opt-in event deduplication** -- New `ICdcIdempotencyFilter` with two implementations: `InMemoryCdcIdempotencyFilter` (bounded 10K cache, single-instance) and `SqlServerCdcIdempotencyFilter` (persistent, multi-instance). Register via `UseInMemoryIdempotencyFilter()` or `UseSqlServerIdempotencyFilter()` on `ICdcBuilder`.
- **SQL Server persistent filter** -- Stores processed event keys in `[Cdc].[CdcProcessedEvents]` with composite PK `(TableName, Lsn, SeqVal)`. Configurable retention, batched cleanup, `IValidateOptions<T>` + `ValidateOnStart()`.
- See [CDC Idempotency Filtering](./patterns/cdc.md#idempotency-filtering) for full details.

### CDC Performance + Error Recovery (Sprints 824–826)

- **Batch checkpoint writes** -- Per-table instead of per-event, reducing I/O by up to 50× per poll cycle.
- **Adaptive polling** -- Skips delay when work was found for lower end-to-end latency. Exponential backoff on errors (capped at 5× polling interval) prevents tight retry storms.
- **SQL Error 313 recovery** -- CDC table-valued function boundary errors now trigger graceful stale position recovery instead of unhandled failures. New `TvfInsufficientArguments` reason code.
- **Point query optimization** -- Reverted `fn_cdc_get_all_changes` from range to point queries to prevent SQL execution timeouts on high-volume tables.
- **Log noise reduction** -- Per-row success logging demoted to `Debug`; batch summary remains at `Information`.

### Projection Store Flat Storage Refactor (Sprint 827)

- **ElasticSearch** -- Projections stored flat as the document root (no envelope wrapper). Custom repositories using `ElasticRepositoryBase<T>` can query the same index with natural field names.
- **Cosmos DB, DynamoDB, MongoDB** -- Framework metadata moved to a `_projection` nested object, keeping consumer properties at the document root for natural querying.

---

## April 2026 — Performance + Container Deployment + AOT Epic Complete

### DI Improvements

- **Startup prerequisite validators** -- Six subsystems (EventSourcing, Outbox, Inbox, Saga, LeaderElection, Persistence) now fail-fast at `IHost.StartAsync` with actionable error messages when a consumer calls `Add*()` without registering a concrete provider. No more cryptic failures at first use.
- **Non-keyed convenience aliases** -- All subsystem packages register non-keyed forwarding aliases to their keyed `"default"` singletons. Consumers can inject `IEventStore`, `IOutboxStore`, `ISagaStore`, `IInboxStore`, `ILeaderElection`, `ILeaderElectionFactory`, `ISnapshotStore`, `IOutboxStoreAdmin`, and `IPersistenceProvider` directly without `[FromKeyedServices("default")]`.
- **CDC SqlServer deferred DatabaseName** -- `BindConfiguration` now populates `DatabaseName` at DI resolution time, so `DatabaseName` no longer requires the fluent `.DatabaseName("X")` call when it is present in the configuration section.

### Performance Optimizations

- **Ultra-local dispatch: ~35 ns / 24 B** -- 1.28x faster than MediatR with 6.3x less memory
- **Zero-allocation handler internals** -- handler invocation (6.0 ns) and handler activation (24.4 ns) allocate 0 B
- **LightMode opt-in** -- `UseLightMode = true` disables correlation ID generation for maximum throughput
- **CI performance gate** -- MediatR parity threshold enforced on every PR, preventing performance regressions
- **5 auto-optimize experiment rounds** -- typeof optimization, cancellation skip, InitializeFast, hot-path reorder

### Container Deployment Guide (Sprint 761)

- **8-section consumer guide** -- Dockerfile recipes (JIT/ReadyToRun/AOT), Kubernetes health probes, GC tuning profiles, graceful shutdown, sidecar patterns, Azure Container Apps, and observability
- **Sample Dockerfiles** -- Production-ready Dockerfiles for getting-started, transport, and AOT samples with multi-stage builds and non-root execution
- **Kubernetes manifests** -- Sample deployment YAML with startup/readiness/liveness probes, resource limits, and drain timeout alignment
- **Health check verification** -- `MultiTransportHealthCheck` confirmed correct: reports Unhealthy before transports finish starting (correct for K8s readiness probes)

### AOT Epic Complete (Sprints 758-762)

- **150 of 170 packages AOT-compatible** -- all remaining 20 are blocked by external SDK dependencies, not Excalibur code
- **Phase B1 closed** (7/7 Tier 1 packages) -- FluentValidation dual-path with source-generated `IAotValidationDispatcher`
- **Phase B2 started** -- AzureServiceBus AOT via `MessageDeserializerRegistry` typed pattern (first Tier 2 conversion)
- **Tier 2 spikes complete** -- Kafka (Tier 3: Confluent SDK blocker), MessagePack (Tier 2b: partial), AWS ClaimCheck/Compliance (Tier 3: AWS SDK blocker)
- **994 suppressions audited** -- zero dishonest suppressions across all `IsAotCompatible=true` packages
- **CI suppression gate** -- 992-entry baseline blocks new unapproved suppressions; AOT binary smoke test verifies published binary runs
- **AOT benchmarks** -- BenchmarkDotNet baselines established comparing AOT vs JIT paths
- **Generator cleanup** -- 3 disabled generators archived, 2 active generators verified, consolidation evaluated and deferred (current architecture is optimal)
- **Both epics closed** -- AOT Microsoft-Quality Completeness + Container Deployment Guide, zero open backlog items

### API Unification Epic Complete (Sprints 763-769)

- **Canonical builder pattern** -- All 18+ SQL Server and Postgres subsystem packages unified to a single `subsystem.UseProvider(Action<IBuilder>)` entry point pattern
- **SQL Server: 4 canonical connection overloads** -- `ConnectionString`, `ConnectionFactory`, `ConnectionStringName`, `BindConfiguration` (Sprints 763-765)
- **Postgres: 5 canonical connection overloads** -- Same 4 plus `DataSource(NpgsqlDataSource)` for modern Npgsql pooling (Sprints 766-769)
- **9 Postgres builder interfaces** -- EventSourcing, Saga, Inbox, LeaderElection, Outbox, Data, CDC, Compliance, AuditLogging
- **All paths converge to NpgsqlDataSource** -- `ConnectionString` and `ConnectionStringName` create `NpgsqlDataSource` internally for proper pooling
- **Compliance unification** -- Erasure + DataInventory + LegalHold unified under single `IPostgresComplianceBuilder`
- **231 Postgres builder tests** across 4 sprints, 10/10 CI shards GREEN on every sprint
- **MongoDB: 4 canonical connection overloads** -- `ConnectionString`, `Client(IMongoClient)`, `ClientFactory`, `BindConfiguration` (Sprints 773-774)
- **7 MongoDB builder interfaces** -- EventSourcing, Saga, Inbox, LeaderElection, Outbox, Data, CDC
- **227 MongoDB builder tests** across 2 sprints
- **CosmosDb: 5 canonical connection overloads** -- `ConnectionString`, `Endpoint(+authKey)`, `Client(CosmosClient)`, `ClientFactory`, `BindConfiguration` (Sprint 775)
- **6 CosmosDb builder interfaces** -- EventSourcing, Saga, Inbox, Outbox, Data, CDC — 243 tests
- **Redis: 4 canonical connection overloads** -- `ConnectionString`, `ConnectionMultiplexer`, `MultiplexerFactory`, `BindConfiguration` (Sprint 776)
- **5 Redis builder interfaces** -- EventSourcing, Inbox, LeaderElection, Outbox, Data — 153 tests
- **Phase B complete** -- MongoDB + CosmosDb + Redis = 18 non-ADO.NET builders, 623 tests total
- **Old overloads deleted** -- greenfield policy, no `[Obsolete]` stubs
- **ValidateOnStart on every builder** -- catches missing connections at startup

### Dispatcher Bug Fix (Sprint 759)

- **Exception propagation fix** -- `DispatchAsync` no longer silently wraps handler exceptions in `MessageResult.Failed()`. Handler exceptions now propagate to callers as expected. 12 exception-swallowing catch blocks removed from the DirectLocal fast path.

---

## Previous Highlights

### Security

- **Asymmetric message signing** -- ECDSA P-256 via `CompositeMessageSigningService` for verifiable message integrity
- **PII-safe telemetry** -- `ITelemetrySanitizer` with SHA-256 hashing prevents sensitive data from leaking into traces and metrics
- **Message encryption** -- AES-256-GCM envelope encryption with pluggable key providers (Azure Key Vault, AWS KMS, HashiCorp Vault)

### Transports

- **Six transport providers** -- Kafka, RabbitMQ, Azure Service Bus, AWS SQS, Google Pub/Sub, and In-Memory
- **Microsoft-style transport API** -- `ITransportSender` (3 methods), `ITransportReceiver` (4 methods), `ITransportSubscriber` with decorator chain and builder pattern
- **Multi-transport routing** -- Route different message types to different brokers in the same application
- **Streaming pull** -- Google Pub/Sub streaming pull support for high-throughput scenarios

### Reliability

- **Outbox pattern** -- Reliable at-least-once delivery with SQL Server and PostgreSQL stores
- **Inbox pattern** -- Idempotent message processing with configurable deduplication windows
- **Dead letter queue** -- Universal DLQ support across all transports with configurable retry policies
- **Polly v8 resilience** -- Circuit breaker, retry, and timeout via `ResiliencePipeline` integration

### Observability

- **OpenTelemetry native** -- `ActivitySource` and `Meter` instrumentation across all packages
- **Health checks** -- Readiness and liveness probes for transports, event stores, and background services
- **Audit logging** -- SIEM integration with Datadog, Splunk, and Microsoft Sentinel exporters

### Event Sourcing

- **SQL Server and CosmosDB event stores** -- Production-ready persistence with optimistic concurrency
- **Snapshot strategies** -- Time-based, count-based, and hybrid snapshot policies with BFS version upgrading
- **Event upcasting** -- Schema evolution with type-safe event transformers
- **GDPR erasure** -- Crypto-shredding support via `IEventStoreErasure`

### API Quality

- **Interface Segregation** -- All public interfaces comply with the 5-method gate (94 interfaces decomposed across Sprints 743-745)
- **Options compliance** -- All Options types comply with the 10-property gate (69 types split with sub-options)
- **ValidateOnStart everywhere** -- All `Add*` DI registration methods validate options at startup, catching misconfigurations before the first request
- **Zero quality debt** -- Sprint 746 cleared every open issue (P0 through P3) for the first time in project history

### Native AOT

- **150 of 170 packages** are `IsAotCompatible=true` -- all remaining 20 packages are blocked solely by external SDK dependencies (Confluent.Kafka, AWS SDK, Google Cloud SDK, etc.), not by Excalibur code
- **Phase B1 complete** -- all 7 Tier 1 packages resolved: Saga, Caching, Security, AwsLambda, Compliance, gRPC, Protobuf, FluentValidation
- **AzureServiceBus AOT support** -- `MessageDeserializerRegistry` typed pattern replaces reflection-based deserialization; first Tier 2 conversion (Sprint 759)
- **FluentValidation AOT support** -- `AotFluentValidatorResolver` with source-generated `IAotValidationDispatcher` for compile-time type-switch validator dispatch (Sprint 758)
- **gRPC AOT support** -- `GrpcJsonSerializerContext` source-gen replaces reflection-based JSON serialization across all 10 transport types
- **Caching AOT support** -- `Excalibur.Dispatch.Caching` uses `CachePolicyRegistry` with the Explicit-Generic-DI pattern (zero `MakeGenericType` at runtime)
- **Saga AOT support** -- `Excalibur.Saga` uses source-gen registry population via `IPostConfigureOptions` pattern (zero `MakeGenericType` at runtime)
- **AOT sample app** -- Consumer-facing sample with Core Dispatch, EventSourcing, and Transport scenarios that publish and run with `dotnet publish -p:PublishAot=true`
- **AOT performance benchmarks** -- BenchmarkDotNet baselines: dispatch 3% faster, handler activation 3.87x faster, serialization 15-31% faster in AOT vs JIT paths (Sprint 759)
- **CI AOT enforcement** -- Suppression baseline gate (992 entries) blocks new unapproved suppressions; AOT binary smoke test verifies published binary runs (Sprint 758)
- **1,022+ IL suppressions audited** -- every suppression in Tier 1 packages classified as justified or removed
- **Dual-path architecture** -- `RuntimeFeature.IsDynamicCodeSupported` branching ensures JIT and AOT paths are both first-class

### Developer Experience

- **Roslyn analyzers** -- Compile-time checks for common Dispatch mistakes (DISP001-DISP004)
- **Source generators** -- AOT-compatible handler registration, serialization, and saga coordination
- **`dotnet new` templates** -- `excalibur-dispatch`, `excalibur-eventsourcing`, `excalibur-saga` project scaffolding
- **112,000+ automated tests** -- Unit, integration, conformance, and performance test suites across 10 CI shards

### Compliance

- **FedRAMP, SOC 2, HIPAA, GDPR** -- Compliance checklists with framework capability mapping
- **SBOM generation** -- Software Bill of Materials support for supply chain security
- **Key escrow** -- Regulatory key escrow with SQL Server persistence

---

## Pre-Release Versioning

During the alpha phase, each NuGet publish increments the alpha suffix (`3.0.0-alpha.1`, `3.0.0-alpha.2`, etc.). See [Versioning Strategy](./migration/version-upgrades.md) for the full release stage roadmap.

## Breaking Changes

Breaking changes during alpha are documented per-release. Before upgrading:

1. Review the release notes on [GitHub Releases](https://github.com/TrigintaFaces/Excalibur/releases)
2. Check `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` in affected packages
3. Run your test suite against the new version

## See Also

- [Versioning Strategy](./migration/version-upgrades.md) -- SemVer policy, deprecation rules, upgrade best practices
- [Getting Started](./getting-started/index.md) -- Install and build your first handler
- [Package Guide](./package-guide.md) -- Choose the right packages for your scenario
