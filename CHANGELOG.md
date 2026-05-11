# Changelog

All notable changes to Excalibur and Excalibur.Dispatch are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- **MongoDB.Driver 2.x → 3.x migration** -- Upgraded `MongoDB.Driver` from 2.30.0 to 3.8.0 across all 8 shipping MongoDB packages (`Excalibur.Data.MongoDB`, `Excalibur.EventSourcing.MongoDB`, `Excalibur.Cdc.MongoDB`, `Excalibur.Saga.MongoDB`, `Excalibur.Inbox.MongoDB`, `Excalibur.Outbox.MongoDB`, `Excalibur.LeaderElection.MongoDB`, `Excalibur.Compliance`). Key migration changes: `_ownsClient` pattern for `MongoClient` `IDisposable` lifecycle tracking; sync `Indexes.CreateOne()` → async `CreateOneAsync()` in leader election; `Cluster.Description.Servers`/`WireVersionRange` health check → `buildInfo`/`serverStatus` commands. `MongoDbComplianceStore` gains `IDisposable`. **Breaking change** for consumers who subclass sealed `MongoClient`/`MongoDatabase`/`MongoCollection<T>` (unlikely — use interfaces for mocking).

- **DataProcessing: cursor-based paging replaces offset-based paging** -- `IRecordFetcher<T>.FetchBatchAsync` now accepts `string? cursor` (opaque token) instead of `long skip`, returning `CursorFetchResult<TRecord>` with the next cursor. `IDataProcessor.RunAsync` accepts a `string? processedCursor` for crash-safe resume. Dual-cursor tracking separates transient fetch position from durable processed checkpoint. SQL schema adds `FetchCursor`/`ProcessedCursor` columns with `COALESCE` preservation. **Breaking change** — all `DataProcessor<T>` implementations must update their `FetchBatchAsync` override signature.

- **IErasureService ISP split** -- `ExecuteAsync` removed from the public `IErasureService` interface (now 4 methods). Execution is handled internally by `ErasureSchedulerBackgroundService` via new `internal IErasureExecutor`. Consumers submit requests via `RequestErasureAsync` and monitor via `GetStatusAsync`. **Breaking change** if calling `IErasureService.ExecuteAsync` directly (use the background scheduler instead).
- **ISystemLoadMonitor CancellationToken** -- `GetCurrentLoadAsync()` now requires a `CancellationToken` parameter per .NET convention. **Breaking change** for `ISystemLoadMonitor` implementors.

### Fixed

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
