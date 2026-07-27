# Excalibur.Dispatch.Transport.Mqtt

MQTT transport for Excalibur. Publish/subscribe messaging over the MQTTnet client, with QoS 0/1/2 delivery
mapping and MQTT-5 request/reply.

## What this package provides (W2 scaffold)

- `MqttOptions` — broker host/port, client id, topic, QoS level, TLS, optional shared-subscription and
  MQTT-5 response-topic, credentials, validated at startup (`ValidateOnStart`).
- `MqttQualityOfService` — QoS 0/1/2 → delivery-guarantee mapping.
- `IMqttConnectionProvider` — creates MQTT clients and builds their connection options.
- `AddMqttTransport(...)` — registers the connection provider and validated options.

## Capability notes (honest boundary)

MQTT is pub/sub, not a competing-consumer broker: without MQTT-5 shared subscriptions every subscriber
receives every message (`UseSharedSubscription` opts in on a supporting broker). Exactly-once delivery
requires QoS 2; ordering and exactly-once are not guaranteed under QoS 0/1.

## Usage

```csharp
services.AddMqttTransport("mqtt", o =>
{
    o.Host = "localhost";
    o.Port = 1883;
    o.ClientId = "dispatch-1";
    o.Topic = "dispatch/events";
    o.QualityOfService = MqttQualityOfService.AtLeastOnce;
});
```

Credentials (`Username`/`Password`) must come from configuration or a secret manager — never commit values.
