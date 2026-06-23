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
