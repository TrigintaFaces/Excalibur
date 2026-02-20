# Changelog

All notable changes to the Excalibur framework are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - Unreleased

Initial release of the Excalibur messaging framework and Excalibur application framework.

### Added

#### Dispatch (Messaging Core)

- **Message Dispatching** -- Type-safe `IDispatcher` with `IDispatchAction`, `IDispatchAction<TResult>`, `IDispatchEvent` message types and `IActionHandler<T>`, `IActionHandler<T,R>`, `IEventHandler<T>` handler interfaces
- **Streaming Handlers** -- `IStreamingDocumentHandler`, `IStreamConsumerHandler`, `IStreamTransformHandler`, `IProgressDocumentHandler` for `IAsyncEnumerable`-based streaming pipelines
- **Middleware Pipeline** -- Configurable middleware chain with built-in validation, authentication, authorization, logging, caching, tenant identity, and metrics middleware
- **Pipeline Profiles** -- Named pipeline configurations for per-message-type middleware stacks
- **Multi-Transport Routing** -- Three-layer routing model (transport selection, endpoint routing, message mapping) with `ITransportSender` (3 methods), `ITransportReceiver` (3 methods), `ITransportSubscriber` (push-based)
- **Transport Providers** -- RabbitMQ, Apache Kafka, Azure Service Bus, AWS SQS, Google Pub/Sub with dead-letter queue support across all transports
- **Serialization** -- System.Text.Json (default), MessagePack (zero-copy), MemoryPack, Protocol Buffers with ISP-split serialization interfaces
- **Security** -- Field-level encryption, JWT authentication, RBAC authorization, rate limiting, credential stores (Azure Key Vault, AWS Secrets Manager, HashiCorp Vault)
- **Compliance** -- GDPR erasure, SOC2/HIPAA/FedRAMP audit logging, data masking
- **Observability** -- OpenTelemetry integration (traces, metrics, logs), `ITelemetrySanitizer` for PII-safe telemetry with SHA-256 hashing
- **Resilience** -- Polly v8 integration with circuit breaker, retry, timeout, bulkhead patterns
- **Caching** -- Distributed caching with adaptive TTL, LRU eviction, tag-based invalidation, claim check pattern
- **Hosting** -- ASP.NET Core, Azure Functions, AWS Lambda, Google Cloud Functions hosting
- **Testing** -- `Excalibur.Dispatch.Testing` package with `DispatchTestHarness`, transport test doubles, Shouldly assertion extensions
- **Source Generators** -- Roslyn source generators for handler registration and analyzers for common mistakes
- **AOT Support** -- `IsAotCompatible` across ~42 packages, `[LoggerMessage]` source generation, `JsonSerializerContext`
- **PublicAPI Tracking** -- `Microsoft.CodeAnalysis.PublicApiAnalyzers` for API surface management

#### Excalibur (Application Framework)

- **Domain Modeling** -- `AggregateRoot`, `EntityBase`, `ValueObject`, `DomainEventBase`, `DomainException`, `IRepository`
- **Event Sourcing** -- `IEventStore`, snapshot stores, event upcasting (BFS shortest-path `SnapshotVersionManager`), projections
- **Event Store Providers** -- SQL Server, PostgreSQL, CosmosDB, DynamoDB, Firestore, MongoDB, Redis, Elasticsearch, InMemory
- **Saga/Process Manager** -- `ISagaBuilder` with `AddExcaliburSaga(Action<ISagaBuilder>)`, state machines, timeouts
- **Outbox Pattern** -- `IOutboxStore` with ISP split (`IOutboxStoreAdmin`), transactional outbox across 10 providers, adaptive polling
- **Change Data Capture** -- Unified `ICdcStateStore` across 5 providers, SRP-split processor classes
- **Leader Election** -- Redis, SQL Server, Consul, Kubernetes providers with health checks and telemetry
- **Unified Builder** -- `AddExcalibur(Action<IExcaliburBuilder>)` for configuring all subsystems (event sourcing, outbox, CDC, sagas, leader election)
- **Testing** -- `SagaTestFixture`, `AggregateTestFixture`, conformance test kits
- **A3 (Authentication, Authorization, Audit)** -- Activity-based authorization, grant management, structured audit events

### Infrastructure

- Source/test project counts and package inventory are validated continuously by CI governance scripts
- All packages multi-target .NET 8.0, .NET 9.0, and .NET 10.0
- Deterministic builds with SourceLink
- Central package management (`Directory.Packages.props`)
- Multi-license: Excalibur 1.0, AGPL-3.0, SSPL-1.0, Apache-2.0
- GitHub Actions CI/CD with build, test, release, quality gates, conformance testing, and sample validation
- 59 sample projects across 13 categories

### Backlog Clearance (Sprint 582)

- **Audit Log Encryption at Rest** -- `EncryptingAuditEventStore` delegating decorator with per-field encryption (ActorId, IpAddress, Reason, UserAgent), `AuditEncryptionOptions`, `UseAuditLogEncryption()` DI extension
- **Query Builder Abstraction** -- `IQueryBuilder<T>` (5 methods: Where, OrderBy, Take, Skip, ToListAsync) with `InMemoryQueryBuilder<T>` for testing
- **Saga Idempotency** -- `InMemorySagaIdempotencyProvider` default implementation with `ConcurrentDictionary` storage
- **SQL Server Leader Election Health Check** -- `AddSqlServerLeaderElectionHealthCheck()` DI convenience extension with tag-based registration
- **Leader Election AOT** -- `LeaderElectionJsonContext` source-generated JSON, removed `[RequiresUnreferencedCode]`/`[RequiresDynamicCode]` from `SqlServerHealthBasedLeaderElection`
- **AOT Validation Tooling** -- Enhanced `trim-aot-audit.ps1` with `JsonSerializerContext` detection, new `Validate-AotBuild.ps1` for CI
- **CI/CD Fixes** -- Created `Validate-AotBuild.ps1` to resolve broken workflow references

### Quality (Sprints 540-582)

Over 40 sprints of systematic quality hardening:

- **110 P0 critical fixes** (Sprints 540-546) -- Thread-safety, SQL injection prevention, disposal patterns, AOT remediation
- **875+ P1/P2 fixes** (Sprints 547-569) -- Interface ISP splits, Options pattern migration, ValidateOnStart sweep, PII-safe telemetry, volatile disposal guards
- **Interface Simplification** -- All public interfaces reduced to max 5 methods with ISP sub-interfaces for advanced features
- **Options Hardening** -- Max 10 properties per Options class, `IValidateOptions<T>` cross-property validators, `ValidateOnStart` across all DI registrations
- **PII-Safe Telemetry** (Sprint 565-567) -- `ITelemetrySanitizer` infrastructure, SHA-256 hashing, exception sanitization, middleware hardening
- **Thread-Safety Sweep** (Sprint 569) -- `volatile _disposed` across 202 fields in 152 files
- **Settings to Options Migration** -- 40+ `*Settings` classes renamed to `*Options` with DataAnnotations validation

### Key Design Patterns

| Pattern | Description |
|---------|-------------|
| Microsoft-First | Every interface follows Microsoft's API surface (max 5 methods, decorator chains, builder pattern) |
| Thread-Safety | `volatile` hot-path reads, `ConcurrentDictionary.GetOrAdd()`, `Interlocked` counters, bounded caches (cap=1024) |
| Disposal | `IAsyncDisposable` + `ConcurrentBag<Task>` drain + `volatile _disposed` guard |
| SQL Injection | `[GeneratedRegex]` whitelist + bracket-escape defense-in-depth |
| Options | `IOptions<T>` + `IPostConfigureOptions<T>` + `IValidateOptions<T>` + `ValidateOnStart` |
| PII Protection | `ITelemetrySanitizer` with SHA-256 hashing, `SetSanitizedErrorStatus`, `SensitiveDataPostConfigureOptions` |
| AOT | `[LoggerMessage]` source-gen, `JsonSerializerContext`, explicit `AddX<T>()` DI registration |

