---
sidebar_position: 15
title: Aggregate Handlers & Cascading
description: Route dispatched commands to event-sourced aggregates with the Decider pattern, and stage follow-up messages with cascading.
---

# Aggregate Handlers & Cascading

`Excalibur.EventSourcing.Handlers` provides a **Decider** registration that wires a dispatched command straight to an event-sourced aggregate — load, decide, save with optimistic concurrency — without hand-writing a handler class. An optional **cascading** convention lets a handler return follow-up messages that are reliably staged to the outbox.

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

A handler can emit follow-up messages that are **automatically staged to the outbox** by returning a result that implements `ICascade`. This is opt-in by return-type convention — any result that does not implement `ICascade` produces no cascade.

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
