---
sidebar_position: 15
title: Per-Transport Extension Point
description: Resolve a specific transport's sender or receiver directly with keyed dependency injection.
---

# Per-Transport Extension Point (Keyed Senders & Receivers)

Most applications never touch a transport directly — you dispatch a message and the pipeline routes it to
the configured transport for you. When you *do* need low-level access to one specific transport (for a
custom relay, a health probe, or a bespoke publishing path), each transport registers a **keyed**
[`ITransportSender`](#the-consumer-seam) and `ITransportReceiver` you can resolve by name.

## Two abstractions, two audiences

Excalibur separates the transport surface into a framework-wiring layer and a consumer-facing seam:

| Abstraction | Audience | Role |
|-------------|----------|------|
| `ITransportAdapter`, `IMessageBus` | **Framework wiring** | The routing/lifecycle abstractions the dispatch pipeline uses to move messages. You configure them through `AddDispatch(...)` and routing rules; you rarely resolve them yourself. |
| `ITransportSender`, `ITransportReceiver` | **Consumer seam** | The per-transport send/receive contract. Registered **keyed by transport name**, so you can resolve exactly the one you want. |

Adding a transport with a name registers both keyed services:

```csharp
services.AddKafkaTransport("orders", kafka =>
    kafka.BootstrapServers("localhost:9092"));
```

`AddKafkaTransport("orders", …)` registers a keyed `ITransportSender` and a keyed `ITransportReceiver`
under the key `"orders"`. Every transport package follows the same convention — the registration is
guaranteed family-wide by a cross-transport DI regression test.

## The consumer seam

Resolve a named transport's sender with `GetRequiredKeyedService<ITransportSender>(name)`:

```csharp
using Excalibur.Dispatch.Transport;
using Microsoft.Extensions.DependencyInjection;

// The key is the name you passed to AddKafkaTransport("orders", …).
var sender = provider.GetRequiredKeyedService<ITransportSender>("orders");

var result = await sender.SendAsync(
    new TransportMessage(/* … */),
    cancellationToken);
```

`ITransportSender` exposes:

- `Task<SendResult> SendAsync(TransportMessage message, CancellationToken cancellationToken)`
- `Task<BatchSendResult> SendBatchAsync(IReadOnlyList<TransportMessage> messages, CancellationToken cancellationToken)`
- `Task FlushAsync(CancellationToken cancellationToken)`

The receiver side is symmetric — resolve `ITransportReceiver` by the same key:

```csharp
var receiver = provider.GetRequiredKeyedService<ITransportReceiver>("orders");
```

## When to use it

Reach for the keyed seam only when you need transport-specific control that the pipeline doesn't already
give you:

- A custom relay or bridge between two named transports.
- A liveness/health probe that flushes or pings a specific broker.
- A one-off publishing path outside the normal routing rules.

For ordinary send/receive, prefer dispatching through the pipeline and configuring
[message routing](../patterns/routing.md) — it keeps your handlers transport-agnostic and lets you change
brokers without touching call sites.
