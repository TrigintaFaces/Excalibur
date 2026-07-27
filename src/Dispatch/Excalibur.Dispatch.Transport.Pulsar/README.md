# Excalibur.Dispatch.Transport.Pulsar

Apache Pulsar transport for the Excalibur framework, built on the [DotPulsar](https://github.com/apache/pulsar-dotpulsar) client.

Provides competing-consumer message streaming with a subscription model that mirrors the framework's
consumer-group transport semantics.

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
