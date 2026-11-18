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

**Outbound CloudEvents use the registered MQTT encoder.** Register core payload serialization and
`AddCloudEventsForMqtt`, then attach a `CloudEvent` under `TransportMessage.Properties["cloudevent"]`.
The named sender honors `CloudEventOptions.DefaultMode`: binary mode carries the attributes as MQTT-5
user properties using their bare CloudEvents names, while structured mode carries the complete JSON
event with `application/cloudevents+json` content type. `MqttCloudEventOptions.Retain` applies to these
outbound events. Ordinary messages retain their existing payload and application properties.

The sender supplies the configured topic, QoS, response topic and correlation data. Unrelated application
properties remain available; they cannot replace encoder-provided attributes or introduce absent standard
CloudEvents attributes. The in-process `cloudevent` marker is consumed by the sender and never emitted as a
user property on an encoded event.

For trimming and Native AOT, configure the core serializer with source-generated metadata for every type
carried in `CloudEvent.Data`. The MQTT encoder uses that resolver. Publish and run the consuming
application to verify its complete dependency and payload configuration.

```csharp
using System.Text.Json.Serialization;
using CloudNative.CloudEvents;
using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Transport;
using Microsoft.Extensions.DependencyInjection;

public static class MqttCloudEventExample
{
    public static void Configure(IServiceCollection services)
    {
        services.AddDispatch(builder => builder.AddCloudEvents(OrderJsonContext.Default));
        services.AddCloudEventsForMqtt(
            mqtt => mqtt.Retain = true,
            cloudEvents => cloudEvents.DefaultMode = CloudEventMode.Binary);
    }

    // Invoke only after the host has registered AddMqttTransport("mqtt", ...)
    // and built its provider. The caller owns that provider and its disposal.
    public static async Task SendAsync(IServiceProvider provider, CancellationToken cancellationToken)
    {
        var sender = provider.GetRequiredKeyedService<ITransportSender>("mqtt");
        await sender.SendAsync(new TransportMessage
        {
            Properties = new Dictionary<string, object>
            {
                ["cloudevent"] = new CloudEvent
                {
                    Id = "order-created-42",
                    Source = new Uri("urn:orders"),
                    Type = "com.example.order.created.v1",
                    DataContentType = "application/json",
                    Data = new OrderCreated("order-42"),
                },
            },
        }, cancellationToken);
    }
}

public sealed record OrderCreated(string OrderId);

[JsonSerializable(typeof(OrderCreated))]
internal partial class OrderJsonContext : JsonSerializerContext { }
```

The example uses `CloudNative.CloudEvents`, `Excalibur.Dispatch.CloudEvents`,
`Excalibur.Dispatch.Transport`, `Microsoft.Extensions.DependencyInjection`, and
`System.Text.Json.Serialization`.

## Usage

```csharp
using Excalibur.Dispatch.Transport.Mqtt;
using Microsoft.Extensions.DependencyInjection;

public static class MqttRegistration
{
    public static void Configure(IServiceCollection services)
    {
        services.AddMqttTransport("mqtt", o =>
        {
            o.Host = "localhost";
            o.Port = 1883;
            o.ClientId = "dispatch-1";
            o.Topic = "dispatch/events";
            o.QualityOfService = MqttQualityOfService.AtLeastOnce;
            o.RequireTls = false; // Local development only; configure TLS for a remote broker.
        });
    }
}
```

Credentials (`Username`/`Password`) must come from configuration or a secret manager — never commit values.

## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.
