// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IL2026 // RequiresUnreferencedCode
#pragma warning disable IL3050 // RequiresDynamicCode

using CloudNative.CloudEvents;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Options.CloudEvents;
using Excalibur.Dispatch.Transport.Google;

using Google.Cloud.PubSub.V1;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.CloudEvents;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class GooglePubSubCloudEventAdapterShould
{
	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new GooglePubSubCloudEventAdapter(
				null!,
				new GooglePubSubCloudEventOptions(),
				NullLogger<GooglePubSubCloudEventAdapter>.Instance));
	}

	[Fact]
	public void ThrowWhenPubSubOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new GooglePubSubCloudEventAdapter(
				Microsoft.Extensions.Options.Options.Create(new CloudEventOptions()),
				null!,
				NullLogger<GooglePubSubCloudEventAdapter>.Instance));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new GooglePubSubCloudEventAdapter(
				Microsoft.Extensions.Options.Options.Create(new CloudEventOptions()),
				new GooglePubSubCloudEventOptions(),
				null!));
	}

	[Fact]
	public async Task ConvertStructuredCloudEventRoundTrip()
	{
		var adapter = CreateAdapter();
		var cloudEvent = new CloudEvent(CloudEventsSpecVersion.V1_0)
		{
			Type = "orders.created",
			Source = new Uri("https://source.excalibur.io"),
			Id = "evt-structured-1",
			Data = new { orderId = "A-1" },
			DataContentType = "application/json",
		};

		var message = await adapter.ToTransportMessageAsync(cloudEvent, CloudEventMode.Structured, CancellationToken.None);
		var roundTrip = await adapter.FromTransportMessageAsync(message, CancellationToken.None);

		message.Attributes.ShouldContainKey("content-type");
		message.Attributes["content-type"].ShouldContain("cloudevents");
		roundTrip.Id.ShouldBe("evt-structured-1");
		roundTrip.Type.ShouldBe("orders.created");
	}

	[Fact]
	public async Task ConvertBinaryCloudEventRoundTripAndRestoreDispatchExtensions()
	{
		var adapter = CreateAdapter();
		var cloudEvent = new CloudEvent(CloudEventsSpecVersion.V1_0)
		{
			Type = "orders.shipped",
			Source = new Uri("https://source.excalibur.io"),
			Id = "evt-binary-1",
			Data = "payload",
			DataContentType = "text/plain",
		};
		cloudEvent["correlationid"] = "corr-42";
		cloudEvent["attempt"] = "2";

		var message = await adapter.ToTransportMessageAsync(cloudEvent, CloudEventMode.Binary, CancellationToken.None);
		var roundTrip = await adapter.FromTransportMessageAsync(message, CancellationToken.None);

		message.Attributes.ShouldContainKey("ce-specversion");
		message.Attributes.ShouldContainKey("ce-type");
		message.Attributes.ShouldContainKey("ce-source");
		message.Attributes.ShouldContainKey("ce-id");
		message.Attributes.ShouldContainKey("ce-attempt");
		message.Attributes.ShouldContainKey("dispatch-correlationid");
		roundTrip["attempt"]?.ToString().ShouldBe("2");
		roundTrip["correlationid"]?.ToString().ShouldBe("corr-42");
		roundTrip["dispatchcorrelationid"]?.ToString().ShouldBe("corr-42");
	}

	[Fact]
	public async Task SetOrderingKeyFromPartitionKeyWhenEnabled()
	{
		var adapter = CreateAdapter(pubSubOptions: new GooglePubSubCloudEventOptions { UseOrderingKeys = true });
		var cloudEvent = new CloudEvent(CloudEventsSpecVersion.V1_0)
		{
			Type = "orders.partitioned",
			Source = new Uri("https://source.excalibur.io"),
			Id = "evt-binary-2",
			Data = "payload",
		};
		cloudEvent["partitionkey"] = "order-123";

		var message = await adapter.ToTransportMessageAsync(cloudEvent, CloudEventMode.Binary, CancellationToken.None);

		message.OrderingKey.ShouldBe("order-123");
	}

	[Fact]
	public async Task TryDetectModeReturnStructuredWhenStructuredPayload()
	{
		var adapter = CreateAdapter();
		var cloudEvent = new CloudEvent(CloudEventsSpecVersion.V1_0)
		{
			Type = "orders.detect",
			Source = new Uri("https://source.excalibur.io"),
			Id = "evt-detect-1",
			Data = new { x = 1 },
			DataContentType = "application/json",
		};
		var message = await adapter.ToTransportMessageAsync(cloudEvent, CloudEventMode.Structured, CancellationToken.None);

		var mode = await GooglePubSubCloudEventAdapter.TryDetectMode(message, CancellationToken.None);

		mode.ShouldBe(CloudEventMode.Structured);
	}

	[Fact]
	public async Task TryDetectModeReturnBinaryWhenBinaryAttributesPresent()
	{
		var adapter = CreateAdapter();
		var cloudEvent = new CloudEvent(CloudEventsSpecVersion.V1_0)
		{
			Type = "orders.detect.binary",
			Source = new Uri("https://source.excalibur.io"),
			Id = "evt-detect-2",
			Data = "payload",
		};
		var message = await adapter.ToTransportMessageAsync(cloudEvent, CloudEventMode.Binary, CancellationToken.None);

		var mode = await GooglePubSubCloudEventAdapter.TryDetectMode(message, CancellationToken.None);

		mode.ShouldBe(CloudEventMode.Binary);
	}

	[Fact]
	public async Task ThrowJsonExceptionWhenBinaryAttributesAreIncompleteAndPayloadIsNotJson()
	{
		var adapter = CreateAdapter();
		var message = new PubsubMessage
		{
			Data = global::Google.Protobuf.ByteString.CopyFromUtf8("not-a-cloudevent")
		};
		message.Attributes["ce-specversion"] = "1.0";
		message.Attributes["ce-type"] = "orders.created";
		message.Attributes["ce-source"] = "https://source.excalibur.io";
		// ce-id intentionally missing

		await Should.ThrowAsync<System.Text.Json.JsonException>(() =>
			adapter.FromTransportMessageAsync(message, CancellationToken.None));
	}

	private static GooglePubSubCloudEventAdapter CreateAdapter(GooglePubSubCloudEventOptions? pubSubOptions = null)
	{
		var options = Microsoft.Extensions.Options.Options.Create(new CloudEventOptions
		{
			DefaultSource = new Uri("https://default.excalibur.io"),
			DefaultMode = CloudEventMode.Structured,
			DispatchExtensionPrefix = "dispatch",
		});

		return new GooglePubSubCloudEventAdapter(
			options,
			pubSubOptions ?? new GooglePubSubCloudEventOptions(),
			NullLogger<GooglePubSubCloudEventAdapter>.Instance);
	}
}

#pragma warning restore IL2026
#pragma warning restore IL3050
