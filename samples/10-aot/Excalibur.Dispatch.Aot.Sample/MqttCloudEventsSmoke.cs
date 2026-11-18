// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Buffers;
using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

using CloudNative.CloudEvents;

using Excalibur.Dispatch.Aot.Sample.Messages;
using Excalibur.Dispatch.Aot.Sample.Serialization;
using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Mqtt;

using Microsoft.Extensions.DependencyInjection;

using MQTTnet;
using MQTTnet.Formatter;
using MQTTnet.Protocol;

namespace Excalibur.Dispatch.Aot.Sample;

/// <summary>Verifies the real MQTT CloudEvents send path from a native executable to a local broker.</summary>
internal static class MqttCloudEventsSmoke
{
    private const string EventType = "com.excalibur.inventory.updated.v1";
    private const string CorrelationId = "native-mqtt-request";

    public static async Task RunAsync()
    {
        Require(!RuntimeFeature.IsDynamicCodeSupported, "this control requires the published native executable");
        Require(!JsonSerializer.IsReflectionEnabledByDefault, "reflection-based JSON must remain disabled");

        var host = Environment.GetEnvironmentVariable("DISPATCH_MQTT_HOST")
            ?? throw new InvalidOperationException("Set DISPATCH_MQTT_HOST to the local smoke broker address.");
        Require(string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || (IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address)),
            "the plaintext smoke broker must be on loopback");
        Require(int.TryParse(Environment.GetEnvironmentVariable("DISPATCH_MQTT_PORT"),
            NumberStyles.None, CultureInfo.InvariantCulture, out var port) && port is > 0 and <= 65535,
            "set DISPATCH_MQTT_PORT to the local smoke broker port");

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        await VerifyBindingAsync(host, port, CloudEventMode.Binary, timeout.Token).ConfigureAwait(false);
        await VerifyBindingAsync(host, port, CloudEventMode.Structured, timeout.Token).ConfigureAwait(false);
        Console.WriteLine("Native MQTT CloudEvents: binary and structured messages observed at the broker with generated payloads and reflection disabled.");
    }

    private static async Task VerifyBindingAsync(string host, int port, CloudEventMode mode, CancellationToken cancellationToken)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var topic = "excalibur/native-smoke/" + suffix;
        var responseTopic = topic + "/reply";
        var received = Channel.CreateBounded<MqttApplicationMessage>(new BoundedChannelOptions(1)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait,
        });

        using var observer = new MqttClientFactory().CreateMqttClient();
        observer.ApplicationMessageReceivedAsync += args =>
        {
            Require(received.Writer.TryWrite(args.ApplicationMessage), "the broker delivered an unexpected extra message");
            return Task.CompletedTask;
        };
        await ConnectObserverAsync(observer, host, port, suffix, topic, cancellationToken).ConfigureAwait(false);

        await using var provider = CreateProvider(host, port, suffix, topic, responseTopic, mode);
        var sender = provider.GetRequiredKeyedService<ITransportSender>("native-smoke");
        var eventId = "inventory-" + suffix;
        var message = CreateMessage(suffix, eventId);
        var sent = await sender.SendAsync(message, cancellationToken).ConfigureAwait(false);
        Require(sent.IsSuccess && sent.MessageId == message.Id, "the production sender did not confirm publish");
        var wire = await received.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        VerifyWire(wire, mode, eventId, topic, responseTopic);
    }

    private static async Task ConnectObserverAsync(IMqttClient observer, string host, int port,
        string suffix, string topic, CancellationToken cancellationToken)
    {
        var connected = await observer.ConnectAsync(new MqttClientOptionsBuilder()
            .WithProtocolVersion(MqttProtocolVersion.V500)
            .WithTcpServer(host, port)
            .WithClientId("native-observer-" + suffix)
            .WithCleanStart()
            .Build(), cancellationToken).ConfigureAwait(false);
        Require(connected.ResultCode == MqttClientConnectResultCode.Success, "the observer did not connect");
        var subscribed = await observer.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter(filter => filter.WithTopic(topic)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .WithRetainAsPublished(true))
            .Build(), cancellationToken).ConfigureAwait(false);
        Require(subscribed.Items.Count == 1
            && subscribed.Items.Single().ResultCode == MqttClientSubscribeResultCode.GrantedQoS1,
            "the observer subscription was not granted at QoS 1");
    }

    private static ServiceProvider CreateProvider(string host, int port, string suffix,
        string topic, string responseTopic, CloudEventMode mode)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDispatch(builder => builder.AddCloudEvents(AppJsonSerializerContext.Default));
        services.AddCloudEventsForMqtt(options => options.Retain = true, options => options.DefaultMode = mode);
        services.AddMqttTransport("native-smoke", options =>
        {
            options.Host = host;
            options.Port = port;
            options.ClientId = "native-producer-" + suffix;
            options.Topic = topic;
            options.ResponseTopic = responseTopic;
            options.QualityOfService = MqttQualityOfService.AtLeastOnce;
            options.PersistentSession = false;
            options.RequireTls = false; // The explicit loopback restriction above makes this a local control.
        });
        return services.BuildServiceProvider();
    }

    private static TransportMessage CreateMessage(string suffix, string eventId) =>
        new()
        {
            Id = "transport-" + suffix,
            Body = "unencoded placeholder"u8.ToArray(),
            ContentType = "text/plain",
            CorrelationId = CorrelationId,
            Properties = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["cloudevent"] = new CloudEvent
                {
                    Id = eventId,
                    Source = new Uri("urn:excalibur:native-mqtt"),
                    Type = EventType,
                    Subject = "SKU-AOT-MQTT",
                    DataContentType = "application/json",
                    Data = new MqttInventoryPayload
                    {
                        Sku = "SKU-AOT-MQTT",
                        NewQuantity = 42,
                    },
                },
                ["x-client"] = "native-smoke",
            },
        };

    private static void VerifyWire(MqttApplicationMessage wire, CloudEventMode mode, string eventId, string topic, string responseTopic)
    {
        Require(wire.Topic == topic && wire.ResponseTopic == responseTopic, "MQTT routing metadata was lost");
        Require(wire.QualityOfServiceLevel == MqttQualityOfServiceLevel.AtLeastOnce && wire.Retain,
            "MQTT QoS or retain configuration was lost");
        Require(Encoding.UTF8.GetString(wire.CorrelationData ?? []) == CorrelationId, "MQTT correlation data was lost");
        Require(wire.UserProperties.All(property => property.Name != "cloudevent"), "the in-process marker leaked onto the wire");
        RequireProperty(wire, "specversion", "1.0");
        RequireProperty(wire, "id", eventId);
        RequireProperty(wire, "source", "urn:excalibur:native-mqtt");
        RequireProperty(wire, "type", EventType);
        RequireProperty(wire, "subject", "SKU-AOT-MQTT");
        RequireProperty(wire, "x-client", "native-smoke");

        using var json = JsonDocument.Parse(wire.Payload.ToArray());
        var payload = json.RootElement;
        if (mode == CloudEventMode.Structured)
        {
            Require(wire.ContentType == "application/cloudevents+json", "structured content type was lost");
            Require(payload.GetProperty("id").GetString() == eventId
                && payload.GetProperty("type").GetString() == EventType, "structured event metadata was lost");
            payload = payload.GetProperty("data");
        }
        else
        {
            Require(wire.ContentType == "application/json", "binary payload content type was lost");
        }

        Require(payload.GetProperty("sku").GetString() == "SKU-AOT-MQTT"
            && payload.GetProperty("newQuantity").GetInt32() == 42,
            "the source-generated payload did not reach the broker");
    }

    private static void RequireProperty(MqttApplicationMessage wire, string name, string value)
    {
        var properties = wire.UserProperties.Where(property => property.Name == name).ToList();
        Require(properties.Count == 1 && properties[0].Value == value,
            "CloudEvents MQTT user property was absent, duplicated or incorrect: " + name);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException("Native MQTT CloudEvents: " + message);
        }
    }
}
