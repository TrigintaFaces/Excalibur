---
sidebar_position: 12
title: Apache Pulsar
description: Apache Pulsar transport primitives — keyed sender/receiver over DotPulsar for direct send, receive, and acknowledge.
---

# Apache Pulsar Transport

`Excalibur.Dispatch.Transport.Pulsar` registers the **transport primitives** for [Apache Pulsar](https://pulsar.apache.org/): a keyed [`ITransportSender`](./index.md) and [`ITransportReceiver`](./index.md) that send, receive, and acknowledge messages against a Pulsar broker over the [DotPulsar](https://github.com/apache/pulsar-dotpulsar) client.

:::info Scope of this package
This package provides the keyed **transport primitives** (`ITransportSender`/`ITransportReceiver` for direct byte-level send/receive) **plus a keyed [`IMessageBus`](#publishing-typed-dispatch-messages) publisher** — so you can publish typed dispatch messages (actions, events, documents) to Pulsar through the pipeline's serialization. Full pipeline-integrated **consumption** (a receive-side adapter that dispatches inbound Pulsar messages end-to-end through middleware and routing) is **not** part of this package. Request/reply is not natively supported by Pulsar.

If you need full pipeline-integrated send **and** receive today, use one of the [pipeline-integrated transports](./index.md) (Kafka, RabbitMQ, Azure Service Bus, AWS SQS, Google Pub/Sub).
:::

## Before You Start

- **.NET 10.0**
- A running Apache Pulsar broker (local or hosted)
- Familiarity with [transports](./index.md)

## Installation

```bash
dotnet add package Excalibur.Dispatch.Transport.Pulsar
```

**Dependencies:** `Excalibur.Dispatch.Abstractions`, `Excalibur.Dispatch.Transport.Abstractions`, `DotPulsar`

## Registration

Register a named transport with `AddPulsarTransport`. The name is used for multi-transport routing and to resolve the keyed sender/receiver.

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddPulsarTransport("events", pulsar =>
{
    pulsar.ServiceUrl("pulsar+ssl://pulsar.internal:6651")
          .Topic("orders")
          .SubscriptionName("order-processors")
          .SubscriptionType(PulsarSubscriptionType.Shared);
});
```

To register with the default name (`pulsar`), omit the name argument:

```csharp
services.AddPulsarTransport(pulsar =>
{
    pulsar.ServiceUrl("pulsar+ssl://pulsar.internal:6651")
          .Topic("orders")
          .SubscriptionName("order-processors");
});
```

### Transport security

The broker can be reached in the clear, so an unencrypted connection is refused by default. The
refusal happens when the transport is resolved -- while the host is starting -- rather than on the
first message, and it raises `TransportSecurityException`.

The scheme is the whole posture: `pulsar://` and `http://` are plaintext, `pulsar+ssl://` and
`https://` are not. For a local broker, opt out explicitly with `RequireTls(false)`:

```csharp
services.AddPulsarTransport("local-dev", pulsar =>
{
    pulsar.ServiceUrl("pulsar://localhost:6650")
          .RequireTls(false)   // Local brokers only: the auth token travels in the clear
          .Topic("orders")
          .SubscriptionName("order-processors");
});
```

### Builder options

| Method | Purpose |
|--------|---------|
| `ServiceUrl(string)` | The Pulsar broker service URL (e.g. `pulsar://localhost:6650`). |
| `Topic(string)` | The topic to produce to and consume from. |
| `SubscriptionName(string)` | The consumer subscription name. |
| `SubscriptionType(PulsarSubscriptionType)` | The subscription mode (see below). Defaults to `Shared`. |

### Subscription types

`PulsarSubscriptionType` maps to Pulsar's native subscription modes:

| Value | Semantics |
|-------|-----------|
| `Shared` (default) | Competing consumers — messages are delivered round-robin across all consumers on the subscription. Matches consumer-group transport semantics. |
| `Exclusive` | Only one consumer may attach to the subscription. |
| `Failover` | One active consumer with standby consumers that take over on failure. |
| `KeyShared` | Messages with the same key are delivered to the same consumer. |

Options are validated at startup (`ValidateOnStart`), so a missing service URL or topic fails fast with `OptionsValidationException`.

## Using the sender and receiver

The sender and receiver are registered as **keyed** singletons under the transport name. Resolve them with `GetRequiredKeyedService`:

```csharp
using Excalibur.Dispatch.Transport;

var sender = provider.GetRequiredKeyedService<ITransportSender>("events");
var receiver = provider.GetRequiredKeyedService<ITransportReceiver>("events");

// Send
await sender.SendAsync(
    new TransportMessage
    {
        Body = JsonSerializer.SerializeToUtf8Bytes(order),
        ContentType = "application/json",
        MessageType = "OrderPlaced",
    },
    cancellationToken);

// Receive and acknowledge
var messages = await receiver.ReceiveAsync(maxMessages: 10, cancellationToken);
foreach (var message in messages)
{
    Process(message);
    await receiver.AcknowledgeAsync(message, cancellationToken);
}
```

## Publishing typed dispatch messages

`AddPulsarTransport` also registers a keyed [`IMessageBus`](./index.md) under the transport name, so you can publish typed dispatch messages (actions, events, documents) through the framework's serialization rather than hand-building `TransportMessage` bodies:

```csharp
using Excalibur.Dispatch.Transport;

var bus = provider.GetRequiredKeyedService<IMessageBus>("events");

await bus.PublishAsync(new OrderPlaced(orderId), context, cancellationToken);
```

`PublishAsync` has overloads for `IDispatchAction`, `IDispatchEvent`, and `IDispatchDocument`. The message is serialized with the configured `IEventSerializer` and produced to the configured topic.

## Payload-size guard

The receiver enforces a payload-size limit measured before deserialization. It defaults to the standard transport-ingress bound and is tuned via `PulsarOptions.Receive.MaxPayloadBytes` — set it to `null` to opt out. A non-positive value is rejected at startup.

## What's Next

- [Transports Overview](./index.md) — All transports and pipeline integration
- [Choosing a Transport](./choosing-a-transport.md) — Decision guide
