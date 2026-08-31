// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IL2026 // RequiresUnreferencedCode
#pragma warning disable IL3050 // RequiresDynamicCode

using System.Text.Json;

using CloudNative.CloudEvents;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Options.CloudEvents;
using Excalibur.Dispatch.Transport.Aws;
using Excalibur.Dispatch.Transport.Azure;
using Excalibur.Dispatch.Transport.Google;
using Excalibur.Dispatch.Transport.Kafka;
using Excalibur.Dispatch.Transport.RabbitMQ;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.CloudEvents;

/// <summary>
/// Every transport must reach the same decode decision for the same media type. A CloudEvents
/// content type is case-insensitive and may carry parameters, so all of these spellings denote
/// one type and must all yield a parsed JSON payload rather than a raw string.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class CloudEventBodyDecodingShould
{
	private const string Payload = "{\"orderId\":42}";

	/// <summary>
	/// Spellings of a JSON <c>datacontenttype</c>. All denote one media type and must decode identically
	/// on every transport.
	/// </summary>
	public static TheoryData<string> JsonContentTypes() =>
	[
		"application/json",
		"APPLICATION/JSON",
		"Application/Json",
		"application/json; charset=utf-8",
		"APPLICATION/JSON; CHARSET=UTF-8",
	];

	/// <summary>
	/// The structured-mode envelope media type, seen here as a <c>datacontenttype</c>. Only the transports
	/// that do not claim it for envelope detection can reach their body decoder with it; on Event Hubs,
	/// Service Bus and RabbitMQ the same value on the transport content-type selects the structured
	/// formatter instead, which is exercised by the structured-mode facts below.
	/// </summary>
	public static TheoryData<string> StructuredContentTypes() =>
	[
		"application/cloudevents+json",
		"APPLICATION/CLOUDEVENTS+JSON",
		"Application/CloudEvents+JSON",
		"application/cloudevents+json; charset=utf-8",
	];

	public static TheoryData<string> NonJsonContentTypes() =>
	[
		"text/plain",
		"text/plain; charset=utf-8",
	];

	[Theory]
	[MemberData(nameof(JsonContentTypes))]
	public async Task ParseJsonBodiesOnEventHubs(string contentType) =>
		AssertParsed(await RoundTripEventHubs(contentType).ConfigureAwait(true));

	[Theory]
	[MemberData(nameof(NonJsonContentTypes))]
	public async Task LeaveNonJsonBodiesUnparsedOnEventHubs(string contentType) =>
		AssertNotParsed(await RoundTripEventHubs(contentType).ConfigureAwait(true));

	[Theory]
	[MemberData(nameof(JsonContentTypes))]
	public async Task ParseJsonBodiesOnServiceBus(string contentType) =>
		AssertParsed(await RoundTripServiceBus(contentType).ConfigureAwait(true));

	[Theory]
	[MemberData(nameof(NonJsonContentTypes))]
	public async Task LeaveNonJsonBodiesUnparsedOnServiceBus(string contentType) =>
		AssertNotParsed(await RoundTripServiceBus(contentType).ConfigureAwait(true));

	[Theory]
	[MemberData(nameof(JsonContentTypes))]
	[MemberData(nameof(StructuredContentTypes))]
	public async Task ParseJsonBodiesOnSqs(string contentType) =>
		AssertParsed(await RoundTripSqs(contentType).ConfigureAwait(true));

	[Theory]
	[MemberData(nameof(NonJsonContentTypes))]
	public async Task LeaveNonJsonBodiesUnparsedOnSqs(string contentType) =>
		AssertNotParsed(await RoundTripSqs(contentType).ConfigureAwait(true));

	[Theory]
	[MemberData(nameof(JsonContentTypes))]
	public async Task ParseJsonBodiesOnRabbitMq(string contentType) =>
		AssertParsed(await RoundTripRabbitMq(contentType).ConfigureAwait(true));

	[Theory]
	[MemberData(nameof(NonJsonContentTypes))]
	public async Task LeaveNonJsonBodiesUnparsedOnRabbitMq(string contentType) =>
		AssertNotParsed(await RoundTripRabbitMq(contentType).ConfigureAwait(true));

	[Theory]
	[MemberData(nameof(JsonContentTypes))]
	[MemberData(nameof(StructuredContentTypes))]
	public async Task ParseJsonBodiesOnKafka(string contentType) =>
		AssertParsed(await RoundTripKafka(contentType).ConfigureAwait(true));

	[Theory]
	[MemberData(nameof(NonJsonContentTypes))]
	public async Task LeaveNonJsonBodiesUnparsedOnKafka(string contentType) =>
		AssertNotParsed(await RoundTripKafka(contentType).ConfigureAwait(true));

	[Theory]
	[MemberData(nameof(JsonContentTypes))]
	[MemberData(nameof(StructuredContentTypes))]
	public async Task ParseJsonBodiesOnGooglePubSub(string contentType) =>
		AssertParsed(await RoundTripGooglePubSub(contentType).ConfigureAwait(true));

	[Theory]
	[MemberData(nameof(NonJsonContentTypes))]
	public async Task LeaveNonJsonBodiesUnparsedOnGooglePubSub(string contentType) =>
		AssertNotParsed(await RoundTripGooglePubSub(contentType).ConfigureAwait(true));

	private static void AssertParsed(object? data)
	{
		var element = data.ShouldBeOfType<JsonElement>();
		element.ValueKind.ShouldBe(JsonValueKind.Object);
		element.GetProperty("orderId").GetInt32().ShouldBe(42);
	}

	private static void AssertNotParsed(object? data) => data.ShouldNotBeOfType<JsonElement>();

	private static CloudEvent CreateCloudEvent(string contentType) =>
		new(CloudEventsSpecVersion.V1_0)
		{
			Type = "orders.placed",
			Source = new Uri("https://source.excalibur.io"),
			Id = "event-1",
			DataContentType = contentType,
			Data = Payload,
		};

	private static Microsoft.Extensions.Options.IOptions<CloudEventOptions> CreateOptions() =>
		Microsoft.Extensions.Options.Options.Create(new CloudEventOptions
		{
			DefaultSource = new Uri("https://test.excalibur.io"),
			DefaultMode = CloudEventMode.Binary,
		});

	private static async Task<object?> RoundTripEventHubs(string contentType)
	{
		var adapter = new AzureEventHubsCloudEventAdapter(
			CreateOptions(),
			Microsoft.Extensions.Options.Options.Create(new AzureEventHubsCloudEventOptions()));

		var transport = await adapter
			.ToTransportMessageAsync(CreateCloudEvent(contentType), CloudEventMode.Binary, CancellationToken.None)
			.ConfigureAwait(true);

		return (await adapter.FromTransportMessageAsync(transport, CancellationToken.None).ConfigureAwait(true)).Data;
	}

	private static async Task<object?> RoundTripServiceBus(string contentType)
	{
		var adapter = new AzureServiceBusCloudEventAdapter(
			CreateOptions(),
			Microsoft.Extensions.Options.Options.Create(new AzureServiceBusCloudEventOptions()),
			NullLogger<AzureServiceBusCloudEventAdapter>.Instance);

		var transport = await adapter
			.ToTransportMessageAsync(CreateCloudEvent(contentType), CloudEventMode.Binary, CancellationToken.None)
			.ConfigureAwait(true);

		return (await adapter.FromTransportMessageAsync(transport, CancellationToken.None).ConfigureAwait(true)).Data;
	}

	private static async Task<object?> RoundTripSqs(string contentType)
	{
		var adapter = new AwsSqsCloudEventAdapter(
			CreateOptions(),
			NullLogger<AwsSqsCloudEventAdapter>.Instance);

		var transport = await adapter
			.ToTransportMessageAsync(CreateCloudEvent(contentType), CloudEventMode.Binary, CancellationToken.None)
			.ConfigureAwait(true);

		return (await adapter.FromTransportMessageAsync(transport, CancellationToken.None).ConfigureAwait(true)).Data;
	}

	private static async Task<object?> RoundTripRabbitMq(string contentType)
	{
		var adapter = new RabbitMqCloudEventAdapter(
			CreateOptions(),
			Microsoft.Extensions.Options.Options.Create(new RabbitMqCloudEventOptions()),
			NullLogger<RabbitMqCloudEventAdapter>.Instance);

		var transport = await adapter
			.ToTransportMessageAsync(CreateCloudEvent(contentType), CloudEventMode.Binary, CancellationToken.None)
			.ConfigureAwait(true);

		return (await adapter.FromTransportMessageAsync(transport, CancellationToken.None).ConfigureAwait(true)).Data;
	}

	private static async Task<object?> RoundTripKafka(string contentType)
	{
		var adapter = new KafkaCloudEventAdapter(
			CreateOptions(),
			new KafkaCloudEventOptions(),
			NullLogger<KafkaCloudEventAdapter>.Instance);

		var transport = await adapter
			.ToTransportMessageAsync(CreateCloudEvent(contentType), CloudEventMode.Binary, CancellationToken.None)
			.ConfigureAwait(true);

		return (await adapter.FromTransportMessageAsync(transport, CancellationToken.None).ConfigureAwait(true)).Data;
	}

	private static async Task<object?> RoundTripGooglePubSub(string contentType)
	{
		var adapter = new GooglePubSubCloudEventAdapter(
			CreateOptions(),
			new GooglePubSubCloudEventOptions(),
			NullLogger<GooglePubSubCloudEventAdapter>.Instance);

		var transport = await adapter
			.ToTransportMessageAsync(CreateCloudEvent(contentType), CloudEventMode.Binary, CancellationToken.None)
			.ConfigureAwait(true);

		return (await adapter.FromTransportMessageAsync(transport, CancellationToken.None).ConfigureAwait(true)).Data;
	}
}
