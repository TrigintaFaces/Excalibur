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

## CloudEvents

**Inbound CloudEvents are decoded automatically.** Every receiver on this transport is wrapped by the
framework's decoding decorator, so a message carrying CloudEvents markers arrives with the decoded event
attached to it — body and properties untouched, and a message that is not a CloudEvent passes through
unchanged. Every message in the batch is delivered, including a malformed one: it arrives carrying a
decode error instead of a decoded event, never dropped. There is nothing to register.

**Outbound CloudEvents formatting is opt-in.** It is not applied unless you register it:

```csharp
services.AddCloudEventsForMqtt(mqtt => { /* MqttCloudEventOptions */ });
```

Both CloudEvents bindings are supported on the send path, per the CNCF CloudEvents MQTT protocol binding:
**structured mode** carries the whole event as a JSON payload with `ContentType` set to
`application/cloudevents+json`, and **binary mode** carries the attributes as MQTT v5 user properties named
exactly as the attributes are. The naming is not ours to choose — renaming it would make our events
unreadable to conformant consumers and theirs unreadable to us.

Registering the bundled encoder is annotated for trimming and ahead-of-time builds — it serializes
payloads with reflection-based JSON. Supply your own `ICloudEventEncoder<TOutbound>` over a
source-generated serializer to avoid the requirement. **This applies to the send path only; inbound
decoding is trim-safe and ahead-of-time-safe.**

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
