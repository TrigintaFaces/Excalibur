# Changelog

All notable changes to Excalibur and Excalibur.Dispatch are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed

- **Sprint 844 — transient circuit-breaker dead-letter correctness (2 fixes)** -- An open circuit breaker is a **transient short-circuit, not a delivery failure**: the message never reached the handler/transport, so dead-lettering it (and, on the high-volume batch path, dead-lettering an entire batch) lost messages on a recoverable outage. Both processors now treat an open breaker as retryable. Each fix has a non-vacuous independent engage-test (RED on the pre-fix code):
  - **`InboxProcessor` retries on an open circuit breaker instead of dead-lettering (`Excalibur.Outbox` + inbox stores)** -- both the pre-dispatch check and a mid-dispatch `CircuitBreakerOpenException` now leave the inbox entry re-admittable for retry with its **attempt count unchanged** (no attempt consumed) and **without writing the deduplication store** (a dedup write would make the retry look like a false duplicate), rather than routing it to the DLQ. Only genuine `MaxAttempts`-exhausted failures dead-letter (`DeadLetterReason.MaxRetriesExceeded`). (bd-v9jq1a)
  - **`OutboxProcessor` leaves a record re-claimable on an open circuit breaker (`Excalibur.Outbox`)** -- the single-record (`DispatchReservedRecordAsync`) and high-volume batch paths now leave a circuit-breaker-open record re-claimable with its attempt count unchanged, so the next poll retries once the breaker recovers, instead of dead-lettering (which on the batch path caused bulk loss on a transient outage). Only `MaxAttempts`-exhausted failures dead-letter. (bd-2tvy5s)
- **Sprint 843 — concurrency-correctness cluster (3 fixes)** -- A targeted P1 sweep of lock-discipline and fail-open defects in the caching and threading primitives. Each fix is a thread-safety correctness change with a non-vacuous independent engage-test (RED on the pre-fix code); all three public types keep their existing signatures (`ICacheKeyBuilder.CreateKey`'s nullable return is noted under **Changed**):
  - **`LruCache<TKey,TValue>` no longer holds its lock across the value factory (`Excalibur.Dispatch.Caching`)** -- `GetOrAdd` invoked the user-supplied `valueFactory` while holding the cache lock, so a slow factory (a database read, HTTP call, or deserialization) serialized every other key's cache operation behind it, and a factory that re-entered the same cache could deadlock or corrupt LRU state. The factory now runs **outside** the lock — matching the `ConcurrentDictionary.GetOrAdd` contract (under concurrent misses for the same key the factory may run more than once, but exactly one entry is committed and every caller receives it) — followed by a lock re-acquire + double-check that returns the winning value if another thread committed first, with the winner inserted **inline** (never via a re-entrant `Set()`, closing the `bd-gblom` evict-and-discard defect). Public signature unchanged. (bd-xj7lfn)
  - **`KeyedLock` closes the keyed-semaphore cleanup race (`Excalibur.Dispatch`)** -- the per-key `SemaphoreSlim` could be removed and disposed while a concurrent waiter still referenced it (a waiter observing `ObjectDisposedException`, or two holders acquiring one key). Each key now carries a reference count mutated exclusively under the lock; the semaphore is removed and disposed only when the last reference is released, making "remove a key's semaphore while it is still referenced" structurally inexpressible. A follow-up made `LockHandle._disposed` `volatile` to satisfy the project's disposed-field memory-visibility conformance. Public signatures unchanged. (bd-ftq4u3, bd-6il0sf)
  - **`DefaultCacheKeyBuilder` fails open instead of fabricating a cache key (`Excalibur.Dispatch.Caching`)** -- when a cache key could not be derived — an `ICacheable<T>` action whose `GetCacheKey()` reflection failed, a runtime type with no resolvable name, or an unserializable action — the builder risked returning a fabricated/guessed key that could cause a false cross-request cache hit (one caller's data served to another). `CreateKey` now returns `null` ("do not cache") for any "cannot derive a key" condition — never throwing, never fabricating a substitute key — and the caching middleware skips the cache and invokes the handler directly. A cross-cutting cache must never break (or corrupt) the core operation. Skipped derivations are logged at `Debug`. See [Caching → Key derivation and fail-open behavior](docs-site/docs/performance/caching.md). (bd-5n1v5n, folds bd-393l2s)
- **Sprint 842 — silent-failure / false-guarantee P1 sweep (6 fixes, ADR-336 Wave 2)** -- Continues the ADR-336 class-extinction onto seams that advertised a durability/correctness guarantee but silently shipped *not-X* — a dropped event reported as success, an "atomic" contract that wasn't, a recovery path that never recovered. Each fix surfaces or structurally prevents the silent failure, with a non-vacuous independent engage-test (RED on the pre-fix code) or a structural contract change:
  - **`DefaultAuditLogger` is now fail-closed (`Excalibur.AuditLogging` / `Excalibur.Compliance.Abstractions`)** -- a store failure in `IAuditLogger.LogAsync` was caught and masked behind a success-shaped `AuditEventId` (the `SequenceNumber == -1` sentinel), so a dropped compliance event looked durably recorded. `LogAsync` now **throws the new `AuditPersistenceException`** on a store failure; a returned `AuditEventId` therefore always denotes a durably persisted event. Genuine cancellation (`OperationCanceledException`) is rethrown unwrapped. Callers that need fail-open availability must catch `AuditPersistenceException` and apply their own retry/queue policy. **Behavior change** — see [Audit Logging → Failure Handling](docs-site/docs/security/audit-logging.md). (bd-sf3uz1)
  - **Atomic claim-protocol for inbox idempotency (`Excalibur.Dispatch` + inbox/dedup stores)** -- `IdempotentHandlerMiddleware` used a racy check-then-act (`IsProcessedAsync`/`IsDuplicateAsync` then a later mark), so two concurrent duplicates could both pass the check. It now **claims atomically before the handler runs** via the new `IClaimableInboxStore`/`IClaimableDeduplicator` capability (Postgres `INSERT … ON CONFLICT DO NOTHING`, SqlServer `MERGE WITH (HOLDLOCK)`, in-memory `ConcurrentDictionary.TryAdd`) and **releases the claim on handler failure** so a redelivery is re-admitted (a claim-then-leave-terminal would silently drop a failed message). A startup `ValidateOnStart` guard fails fast if a registered store/deduplicator lacks the capability; store decorators forward it and fail loud (`NotSupportedException`) on a non-claimable inner. (bd-pux4gk)
  - **`OutboxMiddleware` documents an honest delivery contract (`Excalibur.Dispatch`)** -- the XML doc claimed transactional "Atomicity / guaranteed delivery". The default outbox dispatches **after** the business transaction commits — at-least-once with a crash window between commit and publish, not atomic. The contract now states this plainly and points to the Transactional outbox strategy for zero-loss semantics. No false guarantee is advertised. (bd-y3runp)
  - **`DistributedCircuitBreaker` manual-path cross-instance recovery (`Excalibur.Dispatch`)** -- the manual `RecordSuccessAsync` close-gate read a stale local `_lastKnownState` instead of the authoritative shared store, so a breaker opened on one instance never recovered via another instance's successes. The close-gate now reads `GetStateAsync` (the authoritative store); the `ExecuteAsync` fast path is preserved. (bd-0snskv, supersedes duplicate bd-jul532)
  - **Gap-tolerant cross-provider range paging (`Excalibur.EventSourcing.SqlServer` + `Excalibur.EventSourcing.Postgres`)** -- `ReadRangeAsync` stopped at the first empty batch (`break`), so a gap in the global position sequence silently truncated parallel catch-up reads. Both stores now advance `currentPosition = batchEnd + 1` past gaps, bounded by the caller's `toPosition` (no unbounded tail scan), verified by Docker SQL + Postgres integration engage-tests. (bd-778kpz)
  - **Postgres `GlobalPosition` is now populated (`Excalibur.EventSourcing.Postgres`)** -- `PostgresRangeQueryEventStore` returned `StoredEvent` rows with `GlobalPosition = 0` (the ctor default), breaking global-ordinal ordering and idempotency keys for consumers reading the Postgres global stream. It now selects and maps the real `global_position` column. (bd-gzph5a)
- **Sprint 841 — P1 tail of the "advertised-but-unwired" class (7 fixes, ADR-336)** -- Closes the P1 tail of the same reliability/compliance defect class S840 opened: the framework advertised a durability/compliance guarantee but silently shipped *not-X*. Each fix surfaces the failure path explicitly (throw / fail-fast / halt / terminal-status — never a swallowed failure logged as success) with a non-vacuous independent engage-test (RED on the pre-fix code) or structural enforcement that makes the unwired state inexpressible:
  - **HashiCorp Vault & AWS Secrets Manager credential stores now persist for real (`Excalibur.Security`, `Excalibur.Security.Aws`)** -- both stores were config-fallback placeholders that read plain `IConfiguration` and **silently discarded** every `StoreCredentialAsync` call while logging success. The Vault store now reads/writes the real KV v2 HTTP API via an injectable `IVaultSecretClient` seam; the AWS store persists through `IAmazonSecretsManager` (new `AWSSDK.SecretsManager` dependency, AWS package only). A `StoreCredentialAsync` followed by `GetCredentialAsync` now round-trips against the backend, and a backend failure is surfaced as an error (never logged-as-success). **Behavior change:** the HashiCorp Vault store is **no longer default-registered** — `AddSecureCredentialManagement` registers `EnvironmentVariableCredentialStore` as the default `ICredentialStore` and only wires the Vault store when `Vault:Url` is configured. (bd-ts66sh)
  - **Outbox terminal `DeadLettered` status (`Excalibur.Dispatch.Abstractions` + stores)** -- a retry-exhausted/dead-lettered outbox message had no terminal state: it stayed `Failed`, was re-claimed by the delivery poller after its lease expired, and was re-delivered and re-dead-lettered forever (duplicate delivery + unbounded DLQ growth). Messages now transition to the terminal `OutboxStatus.DeadLettered`, which every store's claim predicate structurally excludes (an explicit allow-list of claimable statuses), so a dead-lettered message can never be re-claimed. (bd-stlcgg)
  - **Inbox `Processing` status is now durably persisted (`Excalibur.Dispatch` inbox middleware + stores)** -- `InboxMiddleware` marked `Processing` in memory only, so the at-most-once concurrency guard and the stuck-processing timeout operated on state no second consumer could observe — dead code. The middleware now persists `InboxStatus.Processing` (via the new `IProcessingTrackingInboxStore` capability) before the handler runs, so a concurrent delivery of the same `(messageId, handlerType)` is durably skipped and the stuck-timeout can reclaim a crashed in-flight message. (bd-dziy0x)
  - **Elasticsearch inbox cleanup respects the cutoff (`Excalibur.Data.ElasticSearch`)** -- `ElasticsearchInboxStore.CleanupAsync(olderThan, …)` issued a `MatchAll` query and deleted **every** inbox document regardless of age (same shape as S840 `74s2he`). It is now date-bound: only documents strictly older than `olderThan` are deleted; documents at or newer than the cutoff are retained. (bd-6toaue)
  - **Transactional outbox staging fails fast when its infrastructure is missing (`Excalibur.EventSourcing`)** -- selecting `OutboxStagingStrategy.Transactional` without a registered `ITransactionalOutboxWriter` + transactional event store silently degraded to non-atomic eventually-consistent staging (integration events lost on a crash between append and stage, no diagnostic). A `ValidateOnStart` guard now throws at startup naming exactly what is missing. Only the **explicit** `Transactional` value trips the guard; `Auto` (documented graceful fallback), `EventuallyConsistent`, and `Deferred` are unaffected. (bd-us5tfv)
  - **Projection hosts no longer silently skip poison events (`Excalibur.EventSourcing`)** -- `AsyncProjectionProcessingHost` and `ProjectionRebuildService` skipped an undeserializable or `null`-deserializing event and advanced past it (silent read-model drift), unlike the S840-fixed `GlobalStreamProjectionHost`. Per ADR-336 Amendment 4: the **continuous `AsyncProjectionProcessingHost` halts on a deserialize-poison event** (stops without advancing the checkpoint, so the position is re-attempted on the next read); the **one-shot `ProjectionRebuildService` fails the rebuild** (rethrows to a `Failed` state, partial state not persisted — no checkpoint, nothing reprocessed). An **apply** failure is **recorded-not-halted in the shared-checkpoint `AsyncProjectionProcessingHost`** (per-projection error + health + observability; read model rebuildable) because halting the shared checkpoint would force the succeeded projections to re-apply the event. (bd-red2ha)
  - **`SqlServerRangeQueryEventStore` reads the correct column (`Excalibur.EventSourcing.SqlServer`)** -- `ReadRangeAsync` queried a non-existent `GlobalPosition` column and threw a missing-column SQL error at runtime on parallel catch-up (masked by in-memory-only tests). It now references the actual global-ordinal `Position` column and returns the events in `[fromPosition, toPosition]` ordered by the global ordinal, verified by a Docker SQL integration engage-test. (bd-ao97rb)
- **Sprint 840 — P0 data-loss & compliance sweep (12 fixes, ADR-336)** -- Eliminated a class of "advertised-but-unwired" bugs where the framework silently shipped not-X (a durability/compliance guarantee was advertised but a seam was unwired). Each fix is structurally enforced — the silent-loss path is now inexpressible at the seam, not merely tested — with a non-vacuous independent regression lock (RED on the pre-fix code):
  - **Audit persistence (`Excalibur.Security`)** -- `AddSecurityAuditing` now **fails fast at registration** when `StoreType=SQL` (no SQL-backed audit store ships in the package) instead of wiring a placeholder that silently discarded every audit event. (bd-kitw4i)
  - **Audit-log encryption after key rotation (`Excalibur.Security`)** -- decryption honors each record's stored `KeyVersion` instead of a hardcoded `1`, so data encrypted under an older key stays readable after rotation; the envelope gains a 1-byte format-version discriminator (distinct from the 4-byte key version). (bd-c1rscn)
  - **Audit archival cutoff (`Excalibur.Data.ElasticSearch`)** -- `ArchiveAuditEventsAsync`/`DeleteArchivedEventsAsync` are date-bound to `cutoffDate` (was `MatchAllQuery`, which archived and deleted *every* audit event regardless of cutoff); deletes only the archived ids, with flush-before-delete ordering so a write failure deletes nothing. (bd-74s2he)
  - **GDPR erasure coverage + verification (`Excalibur.Compliance`)** -- erasure uses a key-aware 3-state coverage gate (Covered/Exempt/Uncovered); a `Completed` certificate is unreachable while any data store is uncovered (was: only the event store was erased, yet the certificate could report success with outbox/inbox/projections/saga state untouched). Erasure verification now carries the deleted-key ids and is no longer vacuous. (bd-fot516, bd-412fo4)
  - **ElasticSearch field-encryption key rotation (`Excalibur.Data.ElasticSearch`)** -- rotation retains prior key versions and decrypts by the stamped version, so previously-encrypted data survives a rotation (was: permanently destroyed all prior ciphertext). (bd-nkmir4)
  - **Global-stream ordering + projection poison-halt (`Excalibur.EventSourcing`)** -- the global stream orders and pages by `GlobalPosition` (was per-aggregate `Version`, which skipped and duplicated projection events); the projection host halts on a poison (undeserializable/unappliable) event instead of advancing the checkpoint past it. (bd-wimxhb, bd-c3jdco)
  - **Aggregate rehydration (`Excalibur.EventSourcing`)** -- `GetByIdAsync` throws on an undeserializable/null event instead of silently skipping it and returning a corrupt source-of-truth aggregate. (bd-ze6pty)
  - **CDC checkpoint (`Excalibur.Cdc.SqlServer`)** -- the checkpoint no longer advances past a swallowed failed change when a later change to the same table succeeds. (bd-j3wzci)
  - **Saga optimistic concurrency (`Excalibur.Saga.SqlServer`)** -- the SQL saga store now *enforces* optimistic concurrency (store-owns-increment CAS that throws `ConcurrencyException` on a stale write) instead of incrementing a version it never checked. (bd-eszc06)
  - **Outbox job lifetime (`Excalibur.Jobs`)** -- `OutboxJob` no longer disposes its injected singleton `IOutboxDispatcher` on every Quartz fire (which caused use-after-dispose after the first cron run). (bd-gh8ov8)

### Added

- **`IInboxStoreAdmin.MarkFailedAsync(messageId, handlerType, errorMessage, retryCount, ct)`** -- New overload on the inbox admin interface (`Excalibur.Dispatch.Abstractions`, namespace `Excalibur.Dispatch`) that sets an entry's retry count **exactly** (no auto-increment), symmetric with `IOutboxStore.MarkFailedAsync(string, string, int, CancellationToken)`. Used by the retry processor for the transient short-circuit case — an open circuit breaker must leave a message re-admittable for retry *without* consuming an attempt — distinct from the core `IInboxStore.MarkFailedAsync(...)` which increments. Implemented across all 9 inbox stores. (Sprint 844, bd-v9jq1a)
- **`AuditPersistenceException`** -- New sealed exception (`: ApiException`) in `Excalibur.Compliance.Abstractions` (namespace `Excalibur.Compliance`), thrown by `IAuditLogger.LogAsync` when the audit store fails to durably persist an event. Carries an optional `EventId` of the unsaved event. Audit logging is now **fail-closed**: a store failure surfaces as this exception rather than being masked behind a success-shaped `AuditEventId`. (Sprint 842, bd-sf3uz1)
- **`IClaimableInboxStore`** -- New optional capability interface (2 methods: `TryClaimAsync(messageId, handlerType, ct)` returning first-writer-wins `bool`, and `ReleaseAsync(messageId, handlerType, ct)`) in `Excalibur.Dispatch.Abstractions`. A segregated capability (composition, not `IInboxStore` inheritance) implementing the atomic claim-before-execute idempotency protocol: claim atomically, finalize via `MarkProcessedAsync` on success, release on failure so a redelivery re-admits. All shipped inbox stores implement it; a `ValidateOnStart` guard fails fast if a registered store omits it. (Sprint 842, bd-pux4gk)
- **`IClaimableDeduplicator`** -- New optional capability interface (2 methods: `TryClaimAsync(messageId, expiry, ct)`, `ReleaseAsync(messageId, ct)`) in `Excalibur.Dispatch.Abstractions`. The in-memory analogue of `IClaimableInboxStore`; the successful claim doubles as the dedup marker. Implemented by the built-in `InMemoryDeduplicator`. (Sprint 842, bd-pux4gk)
- **`OutboxStatus.DeadLettered`** -- New terminal enum member (ordinal 5, append-only) in `Excalibur.Dispatch.Abstractions` marking an outbox message that has permanently failed after exhausting its retry policy and been routed to the dead-letter queue. Outbox store claim predicates exclude it structurally, so it is never re-claimed. (Sprint 841, bd-stlcgg)
- **`IDeadLetterableOutboxStore`** -- New optional capability interface (1 method: `MarkDeadLetteredAsync(messageId, reason, ct)`) in `Excalibur.Dispatch.Abstractions`. A segregated capability (composition, not `IOutboxStore` inheritance — keeps `IOutboxStore` within the ISP threshold) that durably transitions a retry-exhausted message to the terminal `OutboxStatus.DeadLettered`. All shipped Excalibur outbox stores implement it; a `ValidateOnStart` guard fails fast if a custom polling store omits it. (Sprint 841, bd-stlcgg)
- **`IProcessingTrackingInboxStore`** -- New optional capability interface (1 method: `MarkProcessingAsync(messageId, handlerType, ct)`) in `Excalibur.Dispatch.Abstractions`. A segregated capability (composition, not `IInboxStore` inheritance) that durably persists the in-flight `InboxStatus.Processing` status before a handler runs, making the inbox's at-most-once concurrency guard and stuck-processing timeout functional. (Sprint 841, bd-dziy0x)
- **ProjectionContext** -- New read-only record (`IsReplay`, `GlobalPosition`) passed to `When<TEvent>(Action<TProjection, TEvent, ProjectionContext>)` overload on `IProjectionBuilder<T>`. Enables projection handlers to distinguish live events from replay and access global stream position for idempotency. Factory methods: `ProjectionContext.Live` (singleton), `ProjectionContext.Replay(globalPosition)`.
- **WithSearchText** -- New `IProjectionBuilder<T>.WithSearchText(Func<TProjection, string>, Action<TProjection, string>)` method for automatic computed search field generation. AOT-safe dual-delegate approach — no reflection. Computed once per projection upsert. Zero overhead when not configured.
- **IVersionedProjectionStore\<T\>** -- ISP sub-interface of `IProjectionStore<T>` for optimistic concurrency via version tracking. Two methods: `GetVersionedAsync` (returns `VersionedProjection<T>?`) and `UpsertVersionedAsync` (throws `ConcurrencyException` on version mismatch). Numeric `long` version starting at 1, `null` expectedVersion for inserts.
- **VersionedProjection\<T\>** -- Sealed wrapper class containing `Projection` and `Version` properties for concurrency-aware projection reads.
- **CdcTableConfig** -- New bindable POCO in `Excalibur.Cdc` (`[Required] TableName`, optional `CaptureInstance`). `CdcTableTrackingOptions` now derives from it (1-level inheritance), keeping behavioral members (`EventMappings`/`Filter`/mapper delegates) on the derived type so `IConfiguration` only binds the slim POCO. A shared `CdcCaptureInstanceDeriver` derives `CaptureInstances[]` + `CaptureInstanceToTableNameMap` from `Tables` for both the builder and config-driven (`CdcJob`) paths.
- **Bidirectional cursor pagination** -- `CursorPagedResult<T>` gains `PreviousCursor` + `HasPrevious`; `ElasticSearchCursorHelper.ApplyCursorPaging<T>` over-fetches one "peek" row (`Size = pageSize + 1`) so `HasMore`/`HasPrevious` are correct even when a boundary page contains exactly `pageSize` items. `ToCursorResult` now emits both forward and backward cursors so consumers can offer First/Previous/Next/Last. See ADR-334.
- **`IDispatchAmbientScopeAccessor`** -- New abstraction (1 read-only member, `CurrentServiceProvider`) in `Excalibur.Dispatch.Abstractions` that lets a host surface the ambient DI scope so the singleton dispatcher resolves scoped handlers from it. `Excalibur.Dispatch.Hosting.AspNetCore` provides the implementation over `IHttpContextAccessor`, registered automatically by `WebApplicationBuilder.AddDispatch` and exposed for other composition roots via `services.AddDispatchAmbientScope()`. See ADR-335.
- **`Excalibur.Dispatch.AspNetCore` metapackage** -- New experience metapackage bundling `Excalibur.Dispatch` + `Excalibur.Dispatch.Hosting.AspNetCore` + `Excalibur.Dispatch.Observability`. A single `services.AddDispatchAspNetCore(...)` wires the dispatcher, OpenTelemetry instrumentation, and request-scope-aware handler resolution (`AddDispatchAmbientScope`) for the common web scenario.

### Changed

- **`ICacheKeyBuilder.CreateKey` now returns `string?` (`Excalibur.Dispatch.Caching`)** -- the return type changed from `string` to nullable `string?`. A `null` result is the documented "do not cache" signal: the caching middleware bypasses the cache and invokes the handler for that request. Implementations **must be infallible** — return `null` rather than throwing when a key cannot be derived. `DefaultCacheKeyBuilder` follows this contract. **Breaking change** for custom `ICacheKeyBuilder` implementations — update the signature to `string? CreateKey(IDispatchAction, IMessageContext)` and return `null` instead of throwing on a "cannot derive a key" condition. (Sprint 843, bd-5n1v5n)
- **Abstractions namespace alignment (Microsoft convention)** -- All 6 non-compliant Abstractions packages now drop `.Abstractions` from CLR namespaces: `Excalibur.Security.Abstractions` → `Excalibur.Security`, `Excalibur.Jobs.Abstractions` → `Excalibur.Jobs`, `Excalibur.A3.Abstractions` → `Excalibur.A3`, `Excalibur.EventSourcing.Abstractions` → `Excalibur.EventSourcing`, `Excalibur.Data.Abstractions` → `Excalibur.Data`, `Excalibur.Dispatch.Abstractions` → `Excalibur.Dispatch`. Assembly/package names unchanged. All 11 Abstractions packages now follow the Microsoft convention. **Breaking change** — consumers must update `using` directives.
- **Duplicate types removed** -- `RouteInfo`, `HealthCheckResult`, `CausationId`, `MessageVersionMetadata` removed from `Excalibur.Dispatch` (kept canonical versions from Abstractions package, now in shared `Excalibur.Dispatch` namespace).
- **IDE0005 dotnet-format mitigation** -- `.editorconfig` suppresses IDE0005 (remove unnecessary usings) to prevent known Roslyn bug that deletes `using var` disposal statements.
- **ES/OpenSearch SDK type leakage removed** -- `IndexConfiguration`, `AliasDefinition`, `AliasOperation` (ElasticSearch + OpenSearch), `IndexTemplateConfiguration`, `ComponentTemplateConfiguration` (OpenSearch) no longer expose SDK types (`IndexSettings`, `TypeMapping`, `Alias`, `QueryContainer`, `AliasAddAction`) on public boundaries. All replaced with `JsonElement?` per ADR-329. Internal managers deserialize at the implementation boundary. **Breaking change** for consumers directly referencing SDK-typed properties on these models.
- **GCP PubSub SDK fakes replaced with seams** -- `PubSubDeadLetterQueueManager` and `PubSubTransportReceiver` now use `ISubscriberApiClientSeam` internally instead of concrete `SubscriberServiceApiClient`. Public constructors unchanged. Internal seam enables testability without concrete SDK mocking.
- **Microsoft.CodeAnalysis 4.14→5.3** -- Central pin bumped from 4.14.0 to 5.3.0 (Common, CSharp, Workspaces, Analyzers). Source generators remain hard-pinned at 4.14.0 for consumer SDK compatibility (VS 17.14/SDK 9.0.300). Benchmark VersionOverride workaround removed.
- **Roslyn family pin completed** -- Added the missing central pin for the `Microsoft.CodeAnalysis.Scripting` meta package (5.3.0). The earlier 5.3.0 pin set pinned `Scripting.Common` but not the meta `Scripting` package, which `WolverineFx`→`JasperFx.RuntimeCompiler` pulls at 5.0.0 with an exact `Microsoft.CodeAnalysis.CSharp.Scripting [5.0.0]` dependency — colliding with the `>= 5.3.0` pin and breaking `Excalibur.Dispatch.Benchmarks` restore with NU1102 under locked mode (CI). Benchmarks lockfile regenerated.
- **xUnit 2.9→3.x** -- Test infrastructure migrated from xunit 2.9.3 to xunit.v3 3.2.2. IAsyncLifetime Task→ValueTask, Verify.Xunit→Verify.XunitV3, Xunit.SkippableFact replaced with v3 native Assert.SkipUnless/SkipWhen. Zero shipping code changes. Templates updated for v3.
- **Saga: Model B (orchestration) deleted** -- Per ADR-333, all Model B orchestration infrastructure removed. Only Model A (event-driven choreography) remains. `WithOrchestration()` renamed to `WithCoordination()`. **Breaking change** for consumers using `ISagaOrchestrator`, `ISagaStateStore`, `ISagaDefinition`, `ISagaStep`, `AddExcaliburAdvancedSagas()`, or any Model B types.
- **ISagaTimeout\<TMessage\>** -- New declarative timeout interface (1 method: `HandleTimeoutAsync`). Sagas implement `ISagaTimeout<T>` per timeout type. Coordinator dispatches timeouts before `HandleAsync` with bounded reflection cache.
- **Saga API surface reduction** -- `ISagaReminder`, `ISagaOutboxMediator`, `ISagaStateMigrator<TFrom,TTo>` internalized (7 PublicAPI.Shipped entries removed). These are framework implementation details; consumers use `ISagaBuilder` extensions.
- **Saga static state eliminated** -- Static `ConcurrentBag` pending registrations replaced with instance-scoped `SagaPendingRegistrations` to prevent test contamination.
- **excalibur-saga template rewritten** -- Template now uses Model A types (`SagaBase<T>`, `ISagaTimeout<T>`) instead of deleted Model B types.
- **InMemorySagaStore registered as default** -- `AddExcaliburSaga()` now registers `InMemorySagaStore` via `TryAddSingleton`, providing zero-config prototyping. Persistent stores override via `TryAdd` precedence.
- **DispatchHealthCheckOptions.IncludeSaga removed** -- Dead property referencing deleted `ISagaMonitoringService`. Health check string constant cleaned up.
- **CDC table config unified** -- `DatabaseOptions.CaptureInstances` (`string[]`) replaced with `Tables` (`Collection<CdcTableConfig>`). The config-driven Quartz `CdcJob` path (`Jobs:CdcJob:DatabaseConfigs[].Tables`) and the builder/background path now share a single table model, fixing a silent handler mismatch where the config path could not map a capture instance to its logical table name. `CdcJob` logs a fail-fast warning (`JobsEventId.CdcJobNoTablesConfigured = 147204`) when a database has no tables configured. Dead `CdcDefaultCaptureInstances` removed. **Breaking change** — consumers binding `CaptureInstances` must migrate to `Tables` (each entry needs an explicit `TableName`; `CaptureInstance` optional). See `docs/patterns/cdc.md` Option 2b.
- **`ElasticSearchCursorHelper.ToCursorResult` signature** -- third parameter changed from `bool reverseItems` to `Excalibur.EventSourcing.PageNavigation navigation`; the helper derives reverse internally and assigns forward/backward cursors from the displayed boundary items. **Breaking change** — callers pass the navigation direction and must size queries via `ApplyCursorPaging` (or `Size(pageSize + 1)`) instead of `Size(pageSize)`.
- **Third-party notices: deterministic + redistribution-only** -- `eng/ci/notices-generate.ps1` now excludes `PrivateAssets="all"` build-time-only references (analyzers, source generators, `MinVer`, `System.Collections.Immutable`) — they are not redistributed to consumers, so they no longer appear in `THIRD-PARTY-NOTICES.md`. Multi-version conflicts now resolve to the central `Directory.Packages.props` pin and output rows are sorted ordinally, fixing OS-dependent drift between CI (Linux) and local (Windows) — previously `Microsoft.CodeAnalysis.Common` flipped between its central pin (5.3.0) and the source generators' private 4.14.0 override.
- **Dependency bumps** -- `codecov/codecov-action` v6 → v7 (transport-conformance CI workflow); `Microsoft.NET.ILLink.Tasks` 10.0.8 → 10.0.9 (benchmarks lockfile).

### Fixed

- **Scoped handlers resolved from the root container (captive dependency)** -- Dispatching a handler registered `Scoped` (or a handler with a scoped dependency) through the context-less / ultra-local fast paths failed with `Cannot resolve scoped service '…' from root provider` (surfacing as a 500 / failed `IMessageResult` titled "Direct local dispatch failed"). The singleton `LocalMessageBus` now resolves such handlers from a DI scope — the ambient request scope when an `IDispatchAmbientScopeAccessor` supplies one (shared request-scoped state), otherwise a fresh `IServiceScopeFactory` scope disposed after the handler completes. The scope verdict is deterministic (registered lifetime + constructor inspection) and cached, so the root-resolvable hot path (transient/singleton handlers) is unchanged. Scoped handlers decline the no-context ultra-local fast paths and route exclusively through the context-aware dispatch path, so a context-bound dispatch shares the caller's request scope (`IMessageContext.RequestServices`) rather than resolving from a fresh scope. AOT-safe. See ADR-335.

### Security

- **Transitive CVE remediation** -- `MessagePack` 3.1.4 → 3.1.7 (CVE-2026-48109: LZ4 decompression denial-of-service). `SQLitePCLRaw` 2.1.11 → bundle 3.0.3 / `lib.e_sqlite3` 3.50.3 (CVE-2025-6965: bundled SQLite < 3.50.2), pulled transitively by `Microsoft.Data.Sqlite`; remediated via Central Package Management transitive pinning. Verified clean via `dotnet restore eng/ci/shards/ShippingOnly.slnf` and `dotnet list package --vulnerable`.

### Added

- **SagaTimeoutOptionsValidator** -- `IValidateOptions<SagaTimeoutOptions>` enforcing PollInterval ≥ 100ms, BatchSize ≥ 1, ShutdownTimeout > 0. Registered with ValidateOnStart.
- **SagaReminderOptionsValidator** -- `IValidateOptions<SagaReminderOptions>` enforcing delay ranges and cross-property constraints (MinimumDelay < MaximumDelay, DefaultDelay in range). Registered with ValidateOnStart.

### Fixed

- **Elasticsearch cursor pagination off-by-one (phantom next page)** -- `ElasticSearchCursorHelper.ToCursorResult` set `NextCursor` whenever `hits.Count >= pageSize`, so a final page of exactly `pageSize` items reported `HasMore = true` and the next `search_after` fetch returned an empty page (a blank "next" page in the UI). Boundary detection is now driven by the over-fetch peek row, so `HasMore` is `false` on an exactly-full last page. The cursor logic was extracted into a pure, store-agnostic `ResolveCursorBoundaries` core with 8 unit tests (previously untestable because `SearchResponse<T>` cannot be constructed in-process).
- **Jobs: `Disabled` flag ignored by CdcJob and DataProcessingJob** -- `CdcJob.ConfigureJob` and `DataProcessingJob.ConfigureJob` registered their job + trigger unconditionally, so `Jobs:*:Disabled: true` was silently ignored — only `OutboxJob` honored it. Both now apply the same schedule-time gate, so `Disabled: true` uniformly means the job's trigger is never registered with the scheduler. Added disabled→not-registered / enabled→registered coverage to `CdcJobShould`, `DataProcessingJobShould`, and `OutboxJobShould` (the reference impl was previously untested for the gate). **Caveat:** under a persistent Quartz job store the schedule-time gate does not remove an already-persisted job — use the runtime watcher (`AddJobWatcher<TJob, TOptions>`, which pauses via the scheduler) or delete the job. See the Jobs guide (`docs-site/docs/patterns/jobs.md`).
- **Outbox dispatcher/processor only available with A3** -- `IOutboxDispatcher` (`MessageOutbox`) and `IOutboxProcessor` (`OutboxProcessor`) were never registered by the outbox subsystem. The only `IOutboxDispatcher` registration in the framework was A3/Audit's fail-fast `DefaultOutboxDispatcher` stub, so `OutboxJob`, `OutboxBackgroundService`, and audited dispatch could not resolve a real dispatcher unless A3 audit was added. (The registrations were dropped when the implementations moved from `Excalibur.Dispatch` to `Excalibur.Outbox` and never restored.) `AddExcaliburOutbox`/`AddOutbox` now registers both: `OutboxProcessor` as **Transient** (per-instance `Init(dispatcherId)` state — each background partition and dispatcher needs its own) and `MessageOutbox` as **Singleton**. The outbox registration removes A3's fail-fast `DefaultOutboxDispatcher` stub (identified by type) before `TryAdd`ing `MessageOutbox`, so the real dispatcher wins regardless of whether audit or the outbox is composed first, while a consumer-supplied `IOutboxDispatcher` still takes precedence. Added registration + composition-order regression tests.
- **Jobs documentation: built-in job registrations** -- `docs-site` jobs guide now shows the service registration each built-in job requires (`AddSqlServerCdcJob` for `CdcJob`, `AddOutbox(...)` for `OutboxJob`, `AddDataProcessing(...)` for `DataProcessingJob`) instead of only `ConfigureJob` (which merely schedules). Also corrected the examples to pass the root `IConfiguration` to `ConfigureJob`, the two-argument `ConfigureHealthChecks(IHealthChecksBuilder, IConfiguration)` signature, the `OutboxJobOptions` type name, and the `Jobs:OutboxJob` section name.
- **JobWorkerSample no longer schedules unwired jobs** -- The deployment job-host sample scheduled `CdcJob`/`OutboxJob`/`DataProcessingJob` without registering their dependencies (they would fail to activate at trigger time). Since the sample is intentionally database-free (in-memory Quartz store + coordination focus), those jobs are no longer scheduled; the exact registration each requires is documented inline, pointing to `CdcJobQuartz` for a complete runnable CdcJob worker.
- **Elasticsearch/OpenSearch index names lowercased across all composition sites** -- Beyond the projection store, several places composed `{consumerPrefix}-…` index names without lowercasing and would hit the same `invalid_index_name_exception` with an uppercase prefix (e.g. an environment segment): `EventualConsistencyTracker`, `ProjectionRebuildManager`, `SchemaEvolutionHandler`, the Elasticsearch/OpenSearch dead-letter handlers, and the Elasticsearch/OpenSearch audit exporters/sinks. Introduced a shared internal `IndexNameNormalizer` in the Data.ElasticSearch/Data.OpenSearch packages and lowercased the audit-sink prefixes. (Outbox/Inbox use the consumer's raw `IndexName` directly, not a composed name, so they are unchanged.)
- **Elasticsearch/OpenSearch projection index names are fully lowercased** -- The index-name convention lowercased only the projection type name, not the consumer-supplied `IndexPrefix`/`IndexName`. An uppercase segment (e.g. an environment-derived `Development`) produced names like `co-transactions-transaction-Development`, which Elasticsearch/OpenSearch reject with a 400 `invalid_index_name_exception` ("must be lowercase") — surfacing as an inline-projection failure during `SaveAsync`. `ElasticSearchProjectionIndexConvention.GetIndexName` and `OpenSearchProjectionStore.GetIndexName` now lowercase the entire composed name (prefix included). No consumer change required.
- **Inline/async projections resolve scoped stores from a scope, not the root provider** -- `InlineProjectionProcessor` (singleton, invoked from `SaveAsync` via `EventNotificationBroker`) and `AsyncProjectionProcessingHost` (singleton `BackgroundService`) passed their captured **root** `IServiceProvider` to each projection's apply delegate, which resolves the **scoped** `IProjectionStore<T>`. Under DI scope validation (the default in the Development host, and the path Quartz jobs exercise) this threw `AggregateException` → *"Cannot resolve scoped service 'IProjectionStore`1[…]' from root provider."* Both now resolve each projection in a freshly created `IServiceScopeFactory` scope (also isolating scoped state across concurrently-applied projections). Notification handlers in `EventNotificationBroker` are likewise resolved from a scope. Latent since inline projections shipped; surfaces whenever projection stores are scoped (SQL Server/Mongo/Elasticsearch/etc.) and scope validation is enabled. Added a scoped-store-under-validation regression test.
- **SQL Server CDC builder registers the CdcJob factory** -- `AddCdcProcessor(cdc => cdc.UseSqlServer(...))` now also registers `IDataChangeEventProcessorFactory`, so configuring SQL Server CDC makes `CdcJob` resolvable without a separate call. The focused `AddSqlServerCdcJob(IConfiguration)` entry point remains for job-only workers that don't set up the full CDC processing builder.
- **CdcJob processor factory never registered** -- `CdcJob` depends on `IDataChangeEventProcessorFactory`, but no DI extension registered it — Quartz activation failed with *"Unable to resolve service for type 'IDataChangeEventProcessorFactory'"*. Added a single feature-registration entry point `services.AddSqlServerCdcJob(IConfiguration)` (in `Excalibur.Jobs.Cdc`, namespace `Microsoft.Extensions.DependencyInjection`) that binds `CdcJobOptions` from `Jobs:CdcJob` and `TryAdd`s the processor factory plus its SQL Server data-access policy factory. Updated the `CdcJobQuartz` sample (its `AddCdcProcessor()` call never registered the factory despite the comment) and added an `ActivatorUtilities` regression test.
- **CdcJob Quartz activation crash (ambiguous constructor)** -- `CdcJob` declares two public 5-parameter constructors (a `Func<string, SqlConnection>` variant and an `IConfiguration` variant). Quartz's `MicrosoftDependencyInjectionJobFactory` activates jobs via `ActivatorUtilities`, which throws *"Multiple constructors accepting all given argument types"* when both are DI-satisfiable. Marked the `IConfiguration` constructor with `[ActivatorUtilitiesConstructor]` so container activation deterministically selects it (`IConfiguration` is always host-registered, so activation needs no `Func<string, SqlConnection>` registration). Added `ActivatorUtilities` regression tests.
- **NU1608 Roslyn version conflict (benchmarks build)** -- WolverineFx → JasperFx.RuntimeCompiler transitively pulled the `Microsoft.CodeAnalysis` meta-package plus the CSharp.Scripting/Scripting.Common/VisualBasic/VisualBasic.Workspaces satellites at 5.0.0, whose exact-match (`= 5.0.0`) dependencies conflicted with the 5.3.0 Common/CSharp/Workspaces pins. Under `-warnaserror` the NU1608 warnings were fatal. Pinned the whole Roslyn family to 5.3.0 in `Directory.Packages.props` so it stays in lockstep.
- **SecurityEventLogger dispose race** -- `Dispose()` no longer races with `StopAsync()`. Added `volatile _disposed` guard, `IAsyncDisposable` implementation, and cancel-before-dispose sequencing to prevent `ObjectDisposedException` during hosted service shutdown.
- **AddExcaliburAdvancedSagas DI trap** -- Method registered middleware requiring unregistered services. Fixed by deleting Model B entirely (ADR-333).
- **SagaOrchestration sample** -- Rewritten from procedural steps to event-driven choreography using framework types (`SagaBase<T>`, `ISagaTimeout<T>`).
- **ProjectionContext.Replay guard** -- `ProjectionContext.Replay(globalPosition)` now throws `ArgumentOutOfRangeException` for negative values, preventing invalid replay state.
- **ExistsAsync extension method** -- `IProjectionStore<T>.ExistsAsync(id, ct)` checks projection existence without full deserialization. Providers implement `IExistsProjectionStore<T>` escape hatch for optimized paths (e.g., SQL `SELECT TOP 1 1`, CosmosDB `HEAD`); fallback uses `GetByIdAsync` + null check.
- **DistinctValuesAsync extension method** -- `IProjectionStore<T>.DistinctValuesAsync(propertyName, filters, ct)` returns distinct property values for filter dropdown faceting. Providers implement `IDistinctValuesProjectionStore<T>` for native queries (e.g., SQL `DISTINCT`, MongoDB `distinct()`); fallback uses reflection.
- **AddProjection&lt;TProjection, TConfig&gt;()** -- Explicit generic registration for `IProjectionConfiguration<T>` implementations. AOT-safe alternative to `AddProjectionsFromAssembly()` assembly scanning.
- **SqlServer MaterializedViewStore** -- `UseMaterializedViewStore()` builder extension on `ISqlServerEventSourcingBuilder` registers `IMaterializedViewStore` backed by SQL Server. Features `EnsureSchemaAsync()` for idempotent DDL creation, `UPDLOCK,ROWLOCK` position tracking, and configurable table names. Default tables: `MaterializedViews` + `MaterializedViewPositions`.
- **Provider-specific QueryPagedAsync/QueryCursorAsync** -- Single-roundtrip pagination overrides via ISP sub-interfaces: `IPageableProjectionStore<T>` (SqlServer `COUNT(*) OVER()`, CosmosDB/MongoDB parallel count) and `ICursorProjectionStore<T>` (DynamoDB `ExclusiveStartKey`/`LastEvaluatedKey` with opaque cursor tokens). Eliminates N+1 roundtrips for paged/cursor queries.
- **AddElasticSearchProjectionStore&lt;T&gt;()** -- Builder chain extension on `IEventSourcingBuilder` for single-projection ES store registration. Two overloads: options-based and URI-based. Bridges to existing `IServiceCollection` extensions.
- **IIndexMappingConvention** -- Pluggable ES index mapping conventions. Single-method ISP interface (`ConfigureMappings`) with `DefaultIndexMappingConvention` singleton pass-through. Configurable via `ElasticSearchProjectionStoreOptions.IndexMappingConvention`.
- **AOT-safe serialization options** -- Consumer-provided `JsonSerializerOptions` property on CosmosDB and DynamoDB projection store options for AOT-safe serialization via source-gen `JsonSerializerContext`. Consolidated 15+ scattered IL2026/IL3050 suppressions to file-level pragmas. Added `[DynamicallyAccessedMembers]` on TProjection for CosmosDB, DynamoDB, and ElasticSearch stores.

- **Flat projection storage across all document-store backends** -- Removed the `data: { ... }` envelope wrapper from MongoDB, CosmosDB, and DynamoDB projection stores. Projection fields now live at the document root alongside lightweight `_projection` metadata (id, type, updatedAt). ElasticSearch was flattened previously; all four backends now share the same flat storage pattern. Consumer query repositories using `ElasticRepositoryBase<T>` should remove `data.` field path prefixes. **Breaking change** for consumers with custom queries against the old `data.*` field paths or deserializing the envelope `MongoDbProjectionDocument`/`CosmosDbProjectionDocument` types.
- **MongoDbRepositoryBase\<T\>** -- Base class for custom MongoDB query repositories sharing projection collections, matching the existing `ElasticRepositoryBase<T>` pattern. Includes `IMongoDbRepositoryBase<T>` (CRUD) and `IMongoDbRepositoryBaseQuery<T>` (query) ISP interfaces, plus `MongoDbProjectionCollectionConvention` for consistent collection naming.
- **CosmosDbRepositoryBase\<T\>** -- Base class for custom CosmosDB query repositories sharing projection containers. Includes `ICosmosDbRepositoryBase<T>` (CRUD) and `ICosmosDbRepositoryBaseQuery<T>` (SQL query) ISP interfaces, plus `CosmosDbProjectionContainerConvention` for consistent container naming.
- **DynamoDbRepositoryBase\<T\>** -- Base class for custom DynamoDB query repositories sharing projection tables. Includes `IDynamoDbRepositoryBase<T>` (CRUD) and `IDynamoDbRepositoryBaseQuery<T>` (scan) ISP interfaces, plus `DynamoDbProjectionTableConvention` for consistent table naming.

### Fixed

- **MongoDB regex injection in projection queries** -- `BuildContainsFilter` now uses `Regex.Escape()` before constructing `BsonRegularExpression`, preventing regex metacharacters in filter values from being interpreted as patterns.
- **CosmosDB double-parse in GetByIdAsync** -- Changed from `ReadItemAsync<JsonElement>` + `GetRawText()` + `JsonNode.Parse()` to `ReadItemStreamAsync` + `JsonNode.ParseAsync(stream)` for single-parse deserialization.
- **DynamoDB partition key collision** -- `DynamoDbProjectionStore` now preserves the original partition key value in `_projection.origPk` metadata when a projection property name collides with the configurable partition key name. Restored transparently on read.
- **AOT suppression audit false positives** -- `Invoke-AotSuppressionAudit.ps1` now uses fingerprint-based matching (file + warningId + justification) instead of line numbers. Line shifts from code edits no longer trigger false NEW/STALE pairs.

- **SqlServerCdcIdempotencyFilter (Sprint 826)** -- Persistent CDC event deduplication using `[Cdc].[CdcProcessedEvents]` table with composite primary key (TableName, Lsn, SeqVal). Supports configurable retention with batched cleanup via `SqlServerCdcIdempotencyFilterOptions` (schema, table name, retention period, cleanup batch size). Registered via `UseSqlServerIdempotencyFilter()` builder extension on `ICdcBuilder`. Includes `IValidateOptions<T>` validator with `ValidateOnStart()`. Complements the `InMemoryCdcIdempotencyFilter` added in Sprint 825 for single-instance scenarios.
- **ICdcIdempotencyFilter abstraction (Sprint 825)** -- Internal interface for CDC event deduplication with `IsProcessedAsync` and `MarkProcessedAsync`. Default `InMemoryCdcIdempotencyFilter` uses bounded `ConcurrentDictionary` (10K cap, skip-when-full). Opt-in via `UseInMemoryIdempotencyFilter()` on `ICdcBuilder`. Integrated into `CdcChangeApplier` — checks before handler dispatch, marks after success.
- **CDC idempotency documentation (Sprint 826)** -- New docs-site content covering idempotency filter overview (why at-least-once needs dedup), InMemory vs SqlServer filter comparison, DI registration examples, and retention/cleanup guidance. Added to `docs/patterns/cdc.md` and `docs/operations/cdc-troubleshooting.md`.

### Fixed

- **CDC SQL Error 313 stale LSN recovery (Sprint 825)** -- SQL Error 313 ("insufficient arguments") thrown by CDC table-valued functions when LSN falls outside the valid range (e.g., after CDC cleanup jobs) now triggers graceful stale position recovery. Dual-layer defense: (1) defensive pre-check in `CdcChangeDetector.EnqueueTableChangesAsync` validates lastLsn against `fn_cdc_get_min_lsn` per capture instance and resets checkpoint proactively; (2) error code 313 added to `CdcStalePositionDetector.StalePositionErrorNumbers` as safety-net catch filter. New `StalePositionReasonCodes.TvfInsufficientArguments` reason code for diagnostics.
- **CDC adaptive polling error backoff (Sprint 826)** -- `CdcProcessingHostedService` now distinguishes no-work cycles (normal delay) from error cycles (exponential backoff). Consecutive errors increment a backoff multiplier capped at 5× `PollingInterval`, reset to 1× on first successful cycle. Prevents tight error-retry loops under sustained failure conditions.
- **CDC SQL timeout from range queries** -- Reverted `fn_cdc_get_all_changes` from range query `(@fromLsn, @maxLsn)` to point query `(@lsn, @lsn)`. The TVF materializes ALL rows in the `[fromLsn, toLsn]` range before `TOP`/`WHERE` filtering, causing execution timeouts on high-volume tables with large checkpoint gaps. Point queries bound the TVF scan to a single LSN. The outer loop in `ProducerLoopCoreAsync` handles LSN-by-LSN advancement.
- **CDC per-row log noise** -- Demoted `DataChangeEventProcessor.LogChangeEventProcessed` from `Information` to `Debug`. Per-row success logging flooded consumer logs with hundreds of identical lines per poll cycle. The batch summary at `CdcChangeApplier.LogCompletedProcessing` already provides operator-level Information totals.

### Changed

- **CDC performance optimization (Sprint 824)** -- Batch checkpoint writes per-table instead of per-event, adaptive polling skips delay when work found, `CdcDefaultConsumerBatchSize` increased from 10 to 50, pre-computed column filter and shared `DataTypes` dictionary in `CdcRepository.FetchChangesAsync`, cached Polly policy per batch. `ICdcRepository.FetchChangesAsync` now accepts `fromLsn` + `toLsn` range parameters (callers should pass `fromLsn == toLsn` for point queries). `CdcRow.DataTypes` changed from `Dictionary<string, Type>` to `IReadOnlyDictionary<string, Type>`. **Breaking change** for consumers calling `FetchChangesAsync` directly or accessing `CdcRow.DataTypes` as mutable.

### Fixed

- **CDC batch checkpoint data loss** -- Fixed critical bug where `onFatalError`-swallowed exceptions allowed later same-table events to advance the checkpoint past the failed event, permanently skipping it. The table is now removed from checkpoint tracking on failure, ensuring the failed event is reprocessed on the next cycle.

### Changed

- **ServerlessHostOptions ISP split** -- Removed nested `AwsLambda`, `AzureFunctions`, `GoogleCloudFunctions` properties from `ServerlessHostOptions`. Per-platform options now registered independently via `IOptions<AwsLambdaOptions>`, `IOptions<AzureFunctionsOptions>`, `IOptions<GoogleCloudFunctionsOptions>` when calling `AddAwsLambdaHosting()`/`AddAzureFunctionsHosting()`/`AddGoogleCloudFunctionsHosting()`. `ServerlessHostOptions` retains only 6 shared cross-cutting properties. **Breaking change** for consumers accessing nested platform properties.
- **DI naming convention doc fix** -- Removed stale "Known Violations" table and `[Obsolete]` references from `docs/architecture/di-naming-convention.md` (S822 ORACLE F6 closure).

### Added

- **NServiceBus feature-parity evaluation** -- Comprehensive 10-dimension comparison at `management/research/nservicebus-feature-parity-evaluation.md`. Result: parity or superiority across all dimensions. 1 MEDIUM gap (saga timeouts) tracked as bd-k4urle.

### Added

- **MinimalWiring bridge conformance tests** -- A2/A3/A4 bridge shapes (ElasticSearchProjections, DataProcessing, CDC) with bucket classification and isolation/idempotence gates.
- **Security namespace-vs-folder policy doc** -- `docs/architecture/security-namespace-policy.md` documenting when Excalibur.Security folders get sub-namespaces.
- **Builder method naming convention doc** -- `docs/architecture/builder-pattern-convention.md` canonical 4-method connection pattern (ConnectionString, ConnectionStringName, ConnectionFactory, BindConfiguration).
- **Public helper audit** -- `management/audits/s822-public-helper-audit.md` evaluating A2/A3/A4 retained-public class helpers against Required Public API Checklist.

### Changed

- **A3 DI three-pillar naming** -- `AddDispatchAuthorization` → `AddExcaliburAuthorization`, `AddDispatchAdvancedSagas` → `AddExcaliburAdvancedSagas`, `AddDispatchOrchestration` → `AddExcaliburOrchestration`, `AddDispatchHealthChecks` → `AddExcaliburHealthChecks`. Direct renames, no `[Obsolete]` shims. **Breaking change** for consumers calling old method names.
- **Elastic IndexTemplate SDK-type hide** -- `IndexTemplateConfiguration.Template` (`IndexSettings`) and `Mappings` (`TypeMapping`) replaced with opaque `SettingsJson` and `MappingsJson` (`JsonElement?`). Same for `ComponentTemplateConfiguration`. SDK types confined to `Internal/` adapter layer. **Breaking change** for consumers directly setting `Template`/`Mappings` properties.

### Added

- **SmartEnum\<T\> DDD building block** -- Type-safe enumeration base class in `Excalibur.Domain.Model` with `FromId()`, `FromName()`, `TryFromId()`, `TryFromName()`, `GetAll()`. Supports case-insensitive name lookup, equality by ID, and error messages listing valid values. Replaces raw enums for constrained value sets (OrderStatus, PaymentMethod, etc.).
- **CDC DI forwarding registrations** -- All 7 CDC providers now register forwarding DI entries so consumers can resolve processors via base interfaces (`ICdcProcessor<T>`, `ICdcStreamProcessor<T,TPos>`) in addition to provider-specific marker interfaces.
- **SDK seam interfaces** -- `IStorageClientSeam` (GCP), `IServiceBusSenderSeam`/`IServiceBusReceiverSeam`/`IServiceBusProcessorSeam` (Azure ServiceBus), `IPublisherClientSeam`/`ISubscriberClientSeam` (GCP PubSub), `IArmClientSeam` (Azure ARM). Internal adapter pattern replaces concrete SDK fakes in tests with proper seams. SDK governance fakes reduced from 11 to 4.
- **DataProcessing assembly scanners** -- `AddProcessorsFromAssembly` and `AddRecordHandlersFromAssembly` on `IDataProcessingBuilder`. AOT-annotated; explicit registration alternatives available.

### Changed

- **ExcaliburHeaderNames + Cultures moved** -- Moved from `Excalibur.Domain` to `Excalibur.Application`. These are HTTP infrastructure concerns, not domain model types. Consumer `using` statements must update. A3 consumers use type aliases to avoid namespace collision with `ApplicationContext`.
- **Swashbuckle 6→10 migration** -- `SwaggerGenOptionsExtensions` updated for Microsoft.OpenApi v2 API (`OpenApiSchema` constructor changes). Package reference updated in `Directory.Packages.props`.
- **CdcJobQuartz sample composition** -- Consolidated 3 separate registration calls (1×`AddDispatch` + 2×`AddExcalibur`) into single `AddExcalibur` root with `ScanAssemblies()`, `AddJobs()`, `AddEventSourcing()` chained.

- **CDC ISP two-tier hierarchy** -- New `ICdcProcessor<TEvent>` (batch, 1 method) and `ICdcStreamProcessor<TEvent, TPosition>` (streaming, 3 methods) base interfaces in `Excalibur.Cdc`. All 7 CDC providers (CosmosDB, MongoDB, Postgres, DynamoDB, Firestore, SqlServer, InMemory) converted to marker interfaces inheriting the appropriate base. Compile-time safety: injecting a poll-only provider where streaming is required now fails at compile time. **Breaking change** -- provider interfaces no longer declare methods directly; consumers must code against the base interfaces or the provider marker.
- **DelegatingPersistenceProvider** -- Abstract decorator base class following Microsoft `DelegatingHandler` pattern. All methods virtual, forwarding to `Inner`. Paired with `PersistenceProviderBuilder` (sealed, `ChatClientBuilder` pattern) for fluent `Use()` + `Build()` composition.
- **IRepository\<TEntity, TKey\>** -- Non-event-sourced CRUD repository abstraction in `Excalibur.Domain`: `GetByIdAsync`, `SaveAsync` (upsert), `DeleteAsync`. Distinct from `IEventSourcedRepository<T,TKey>`.
- **DataProcessing assembly scanners** -- `AddProcessorsFromAssembly` and `AddRecordHandlersFromAssembly` extension methods on `IDataProcessingBuilder`. AOT-annotated with `[RequiresUnreferencedCode]`; explicit `AddProcessor<T>` / `AddRecordHandler<THandler,TRecord>` available as AOT-safe alternatives.

### Changed

- **Snapshot.Data byte\[\] → ReadOnlyMemory\<byte\>** -- `ISnapshot.Data` and `Snapshot.Data` changed from `byte[]` to `ReadOnlyMemory<byte>` for improved immutability and zero-copy slicing. `Snapshot.Create()` factory still accepts `byte[]` via implicit conversion. All 8 snapshot store implementations updated. **Breaking change** for custom `ISnapshot` implementations.
- **Serverless host provider cleanup** -- AWS Lambda, Azure Functions, and Google Cloud Functions host providers now consistently emit `LogLevel.Warning` stubs for telemetry options without platform SDK integration. Dead stub methods (`ConfigureXRayTracing`, `ConfigureLambdaMetrics`, `ConfigureGoogleCloudTracing`, `ConfigureGoogleCloudMetrics`) removed.
- **LeaderElectionOptionsValidator sealed** -- Changed from `public class` to `public sealed class`.
- **CDC method renames** -- `ProcessCdcChangesAsync` → `ProcessBatchAsync` (SqlServer), `ProcessChangesAsync` → `ProcessBatchAsync` (InMemory) for unified contract consistency.

### Removed

- **SqlServer-specific ICdcProcessor deleted** -- Replaced by `ISqlServerCdcProcessor` marker interface extending the new generic `ICdcProcessor<T>`.
- **Duplicate CDC provider method declarations** -- 200+ lines of duplicated interface method declarations across 6 CDC providers eliminated by inheritance from base interfaces.

### Removed

- **Authorization RequestProvider layer deleted** -- 36 legacy `RequestProvider` files removed from `Excalibur.Data.SqlServer` and `Excalibur.Data.Postgres`. SQL is now inlined directly into Store implementations (`SqlServerGrantStore`, `SqlServerActivityGroupStore`, `PostgresGrantStore`, `PostgresActivityGroupStore`). The Store pattern (`IGrantStore`, `IActivityGroupStore`) was already the public contract; RequestProviders were never DI-registered or consumer-accessible. ~98 `PublicAPI.Shipped.txt` entries removed. No functional changes.

### Changed

- **MongoDB.Driver 2.x → 3.x migration** -- Upgraded `MongoDB.Driver` from 2.30.0 to 3.8.0 across all 8 shipping MongoDB packages (`Excalibur.Data.MongoDB`, `Excalibur.EventSourcing.MongoDB`, `Excalibur.Cdc.MongoDB`, `Excalibur.Saga.MongoDB`, `Excalibur.Inbox.MongoDB`, `Excalibur.Outbox.MongoDB`, `Excalibur.LeaderElection.MongoDB`, `Excalibur.Compliance`). Key migration changes: `_ownsClient` pattern for `MongoClient` `IDisposable` lifecycle tracking; sync `Indexes.CreateOne()` → async `CreateOneAsync()` in leader election; `Cluster.Description.Servers`/`WireVersionRange` health check → `buildInfo`/`serverStatus` commands. `MongoDbComplianceStore` gains `IDisposable`. **Breaking change** for consumers who subclass sealed `MongoClient`/`MongoDatabase`/`MongoCollection<T>` (unlikely — use interfaces for mocking).

- **DataProcessing: cursor-based paging replaces offset-based paging** -- `IRecordFetcher<T>.FetchBatchAsync` now accepts `string? cursor` (opaque token) instead of `long skip`, returning `CursorFetchResult<TRecord>` with the next cursor. `IDataProcessor.RunAsync` accepts a `string? processedCursor` for crash-safe resume. Dual-cursor tracking separates transient fetch position from durable processed checkpoint. SQL schema adds `FetchCursor`/`ProcessedCursor` columns with `COALESCE` preservation. **Breaking change** — all `DataProcessor<T>` implementations must update their `FetchBatchAsync` override signature.

- **IErasureService ISP split** -- `ExecuteAsync` removed from the public `IErasureService` interface (now 4 methods). Execution is handled internally by `ErasureSchedulerBackgroundService` via new `internal IErasureExecutor`. Consumers submit requests via `RequestErasureAsync` and monitor via `GetStatusAsync`. **Breaking change** if calling `IErasureService.ExecuteAsync` directly (use the background scheduler instead).
- **ISystemLoadMonitor CancellationToken** -- `GetCurrentLoadAsync()` now requires a `CancellationToken` parameter per .NET convention. **Breaking change** for `ISystemLoadMonitor` implementors.

### Fixed

- **DataProcessorDiscovery AOT split (P0)** -- `TryGetRecordType` split into AOT-safe (attribute-only) and `TryGetRecordTypeWithReflection` (fallback). Assembly-scanning DI path uses reflection; all other paths are AOT-compatible. `[RequiresUnreferencedCode]` scoped to reflection-only path.
- **HandlerInvokerRegistry ValueTask support (P1)** -- `CreateInvoker` now handles `ValueTask` and `ValueTask<T>` return types. `TargetInvocationException` unwrapped via `ExceptionDispatchInfo` to preserve stack traces.
- **StaticPipelineGenerator CS0122 (P1)** -- Source generator no longer casts to internal `Dispatcher` class; uses `IDispatcher` interface instead. Namespace filter prevents interceptor recursion.
- **HashiCorpVault DI double-registration (P1)** -- Changed to singleton forwarding pattern: concrete type registered once, both `ICredentialStore` and `IWritableCredentialStore` forwarded to same instance.
- **DataProcessorRegistry DI mismatch (P1)** -- All 4 `AddDataProcessor` overloads + assembly-scanning path now register both concrete type and `IDataProcessor` interface.
- **DefaultOutboxDispatcher sentinel (P2)** -- `GetPendingMessagesAsync` returns `Enumerable.Empty` instead of throwing when no real outbox is configured. Write operations still throw as fail-fast.
- **10 flaky test fixes** -- Timing thresholds increased (500ms→2000ms CTS, 5s→30s background services), `OperationName`-filtered activity assertions, async delegate fixes, per-test Kafka topic isolation, async disposal for ES adapter.
- **ContextFlowMetrics null safety (P0)** -- 13 counter/histogram fields in `ContextFlowMetrics` used `null!` initialization. Added null-conditional operators (`?.`) to prevent `NullReferenceException` if meter instrument creation fails.
- **MongoDbTenantEventStoreResolver MongoClient leak** -- Cached tenant event stores held undisposed `MongoClient` instances (leaking connection pools since MongoDB.Driver 3.x makes `MongoClient` `IDisposable`). Resolver now implements `IAsyncDisposable` with proper `_clientCache` tracking and ordered disposal.

### Documentation

- **CDC SqlServer XML doc improvements** -- Added XML documentation to `ICdcRepository`, `IDatabaseOptions`, and `DataChangeEventProcessor` in `Excalibur.Cdc.SqlServer`. No behavioral changes.

### Security

- **Snappier 1.3.0 → 1.3.1** ([GHSA-pggp-6c3x-2xmx](https://github.com/advisories/GHSA-pggp-6c3x-2xmx)) -- Infinite-loop vulnerability in `SnappyStream` decompression; 15 bytes of malformed framed-format data can freeze a thread. Transitive dependency via MemoryPack affecting 55 packages. Resolved by bumping `Directory.Packages.props`.

### Fixed

- **DataProcessing: ProcessedCursor never persisted** -- The consumer loop passed `null` for `processedCursor` on every checkpoint, so `COALESCE` in SQL always preserved the existing `NULL` value. Introduced internal `PagedRecord` struct that tags the last record per producer page with the page cursor; consumer now persists the cursor at page-boundary checkpoints, enabling correct crash-recovery resume.
- **DataProcessing: DDL CompletedCount INT → BIGINT** -- Column type mismatched the `long` in C# (`DataTaskRequest.CompletedCount`), risking overflow at ~2.1B records. Fixed in docs-site DDL and sample setup script.
- **DataProcessing: invalid filtered index in DDL** -- `WHERE [Attempts] < [MaxAttempts]` uses column-to-column comparison, which SQL Server filtered indexes do not support. Replaced with a covering index keyed on `[CreatedAt]` (the polling query's ORDER BY column).
- **DataProcessing: IAsyncDisposable record disposal** -- Consumer now prefers `IAsyncDisposable.DisposeAsync()` over `IDisposable.Dispose()` for record cleanup, consistent with framework-wide async disposal pattern.
- **Money value object STJ deserialization** -- `Money` had two parameterized constructors with no `[JsonConstructor]`, causing `NotSupportedException` during System.Text.Json deserialization (e.g., in ElasticSearch projections). Added private `[JsonConstructor]` constructor. Also added defensive `[JsonConstructor]` to `Address` to prevent the same issue if a second constructor is ever added.
- **SqlServerIdentityMapStore.CreateConnection() infinite recursion** -- `_connectionFactory?.Invoke() ?? CreateConnection()` called itself when no explicit connection factory was registered (i.e., `ConnectionString()` or `BindConfiguration()` paths), causing `StackOverflowException` on every database operation. Fixed to fall back to `new SqlConnection(_options.ConnectionString)`.
- **Pre-publish audit: 18 runtime bug fixes across 11 packages**
  - **DI forwarding registration** -- `DataProcessingBuilder.AddProcessor<T>()` now registers concrete type so `DataProcessorRegistry` can resolve processors by concrete type (fixes `InvalidOperationException: No service for type`)
  - **SecurityEventLogger hard-cast** -- replaced unsafe `(SecurityEventLogger)sp.GetRequiredService<ISecurityEventLogger>()` with forwarding pattern (fixes `InvalidCastException` when consumers provide custom `ISecurityEventLogger`)
  - **Idempotent DI registrations** -- converted `AddSingleton`/`AddScoped` → `TryAddSingleton`/`TryAddScoped` across Security, Observability, GooglePubSub, Compliance, and Serverless packages to prevent duplicate registrations on repeated calls; `ICredentialStore` uses concrete-type guard instead (multi-registration interface where multiple stores coexist)
  - **Serializer double-dispose** -- added `_disposed` guard to `DispatchJsonSerializer`, `CompositeAotJsonSerializer`, and `AotJsonSerializer` (`ThreadLocal<T>.Dispose()` throws `ObjectDisposedException` on double-dispose)
  - **CreateScope → CreateAsyncScope** -- `ColdStartOptimizerBase` and new `DispatchTestHarness.CreateAsyncScope()` (missed in prior sweep)
  - **IAsyncDisposable** -- added to `MultiRegionKeyProvider` (replaces spin-wait), `LongPollingOptimizer`, `StreamHealthMonitor` (fixes disposal race), `CloudMonitoringExporter` (added `_disposed` guard)
  - **Load balancer thread-safety** -- `WeightedRoundRobinLoadBalancer` counters now use `Interlocked.Increment`; both load balancers add `volatile` to snapshot fields for correct double-checked locking
  - **CachingMiddleware** -- explicit null check on `DeserializeCachedValue` return (was `null!`); swallowed `ICachePolicy` exceptions now logged via `LogWarning`
- **CreateScope → CreateAsyncScope** across 12 framework services -- `DataProcessingHostedService`, `DataProcessor<T>`, `SagaTimeoutDeliveryService`, `QuartzJobAdapter`, `OutboxProcessor`, `InboxProcessor`, `PoisonMessageHandler`, `SnapshotCreationJob`, `ProjectionRebuildJob`, `OutboxProcessorJob`, ElasticSearch/OpenSearch `HostExtensions`, and `JitAccessExpiryService` now use `CreateAsyncScope()` to correctly dispose services implementing `IAsyncDisposable` (fixes `InvalidOperationException` when processors inherit from `DataProcessor<T>`)
- **dependency-review-action@v5 → v4** -- CI security workflow referenced non-existent action version
- **Nullability test fixes** -- `DataProcessingBuilderShould` CS8764 (`DbConnection.ConnectionString` override) and `EphemeralProjectionEngineExtendedShould` CS8620 (FakeItEasy `Returns` type inference)
- **Docusaurus MDX v3 parse errors** -- 4 docs files using `{#custom-id}` heading syntax converted to `<div id="..." />` anchors
- **23 npm vulnerabilities resolved** -- upgraded Docusaurus 3.9→3.10 (added `@docusaurus/faster`), overrode `minimatch@10.2.5` and `serialize-javascript@7.0.5`

### Changed

- **Versioning: GitVersion → MinVer migration** -- Package versioning now uses [MinVer](https://github.com/adamralph/minver) 6.0.0 (Polly pattern) instead of GitVersion. Versions are computed from git tags (`v3.0.0-alpha.N`); commits after a tag auto-increment the pre-release identifier. Local dev defaults to `3.0.0-alpha.0`. Release workflow updated to pass `MinVerVersionOverride` for `workflow_dispatch` builds. `GitVersion.yml` removed. SourceGenerators project carries an explicit MinVer reference (opts out of CPM).
- **Release workflow hardened** -- `release.yml` build step now passes `MinVerVersionOverride` to ensure correct version in both build and pack phases; removed redundant `AssemblyVersion`/`FileVersion`/`InformationalVersion` overrides from `dotnet pack` (MinVer sets all four version properties during build)

### Added

- **`ICdcBuilder.BindProcessingConfiguration(string sectionPath)`** -- allows binding `CdcProcessingOptions` to an `IConfiguration` section (e.g., `appsettings.json`) via the CDC builder fluent API
- **`WithProjectionHealthChecks()`** -- opt-in projection health check registration (previously auto-registered by `UseEventNotification()`)
- **`IProjectionRebuildService.GetStatusAsync<TProjection>()`** -- type-safe per-projection rebuild status query
- **`IProjectionRebuildService.GetAllStatusesAsync()`** -- bulk rebuild status monitoring
- **`PersistencePrerequisiteValidator`** + **`InboxPrerequisiteValidator`** -- fail-loud-at-host-start probes for missing persistence/inbox provider registrations
- **Non-keyed DI forwarding aliases** across 6 subsystems (EventSourcing, LeaderElection, Outbox, Saga, Inbox, Persistence) -- consumers can inject stores directly without `[FromKeyedServices]`

### Changed

- **`ProjectionRebuildService`** narrowed from `public sealed` to `internal sealed` — consumers use `IProjectionRebuildService` interface via DI
- **Projection health checks** are now opt-in via `WithProjectionHealthChecks()` instead of auto-registered — reduces overhead for consumers who don't need health monitoring

- **AddDispatchInstrumentation()** unified OTel entry point -- registers all 18 meters + 26 ActivitySources in one call, with auto-wire via `AddDispatchPipeline()`
- **Excalibur.Dispatch.Analyzers** package with 6 diagnostic rules (DISP101-DISP106): DI namespace enforcement, extension class naming, CancellationToken interface conventions, namespace segment validation, ConfigureAwait enforcement, blocking call detection
- **Templates CI workflow** validating all 8 `dotnet new` templates produce buildable projects
- **DocFX API reference workflow** for automated API documentation generation
- **Coverage threshold enforcement** -- quality gates now fail (not just report) below 65% combined coverage
- **ADR-326**: dependency-update commit-hygiene policy (patch grouping / one-per-commit minor / rationale-required major) -- governs all subsequent dep-bump sprints
- **ADR-142 §D7.1**: canonical `Store` / `Provider` / `Manager` / `Operations` domain-role suffix taxonomy formalized with selection rule (naming-test then shape-test) and the S799-S802 14-seam precedent table
- **PrerequisiteValidators** (4): `EventSourcingPrerequisiteValidator`, `LeaderElectionPrerequisiteValidator`, `OutboxPrerequisiteValidator`, `SagaPrerequisiteValidator` -- `internal sealed IHostedService` probes that fail loud at host start if the subsystem's required abstraction is missing from the container (actionable error message names subsystem, missing type, and provider registration path)
- **MinimalWiringConformanceTestKit.IgnoredDescriptorPredicates** hook for upstream-SDK non-idempotence scenarios
- **XUnit `CollectionDefinition`** on `Excalibur.Saga.Tests.StateMachine.*` to serialize shared-state tests (fixes under-parallel-load flakiness)
- **Windows AOT publish prerequisites** section in `docs/architecture/aot-compatibility.md` -- documents MSVC Build Tools + Windows 11 SDK requirement
- **`CursorEncoder`** (in `Excalibur.EventSourcing.Abstractions`): typed cursor serialization primitive for cursor-based pagination — encode/decode strongly-typed position tokens with tamper-evident HMAC option. Base-64Url wire format; stable across processes.
- **`ElasticIndexMappingBuilder`** + **`IElasticIndexConfiguration`** (in `Excalibur.Data.ElasticSearch`): fluent builder for ES index mappings with per-field type/analyzer/subfield configuration; decouples projection definitions from raw Elastic SDK mapping DSL.
- **`ElasticSearchCursorHelper`** (in `Excalibur.Data.ElasticSearch`): opinionated cursor helper for ES-backed paginated queries; pairs with `CursorEncoder` for end-to-end cursor pagination.
- **`docs-site/docs/data-access/pagination.md`**: consumer guide for cursor-vs-offset pagination, including ES-specific recipes.
- **`AsyncProjectionProcessingHost`** -- background hosted service for continuous projection processing with cursor tracking, batch processing, and graceful shutdown
- **`SqlServerGlobalStreamQuery`** -- SQL Server implementation for global stream projection queries
- **`docs-site/docs/data-access/data-request.md`**: consumer guide for IDataRequest usage patterns
- **Typed dispatch** -- `IDispatcher.DispatchAsync<TResponse>(IDispatchAction<TResponse>)` overloads that infer `TResponse` from the action parameter type, eliminating explicit dual type arguments at the call site. Includes context-free, explicit-context, and `DispatchChildAsync` variants. Backed by `TypedDispatchDelegateCache` for zero-alloc hot-path dispatch.
- **`DispatchActionExtensionGenerator`** source generator -- emits per-action strongly-typed extension methods when `EnableTypedDispatchExtensions()` is opted in via `DispatchBuilder`.
- **`HandlerRegistrySourceGenerator`** -- source-generated `AddDiscoveredHandlers()` extension for fully AOT-safe handler registration. Zero reflection, replaces `HandlerRegistryBootstrapper` and `HandlerRegistryExtensions`.

### Fixed

- **AOT pre-warm guard**: skip reflection-based `HandlerActivator`/`HandlerInvoker` cache pre-warm when `RuntimeFeature.IsDynamicCodeSupported` is `false`; prevents `PlatformNotSupportedException` in native AOT deployments
- **Flaky CI**: `ErasureSchedulerBackgroundServiceShould.Continue_after_processing_error` timeout increased from 5s to 10s to match peer background-service tests under full-suite CI load

### Changed

- **Projection system rework**: `EventNotificationBroker` enhanced with reflection caching and improved observability; `ProjectionRebuildService` batch rebuild support added; `IProjectionBuilder` simplified; `InMemoryProjectionRegistry` and `InMemoryCursorMapStore` hardened
- **CDC SqlServer decomposition**: monolithic `CdcProcessor` decomposed into focused collaborators (`CdcChangeDetector`, `CdcChangeApplier`, `CdcCheckpointManager`, `CdcRepository`). `DataChangeEvent`/`DataChangeExtensions` hardened, `CdcRecoveryOptions` validation added, dead `DatabaseOptions`/`IDatabaseOptions` removed. PublicAPI baselines updated.
- **DataProcessing quality hardening**: `DataProcessor`/`DataOrchestrationManager` hardened with structured logging, `CancellationToken` propagation, disposal guards. Added `DataProcessingHealthCheck` + `DataProcessingHealthState` health-check infrastructure. Exception types improved with serialization support. Dapper SQL requests updated.
- **AOT suppression baseline refreshed** after source-generator, handler, CDC, and data-processing infrastructure changes
- **.NET 10 dependency bump**: `Microsoft.Extensions.*` and `System.*` packages updated from 10.0.6 → 10.0.7

### Removed

- **Dead projection code removed**: `CursorPageRequest` (relocated to cursor pagination), `DirtyCheckingMode`, `IMultiStreamProjectionBuilder`, `MultiStreamProjectionBuilder` -- superseded by simplified projection builder API
- **5 dead source generators** deleted: `HandlerActivationGenerator`, `HandlerInvocationGenerator`, `MessageFactorySourceGenerator`, `MessageTypeRegistrySourceGenerator`, `ZeroAllocationHandlerInvokerGenerator` — all were unused/superseded by `HandlerRegistrySourceGenerator` and `HandlerInvokerSourceGenerator`
- **Handler infrastructure simplified** (-1,626 lines): extracted `HandlerActivatorRegistry` and `ResultFactoryRegistry` with thread-safe public APIs for AOT source-gen integration; `HandlerInvoker`/`HandlerActivator` internals consolidated

### Changed

- **Money value object: ISO 4217 currency separation** (`bd-j8q8e` P1-3). `Money` constructor now accepts `string currencyCode` (ISO 4217 — "USD", "EUR", "GBP") as the primary identifier. Previous `cultureName` parameter is removed — culture is a display concern handled by `ToString(CultureInfo)`. Follows the pattern used by `java.util.Currency` + `NumberFormat` and NodaMoney. Multi-currency applications can now correctly represent currency identity independent of user locale. Breaking API change; consumers update from `new Money(100, "en-US")` to `new Money(100, "USD")`. `MoneyTypeHandler` and `NullableMoneyTypeHandler` (SqlServer) updated accordingly.
- **Pagination primitives relocated**: `CursorPageRequest`, `CursorPagedResult`, `PageNavigation`, `PagedResult` moved from `Excalibur.Domain` to `Excalibur.EventSourcing.Abstractions` (event-sourcing is the primary consumer). `Excalibur.Domain.PublicAPI.Shipped.txt` drops the four types; `Excalibur.EventSourcing.Abstractions.PublicAPI.Shipped.txt` adds them. Consumer impact: namespace-only `using` change; no type shape edits. Aligns with Dispatch/Excalibur separation — Domain stays focused on aggregate/entity/value-object primitives.
- **Benchmark baseline refreshed to `20260420` epoch** (`benchmarks/baselines/net10.0/dispatch-comparative-20260420/`). BenchmarkDotNet `0.15.8` on .NET SDK `10.0.202` / Runtime `10.0.6`. 16 reports committed across Comparative + WarmPath configurations. Prior `20260302` baseline preserved on disk as superseded (not cited for new claims). Absolute numbers are **not cross-diffable across the BDN 0.15.4 → 0.15.8 epoch boundary**; ratios within each report remain apples-to-apples. See `docs/performance/competitor-benchmarks.md` and `docs/benchmarks/results/current/performance-report.md` for refreshed headline numbers. One row (100-concurrent-commands allocation) is flagged under investigation pending a methodology-matched WarmPath rerun.
- **CS1591 XML documentation** now enforced on all shipping packages at build time; suppression moved to non-shipping code only
- Options validation error messages now include type names and config section guidance
- **Target framework: `.NET 10 only`** -- dropped net8.0 / net9.0 multi-target. Templates, Dockerfiles, docs (compatibility-matrix, deployment, aot-compatibility, cicd-testing-package-pipeline), CONTRIBUTING.md, RELEASE.md, and eng scripts updated accordingly
- **Dep currency sweep** (36 families bumped in S810): `Serilog` 3→4, `Microsoft.ApplicationInsights` 2→3 (removed deprecated `EnableEventCounterCollectionModule` + `EnableAdaptiveSampling`), `Google.Cloud.Firestore` 3→4, `Medo.Uuid7` 1→3 (byte-layout contract migration), plus `System.Security.Cryptography.Xml` 10.0.6 CVE pin-forward, Testcontainers, Polly, OpenTelemetry, NBomber, FluentMigrator, and ~24 more. See `management/sprints/sprint-810-review.md`
- **Security folder refactor**: 14 root files organized into `/CredentialStores/`, `/EventStores/`, `/Middleware/` with matching sub-namespaces; 3 DI-extension classes consolidated
- **Test project rename** (S806 follow-through): 11 test projects under `tests/unit/` + `tests/benchmarks/` renamed from `Excalibur.Dispatch.{AuditLogging*,Compliance*}` to `Excalibur.{AuditLogging*,Compliance*}` matching src-side package rename. 172 namespace updates, 12 `InternalsVisibleTo` updates, 4 transitive `PackageReference` updates, cascade across `.sln` / 6 `.slnf` / manifest / governance / AOT baseline
- **IndexTemplateDescriptor.Metadata**: SDK-type hide -- `IReadOnlyDictionary<string, object?>?` → `IReadOnlyDictionary<string, string>?` (projects via `.ToString()` in internal adapter)
- **System.Threading.Lock**: adopted .NET 9+ `Lock` type in `InMemoryApiKeyManager` and `LeastLoadedPlacementStrategy` (IDE0330 compliance, dropped pragma)
- **`field` keyword (C# 13) suggestion enabled** (`IDE0370` severity: `suggestion`)
- **Elastic.Clients.Elasticsearch 8.17 → 9.3.4 migration** (S813, `bd-yo3wzb`). Completes the forward-path blocked by the S810 `Elastic.Transport` pin-back. 101 consumer files migrated across 4 packages (`Excalibur.Data.ElasticSearch`, `Excalibur.AuditLogging.Elasticsearch`, `Excalibur.Inbox.ElasticSearch`, `Excalibur.Outbox.ElasticSearch`). `Elastic.Transport` restored to 0.16.0, `Testcontainers.Elasticsearch` restored to 4.11.0 (pin-back comments removed). All 1578 ES-related tests pass (1560 unit + 18 integration).
- **Security folder/namespace consolidation** (S814, `bd-0e9c5k` + `bd-o2q4zi` + `bd-tm4qck`). Resolved S808 criterion #4 violation: 6 credential types in `/Configuration/` now match their folder namespace; duplicate credential-store locations consolidated to a single canonical home.
- **NServiceBus added to comparative benchmark suite** (S814, `bd-g4o754`). New `NServiceBusComparisonBenchmarks` class mirroring existing MediatR/Wolverine/MassTransit comparison shape. Wired into `eng/run-comparative-benchmarks.ps1`.
- **Comparative benchmark script coverage completed** (S814, `bd-4jexm0`). `eng/run-comparative-benchmarks.ps1` filter + expected-reports arrays now cover all benchmark classes including `RoutingFirstParityBenchmarks`.
- **Benchmark dependencies refreshed** (S815, `bd-wbfq4f`): MediatR 12.2.0→12.5.0, MassTransit 8.4.1→8.5.9, WolverineFx 5.2.0→5.31.1 (latest pre-commercial versions; benchmark-only, no shipping-package impact). Full 9-class comparative rerun on updated deps. See `docs/performance/competitor-benchmarks.md` for licensing context.

### Fixed

- **Governance: Core package no longer pulls Azure.* transitives.** Removed unused `Microsoft.ApplicationInsights` PackageReference from `Excalibur.Dispatch.csproj` (zero .cs consumers; dead weight). Post-S810 the v3 transitive graph pulled `Azure.Core` + `Azure.Monitor.OpenTelemetry.Exporter` into the Core package, which the `transitive-bloat-report.ps1` governance gate correctly flags as a prohibited provider-SDK intrusion. Telemetry/OTel integration belongs in `Excalibur.Dispatch.Observability`, not Core.
- **Governance: `management/package-map.yaml` restored** with comprehensive categorization of all 170 shipping packages (Abstractions / Core / Framework / Hosting / Provider / Excalibur / Testing / Tool / Metapackage). The `transitive-bloat-report.ps1` governance gate no longer falls back to conservative heuristics; it now deterministically categorizes every project. File was deleted during a 2025 cleanup and had been silently missing since.
- **Elastic SDK seam regression (S810 follow-up).** CI integration-tests surfaced 12 Elasticsearch conformance/integration failures with `Elastic.Transport.UnexpectedTransportException: The JSON value could not be converted to Elastic.Clients.Elasticsearch.IndexManagement.IndexMappingRecord` plus cascade `NullReferenceException`s. Root cause: `Elastic.Transport` is a pre-1.0 library with breaking minor-version changes; S810's 0.10.1→0.16.0 bump combined with `Testcontainers.Elasticsearch` 4.7→4.11 (newer ES server image) produced a client/server JSON schema mismatch for `IndexMappingRecord`. Pinned both back (`Elastic.Transport` → 0.10.1, `Testcontainers.Elasticsearch` → 4.7.0) until the paired `Elastic.Clients.Elasticsearch` 8→9 migration completes (follow-up tracked separately). Both `Directory.Packages.props` pins carry inline comments documenting the revert rationale.
- 154+ CI build errors: broken XML crefs, AOT annotation mismatches, null dereferences in source generators
- TrimmerRoots.xml stale reference to deleted `Messaging.MessageResult`
- Testing and CloudEvents samples registered in governance matrix
- PublicAPI baselines promoted (all Unshipped -> Shipped), `*REMOVED*` entries cleared
- Internal type crefs in public XML docs replaced with `<c>` tags (cross-assembly resolution)
- AOT annotation inheritance on override methods (`IEventSerializer.ResolveType`, `AotJsonEventSerializer`, Jobs.* packages)
- **`DefaultFunctionContext.Items` fresh-dictionary bug** in `Excalibur.Dispatch.Hosting.AzureFunctions` -- `get => new Dictionary<>()` caused silently-lost writes; replaced with stable backing field + regression test
- **Saga flaky test family** (`Excalibur.Saga.Tests.StateMachine.*` under parallel load) resolved via xUnit `CollectionDefinition(DisableParallelization=true)` -- confirmed by 20× shard-08 iterations
- **Governance manifest drift** (pre-existing): removed 3 stale `src/Dispatch/Excalibur.Security*` entries, added 2 missing `src/Excalibur/Excalibur.Security.{Aws,Azure}` entries -- `eng/validate-solution.ps1` now PASSES 342=342=342 projects
- **ProblemDetailsOpenApi NuGet packaging** -- `problem-details.openapi.yaml` was packed as `contentFiles/any/any/`, causing NuGet to inject the YAML file into every consumer project and producing CS build errors. Switched to `EmbeddedResource`; replaced `GetYamlPath()` with `GetYaml()` and `GetYamlStream()` APIs that read from the embedded resource
- **AzureFunctions test-bootstrap types moved out of prod** -- `DefaultFunctionContext`, `DefaultTraceContext`, `DefaultRetryContext`, `DefaultInvocationFeatures` relocated from `src/Dispatch/Excalibur.Dispatch.Hosting.AzureFunctions/` into `tests/unit/Excalibur.Dispatch.Hosting.Serverless.Tests/Bootstrap/` (prod file shrank 329 → 203 LOC)
- **`SamplesOnly.slnf`** missing `samples/05-serverless/AzureFunctions` entry added
- **`Templates` multi-target-template brokenness**: `template.json` advertised net8/net9 Framework choices but csprojs hardcoded net10.0; `--Framework net8.0` silently produced net10.0 output. All 8 templates + 7 Dockerfiles + `eng/test-templates.ps1` + `templates-ci.yml` collapsed to net10.0 only. Also fixed 2 pre-existing JSON syntax errors (trailing comma + duplicate brace) in `excalibur-outbox` and `excalibur-saga` template.json
- **THIRD-PARTY-NOTICES.md regenerated** -- was stale with respect to S810 dep bumps (Medo.Uuid7 1.4.0→3.2.0, Polly 8.6.4→8.6.6, Microsoft.ApplicationInsights 2.23→3.1, Serilog 3.1→4.3, Google.Cloud.Firestore 3.7→4.2, System.Text.Json 10.0.0→10.0.6, ~170 other ripple updates)
- **15 orphan `src/Dispatch/Excalibur.Dispatch.{AuditLogging*,Compliance*}/` directories** removed (S806 physical-move residue: pure `obj/` NuGet restore artifacts, untracked by git)

## [3.0.0-alpha] - Pre-release Development

The 3.0.0 alpha series represents a complete ground-up redesign of the Excalibur.Dispatch framework. This section captures the cumulative changes across the alpha development cycle.

### Architecture & Core

- **Microsoft-style transport layer**: `ITransportSender` (3 methods), `ITransportReceiver` (3 methods), `ITransportSubscriber` (push-based) -- replacing bloated `ICloudMessagePublisher` (10 methods) and `CloudMessage` (36 properties)
- **TransportMessage** (9 properties) with `DelegatingTransport*` decorator bases and builder pattern (`Use()` + `Build()`)
- **Ultra-local dispatch API** via `IDirectLocalDispatcher` with `ValueTask`/`ValueTask<T>` paths for local success scenarios
- **Precompiled middleware chain pathing** and no-middleware fast path for zero-overhead dispatch routing
- **MessageResult unification**: single `Abstractions.MessageResult` static factory with `Success()`, `Cancelled()`, `Failed()` methods and cached singletons
- **MessageContext** with pooled defaults and lazy `Items` to reduce hot-path allocations
- **Endpoint mapping** aligned to action semantics (`Dispatch*Action` naming) with strict cancellation propagation

### Transport & Messaging

- **Dead Letter Queue** universal support: Kafka (`{topic}.dead-letter`), AWS SQS (native), Azure Service Bus (`$DeadLetterQueue`), RabbitMQ (DLX), Google PubSub
- 8 transport decorators: Telemetry, DeadLetter, Ordering, Retry, and more
- DI registration parity: 3 interfaces x 5 transports = 15 builder registrations
- Transport conformance test kit validating all providers
- `CancellationToken = default` removed from entire codebase (~1,806 instances)
- `ConfigureAwait(false)` added to ~329 await statements

### Quality & Safety (985+ fixes)

- **110 P0 critical fixes**: SQL injection prevention, thread-safety (`volatile`, `ConcurrentBag`, `Interlocked`, bounded `ConcurrentDictionary`), async void elimination, disposal safety (`IAsyncDisposable` + drain pattern)
- **875+ P1/P2 fixes**: resilience, middleware, DI, namespace conventions, AOT annotations
- **Zero open issues milestone** -- 0 P0, 0 P1, 0 P2, 0 P3
- `volatile _disposed` sweep: 202 fields across 152 files
- `[GeneratedRegex]` for SQL validation (AOT-safe)
- HMACSHA256 signing for GDPR data subject hashing

### PII-Safe Telemetry

- `ITelemetrySanitizer` (2 methods), `HashingTelemetrySanitizer` (SHA-256 with bounded cache), `NullTelemetrySanitizer`
- `SetSanitizedErrorStatus` extension for OTel spans
- 7 middleware PII-hardened: Authentication, JwtAuthentication, Authorization, AuditLogging, Logging, TenantIdentity, MetricsLogging
- `SensitiveDataPostConfigureOptions` flows `IncludeRawPii` to all `IncludeSensitiveData` flags
- Consumer migration guide: `docs/guides/pii-telemetry-migration.md`

### Interface & Options Compliance

- **94 interfaces** decomposed to ISP compliance (<=5 methods)
- **69 Options types** split to <=10 properties with sub-options composition
- **ValidateOnStart** for ~100 DI registrations across all packages
- **DataAnnotations** `[Required]`/`[Range]` on ~60 Options classes
- **IValidateOptions<T>** cross-property validators for 15+ Options types
- `IMeterFactory` lifecycle migration: static `new Meter(...)` to DI-managed across 5 subsystems
- 85 backward-compat shims removed
- 13 ElasticSearch `*Settings` renamed to `*Options`

### Event Sourcing & Domain

- Snapshot upgrading: `SnapshotUpgrader<TFrom,TTo>` + `SnapshotVersionManager` (BFS shortest-path)
- GDPR: `DataSubjectHasher`, `IEventStoreErasure`, atomic `InMemoryErasureStore`
- `IEventStore` simplified to 3 methods: `LoadAsync` x2, `AppendAsync`
- `DomainEventBase` abstract record, `DomainException` decoupled from `ApiException`
- `DateTime` to `DateTimeOffset` migration
- IdentityMap feature with CompositeKey validation and MERGE pattern

### Patterns & Infrastructure

- CDC: `CdcProcessor` SRP split into `CdcChangeDetector` + `CdcCheckpointManager` + `CdcChangeApplier`
- `ISagaBuilder` Microsoft-style `AddExcaliburSaga(Action<ISagaBuilder>)` with Use/Build
- `IOutboxStore` ISP: core (5 methods) + `IOutboxStoreAdmin` across 10 providers
- `IRepository` abstraction, `IDomainEventEnricher`, `TelemetryPersistenceProvider`
- `ColdStartOptimizerBase` shared serverless cold start optimization
- `TransactionScopeBase` shared base for ITransactionScope implementations
- `IAuditActorProvider` configurable actor identity for audit logging

### Testing & DX

- `AddDispatchTesting()` DI, `SagaTestFixture`, `AggregateTestFixture`
- Transport test doubles: `InMemoryTransportSender/Receiver/Subscriber`
- `Excalibur.Dispatch.Testing.Shouldly` assertion package
- `TestMeterFactory` helper for unit tests needing functional meters
- 2,413 test files migrated to standardized trait constants
- 35,000+ tests across 80+ test projects

### AOT & Trimming

- `IsAotCompatible` for ~42 packages
- Source generators for compile-time handler resolution
- `[LoggerMessage]` source-gen migration for structured logging
- Explicit generic DI registration for AOT safety

### Analyzers

- **DISP001**: Handler Not Discoverable
- **DISP002**: Missing AutoRegister Attribute
- **DISP003**: Reflection Without AOT Annotation
- **DISP004**: Optimization Hint
- **DISP005**: Handler Should Be Sealed
- **DISP006**: Message Type Missing Dispatch Interface
- **DISP101**: DI Extension Wrong Namespace
- **DISP102**: Extension Class 'I' Prefix
- **DISP103**: CancellationToken Default in Interface
- **DISP104**: '.Core.' Namespace Segment
- **DISP105**: Missing ConfigureAwait(false)
- **DISP106**: Blocking Call in Async Method

### CI/CD & Governance

- 18 GitHub Actions workflows (CI, release, quality gates, governance, performance, AOT validation, CodeQL, secrets, docs)
- 25+ PowerShell/shell quality gate scripts
- 14 CI shards for parallel test execution
- CycloneDX SBOM generation in security and release pipelines
- Public API baseline enforcement (RS0016/RS0017 as errors)
- Solution governance validation with 317/317 project compliance
- Template validation, DocFX API docs generation

### Packaging

- 175+ shipping NuGet packages
- Triple-targeting: net8.0, net9.0, net10.0
- SourceLink, deterministic builds, symbol packages (snupkg)
- Per-package README.md for NuGet.org display
- Multi-license stack: Excalibur 1.0 / AGPL-3.0 / SSPL-1.0 / Apache-2.0
- 8 `dotnet new` templates: dispatch-api, dispatch-minimal-api, dispatch-worker, dispatch-serverless, excalibur-ddd, excalibur-cqrs, excalibur-saga, excalibur-outbox
- 68 sample projects across 13 categories

[Unreleased]: https://github.com/TrigintaFaces/Excalibur/compare/v3.0.0-alpha.85...HEAD
