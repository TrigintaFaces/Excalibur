---
sidebar_position: 13
title: MQTT
description: MQTT transport primitives — keyed sender/receiver over MQTTnet for direct publish, subscribe, and acknowledge with QoS and shared-subscription support.
---

# MQTT Transport

`Excalibur.Dispatch.Transport.Mqtt` registers the **transport primitives** for [MQTT](https://mqtt.org/): a keyed [`ITransportSender`](./index.md) and [`ITransportReceiver`](./index.md) that publish, subscribe, and acknowledge messages against an MQTT broker over the [MQTTnet](https://github.com/dotnet/MQTTnet) client. The transport pins **MQTT 5.0** so it can use response topics, correlation data, and shared subscriptions.

:::info Scope of this package
This package provides the **low-level transport primitives only** — the keyed sender and receiver you resolve by transport name to move bytes directly. High-level integration into the dispatch pipeline (a transport adapter that publishes and consumes typed dispatch messages end-to-end, participating in middleware, serialization, and routing) is **not** part of this package and is provided separately.

If you need full pipeline-integrated messaging today, use one of the [pipeline-integrated transports](./index.md) (Kafka, RabbitMQ, Azure Service Bus, AWS SQS, Google Pub/Sub).
:::

## Before You Start

- **.NET 10.0**
- A running MQTT broker that supports MQTT 5.0 (for example, [Eclipse Mosquitto](https://mosquitto.org/))
- Familiarity with [transports](./index.md)

## Installation

```bash
dotnet add package Excalibur.Dispatch.Transport.Mqtt
```

**Dependencies:** `Excalibur.Dispatch.Abstractions`, `Excalibur.Dispatch.Transport.Abstractions`, `MQTTnet`

## Registration

Register a named transport with `AddMqttTransport`. The name is used for multi-transport routing and to resolve the keyed sender/receiver.

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddMqttTransport("events", mqtt =>
{
    mqtt.Host = "localhost";
    mqtt.Port = 1883;
    mqtt.ClientId = "order-service";
    mqtt.Topic = "orders";
    mqtt.QualityOfService = MqttQualityOfService.AtLeastOnce;
    mqtt.UseSharedSubscription = true;
    mqtt.SharedSubscriptionGroup = "order-processors";
});
```

`AddMqttTransport` requires an explicit transport name. Options are validated at startup (`ValidateOnStart`), so a missing `Host`, `ClientId`, or `Topic` fails fast with `OptionsValidationException`.

### Options

| Property | Type | Default | Purpose |
|----------|------|---------|---------|
| `Host` | `string` | *(required)* | The broker host name. |
| `Port` | `int` | `1883` | The broker TCP port (1..65535); typically `8883` for TLS. |
| `ClientId` | `string` | *(required)* | The client id presented to the broker. Must be unique per connected client. |
| `Topic` | `string` | *(required)* | The topic to publish to and subscribe from. |
| `QualityOfService` | `MqttQualityOfService` | `AtLeastOnce` | The delivery guarantee (see below). |
| `UseTls` | `bool` | `false` | Connect to the broker over TLS. |
| `UseSharedSubscription` | `bool` | `false` | Subscribe using an MQTT-5 shared subscription so multiple consumers compete for messages. Requires an MQTT-5 broker that supports shared subscriptions. |
| `SharedSubscriptionGroup` | `string` | `"dispatch"` | The shared-subscription group applied when `UseSharedSubscription` is enabled. Must be a shared, stable name distinct from `ClientId`, or subscribers do not compete. |
| `ResponseTopic` | `string?` | `null` | The MQTT-5 response topic for the request/reply pattern, or `null` when unused. |
| `Username` | `string?` | `null` | The user name for broker authentication, or `null` for none. |
| `Password` | `string?` | `null` | The password for broker authentication, or `null` for none. Source from a secret manager; never commit a value. |
| `MaxPayloadBytes` | `int?` | `null` | The maximum accepted inbound payload size in bytes, or `null` for no limit (see below). |

### Quality of service

`MqttQualityOfService` maps to the MQTT delivery levels and governs both publish and subscribe:

| Value | Semantics |
|-------|-----------|
| `AtMostOnce` (0) | Fire-and-forget; no broker acknowledgement. |
| `AtLeastOnce` (1, default) | Acknowledged delivery; messages may be redelivered (duplicates possible). |
| `ExactlyOnce` (2) | Exactly-once delivery end-to-end. |

Ordering is not guaranteed under QoS 0/1. Exactly-once end-to-end requires QoS 2.

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

The receiver uses **manual acknowledgement**: an inbound message is only PUBACK/PUBCOMP'd (QoS 1/2) when you call `AcknowledgeAsync`. The sender and receiver open **separate connections** and present distinct client ids derived from `ClientId`, so a single configured client id is safe for both.

### Redelivery and rejection

MQTT has no active per-message negative-acknowledge or requeue. Calling `RejectAsync` therefore **withholds** the acknowledgement rather than requeuing — the broker redelivers the unacknowledged QoS 1/2 message when the session resumes.

### Competing consumers (shared subscriptions)

By default each subscriber receives every message (fan-out). Set `UseSharedSubscription = true` to have grouped consumers compete: the receiver subscribes to `$share/{SharedSubscriptionGroup}/{Topic}` and the broker load-balances across the group. This is an MQTT-5 feature — the transport pins protocol version 5.0 so the `$share/…` filter is honored rather than treated as a literal topic name.

### Payload-size guard

Set `MaxPayloadBytes` to reject oversized inbound payloads before they are buffered. The size is measured on receipt; an oversized message can never be processed, so it is acknowledged and dropped (rather than looped by broker redelivery). The guard is **off by default** (`null` = no limit).

## What's Next

- [Transports Overview](./index.md) — All transports and pipeline integration
- [Choosing a Transport](./choosing-a-transport.md) — Decision guide
