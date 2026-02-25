# Capability Ownership Matrix

> Auto-generated from `management/governance/framework-governance.json`.

| Capability | Owner | Dispatch Packages | Excalibur Packages | Rationale |
|---|---|---|---|---|
| Message contracts and dispatch pipeline | Dispatch | Excalibur.Dispatch, Excalibur.Dispatch.Abstractions | — | Core message contract and dispatch execution must remain framework-agnostic. |
| Middleware and cross-cutting policies | Dispatch | Excalibur.Dispatch, Excalibur.Dispatch.Patterns, Excalibur.Dispatch.Resilience.Polly | — | Pipeline middleware is reusable without CQRS/domain concerns. |
| Transport abstraction and adapters | Dispatch | Excalibur.Dispatch.Transport.Abstractions, Excalibur.Dispatch.Transport.AwsSqs, Excalibur.Dispatch.Transport.AzureServiceBus, Excalibur.Dispatch.Transport.GooglePubSub, Excalibur.Dispatch.Transport.Kafka, Excalibur.Dispatch.Transport.RabbitMQ | — | Transport adapters are independent of domain persistence and CQRS. |
| Serialization diagnostics hooks | Dispatch | Excalibur.Dispatch.Serialization.MemoryPack, Excalibur.Dispatch.Serialization.MessagePack, Excalibur.Dispatch.Serialization.Protobuf, Excalibur.Dispatch.Observability | — | Serialization and observability primitives are messaging concerns. |
| Minimal hosting bridge | Dispatch | Excalibur.Dispatch.Hosting.AspNetCore, Excalibur.Dispatch.Hosting.Serverless.Abstractions | — | Thin hosting bridges expose dispatcher capabilities only. |
| CQRS and domain orchestration | Excalibur | — | Excalibur.Application, Excalibur.Domain | CQRS composition and domain model behavior are Excalibur concerns. |
| Event sourcing, outbox, saga orchestration | Excalibur | — | Excalibur.EventSourcing, Excalibur.Outbox, Excalibur.Saga | State persistence and orchestration policies are opinionated platform behavior. |
| Enterprise hosting templates | Excalibur | — | Excalibur.Hosting, Excalibur.Hosting.Web, Excalibur.Hosting.AzureFunctions, Excalibur.Hosting.AwsLambda, Excalibur.Hosting.GoogleCloudFunctions | Production hosting defaults and templates belong in wrapper framework. |
| Compliance providers and key management | Excalibur | Excalibur.Dispatch.Compliance.Abstractions | Excalibur.Compliance.SqlServer, Excalibur.Compliance.Postgres | Dispatch exposes hooks while Excalibur owns concrete compliance providers. |

## Provider Naming Policy

| Policy | Value |
|---|---|
| Canonical Postgres package | Excalibur.Data.Postgres |
| Legacy compatibility package | n/a |
| Canonical name | Postgres |
| Deprecation window | 2026-12-31 |

Use Excalibur.Data.Postgres for new integrations.

