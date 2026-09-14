---
sidebar_position: 16
title: gRPC
description: gRPC transport for Excalibur.Dispatch — unary send, unary receive, server-streaming subscribe, with TLS enforced by default, HTTP/2 keep-alive, and a configurable retry policy.
---

# gRPC Transport

`Excalibur.Dispatch.Transport.Grpc` moves dispatch messages over [gRPC](https://grpc.io/). It registers a
keyed [`ITransportSender`](./index.md), [`ITransportReceiver`](./index.md) and `ITransportSubscriber`, plus
a transport adapter that bridges them into the dispatch pipeline, a dead-letter queue manager, and a health
check.

The three call shapes map onto gRPC as you would expect: **sending** is a unary call, **receiving** is a
unary request/response, and **subscribing** is a server-streaming call held open for the life of the
subscription.

## Before You Start

- **.NET 10.0**
- A gRPC server implementing the four methods this transport calls (see [Method paths](#method-paths))
- Familiarity with [transports](./index.md)

## Installation

```bash
dotnet add package Excalibur.Dispatch.Transport.Grpc
```

## Registration

The minimum configuration is a server address:

```csharp
services.AddGrpcTransport(options =>
{
    options.ServerAddress = "https://localhost:5001";
});
```

There are four overloads. Two take a configuration action and two bind an `IConfiguration` section; each
form is available with or without a transport name:

```csharp
services.AddGrpcTransport(options => { /* … */ });                    // name defaults to "default"
services.AddGrpcTransport(configuration.GetSection("Grpc"));
services.AddGrpcTransport("orders", options => { /* … */ });
services.AddGrpcTransport("orders", configuration.GetSection("Grpc:Orders"));
```

The name is the keyed-service key, so two named gRPC transports in one container each keep their own
options and their own adapter rather than the second silently replacing the first.

:::note The `IConfiguration` overloads are not trimming- or AOT-safe
They bind through reflection. In a trimmed or Native AOT application use the `Action<GrpcTransportOptions>`
overloads instead.
:::

### Resolving the sender and receiver

Everything is registered **keyed** by transport name:

```csharp
using Excalibur.Dispatch.Transport;

var sender   = provider.GetRequiredKeyedService<ITransportSender>("orders");
var receiver = provider.GetRequiredKeyedService<ITransportReceiver>("orders");
```

## TLS is required unless you opt out

`RequireTls` defaults to **`true`**, and a non-`https` server address then fails rather than connecting in
the clear. The failure names both ways out:

> Cannot create the gRPC channel: TLS is required but the server address is `…`, which is not an https
> endpoint, so call metadata and message payloads would cross the wire in the clear. Set
> `GrpcTransportOptions.ServerAddress` to an https address, or set `GrpcTransportOptions.RequireTls` to
> false to accept a cleartext connection.

Set `RequireTls = false` only where the cleartext hop is genuinely contained — a sidecar on the same pod, a
loopback address — and treat it as a decision you have made rather than a default you inherited.

## Options

Options are validated at **startup**, not on first send, so a misconfiguration fails the host rather than
the first message.

| Option | Default | Meaning |
|--------|---------|---------|
| `ServerAddress` | *(required)* | Absolute `http` or `https` URI of the gRPC server |
| `RequireTls` | `true` | Refuse a non-`https` address |
| `DeadlineSeconds` | `30` | Per-call deadline |
| `Destination` | `"grpc-default"` | Logical destination carried on messages |
| `MaxPayloadBytes` | framework default | Reject an oversized payload; `null` opts out of the limit |
| `MaxSendMessageSize` | *(unset)* | gRPC send-size cap, in bytes |
| `MaxReceiveMessageSize` | *(unset)* | gRPC receive-size cap, in bytes |

### Connection keep-alive

Long-lived subscribe streams go half-open through idle NAT and load-balancer timeouts without traffic on
them, so the channel pings.

| Option | Default | Meaning |
|--------|---------|---------|
| `KeepAlivePingDelaySeconds` | `60` | Interval between keep-alive pings |
| `KeepAlivePingTimeoutSeconds` | `20` | How long to wait for a ping response |
| `PooledConnectionIdleTimeoutSeconds` | `300` | Idle connection lifetime in the pool |
| `EnableMultipleHttp2Connections` | `true` | Allow more than one HTTP/2 connection per endpoint |

`KeepAlivePingTimeoutSeconds` must be **less than** `KeepAlivePingDelaySeconds`; startup validation rejects
the reverse.

### Retries

| Option | Default | Meaning |
|--------|---------|---------|
| `EnableRetries` | `true` | Enable the gRPC retry policy |
| `EnableHedging` | `false` | Enable hedging instead of sequential retries |
| `MaxRetryAttempts` | `5` | Attempt cap |
| `RetryInitialBackoffSeconds` | `1` | First backoff, must be greater than zero |
| `RetryMaxBackoffSeconds` | `5` | Backoff ceiling, must be at least the initial backoff |
| `RetryBackoffMultiplier` | `1.5` | Growth factor, must be at least 1 |
| `RetryableStatusCodes` | `[Unavailable]` | Status codes that are retried |

With retries or hedging enabled, `RetryableStatusCodes` must contain at least one code — an empty list with
retries on is rejected at startup rather than quietly retrying nothing.

### Method paths

The transport calls four methods, and each path is configurable so it can sit alongside an existing service
definition:

| Option | Default |
|--------|---------|
| `SendMethodPath` | `/dispatch.transport.DispatchTransport/Send` |
| `SendBatchMethodPath` | `/dispatch.transport.DispatchTransport/SendBatch` |
| `ReceiveMethodPath` | `/dispatch.transport.DispatchTransport/Receive` |
| `SubscribeMethodPath` | `/dispatch.transport.DispatchTransport/Subscribe` |

## CloudEvents

**Inbound decoding is automatic.** Every receiver on this transport is wrapped by the framework's decoding
decorator, so a message carrying CloudEvents markers arrives with the decoded event attached — body and
properties untouched, and a message that is not a CloudEvent passes through unchanged. Every message in a
batch is delivered, including a malformed one: it arrives carrying a decode error rather than being dropped.
There is nothing to register and no option to set.

**Outbound is opt-in, per message.** Attach the event to the message you were going to send anyway:

```csharp
await sender.SendAsync(message.WithCloudEvent(cloudEvent), cancellationToken);
```

The event is published in CloudEvents **structured mode** — the whole event becomes a JSON document in the
body under the `application/cloudevents+json` content type. A message you do not call `WithCloudEvent` on
reaches the wire byte-identical, so ordinary traffic is unaffected. This path needs no trimming or
ahead-of-time annotation: the envelope is written with a UTF-8 JSON writer rather than a reflection-based
serializer.

## Reaching the underlying channel

For anything this transport does not expose, ask the sender or receiver for the `GrpcChannel` it is using:

```csharp
using Grpc.Net.Client;

var channel = (GrpcChannel?)sender.GetService(typeof(GrpcChannel));
```

The channel is owned by the transport — use it, do not dispose it.

## What's Next

- [Choosing a transport](./choosing-a-transport.md) — how this compares with the others
- [Multi-transport](./multi-transport.md) — running more than one transport in a host
- [Transports overview](./index.md) — the sender, receiver and subscriber contracts
