---
sidebar_position: 14
title: IBM MQ
description: IBM MQ transport primitives — keyed sender/receiver over the IBM MQ managed .NET client with unit-of-work-per-message acknowledgement.
---

# IBM MQ Transport

`Excalibur.Dispatch.Transport.IbmMq` registers the **transport primitives** for [IBM MQ](https://www.ibm.com/products/mq): a keyed [`ITransportSender`](./index.md) and [`ITransportReceiver`](./index.md) that send, receive, and acknowledge messages against an IBM MQ queue manager over the IBM MQ managed .NET client.

:::info Scope of this package
This package provides the **low-level transport primitives only** — the keyed sender and receiver you resolve by transport name to move bytes directly. High-level integration into the dispatch pipeline (a transport adapter that publishes and consumes typed dispatch messages end-to-end, participating in middleware, serialization, and routing) is **not** part of this package and is provided separately.

If you need full pipeline-integrated messaging today, use one of the [pipeline-integrated transports](./index.md) (Kafka, RabbitMQ, Azure Service Bus, AWS SQS, Google Pub/Sub).
:::

## Before You Start

- **.NET 10.0**
- A reachable IBM MQ queue manager (local or hosted) with a server-connection channel and queue
- Familiarity with [transports](./index.md)

## Installation

```bash
dotnet add package Excalibur.Dispatch.Transport.IbmMq
```

**Dependencies:** `Excalibur.Dispatch.Abstractions`, `Excalibur.Dispatch.Transport.Abstractions`, `IBMMQDotnetClient`

:::caution The IBM MQ driver is not open source

`IBMMQDotnetClient` is IBM's own MQ classes for .NET and is **not** distributed under an
OSI-approved open-source license. The package declares no SPDX license expression; it sets
`requireLicenseAcceptance` and points at
[IBM's license terms](https://www.ibm.com/support/customer/csol/terms/?id=L-MKDD-7KHY2Q), and ships
IBM's terms inside the package under `licenses/`. Those terms open:

> IMPORTANT: READ CAREFULLY
>
> Two license agreements are presented below.
>
> 1. IBM International License Agreement for Evaluation of Programs
> 2. IBM International Program License Agreement
>
> If Licensee is obtaining the Program for purposes of productive use (other than evaluation, testing,
> trial "try or buy," or demonstration): By clicking on the "Accept" button below, Licensee accepts the
> IBM International Program License Agreement, without modification.

The evaluation agreement carries a 90-day evaluation period; the International Program License
Agreement is the one the terms name for productive use.

Excalibur redistributes no IBM software and asserts nothing about your entitlement on your behalf --
the same position it takes on the [Oracle driver](../data-providers/oracle.md). Installing this
package makes NuGet install the driver into your application, so the obligations are yours. Read the
terms shipped inside the driver package, and the terms at the URL above, and confirm your deployment
is covered before you ship.

Every [pipeline-integrated transport](./index.md) carries an OSI-approved driver license. Choosing a
different transport is the remedy if these terms do not suit you.

:::

## Registration

Register a named transport with `AddIbmMqTransport`. The name is used for multi-transport routing and to resolve the keyed sender/receiver.

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddIbmMqTransport("orders", ibmmq =>
{
    ibmmq.QueueManager = "QM1";
    ibmmq.Host = "localhost";
    ibmmq.Port = 1414;
    ibmmq.Channel = "DEV.APP.SVRCONN";
    ibmmq.QueueName = "DEV.QUEUE.1";

    // TLS: IBM MQ negotiates TLS from the CipherSpec agreed on the SVRCONN channel
    ibmmq.SslCipherSpec = "ANY_TLS12_OR_HIGHER";
    ibmmq.SslPeerName = "CN=QM1,O=Example";   // optional: pin the queue manager's DN

    // Optional
    ibmmq.ReplyToQueue = "DEV.REPLY.1";
    ibmmq.UserId = "app";                 // source credentials from configuration or a secret manager
    ibmmq.Password = "<from-secret-store>";

    // Receive tuning
    ibmmq.Receive.MaxBatchSize = 10;
    ibmmq.Receive.WaitIntervalMilliseconds = 1000;
    ibmmq.Receive.MaxPayloadBytes = 1_048_576;
});
```

### Transport security

The broker can be reached in the clear, so an unencrypted connection is refused by default. The
refusal happens when the transport is resolved -- while the host is starting -- rather than on the
first message, and it raises `TransportSecurityException`.

There is no separate "use TLS" switch: a channel with no CipherSpec carries the user id, password and
every message body in the clear, so `SslCipherSpec` must name the CipherSpec configured on the SVRCONN
channel. For a developer queue manager, opt out explicitly:

```csharp
services.AddIbmMqTransport("local-dev", ibmmq =>
{
    ibmmq.QueueManager = "QM1";
    ibmmq.Host = "localhost";
    ibmmq.Channel = "DEV.APP.SVRCONN";
    ibmmq.QueueName = "DEV.QUEUE.1";
    ibmmq.RequireTls = false;   // Developer queue managers only
});
```

### Options

| Property | Type | Default | Purpose |
|----------|------|---------|---------|
| `QueueManager` | `string` | `""` (required) | The name of the target queue manager. |
| `Host` | `string` | `""` (required) | The host name of the queue manager listener. |
| `Port` | `int` | `1414` | The TCP port of the queue manager listener; must be in `1..65535`. |
| `Channel` | `string` | `""` (required) | The server-connection channel name (commonly `DEV.APP.SVRCONN` on developer queue managers). |
| `QueueName` | `string` | `""` (required) | The queue that messages are sent to and received from. |
| `ReplyToQueue` | `string?` | `null` | The reply-to queue for the native request/reply pattern, or `null` when unused. |
| `UserId` | `string?` | `null` | The user id for client authentication, or `null` for none. |
| `Password` | `string?` | `null` | The password for client authentication, or `null` for none. |
| `Receive` | `IbmMqReceiveTuningOptions` | (see below) | Receive-side tuning options. |

### Receive tuning (`Receive`)

| Property | Type | Default | Purpose |
|----------|------|---------|---------|
| `MaxBatchSize` | `int` | `10` | The maximum number of messages to drain per receive call; must be in `1..256`. Because the receiver opens one queue-manager connection per in-flight message, this bounds the concurrent connections a single receive can open (`MaxBatchSizeCeiling` = 256). |
| `WaitIntervalMilliseconds` | `int` | `1000` | The get-wait interval before a receive call returns empty; must be non-negative. Only the first get in a batch waits — the rest return immediately. |
| `MaxPayloadBytes` | `int?` | `null` | The maximum accepted inbound payload size in bytes, or `null` to opt out of the limit. |

Options are validated at startup (`ValidateOnStart`), so a missing queue manager, host, channel, or queue name — or a port or batch size out of range — fails fast with `OptionsValidationException`.

## Using the sender and receiver

The sender and receiver are registered as **keyed** singletons under the transport name. Resolve them with `GetRequiredKeyedService`:

```csharp
using Excalibur.Dispatch.Transport;

var sender = provider.GetRequiredKeyedService<ITransportSender>("orders");
var receiver = provider.GetRequiredKeyedService<ITransportReceiver>("orders");

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

### Unit of work per message

Each received message holds its own queue-manager connection and syncpoint. `AcknowledgeAsync` commits (removes) exactly that message, and `RejectAsync` backs it out so the queue manager redelivers it — true per-message acknowledge and reject in any order. Outstanding units of work are bounded by the caller's `maxMessages` and are always committed or backed out (never leaked), including on disposal and cancellation. On the send side, each send opens a connection, puts the message under a unit of work, commits, and disconnects, so a failed put never leaves an uncommitted message.

## Payload-size guard

The receiver enforces an inbound payload-size limit measured before the message is delivered, tuned via `IbmMqOptions.Receive.MaxPayloadBytes` — set it to `null` to opt out. An oversized message is discarded (committed under syncpoint) rather than backed out, so the queue manager does not loop redelivering an unprocessable payload.

## What's Next

- [Transports Overview](./index.md) — All transports and pipeline integration
- [Choosing a Transport](./choosing-a-transport.md) — Decision guide
