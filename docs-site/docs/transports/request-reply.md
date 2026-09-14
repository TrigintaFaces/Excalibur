---
sidebar_position: 18
title: Request/Reply (Azure Service Bus)
description: The Azure Service Bus request/reply seam — options, validation, and the client contract you implement, since the framework ships no implementation of it.
---

# Request/Reply over Azure Service Bus

`Excalibur.Dispatch.Transport.AzureServiceBus` carries a **request/reply seam**: a client contract, validated options, and registration extensions for the session-based request/response pattern over [Azure Service Bus](https://learn.microsoft.com/azure/service-bus-messaging/message-sessions).

:::caution You implement the client — the framework ships no implementation
`IRequestReplyClient` is a **seam, not a turnkey transport.** No implementation of it ships in this or any other package, and every registration overload requires you to supply one, either as a type argument or from a factory. If you register nothing, nothing resolves.

This is deliberate: correlating a reply depends on your session strategy, your reply-queue topology, and how long you are willing to hold a pending request — decisions the framework cannot make for you. What the framework does provide is the contract, the options, and startup validation of them.
:::

## Before You Start

- **.NET 10.0**
- An Azure Service Bus namespace with **sessions enabled** on the reply queue
- Familiarity with [transports](./index.md) and [Azure Service Bus](./azure-service-bus.md)

## Installation

```bash
dotnet add package Excalibur.Dispatch.Transport.AzureServiceBus
```

## The contract you implement

```csharp
public interface IRequestReplyClient : IAsyncDisposable
{
    Task<RequestReplyMessage> SendRequestAsync(
        RequestReplyMessage request,
        string destinationEntity,
        CancellationToken cancellationToken);

    Task<RequestReplyMessage?> ReceiveReplyAsync(
        string sessionId,
        CancellationToken cancellationToken);
}
```

`ReceiveReplyAsync` returns `null` when no reply arrives — the nullable return is how "no reply" is reported, so a timeout is an ordinary result to handle rather than an exception to catch.

## Registration

Supply your implementation as a type argument:

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddAzureServiceBusRequestReply<MyRequestReplyClient>(options =>
{
    options.ReplyQueueName = "orders-replies";
    options.ReplyTimeout = TimeSpan.FromSeconds(15);
});
```

Bind the options from configuration instead:

```csharp
services.AddAzureServiceBusRequestReply<MyRequestReplyClient>(
    configuration.GetSection("ServiceBus:RequestReply"));
```

Or construct the client yourself with a factory — use this when the client needs a dependency that DI cannot supply by constructor injection, such as a pre-built `ServiceBusClient`:

```csharp
services.AddAzureServiceBusRequestReply(
    options => options.ReplyQueueName = "orders-replies",
    sp => new MyRequestReplyClient(sp.GetRequiredService<ServiceBusClient>()));
```

The client is registered as a **singleton** in every overload. It implements `IAsyncDisposable`, so the container disposes it at shutdown — hold no per-request state on it.

## Options

`RequestReplyOptions`:

| Option | Default | Notes |
|--------|---------|-------|
| `ReplyQueueName` | *(none — required)* | The session-enabled queue replies are read from. Startup fails without it. |
| `ReplyTimeout` | `30s` | How long to wait for a reply before giving up. |
| `RequestTimeToLive` | `60s` | TTL on the outgoing request; `null` leaves it to the namespace default. |
| `MaxConcurrentRequests` | `100` | Ceiling on in-flight requests. Must be between **1 and 10000**. |

Options are validated with `ValidateOnStart`, so both a missing `ReplyQueueName` and an out-of-range `MaxConcurrentRequests` fail at host startup rather than on first use.

### ReplyQueueName is required

There is no sensible default for a reply queue, so the validator rejects an empty one at startup. Set it to a queue with **sessions enabled** — the pattern correlates a reply by session id, and a non-session queue cannot honour `ReceiveReplyAsync(sessionId, …)`.

### Keep RequestTimeToLive above ReplyTimeout

The two are independent, and the framework does not reconcile them. If `RequestTimeToLive` is shorter than `ReplyTimeout`, the request can expire while you are still waiting for its reply — you will wait the full timeout for a reply that can no longer be produced. The defaults (60s TTL, 30s wait) are ordered correctly; preserve that relationship when you change either.

## What's Next

- [Azure Service Bus](./azure-service-bus.md) — the pipeline-integrated Azure transport
- [Transports overview](./index.md) — the transport seam
- [Keyed transport seam](./keyed-transport-seam.md) — resolving a transport by name
