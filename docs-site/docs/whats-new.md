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

## April 2026 — Performance + Container Deployment + AOT Epic Complete

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
