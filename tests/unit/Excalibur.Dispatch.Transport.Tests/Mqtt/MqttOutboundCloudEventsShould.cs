// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using CloudNative.CloudEvents;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Mqtt;

using MQTTnet;
using MQTTnet.Protocol;

namespace Excalibur.Dispatch.Transport.Tests.Mqtt;

/// <summary>Exercises outbound CloudEvents through the real named MQTT registration and sender.</summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.CloudEvents")]
[Trait("Pattern", "Regression")]
public sealed partial class MqttOutboundCloudEventsShould
{
	private const string TransportName = "orders-mqtt";
	private const string Topic = "orders/events";
	private const string ResponseTopic = "orders/responses";
	private const string EventType = "com.excalibur.order.submitted.v1";

	[Theory]
	[InlineData(CloudEventMode.Binary, false)]
	[InlineData(CloudEventMode.Binary, true)]
	[InlineData(CloudEventMode.Structured, false)]
	[InlineData(CloudEventMode.Structured, true)]
	public async Task PublishTheConfiguredCloudEventBindingThroughTheNamedSender(
		CloudEventMode mode,
		bool cloudEventsFirst)
	{
		await using var harness = CreateHarness(mode, cloudEventsFirst);
		var message = EventMessage("event-1");
		message.Properties["x-client"] = "checkout";
		message.Properties["id"] = "forged-id";
		message.Properties["type"] = "forged-type";
		message.Properties["x-custom"] = "lower";
		message.Properties["X-Custom"] = "upper";

		var result = await harness.Sender.SendAsync(message, CancellationToken.None);

		result.IsSuccess.ShouldBeTrue();
		result.MessageId.ShouldBe(message.Id);
		var wire = harness.Published.ShouldHaveSingleItem();
		AssertCloudEvent(wire, mode, "event-1");
		wire.Topic.ShouldBe(Topic);
		wire.QualityOfServiceLevel.ShouldBe(MqttQualityOfServiceLevel.ExactlyOnce);
		wire.Retain.ShouldBeTrue("the adapter's configured retain flag must reach the native message");
		Encoding.UTF8.GetString(wire.CorrelationData.ShouldNotBeNull()).ShouldBe("request-7");
		wire.ResponseTopic.ShouldBe(ResponseTopic);
		wire.UserProperties.ShouldContain(property => property.Name == "x-client" && property.Value == "checkout");
		wire.UserProperties.Where(property => property.Name == "x-custom").ShouldHaveSingleItem().Value.ShouldBe("lower");
		wire.UserProperties.Where(property => property.Name == "X-Custom").ShouldHaveSingleItem().Value.ShouldBe("upper");
		wire.UserProperties.ShouldNotContain(property => property.Value == "forged-id" || property.Value == "forged-type");
		wire.UserProperties.ShouldNotContain(property => property.Name == "cloudevent");
	}

	[Fact]
	public async Task PreserveOrdinaryTrafficWhenCloudEventsSupportIsRegistered()
	{
		await using var harness = CreateHarness(CloudEventMode.Binary);
		var message = TransportMessage.FromString("ordinary-body");
		message.ContentType = "text/plain";
		message.CorrelationId = "ordinary-request";
		message.Properties["x-client"] = "ordinary";

		var result = await harness.Sender.SendAsync(message, CancellationToken.None);

		result.IsSuccess.ShouldBeTrue();
		var wire = harness.Published.ShouldHaveSingleItem();
		Encoding.UTF8.GetString(wire.Payload.ToArray()).ShouldBe("ordinary-body");
		wire.ContentType.ShouldBe("text/plain");
		wire.Retain.ShouldBeFalse("CloudEvents retain configuration must not change ordinary messages");
		wire.UserProperties.ShouldHaveSingleItem().Name.ShouldBe("x-client");
		wire.UserProperties[0].Value.ShouldBe("ordinary");
		Encoding.UTF8.GetString(wire.CorrelationData.ShouldNotBeNull()).ShouldBe("ordinary-request");
	}

	[Theory]
	[InlineData(CloudEventMode.Binary)]
	[InlineData(CloudEventMode.Structured)]
	public async Task FormatBooleanExtensionsCanonically(CloudEventMode mode)
	{
		await using var harness = CreateHarness(mode);
		var message = EventMessage("boolean-extension");
		var cloudEvent = (CloudEvent)message.Properties["cloudevent"];
		var flag = CloudEventAttribute.CreateExtension("conformanceflag", CloudEventAttributeType.Boolean);
		cloudEvent[flag] = true;

		var result = await harness.Sender.SendAsync(message, CancellationToken.None);

		result.IsSuccess.ShouldBeTrue();
		var wire = harness.Published.ShouldHaveSingleItem();
		wire.UserProperties.Where(property => property.Name == flag.Name).ShouldHaveSingleItem().Value
			.ShouldBe("true", "MQTT user properties use the CloudEvents canonical Boolean string");
		if (mode == CloudEventMode.Structured)
		{
			using var json = JsonDocument.Parse(wire.Payload.ToArray());
			json.RootElement.GetProperty(flag.Name).ValueKind.ShouldBe(JsonValueKind.True);
		}
	}

	[Theory]
	[InlineData(CloudEventMode.Binary)]
	[InlineData(CloudEventMode.Structured)]
	public async Task FormatIntegerExtensionsIndependentlyOfTheCurrentCulture(CloudEventMode mode)
	{
		await using var harness = CreateHarness(mode);
		var message = EventMessage("integer-extension");
		var cloudEvent = (CloudEvent)message.Properties["cloudevent"];
		var count = CloudEventAttribute.CreateExtension("conformancecount", CloudEventAttributeType.Integer);
		cloudEvent[count] = -42;
		var previousCulture = CultureInfo.CurrentCulture;
		var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
		culture.NumberFormat.NegativeSign = "negative:";

		try
		{
			CultureInfo.CurrentCulture = culture;
			var result = await harness.Sender.SendAsync(message, CancellationToken.None);

			result.IsSuccess.ShouldBeTrue();
			var wire = harness.Published.ShouldHaveSingleItem();
			wire.UserProperties.Where(property => property.Name == count.Name).ShouldHaveSingleItem().Value
				.ShouldBe("-42", "CloudEvents integers use invariant formatting, including the negative sign");
			if (mode == CloudEventMode.Structured)
			{
				using var json = JsonDocument.Parse(wire.Payload.ToArray());
				json.RootElement.GetProperty(count.Name).GetInt32().ShouldBe(-42);
			}
		}
		finally
		{
			CultureInfo.CurrentCulture = previousCulture;
		}
	}

	[Theory]
	[InlineData(CloudEventMode.Binary)]
	[InlineData(CloudEventMode.Structured)]
	public async Task EmitTheTraceParentAttributeExactlyOnce(CloudEventMode mode)
	{
		const string traceParent = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";
		await using var harness = CreateHarness(mode);
		var message = EventMessage("trace-extension");
		var cloudEvent = (CloudEvent)message.Properties["cloudevent"];
		var trace = CloudEventAttribute.CreateExtension("traceparent", CloudEventAttributeType.String);
		cloudEvent[trace] = traceParent;

		var result = await harness.Sender.SendAsync(message, CancellationToken.None);

		result.IsSuccess.ShouldBeTrue();
		var wire = harness.Published.ShouldHaveSingleItem();
		wire.UserProperties.Where(property => property.Name == trace.Name).ShouldHaveSingleItem().Value
			.ShouldBe(traceParent);
	}

	[Theory]
	[InlineData(CloudEventMode.Binary)]
	[InlineData(CloudEventMode.Structured)]
	public async Task EncodeEveryCloudEventInABatch(CloudEventMode mode)
	{
		await using var harness = CreateHarness(mode);
		var first = EventMessage("batch-1");
		var second = EventMessage("batch-2");

		var result = await harness.Sender.SendBatchAsync([first, second], CancellationToken.None);

		result.TotalMessages.ShouldBe(2);
		result.SuccessCount.ShouldBe(2);
		result.FailureCount.ShouldBe(0);
		result.Results.Count.ShouldBe(2);
		harness.Published.Count.ShouldBe(2);
		AssertCloudEvent(harness.Published[0], mode, "batch-1");
		AssertCloudEvent(harness.Published[1], mode, "batch-2");
	}

	[Fact]
	public async Task AccountForCanceledCloudEventBatchInputsWithoutPublishing()
	{
		await using var harness = CreateHarness(CloudEventMode.Binary);
		using var cancellation = new CancellationTokenSource();
		await cancellation.CancelAsync();

		var result = await harness.Sender.SendBatchAsync(
			[EventMessage("canceled-1"), EventMessage("canceled-2")], cancellation.Token);

		result.TotalMessages.ShouldBe(2);
		result.SuccessCount.ShouldBe(0);
		result.FailureCount.ShouldBe(2);
		result.Results.Count.ShouldBe(2);
		result.Results.ShouldAllBe(item => !item.IsSuccess && item.Error != null && item.Error.IsRetryable);
		harness.Published.ShouldBeEmpty();
	}

	[Fact]
	public async Task KeepTheNativeClientOwnedByTheSender()
	{
		await using var harness = CreateHarness(CloudEventMode.Binary);
		_ = await harness.Sender.SendAsync(EventMessage("before-disposal"), CancellationToken.None);

		await harness.Sender.DisposeAsync();
		await harness.Sender.DisposeAsync();

		A.CallTo(() => harness.Client.Dispose()).MustHaveHappenedOnceExactly();
		await Should.ThrowAsync<ObjectDisposedException>(() =>
			harness.Sender.SendAsync(EventMessage("after-disposal"), CancellationToken.None));
		harness.Published.Count.ShouldBe(1);
	}

	private static TransportMessage EventMessage(string id) => new()
	{
		Id = "transport-" + id,
		Body = "unencoded placeholder"u8.ToArray(),
		ContentType = "text/plain",
		CorrelationId = "request-7",
		Properties = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["cloudevent"] = new CloudEvent
			{
				Id = id,
				Type = EventType,
				Source = new Uri("urn:checkout"),
				Subject = "order-42",
				DataContentType = "application/json",
				Data = new OrderPayload("order-42"),
			},
		},
	};

	private static void AssertCloudEvent(MqttApplicationMessage wire, CloudEventMode mode, string id)
	{
		wire.UserProperties.Where(property => property.Name == "specversion").ShouldHaveSingleItem().Value.ShouldBe("1.0");
		wire.UserProperties.Where(property => property.Name == "id").ShouldHaveSingleItem().Value.ShouldBe(id);
		wire.UserProperties.Where(property => property.Name == "type").ShouldHaveSingleItem().Value.ShouldBe(EventType);
		wire.UserProperties.Where(property => property.Name == "source").ShouldHaveSingleItem().Value.ShouldBe("urn:checkout");
		wire.UserProperties.Where(property => property.Name == "subject").ShouldHaveSingleItem().Value.ShouldBe("order-42");
		wire.UserProperties.ShouldNotContain(property => property.Name.StartsWith("ce-", StringComparison.Ordinal));
		using var json = JsonDocument.Parse(wire.Payload.ToArray());
		if (mode == CloudEventMode.Structured)
		{
			wire.ContentType.ShouldBe("application/cloudevents+json");
			json.RootElement.GetProperty("id").GetString().ShouldBe(id);
			json.RootElement.GetProperty("type").GetString().ShouldBe(EventType);
			json.RootElement.GetProperty("data").GetProperty("orderId").GetString().ShouldBe("order-42");
		}
		else
		{
			wire.ContentType.ShouldBe("application/json");
			json.RootElement.GetProperty("orderId").GetString().ShouldBe("order-42");
		}
	}

	private static SenderHarness CreateHarness(CloudEventMode mode, bool cloudEventsFirst = false)
	{
		var client = A.Fake<IMqttClient>();
		A.CallTo(() => client.IsConnected).Returns(true);
		var published = new List<MqttApplicationMessage>();
		A.CallTo(() => client.PublishAsync(A<MqttApplicationMessage>._, A<CancellationToken>._))
			.Invokes((MqttApplicationMessage message, CancellationToken _) => published.Add(message))
			.Returns(new MqttClientPublishResult(0, MqttClientPublishReasonCode.Success, null, []));
		var connection = A.Fake<IMqttConnectionProvider>();
		A.CallTo(() => connection.CreateClient()).Returns(client);

		var services = new ServiceCollection();
		services.AddLogging();
		services.AddKeyedSingleton<IMqttConnectionProvider>(TransportName, connection);
		if (cloudEventsFirst)
		{
			services.AddCloudEventsForMqtt(options => options.Retain = true, options => options.DefaultMode = mode);
		}

		services.AddMqttTransport(TransportName, options =>
		{
			options.Host = "broker.example.test";
			options.ClientId = "cloud-event-producer";
			options.Topic = Topic;
			options.QualityOfService = MqttQualityOfService.ExactlyOnce;
			options.ResponseTopic = ResponseTopic;
			options.RequireTls = false;
		});
		services.AddDispatch(builder => builder.AddCloudEvents(PayloadContext.Default));
		if (!cloudEventsFirst)
		{
			services.AddCloudEventsForMqtt(options => options.Retain = true, options => options.DefaultMode = mode);
		}

		var provider = services.BuildServiceProvider();
		return new SenderHarness(provider, provider.GetRequiredKeyedService<ITransportSender>(TransportName), client, published);
	}

	internal sealed record OrderPayload([property: JsonPropertyName("orderId")] string OrderId);

	[JsonSerializable(typeof(OrderPayload))]
	[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
	internal sealed partial class PayloadContext : JsonSerializerContext
	{
	}

	private sealed class SenderHarness(
		ServiceProvider provider,
		ITransportSender sender,
		IMqttClient client,
		List<MqttApplicationMessage> published) : IAsyncDisposable
	{
		public ITransportSender Sender { get; } = sender;
		public IMqttClient Client { get; } = client;
		public List<MqttApplicationMessage> Published { get; } = published;
		public ValueTask DisposeAsync() => provider.DisposeAsync();
	}
}
