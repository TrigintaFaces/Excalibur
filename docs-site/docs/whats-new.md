---
sidebar_position: 2
title: What's New
description: Release history, recent changes, and upgrade notes for Excalibur.
---

# What's New

Track what's changed across Excalibur releases. For upgrade guidance, see [Versioning Strategy](./migration/version-upgrades.md).

## Current Version: 3.0.0-alpha

Excalibur is in active pre-release development. The framework is functionally complete with 44,000+ automated tests across 119 packages.

---

## Recent Highlights

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

### Developer Experience

- **Roslyn analyzers** -- Compile-time checks for common Dispatch mistakes (DISP001-DISP004)
- **Source generators** -- AOT-compatible handler registration and serialization
- **`dotnet new` templates** -- `excalibur-dispatch`, `excalibur-eventsourcing`, `excalibur-saga` project scaffolding
- **44,000+ automated tests** -- Unit, integration, conformance, and performance test suites

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
