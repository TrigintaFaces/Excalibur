---
sidebar_position: 3
title: Which Event Pattern?
description: Choose deliberately between event notification, event-carried state transfer, event sourcing and CQRS at the moment you declare an event.
---

# Which of the four event patterns?

"Event-driven" names four different patterns that are routinely discussed as one. They are not
alternatives to each other, they solve different problems, and the two you choose between most often are
the two that look most alike.

This page exists because the choice is made **once, at the moment you declare the event**, and is
expensive to change afterwards. Every field you publish is a contract somebody else now depends on.

## The two that are a choice

**Event notification** and **event-carried state transfer** are both about publishing across a boundary,
and both are declared with [`IIntegrationEvent`](#declaring-an-integration-event). Picking between them
is one question:

> Does the consumer need to be **told that something happened**, or does it need **the data**?

| | Event notification | Event-carried state transfer |
|---|---|---|
| **What is on the event** | Identity, and what changed | Everything the consumer needs |
| **What the consumer does** | Calls back to read the detail | Uses the payload; never calls back |
| **Coupling** | To your *API* | To your *data shape* |
| **If you go down** | Consumers stall | Consumers keep working |
| **Cost of adding a field later** | Free — nobody reads the event's body | It is a published contract |
| **Consumer must handle staleness** | No — it reads current state | **Yes** |
| **Consumer must handle reordering** | No | **Yes** — and you must give it the means |

Neither is the right default. Choose the one whose cost you would rather pay.

## The two that are not

**Event sourcing** (events are the system of record for an aggregate) and **CQRS** (reads are served from
a model built separately from the write model) are *internal* to one service. Neither is expressed by
`IIntegrationEvent`, and neither is a substitute for choosing between the two above.

The trap is publishing your event-sourced domain events directly to other services. That makes your
storage format a public contract, and every later change to the aggregate becomes a breaking change for
somebody you do not deploy. Keep domain events inside the boundary and publish a separate integration
event that says what the outside world needs to know.

---

## Declaring an integration event

Both boundary patterns use the same marker; only the shape differs.

### Event notification

Carry identity and nothing more.

```csharp
using Excalibur.Dispatch;

[MessageName("Contoso.Orders.OrderPlaced")]
public sealed record OrderPlaced(string OrderId) : IIntegrationEvent;
```

The consumer reads what it needs when it needs it:

```csharp ignore
using Excalibur.Dispatch.Delivery;

public sealed class SendOrderConfirmation(IOrderApi orders) : IEventHandler<OrderPlaced>
{
    public async Task HandleAsync(OrderPlaced eventMessage, CancellationToken cancellationToken)
    {
        // The event said WHAT happened. Read the detail from the owner.
        var order = await orders.GetAsync(eventMessage.OrderId, cancellationToken);
        // ... use order ...
    }
}
```

You keep control of the surface: nothing a consumer can see is a field you did not deliberately expose
through your API. You pay for it with a callback per consumer, and with consumers that cannot proceed
while your service is unavailable.

### Event-carried state transfer

Carry the data, and accept the three obligations that come with it.

```csharp
using Excalibur.Dispatch;

[MessageName("Contoso.Orders.OrderPlaced")]
public sealed record OrderPlaced(
    string OrderId,
    string CustomerId,
    decimal Total,
    string Currency,
    long Version) : IIntegrationEvent;
```

Note the `Version`. It is not decoration — without it the consumer has no way to meet the third
obligation:

1. **Every field is a published contract.** You can add, but you cannot quietly change or remove.
2. **The copy is stale on arrival.** A consumer must not use it for a decision that requires current
   state — an authorization check, a balance, a stock count.
3. **Events arrive out of order.** The consumer must reject an update older than one it already applied,
   so you must put a version or a timestamp on the event for it to compare.

```csharp ignore
using Excalibur.Dispatch.Delivery;

public sealed class CustomerOrderCache(IOrderReadStore store) : IEventHandler<OrderPlaced>
{
    public async Task HandleAsync(OrderPlaced eventMessage, CancellationToken cancellationToken)
    {
        var existing = await store.FindAsync(eventMessage.OrderId, cancellationToken);

        // Obligation 3: an older event must not overwrite a newer one.
        if (existing is not null && existing.Version >= eventMessage.Version)
        {
            return;
        }

        await store.UpsertAsync(eventMessage, cancellationToken);
    }
}
```

Event handlers must be idempotent regardless: the same event can be redelivered, and more than one
handler can process it.

---

## Event sourcing, briefly

An event that is the source of truth for an aggregate is a domain event, not an integration event:

```csharp
using Excalibur.Dispatch;

[MessageName("Contoso.Orders.OrderPlacedDomainEvent")]
public sealed record OrderPlacedDomainEvent(string OrderId, decimal Total) : IDomainEvent
{
    public string EventId { get; init; } = Guid.NewGuid().ToString();

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    public IDictionary<string, object>? Metadata { get; init; }
}
```

Note that this is a *different event* from the integration event above, with a different declared name.
Message names share one global namespace and a duplicate is refused at registration, so the internal fact
and the published announcement cannot be the same type wearing two hats — which is the constraint doing
the work of keeping your storage format out of your public contract.

`IDomainEvent` carries the event-sourcing metadata the store needs. The aggregate id is supplied to the
event store on append and load, and the stream version is assigned by the store — neither belongs on the
event. See [Domain Events](../event-sourcing/domain-events.md) and
[Event Store](../event-sourcing/event-store.md).

## CQRS, briefly

CQRS is a shape for your own reads, not a message contract. A read model is built by subscribing to your
own events and projecting them into a store shaped for querying. See
[Projections](../event-sourcing/projections.md) and
[Materialized Views](../event-sourcing/materialized-views.md).

---

## How to tell you chose wrong

- **A notification that keeps growing.** Fields were added one at a time until it was carrying state,
  with none of the state-transfer obligations met — no version, no staleness contract, and consumers now
  depending on fields you never meant to publish. This is what you get by not choosing.
- **A state-transfer event whose consumers still call back.** Then it is a notification with a large
  payload: you have taken on the contract cost and kept the availability coupling.
- **A domain event on the wire.** If the type you publish is the type your event store persists, your
  storage format is a public contract.
- **A consumer that applied an update out of order.** The event has no version or timestamp, so the
  consumer could not have done anything else.

## See also

- [Actions and Handlers](./actions-and-handlers.md) — how events are declared, dispatched and handled
- [Domain Events](../event-sourcing/domain-events.md) — the event-sourcing side of the boundary
- [Outbox](../patterns/outbox.md) — publishing an integration event atomically with the state change it describes
- [Message Context](./message-context.md) — correlation and causation across a chain of events
