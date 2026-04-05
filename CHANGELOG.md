# Changelog

All notable changes to Excalibur and Excalibur.Dispatch are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **AddDispatchInstrumentation()** unified OTel entry point with auto-wire telemetry
- **Excalibur.Dispatch.Analyzers** package with 6 diagnostic rules (DISP101-DISP106): DI namespace enforcement, extension class naming, CancellationToken interface conventions, namespace segment validation, ConfigureAwait enforcement, blocking call detection
- **Templates CI workflow** validating all 8 `dotnet new` templates produce buildable projects
- **DocFX API reference workflow** for automated API documentation generation
- **Coverage threshold enforcement** -- quality gates now fail (not just report) below 65% combined coverage

### Changed

- **CS1591 XML documentation** now enforced on all shipping packages at build time; suppression moved to non-shipping code only
- Options validation error messages now include type names and config section guidance

### Fixed

- TrimmerRoots.xml stale reference to deleted `Messaging.MessageResult`
- Testing and CloudEvents samples registered in governance matrix

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
