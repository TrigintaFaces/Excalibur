---
sidebar_position: 1
title: Dispatch vs Excalibur
description: Understand when to use the Dispatch messaging core and when to layer Excalibur for CQRS + hosting.
---

# Dispatch vs Excalibur

This repository ships two cooperating frameworks:

| Layer | What it solves | Install |
|-------|----------------|---------|
| **Dispatch (Messaging Core)** | Message contracts, handlers, middleware, transports, thin ASP.NET Core bridge | `dotnet add package Excalibur.Dispatch` + `Excalibur.Dispatch.Abstractions` |
| **Excalibur (CQRS + Hosting)** | Aggregates, repositories, event stores, sagas, compliance, opinionated hosting templates | Add packages such as `Excalibur.Domain`, `Excalibur.EventSourcing`, `Excalibur.Hosting.Web` |

Dispatch answers **“How do my messages flow?”**  
Excalibur answers **“What hosts and domain patterns do I need?”**

---

## Quick Reference

### Dispatch-Only

Use when you want a MediatR-class dispatcher with minimal dependencies.

```bash
dotnet add package Excalibur.Dispatch
dotnet add package Excalibur.Dispatch.Abstractions
```

```csharp
builder.Services.AddDispatch(typeof(Program).Assembly);

app.MapPost("/orders", async (CreateOrder command, IDispatcher dispatcher, CancellationToken ct) =>
{
    // DispatchAsync extension auto-creates a context when called without one
    var result = await dispatcher.DispatchAsync(command, ct);
    return result.ToHttpResult();
});
```

### Dispatch + Excalibur

Use when you need aggregates, event sourcing, sagas, or hosting templates.

```bash
dotnet add package Excalibur.Domain
dotnet add package Excalibur.EventSourcing
dotnet add package Excalibur.Hosting
```

**Simple** — `AddExcalibur()` registers Dispatch defaults automatically:

```csharp
builder.Services.AddExcalibur(excalibur =>
{
    excalibur
        .AddEventSourcing(es => es.UseEventStore<SqlServerEventStore>())
        .AddOutbox(outbox => outbox.UseSqlServer(connectionString));
});
```

**With custom Dispatch** — call `AddDispatch` when you need transports, pipelines, or middleware:

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

| Capability | Dispatch | Excalibur |
|------------|----------|-----------|
| Message contracts/handlers | ✅ | ↩ uses Dispatch |
| Middleware pipeline | ✅ | ↩ uses Dispatch |
| Minimal ASP.NET Core bridge | ✅ `Excalibur.Dispatch.Hosting.AspNetCore` | ✖ |
| Transports (Kafka, RabbitMQ, Azure Service Bus, AWS SQS) | ✅ `Excalibur.Dispatch.Transport.*` | ✖ |
| Aggregates, repositories, event stores | ✖ | ✅ `Excalibur.Domain`, `Excalibur.EventSourcing.*` |
| Sagas, outbox/inbox processors | ✖ | ✅ `Excalibur.Saga.*`, `Excalibur.Outbox.*` |
| Compliance/audit logging | ✖ | ✅ `Excalibur.Compliance.*`, `Excalibur.AuditLogging.*` |
| Hosting templates (ASP.NET Core, Azure Functions, Lambda, GCF) | Thin bridge only | ✅ `Excalibur.Hosting.*` |

If a capability is not clearly in the table, escalate to the architecture team before adding it.

---

## API Surface Acceptance Criteria

Dispatch APIs are accepted when they are transport/pipeline generic and usable without CQRS/domain persistence coupling.

Excalibur APIs are accepted when they are opinionated around domain orchestration, event sourcing, outbox/saga behavior, or rich hosting composition.

Ownership/API changes are required to update governance source and architecture docs in the same PR.

---

## Migration Path

1. **Start Dispatch-only** for MediatR-style usage (`samples/DispatchMinimal` shows how).
2. **Add transports + observability** using Dispatch packages only.
3. **Introduce Excalibur** when you need aggregates, event sourcing, or hosting.
4. **Adopt Excalibur samples** – `samples/ExcaliburCqrs` mirrors the Dispatch sample but with full CQRS.

Because Excalibur depends on Dispatch, you never rewrite handlers when moving up the stack.

---

## See Also

- [Contributor guide](https://github.com/TrigintaFaces/Excalibur/blob/main/docs/architecture/dispatch-excalibur-boundary.md) – deeper dive for maintainers
- [Dispatch docs](../core-concepts/) – dispatcher, middleware, context, routing
- [Samples](../getting-started/samples.md) – runnable projects demonstrating both layers
