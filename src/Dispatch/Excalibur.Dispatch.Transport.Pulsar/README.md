# Excalibur.Dispatch.Transport.Pulsar

Apache Pulsar transport for the Excalibur framework, built on the [DotPulsar](https://github.com/apache/pulsar-dotpulsar) client.

Provides competing-consumer message streaming with a subscription model that mirrors the framework's
consumer-group transport semantics.

## CloudEvents

**Inbound CloudEvents are decoded automatically.** Every receiver on this transport is wrapped by the
framework's decoding decorator, so a message carrying CloudEvents markers arrives with the decoded event
attached to it — the body and properties are untouched, and a message that is not a CloudEvent passes
through unchanged. Every message in the batch is delivered, including a malformed one: it arrives carrying a
decode error instead of a decoded event, never dropped. There is nothing to register and no option to set.

**Outbound CloudEvents are opt-in per message.** Attach the event to the message you were going to send
anyway:

```csharp
await sender.SendAsync(message.WithCloudEvent(cloudEvent), cancellationToken);
```

The event is published in CloudEvents **structured mode** — the whole event becomes a JSON document in the
body under the `application/cloudevents+json` content type. A message you do not call `WithCloudEvent` on
reaches the wire byte-identical, so ordinary traffic is unaffected, and nothing produces a `CloudEvent` for
you. This path needs no trimming or ahead-of-time annotation: the envelope is written with a UTF-8 JSON
writer rather than a reflection-based serializer.

## Usage

```csharp
services.AddPulsarTransport("events", pulsar =>
{
    pulsar.ServiceUrl("pulsar://localhost:6650")
          .Topic("orders")
          .SubscriptionName("order-processors")
          .SubscriptionType(PulsarSubscriptionType.Shared);
});
```

## Subscription types

`SubscriptionName` is the durable consumer identity (the Pulsar analog of a Kafka consumer group).
`SubscriptionType` controls how messages are distributed across consumers sharing that subscription:

| Type        | Behavior                                                            |
| ----------- | ------------------------------------------------------------------- |
| `Shared`    | Competing consumers, round-robin delivery (default).                |
| `Exclusive` | A single consumer holds the subscription.                           |
| `Failover`  | One active consumer; others stand by.                               |
| `KeyShared` | Same-key messages go to the same consumer, preserving key ordering. |

## Scope

This package registers the Pulsar transport **primitives** — a keyed `ITransportSender` and
`ITransportReceiver` for sending, receiving, and acknowledging messages directly against a Pulsar broker.
High-level integration into the dispatch pipeline (publishing and consuming typed dispatch messages
end-to-end through a transport adapter) is provided separately.

## Capabilities

Request/reply is **not** natively supported. Validation fails fast at startup on invalid configuration.
