# Excalibur.Dispatch.Compat.MassTransit

A **migration path** (not a transport port) for moving simple MassTransit message **consumers** onto
**Excalibur.Dispatch**. Provides a source-compatible `IConsumer<TMessage>` / `ConsumeContext<TMessage>`
shape and a deterministic adapter that bridges a consumer onto the canonical Excalibur
`IEventHandler<TEvent>`.

## What is shimmed (deterministic)

```csharp
// 1. Your migrated consumer (namespace-swapped to the compat namespace):
using Excalibur.Dispatch.Compat.MassTransit;

public sealed class OrderPlacedConsumer : IConsumer<OrderPlaced>
{
    public Task Consume(ConsumeContext<OrderPlaced> context) { /* use context.Message */ }
}

// 2. Annotate the message as a dispatch event (the one documented manual step):
public sealed record OrderPlaced(...) : IDispatchEvent;

// 3. Register — bridges Consume(context) onto IEventHandler<OrderPlaced>.HandleAsync(event, ct):
services.AddMassTransitConsumer<OrderPlacedConsumer, OrderPlaced>();
```

## What is NOT shimmed (documented manual step)

Per spec **OS-3**, this is a migration *path*, not a transport/broker reimplementation. MassTransit
`ConsumeContext` capabilities beyond the message itself — `Respond`, `Publish`, `Send`, `Redeliver`,
headers, etc. — are intentionally **not** provided. Consumer code using them will not compile after the
swap, surfacing the required manual migration step (no silent gap). See the
[MassTransit migration guide](../../../docs/migration/masstransit-saga-migration.md) for sagas and
advanced scenarios.

> Not affiliated with or endorsed by the owner of the MassTransit trademark.
