---
sidebar_position: 8
title: Ordering Validation
description: Enforce strictly-increasing per-key message ordering on receive paths that carry a native transport sequence.
---

# Ordering Validation

`OrderingValidationMiddleware` enforces strictly-increasing per-key ordering for messages that arrived
through a receive path where ordering is enforced. It is **fail-closed**: such a message carrying an
out-of-order sequence, or none at all, throws `OutOfOrderMessageException` rather than passing silently.

## Before You Start

- **.NET 10.0**
- Install the required package:
  ```bash
  dotnet add package Excalibur.Dispatch
  ```
- Familiarity with [built-in middleware](./built-in.md) and your transport's receive-to-dispatch bridge

## The stamp is yours to call

The middleware only ever sees a context that has already been marked. **Nothing in the framework calls
the stamp for you** — there is no first-party "receive boundary" type that holds both a
`TransportReceivedMessage` and an `IMessageContext`, because the transports produce received messages and
your bridge is what turns one into a dispatch. That bridge is the one place holding both, so it is the
one place the stamp can live.

Call `TransportOrderingMetadata.TryStampOrdering(received, context)` from that bridge, before dispatching:

```csharp
using Excalibur.Dispatch.Transport;

// Your Kafka/Azure Service Bus consumer loop, after receiving a message and before dispatching it.
await foreach (var received in receiver.ReceiveAsync(cancellationToken))
{
    var context = messageContextFactory.Create();

    // Reads the transport's native monotonic sequence (Kafka offset, Azure Service Bus
    // SequenceNumber) out of TransportReceivedMessage.ProviderData and stamps it onto the
    // context. Also marks the context as having entered an ordered receive path -- this is
    // what scopes the middleware to these messages, wherever OrderingValidationMiddleware is
    // registered.
    TransportOrderingMetadata.TryStampOrdering(received, context);

    var message = deserializer.Deserialize(received);
    await dispatcher.DispatchAsync(message, context, cancellationToken);
}
```

`TryStampOrdering` returns `true` when a native sequence was found and stamped, `false` when the
transport carries none (for example Google Pub/Sub, whose ordering key is a service-enforced string,
not a sequence). Either way, the context is marked as having entered an ordered receive path — a
transport that stops supplying its sequence must fail loudly, not silently stop being validated.

## Register the middleware

```csharp
services.AddDispatch(typeof(Program).Assembly); // or the parameterless services.AddDispatch()
services.AddOrderingValidation();
```

This registers globally: there is no per-pipeline middleware selector, so the middleware sees every
dispatch in the process. What scopes it to the messages it is about is the receive-path marking above,
not where you register it. A message that never entered a marked receive path — an outbound send, an
in-process command — passes through unchanged.

:::warning Composed with `AddDispatch(configure)`, this middleware does not currently activate

`AddOrderingValidation()` must be paired with the assembly-scanning `AddDispatch(...)` overload shown
above. Composed instead with the builder-lambda form —

```csharp
// NOT YET SUPPORTED for AddOrderingValidation() -- see the warning above.
services.AddDispatch(dispatch => dispatch.AddHandlersFromAssembly(typeof(Program).Assembly));
services.AddOrderingValidation();
```

— the middleware is registered in the container but does not run: nothing throws, nothing logs, and
out-of-order messages are silently accepted. If your composition already uses the builder-lambda form
for other middleware, add ordering validation through the assembly-scanning call above in the same
composition; the two are not mutually exclusive. This is a known gap, not an ordering choice you can
work around by moving the call.
:::

## Which transports carry a native sequence

| Transport | Native sequence | `TryStampOrdering` result |
|-----------|-----------------|----------------------------|
| Kafka | Partition offset | Stamped automatically |
| Azure Service Bus | `SequenceNumber` | Stamped automatically |
| Google Pub/Sub | None (service-enforced ordering key, not a sequence) | Returns `false`; ordering marked-but-unstamped |
| RabbitMQ, AWS SQS, and other transports without a native monotonic sequence | None | Returns `false`; ordering marked-but-unstamped |

For a transport with no native sequence, either don't call `TryStampOrdering` at all (the message never
enters an ordered receive path and the middleware passes it through), or stamp one yourself with
`OrderingContextExtensions.SetOrderingSequence(context, sequence, orderingKey)` after marking the context
with `OrderingContextExtensions.MarkOrderingEnforced(context)` — for example when your own producer
embeds an application-level sequence number in the message body.

## Failure mode

```
OutOfOrderMessageException
```

Thrown when a message that entered a marked receive path arrives with no sequence, or with a sequence
that is not strictly greater than the last one seen for its ordering key. A per-key high-water mark is
tracked by a single stateful registration; registering `AddOrderingValidation()` more than once is
idempotent.

## See Also

- [Built-in Middleware](./built-in.md) -- The full middleware catalog and recommended pipeline order
- [Transports](../transports/index.md) -- Transport-specific receive paths and native capabilities
