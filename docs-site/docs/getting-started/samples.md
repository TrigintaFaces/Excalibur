---
sidebar_position: 4
title: Samples
description: Browse working sample applications demonstrating Dispatch on its own and with the Excalibur wrapper.
---

# Sample Applications

Everything under [`/samples`](https://github.com/TrigintaFaces/Excalibur/tree/main/samples) ships with the repository so you can run it offline. The newest additions highlight the Dispatch→Excalibur upgrade path.

## Before You Start

- **.NET 10.0**
- Clone the repository:

  ```bash
  git clone https://github.com/TrigintaFaces/Excalibur.git
  ```

- Familiarity with [getting started](./index.md) and [actions and handlers](../core-concepts/actions-and-handlers.md)

## Which Sample Should I Start With?

```
What are you building?
├── Console app / hello world     → HelloDispatch
├── Web API                       → WebApiQuickStart
├── MediatR replacement           → DispatchOnly
├── Event sourcing                → EventSourcingIntro
├── Transport messaging
│   ├── RabbitMQ                  → 02-messaging-transports/RabbitMQ
│   ├── Kafka                     → 02-messaging-transports/Kafka
│   ├── Azure Service Bus         → 02-messaging-transports/AzureServiceBus
│   ├── AWS SQS                   → 02-messaging-transports/AwsSqs
│   └── Multiple brokers          → 02-messaging-transports/MultiBusSample
├── Serverless
│   ├── Azure Functions           → 05-serverless/AzureFunctions
│   ├── AWS Lambda                → 05-serverless/AwsLambda
│   └── Google Cloud Functions    → 05-serverless/GoogleCloudFunctions
├── Reliability patterns
│   ├── Outbox                    → 04-reliability/OutboxPattern
│   ├── Retry + circuit breaker   → 04-reliability/RetryAndCircuitBreaker
│   └── Saga orchestration        → 04-reliability/SagaOrchestration
├── Security
│   ├── Message encryption        → 06-security/MessageEncryption
│   └── Audit logging             → 06-security/AuditLogging
├── Observability
│   ├── OpenTelemetry             → 07-observability/OpenTelemetry
│   └── Health checks             → 07-observability/HealthChecks
├── Full-stack reference app      → 11-real-world/PublicApiTodo
├── Projection rebuild (Quartz)   → 09-advanced/persistence-patterns/ProjectionRebuildJob
└── Global stream projection      → 09-advanced/persistence-patterns/GlobalStreamProjectionHost
```

All samples are in the `samples/` directory.

**Recommended progression:** HelloDispatch → WebApiQuickStart → DispatchOnly → then explore by use case above.

## Core Samples

| Sample | Where | Highlights | Quality Badge |
|--------|-------|------------|---------------|
| **DispatchOnly** | `samples/01-getting-started/DispatchOnly/` | Pure Dispatch usage (no Excalibur dependencies). Shows `IDispatchAction/Event/Document`, middleware, and the ASP.NET Core bridge. | `basic` |
| **EventSourcingIntro** | `samples/01-getting-started/EventSourcingIntro/` | Builds on DispatchOnly with Excalibur aggregates, event sourcing, repositories, and hosting defaults. | `production-pattern` |
| **Migration Guide** | `samples/MIGRATION.md` | Step-by-step instructions for moving from MediatR → Dispatch → Excalibur. | `intermediate` |

Clone the repository, restore packages once, then run any sample:

```bash
# Dispatch-only sample
cd samples/01-getting-started/DispatchOnly
dotnet run

# Event Sourcing intro sample
cd samples/01-getting-started/EventSourcingIntro
dotnet run
```

## Sample Certification and Badge Policy

Every sample carries a badge, and a badge is earned rather than asserted: a sample is certified only
after it builds clean in Release and passes its smoke profile, and that check runs on every change we make.
A smoke profile runs in one of two modes, and the difference matters when you are choosing a sample to
trust. **build** mode compiles the sample in Release. **run** mode also starts it and requires it to
reach a success signal that the profile **declares in advance** — either exiting successfully, or, for a
sample that is meant to stay up, printing a nominated readiness line. A run-mode sample that starts and
then hangs is reported as a **failure**, because it never reached the signal; success is declared, never
inferred from the absence of a crash. Most samples are certified in build mode — for those, a certified
badge means the sample compiles against the current API, not that its scenario was executed on this
change.
A sample that stops meeting the bar loses its badge rather than keeping it quietly.

Badge meanings:

- `basic`: focused onboarding sample, minimal dependencies.
- `intermediate`: shows multi-feature composition with moderate operational setup.
- `production-pattern`: demonstrates production-oriented architecture (CQRS, outbox/saga, or host composition).

Current governance status:

| Status | Count | Source |
|--------|-------|--------|
| Certified samples | 95 | `sampleFitness.certified` |
| — of which **executed** (`run` mode) | 32 | `sampleFitness.smokeProfiles` |
| — of which **compiled only** (`build` mode) | 63 | `sampleFitness.smokeProfiles` |
| &nbsp;&nbsp;&nbsp;&nbsp;· because they need infrastructure CI cannot provide | 62 | `buildOnlyReason` |
| &nbsp;&nbsp;&nbsp;&nbsp;· because nobody has yet assessed whether they could run | 1 | `buildOnlyReason` |
| Quarantined samples | 0 | `sampleFitness.quarantined` |

**Read the first two rows as different guarantees, because they are.** Thirty-two samples were started
and reached a success signal they declared in advance. The other sixty-three compiled. Every build-mode
sample declares *why* it is not executed — either it needs a broker, a cloud service or a database that
the pipeline cannot stand up, or nobody has yet established whether it could run unattended. That second
group is a **declared gap, not a pass**: it is counted separately here precisely so the aggregate cannot
be quoted as ninety-five working samples.

This distinction is enforced rather than promised: a build-mode sample with no declared reason, or one
claiming infrastructure without naming it, fails governance validation.

The quarantine list is empty.

## Architecture Samples

| Sample | Where | Highlights |
|--------|-------|------------|
| **Public-API Todo (Full Stack)** | `samples/11-real-world/PublicApiTodo/` | ASP.NET Core Minimal API demonstrating the complete Excalibur consumer DX: Dispatch handlers, AggregateRoot, event sourcing, projections, and REST endpoints. Uses only public APIs — validates the NuGet consumer experience. |
| **Healthcare Vertical Slice API** | `samples/11-real-world/HealthcareApi/` | Minimal API with Dispatch hosting bridge, vertical slice + screaming folder structure. 4 feature slices (Patients, Appointments, Prescriptions, Notifications), cross-slice events, `[Authorize]` bridge, per-slice DI. |

See [Minimal API Hosting Bridge](../deployment/minimal-api-bridge.md) and [Vertical Slice Architecture](../architecture/vertical-slice-architecture.md) for related documentation.

## Pattern Library

The `samples/` folder also contains reference implementations for common patterns:

| Folder | Pattern |
|--------|---------|
| `samples/09-advanced/persistence-patterns/SqlServerEventStore/`, `samples/09-advanced/persistence-patterns/CosmosDbEventStore/`, `samples/09-advanced/persistence-patterns/SnapshotStrategies/` | Event sourcing, snapshot strategies, and schema evolution |
| `samples/09-advanced/persistence-patterns/ProjectionRebuildJob/`, `samples/09-advanced/persistence-patterns/GlobalStreamProjectionHost/` | Quartz-scheduled projection rebuild and continuous global stream tailing |
| `samples/04-reliability/SagaOrchestration/` | Saga orchestration + compensation handlers |
| `samples/02-messaging-transports/TransportBindings/` | Transport bindings and routing |
| `samples/05-serverless/` | Azure/AWS/GCP serverless integrations (hosting packages) |

Each sub-folder contains its own `README.md` with prerequisites and run commands. Use them as starting points for your own apps or as regression tests when editing the framework.

## Contribution Tips

1. Keep Dispatch-only examples free of Excalibur references so consumers can see the minimal footprint.
2. Place full-stack CQRS samples next to the Dispatch versions so readers see the upgrade path.
3. Update this page whenever you add, rename, or delete sample folders.

## What's Next

- [Core Concepts](../core-concepts/index.md) - Understand actions, handlers, pipelines, and message context
- [Handlers](../handlers.md) - Deep dive into action and event handler patterns
- [Pipeline](../pipeline/index.md) - Add middleware behaviors to your message processing

## See Also

- [Getting Started](./index.md) — Install Dispatch and create your first message handler in 5 minutes
- [Project Templates](./project-templates.md) — Scaffold new projects quickly with dotnet new templates
- [Actions and Handlers](../core-concepts/actions-and-handlers.md) — Deep dive into action types and handler patterns used in the samples
