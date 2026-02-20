---
sidebar_position: 4
title: Samples
description: Browse working sample applications demonstrating Dispatch on its own and with the Excalibur wrapper.
---

# Sample Applications

Everything under [`/samples`](https://github.com/TrigintaFaces/Excalibur/tree/main/samples) ships with the repository so you can run it offline. The newest additions highlight the Dispatch→Excalibur upgrade path.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Clone the repository:

  ```bash
  git clone https://github.com/TrigintaFaces/Excalibur.git
  ```

- Familiarity with [getting started](./index.md) and [actions and handlers](../core-concepts/actions-and-handlers.md)

## Core Samples

| Sample | Where | Highlights | Quality Badge |
|--------|-------|------------|---------------|
| **DispatchMinimal** | `samples/DispatchMinimal/` | Pure Dispatch usage (no Excalibur dependencies). Shows `IDispatchAction/Event/Document`, middleware, and the ASP.NET Core bridge. | `basic` |
| **ExcaliburCqrs** | `samples/ExcaliburCqrs/` | Builds on DispatchMinimal with Excalibur aggregates, event sourcing, repositories, and hosting defaults. | `production-pattern` |
| **Migration Guide** | `samples/MIGRATION.md` | Step-by-step instructions for moving from MediatR → Dispatch → Excalibur. | `intermediate` |

Clone the repository, restore packages once, then run any sample:

```bash
# Dispatch-only sample
cd samples/DispatchMinimal
dotnet run

# Excalibur CQRS sample
cd samples/ExcaliburCqrs
dotnet run
```

## Sample Certification and Badge Policy

Sample certification is governed by:

- `management/governance/framework-governance.json`
- `pwsh eng/validate-samples.ps1`
- `pwsh eng/ci/validate-framework-governance.ps1 -Mode Governance -Enforce:$true`

Badge meanings:

- `basic`: focused onboarding sample, minimal dependencies.
- `intermediate`: shows multi-feature composition with moderate operational setup.
- `production-pattern`: demonstrates production-oriented architecture (CQRS, outbox/saga, or host composition).

Current governance status:

| Status | Count | Source |
|--------|-------|--------|
| Certified samples | 17 | `sampleFitness.certified` |
| Quarantined samples | 40 | `sampleFitness.quarantined` |

Quarantined samples stay in the repository but are excluded from certified catalog claims until they pass smoke/build criteria.

## Architecture Samples

| Sample | Where | Highlights |
|--------|-------|------------|
| **Healthcare Vertical Slice API** | `samples/12-vertical-slice-api/HealthcareApi/` | Minimal API with Dispatch hosting bridge, vertical slice + screaming folder structure. 4 feature slices (Patients, Appointments, Prescriptions, Notifications), cross-slice events, `[Authorize]` bridge, per-slice DI. |

See [Minimal API Hosting Bridge](../deployment/minimal-api-bridge.md) and [Vertical Slice Architecture](../architecture/vertical-slice-architecture.md) for related documentation.

## Pattern Library

The `samples/` folder also contains reference implementations for common patterns:

| Folder | Pattern |
|--------|---------|
| `Excalibur.Dispatch.Examples.Patterns.EventSourcing/` | Aggregate roots, event stores, snapshots |
| `Excalibur.Dispatch.Examples.Patterns.Sagas/` | Saga orchestration + compensation handlers |
| `Caching/`, `Channels/`, `TransportBindings/` | Advanced middleware, channel adapters, and transport bindings |
| `CloudNative/` | Azure/AWS/GCP serverless integrations (Excalibur hosting packages) |

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
