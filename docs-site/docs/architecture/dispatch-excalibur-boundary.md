---
sidebar_position: 1
title: Package Architecture
description: Understand how Excalibur package families are structured and their dependency rules.
---

# Package Architecture

Excalibur is organized into focused package families. Each family has a clear responsibility, and dependencies flow in one direction.

## Package Families

| Family | What it solves | Install |
|--------|----------------|---------|
| **Excalibur.Dispatch** | Message contracts, handlers, middleware, transports, ASP.NET Core bridge | `dotnet add package Excalibur.Dispatch` + `Excalibur.Dispatch.Abstractions` |
| **Excalibur.Domain** | Aggregates, entities, value objects, domain patterns | `dotnet add package Excalibur.Domain` |
| **Excalibur.EventSourcing** | Event stores, snapshots, repositories, persistence | `dotnet add package Excalibur.EventSourcing` + provider package |
| **Excalibur.Saga** | Sagas, process managers, orchestration | `dotnet add package Excalibur.Saga` + store package |
| **Excalibur.Hosting** | Opinionated hosting templates (ASP.NET Core, serverless) | `dotnet add package Excalibur.Hosting.Web` |

`Excalibur.Dispatch` is the messaging foundation. All other families depend on it, but it depends on none of them.

---

## Quick Reference

### Messaging Only

Use when you want a MediatR-class dispatcher with minimal dependencies.

```bash
dotnet add package Excalibur.Dispatch
dotnet add package Excalibur.Dispatch.Abstractions
```

```csharp
builder.Services.AddDispatch(typeof(Program).Assembly);

app.MapPost("/orders", async (CreateOrder command, IDispatcher dispatcher, CancellationToken ct) =>
{
    var result = await dispatcher.DispatchAsync(command, ct);
    return result.ToHttpResult();
});
```

### Full Stack

Use when you need aggregates, event sourcing, sagas, or hosting templates.

```bash
dotnet add package Excalibur.Domain
dotnet add package Excalibur.EventSourcing
dotnet add package Excalibur.Hosting
```

**Simple** — `AddExcalibur()` registers messaging defaults automatically:

```csharp
builder.Services.AddExcalibur(excalibur =>
{
    excalibur
        .AddEventSourcing(es => es.UseEventStore<SqlServerEventStore>())
        .AddOutbox(outbox => outbox.UseSqlServer(connectionString));
});
```

**With custom messaging** — call `AddDispatch` when you need transports, pipelines, or middleware:

```csharp
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
    dispatch.UseRabbitMQ(rmq => rmq.HostName("localhost"));
    dispatch.ConfigurePipeline("default", p => p.UseValidation());
});

builder.Services.AddExcalibur(excalibur =>
{
    excalibur
        .AddEventSourcing(es => es.UseEventStore<SqlServerEventStore>())
        .AddOutbox(outbox => outbox.UseSqlServer(connectionString));
});
```

`AddExcalibur` registers Dispatch primitives internally. Both orderings are safe because all registrations use `TryAdd`.

---

## Capability Ownership

**Authoritative source:** [`management/governance/framework-governance.json`](https://github.com/TrigintaFaces/Excalibur/blob/main/management/governance/framework-governance.json) is the single source of truth for ownership and governance mappings.
**Generated ownership table:** [`capability-ownership-matrix.md`](./capability-ownership-matrix.md).
**Migration map:** [`capability-migration-map.md`](./capability-migration-map.md).

| Capability | Excalibur.Dispatch | Excalibur.Domain / EventSourcing / Saga |
|------------|--------------------|-----------------------------------------|
| Message contracts/handlers | owns | uses Dispatch |
| Middleware pipeline | owns | uses Dispatch |
| Minimal ASP.NET Core bridge | `Excalibur.Dispatch.Hosting.AspNetCore` | — |
| Transports (Kafka, RabbitMQ, Azure, AWS) | `Excalibur.Dispatch.Transport.*` | — |
| Aggregates, repositories, event stores | — | `Excalibur.Domain`, `Excalibur.EventSourcing.*` |
| Sagas, outbox/inbox processors | — | `Excalibur.Saga.*`, `Excalibur.Outbox.*` |
| Compliance/audit logging | — | `Excalibur.Compliance.*`, `Excalibur.AuditLogging.*` |
| Hosting templates | Thin bridge only | `Excalibur.Hosting.*` |

If a capability is not clearly in the table, escalate to the architecture team before adding it.

---

## API Surface Acceptance Criteria

`Excalibur.Dispatch` APIs are accepted when they are transport/pipeline generic and usable without CQRS/domain persistence coupling.

`Excalibur.Domain`/`EventSourcing`/`Saga` APIs are accepted when they are opinionated around domain orchestration, event sourcing, outbox/saga behavior, or rich hosting composition.

Ownership/API changes are required to update governance source and architecture docs in the same PR.

---

## Adoption Path

1. **Start with `Excalibur.Dispatch`** for MediatR-style usage (`samples/DispatchMinimal` shows how).
2. **Add transports + observability** using `Excalibur.Dispatch.Transport.*` and `Excalibur.Dispatch.Observability`.
3. **Add `Excalibur.Domain`** when you need aggregates and rich domain modeling.
4. **Add `Excalibur.EventSourcing`** when you need event stores and persistence.
5. **Adopt full stack** – `samples/ExcaliburCqrs` mirrors the Dispatch sample with full CQRS.

Because all packages share the `Excalibur.*` namespace, you never rewrite handlers when adding capabilities.

---

## See Also

- [Contributor guide](https://github.com/TrigintaFaces/Excalibur/blob/main/docs/architecture/dispatch-excalibur-boundary.md) – deeper dive for maintainers
- [Dispatch docs](../core-concepts/) – dispatcher, middleware, context, routing
- [Samples](../getting-started/samples.md) – runnable projects demonstrating different package combinations
