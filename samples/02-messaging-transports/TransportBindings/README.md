# TransportBindings

**Location:** `samples/02-messaging-transports/TransportBindings/`

Canonical end-to-end demonstration of the Excalibur event-ingress pipeline:
named message transports (broker queues) and **typed cron timers**, composed
into the dispatcher through a single declarative `AddEventBindings(...)`
lambda, with handlers resolved by `AddDispatch(assembly)`.

> **redesign:** Earlier revisions of this sample used a fabricated
> "bucket" stand-in and had the `AddEventBindings` lambda commented out as a
> TODO. The sample now exercises the real framework primitives published in
> `Excalibur.Dispatch` — no fabrication, no stand-ins.

## What the sample demonstrates

### 1. Two transport modalities behind the same abstraction

| Registration | What it adds |
|--------------|--------------|
| `services.AddInMemoryTransport("orders")` | A queue-style pull transport. Swap for `AddRabbitMQTransport` / `AddKafkaTransport` / `AddAzureServiceBusTransport` / `AddAwsSqsTransport` / `AddGooglePubSubTransport` / etc. in production — the binding DSL below doesn't change. |
| `services.AddCronTimerTransport<OrderArrivalTimer>("*/10 * * * * *", o => ...)` | A cron-scheduled trigger that publishes `CronTimerTriggerMessage<OrderArrivalTimer>` to the dispatcher on every tick. Built-in overlap prevention, time-zone awareness, health checks, and OpenTelemetry metrics. |

### 2. Typed cron markers eliminate string filtering

```csharp
// Zero-allocation struct marker — uniqueness of the type identifies the timer.
public struct OrderArrivalTimer : ICronTimerMarker;
```

Every `AddCronTimerTransport<TTimer>(...)` call creates an independent timer
keyed by `typeof(TTimer).Name`. The generic `CronTimerTriggerMessage<TTimer>`
gives handlers type-safe routing:

```csharp
public sealed class OrderArrivalHandler
    : IEventHandler<CronTimerTriggerMessage<OrderArrivalTimer>>
{
    // Fires only for OrderArrivalTimer — no string-name filtering.
}
```

### 3. Declarative bindings route inbound messages into the dispatcher

```csharp
services.AddEventBindings(b =>
{
    b.FromQueue("orders")
     .RouteType<OrderReceived>()
     .ToDispatcher("default");

    b.FromTimer(nameof(OrderArrivalTimer))
     .RouteType<CronTimerTriggerMessage<OrderArrivalTimer>>()
     .ToDispatcher("default");
});
```

`FromQueue`, `FromTimer`, and `FromTransport` all produce the same builder
shape (`IInboundRouteBuilder`). The three differ only in the semantics they
read from their named transport registration — the dispatcher side is
uniform.

## Running the sample

```bash
dotnet run --project samples/02-messaging-transports/TransportBindings
```

Expected output (abbreviated):

```
Transport Bindings API Demo
===========================

Registered Transports:
  - orders (type: inmemory)
  - OrderArrivalTimer (type: cron-timer)

Cron timer will fire every 10 seconds — watch the logs.
Press Ctrl+C to stop.

info: TransportBindings.OrderArrivalHandler[0]
      Timer OrderArrivalTimer fired at 2026-04-17T... (cron: */10 * * * * *, tz: UTC)
info: TransportBindings.OrderArrivalHandler[0]
      Timer OrderArrivalTimer fired at 2026-04-17T... (cron: */10 * * * * *, tz: UTC)
...
```

## Swapping in a production broker

The binding lambda does not change — only the transport registration does:

```csharp
// before
services.AddInMemoryTransport("orders");

// after (any of these)
services.AddRabbitMQTransport("orders", r => r.ConnectionString("amqp://localhost"));
services.AddKafkaTransport("orders", k => k.BootstrapServers("localhost:9092"));
services.AddAzureServiceBusTransport("orders", sb => sb.ConnectionString("Endpoint=sb://..."));
services.AddAwsSqsTransport("orders", s => s.Region("us-east-1").QueueName("orders"));
services.AddGooglePubSubTransport("orders", g => g.ProjectId("my-project").SubscriptionName("orders"));
```

## Learn more

- [`docs-site/docs/transports/cron-timer.md`](../../../docs-site/docs/transports/cron-timer.md)
  — Full cron-timer reference (marker struct design, cron expression syntax,
  overlap prevention semantics, health checks, metrics).
- [`docs-site/docs/migration/from-aspnet-eventing-proposal.md`](../../../docs-site/docs/migration/from-aspnet-eventing-proposal.md)
  — Framework positioning: how `AddCronTimerTransport<T>` + `AddEventBindings`
  compare to ASP.NET Core hosted services, IHostedService timers, and
  Quartz.NET.
- `samples/04-reliability/OutboxPattern/` — reliability patterns on the publish side.
- `samples/11-real-world/EnterpriseOrderProcessing/` — full L3 composition
  using these transports end-to-end.

## Key framework types used

| Type | Location |
|------|----------|
| `AddCronTimerTransport<TTimer>(cronExpression, configure)` | `src/Dispatch/Excalibur.Dispatch/Transport/CronTimerTransportServiceCollectionExtensions.cs` |
| `ICronTimerMarker` | `src/Dispatch/Excalibur.Dispatch.Abstractions/Transport/ICronTimerMarker.cs` |
| `CronTimerTriggerMessage<TTimer>` | `src/Dispatch/Excalibur.Dispatch/Transport/CronTimerTriggerMessageOfT.cs` |
| `CronTimerTransportAdapter` (production transport — OTel + health + overlap policy) | `src/Dispatch/Excalibur.Dispatch/Transport/CronTimerTransportAdapter.cs` |
| `AddInMemoryTransport(name, configure)` | `src/Dispatch/Excalibur.Dispatch/Configuration/InMemoryTransportServiceCollectionExtensions.cs` |
| `AddEventBindings(configure)` | `src/Dispatch/Excalibur.Dispatch/Configuration/ServiceCollectionTransportExtensions.cs` |
