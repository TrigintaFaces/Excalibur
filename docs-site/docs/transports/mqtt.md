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
    mqtt.Host = "mqtt.internal";
    mqtt.Port = 8883;
    mqtt.UseTls = true;
    mqtt.ClientId = "order-service";
    mqtt.Topic = "orders";
    mqtt.QualityOfService = MqttQualityOfService.AtLeastOnce;
    mqtt.UseSharedSubscription = true;
    mqtt.SharedSubscriptionGroup = "order-processors";
});
```

`AddMqttTransport` requires an explicit transport name. Options are validated at startup (`ValidateOnStart`), so a missing `Host`, `ClientId`, or `Topic` fails fast with `OptionsValidationException`.

### Transport security

The broker can be reached in the clear, so an unencrypted connection is refused by default. The
refusal happens when the transport is resolved -- while the host is starting -- rather than on the
first message, and it raises `TransportSecurityException`.

MQTT over plain TCP carries the user name, password and every payload in the clear, so `UseTls` must
be set (the TLS listener is normally port 8883). For a local broker with no TLS listener, opt out
explicitly:

```csharp
services.AddMqttTransport("local-dev", mqtt =>
{
    mqtt.Host = "localhost";
    mqtt.Port = 1883;
    mqtt.ClientId = "order-service";
    mqtt.Topic = "orders";
    mqtt.RequireTls = false;   // Local brokers only: credentials travel in the clear
});
```

### Options

| Property | Type | Default | Purpose |
|----------|------|---------|---------|
| `Host` | `string` | *(required)* | The broker host name. |
| `Port` | `int` | `1883` | The broker TCP port (1..65535); typically `8883` for TLS. |
| `ClientId` | `string` | *(required)* | The client id presented to the broker. Must be unique per connected client. |
| `Topic` | `string` | *(required)* | The topic to publish to and subscribe from. |
| `UseTls` | `bool` | `false` | Connect over TLS. |
| `RequireTls` | `bool` | `true` | Refuse the connection unless `UseTls` is set. |
| `QualityOfService` | `MqttQualityOfService` | `AtLeastOnce` | The delivery guarantee (see below). |
| `PersistentSession` | `bool` | `true` | Keep this client's session on the broker across disconnects. This is what makes rejection with redelivery work; see below. |
| `SessionExpiryInterval` | `TimeSpan` | `1 hour` | How long the broker keeps the session after the connection closes — the recovery window. Must be positive and a whole number of seconds. |
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
| `AtMostOnce` (0) | Fire-and-forget; no broker acknowledgement. **Refused for a receiver** — see below. |
| `AtLeastOnce` (1, default) | Acknowledged delivery; messages may be redelivered (duplicates possible). |
| `ExactlyOnce` (2) | Exactly-once delivery end-to-end. |

Ordering is not guaranteed under QoS 0/1. Exactly-once end-to-end requires QoS 2.

:::warning QoS 0 is rejected at startup when a receiver is registered
At QoS 0 the protocol sends no acknowledgement packet, so the receiver's whole settlement surface is
inoperable: `AcknowledgeAsync` settles a message the broker already stopped tracking,
`RejectAsync(…, requeue: true, …)` cannot cause a redelivery — so it discards the message outright — and
`RejectAsync(…, requeue: false, …)` suppresses a redelivery that was never going to happen. Rather than
accept that configuration and report success for operations that do nothing, `AddMqttTransport` fails
validation at startup. Publish-only hosts are unaffected.
:::

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

`RejectAsync` honours the `requeue` argument, and it uses a different mechanism for each outcome:

| Call | Mechanism | Outcome |
|------|-----------|---------|
| `RejectAsync(…, requeue: true, …)` | The acknowledgement is **withheld**. | The broker redelivers the message when the session resumes. |
| `RejectAsync(…, requeue: false, …)` | The acknowledgement is **sent carrying a failure reason code**. | The delivery flow completes and ownership transfers to the client, so the broker does **not** redeliver. |

The second row is the one to reach for in a poison-message handler. Under MQTT 5 a PUBACK completes the
QoS 1 flow whatever its reason code, and at QoS 2 a PUBREC with a reason code of 0x80 or above terminates
the flow before PUBREL — so acknowledging with a failure code is how a client says "I could not process
this, and do not send it again." The transport always connects as MQTT 5, so this is available on every
connection it opens.

:::caution `requeue: false` suppresses redelivery on a best-effort basis
Returning normally means the acknowledgement was handed to the transport. MQTT has no acknowledgement of
an acknowledgement, so if the connection fails before the broker processes the packet, the message stays
outstanding and is redelivered on resume — and nothing in the protocol can distinguish that from success.
**Deduplicate in your dead-letter handler** — but not on `TransportReceivedMessage.Id`, which is unique per
*delivery* and so differs on every redelivery. It has to be: it keys the receiver's pending-settlement map,
and deriving it from a content field a producer may legitimately repeat would let two in-flight messages
settle against each other's handle. Set a deduplication id at the sender (`UseDeduplication`); it travels as
an MQTT user property, the broker retransmits it with the message, and it is readable from
`TransportReceivedMessage.Properties`. That is the key that survives a redelivery.
:::

The requeue promise depends on the session outliving the disconnect, which is why `PersistentSession` defaults to `true`.
The transport connects with a non-clean start and a one-hour session expiry so that a rejected message is still held
by the broker when the client comes back.

:::caution Rejection needs a stable `ClientId`
A session is resumed by **client id**. If you generate `ClientId` per process — a GUID at start-up, a pod name, a
random suffix — the broker cannot match the returning client to the old session, so every restart begins a fresh one
and anything you rejected is gone. Configuration validation cannot catch this: a random id is a valid id. Use a name
that is stable for the deployment.
:::

Setting `PersistentSession` to `false` opts out. The broker then discards the session as soon as the connection
closes, so a rejected message is **dropped rather than redelivered** — with no error and no warning. Choose it only
if you do not rely on rejection, for example when every failure is handled in-process or routed to a dead-letter
topic by your own code.

`SessionExpiryInterval` is the recovery window, sized for reconnects and rolling restarts rather than for outages. A
subscriber that returns inside the window resumes its session and receives what it left unacknowledged; one that
returns after it gets a clean session. The window is encoded on the wire as whole seconds, so a fractional value is
**rejected at start-up** rather than rounded — a silently-shortened recovery window is the defect this setting exists
to prevent.

A persistent session is not free: the broker retains state for each client id, and brokers bound how much they will
hold. If you run many short-lived clients, prefer a shorter window over disabling persistence.

### Competing consumers (shared subscriptions)

By default each subscriber receives every message (fan-out). Set `UseSharedSubscription = true` to have grouped consumers compete: the receiver subscribes to `$share/{SharedSubscriptionGroup}/{Topic}` and the broker load-balances across the group. This is an MQTT-5 feature — the transport pins protocol version 5.0 so the `$share/…` filter is honored rather than treated as a literal topic name.

### Payload-size guard

Set `MaxPayloadBytes` to reject oversized inbound payloads before they are buffered. The size is measured on receipt; an oversized message can never be processed, so it is acknowledged and dropped (rather than looped by broker redelivery). The guard is **off by default** (`null` = no limit).

## What's Next

- [Transports Overview](./index.md) — All transports and pipeline integration
- [Choosing a Transport](./choosing-a-transport.md) — Decision guide
