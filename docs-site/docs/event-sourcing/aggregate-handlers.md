---
sidebar_position: 15
title: Aggregate Handlers & Cascading
description: Route dispatched commands to event-sourced aggregates with the Decider pattern, and stage follow-up messages with cascading.
---

# Aggregate Handlers & Cascading

`Excalibur.EventSourcing.Handlers` provides a **Decider** registration that wires a dispatched command straight to an event-sourced aggregate — load, decide, save with optimistic concurrency — without hand-writing a handler class. An optional **cascading** convention lets a handler return follow-up messages that `UseOutbox()` stages to the outbox.

## Before You Start

- **.NET 10.0**
- Familiarity with [aggregates](./aggregates.md), [repositories](./repositories.md), and [handlers](../handlers.md)

## The Decider

`AddAggregateHandler` registers a handler that, for a given command type, resolves the target aggregate's identity, loads it, applies a domain decision, and saves it back with optimistic concurrency:

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddAggregateHandler<Order, OrderId, ShipOrder>(
    resolveId: command => command.OrderId,
    decide: (order, command, cancellationToken) =>
    {
        order.Ship(command.Carrier, command.TrackingNumber);
        return Task.CompletedTask;
    });
```

- **`resolveId`** extracts the aggregate identifier from the command.
- **`decide`** invokes the aggregate's own domain methods, which raise the aggregate's events. It does **not** persist directly — the handler saves the aggregate after the decision.

The command type must implement `IDispatchAction`, and the aggregate must be an `IAggregateRoot<TKey>` with snapshot support. The handler resolves its `IEventSourcedRepository<TAggregate, TKey>` from the container.

The identity resolver and decision are supplied explicitly (not reflected off the message), so the registration is **AOT-safe** — no runtime code generation.

### Error semantics

| Situation | Surfaces as |
|-----------|-------------|
| Command targets a missing aggregate | `ResourceNotFoundException` |
| A concurrent write raced the save | `ConcurrencyException` |

## Cascading follow-up messages

A handler can emit follow-up messages that are staged to the outbox by returning a result that implements `ICascade`. **Two conditions must both hold, and the return type is only one of them:**

1. The handler's result implements `ICascade` — a result that does not produces no cascade.
2. The pipeline includes the cascade step, which `UseOutbox()` adds.

**The cascade step is not part of any shipped pipeline profile.** A host that configures its pipeline from a profile alone stages no cascades, even though those profiles do stage the handler's own outbox messages — so the follow-up messages are silently dropped rather than reported as an error. Call `UseOutbox()` explicitly:

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.UseOutbox(); // outbox staging and the cascade step
});
```

The cascade step costs nothing when you are not using it: it returns immediately for any result that does not implement `ICascade`.

**How to tell whether cascading is live.** Dispatch a message whose handler returns a non-empty `ICascade` result and read the logs:

| what you see | what it means |
|---|---|
| `Cascade: staged N follow-up message(s)` (Debug) | working |
| `Cascade: handler ... returned N cascaded message(s) but no outbox is staged` (Warning) | the cascade step is in the pipeline but no outbox staging is — the messages were **not** dispatched |
| neither line | the cascade step is not in your pipeline at all — call `UseOutbox()` |

The third row is the silent case, and it is the one to check first: nothing is logged because the middleware never runs.

```csharp
using Excalibur.Dispatch;

public sealed record OrderShipped(IReadOnlyList<IDispatchMessage> Messages) : ICascade;
```

Cascaded messages are enqueued to the **same outbox the handler already writes to**, so they inherit its delivery semantics: atomic when a transactional writer participates in the handler's unit of work, otherwise eventually-consistent **at-least-once** (handlers must be idempotent). An empty `Messages` list produces no cascade.

Because the signal is the return type — not an attribute or reflection — cascading composes with the compile-time dispatch pipeline and stays AOT-safe.

## What's Next

- [Aggregates](./aggregates.md) — Modeling event-sourced aggregates
- [Repositories](./repositories.md) — Loading and saving aggregates
- [Outbox Pattern](../patterns/outbox.md) — How staged messages are delivered
