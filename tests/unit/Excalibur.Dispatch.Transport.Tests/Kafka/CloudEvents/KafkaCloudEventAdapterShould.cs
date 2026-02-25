// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IL2026 // RequiresUnreferencedCode
#pragma warning disable IL3050 // RequiresDynamicCode

using System.Text;
using System.Text.Json;

using CloudNative.CloudEvents;

using Confluent.Kafka;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Options.CloudEvents;
using Excalibur.Dispatch.Transport.Kafka;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.CloudEvents;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class KafkaCloudEventAdapterShould
{
	private readonly KafkaCloudEventAdapter _adapter;

	public KafkaCloudEventAdapterShould()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new CloudEventOptions
		{
			DefaultSource = new Uri("https://test.excalibur.io"),
			DefaultMode = CloudEventMode.Structured,
			DispatchExtensionPrefix = "dispatch",
		});

		_adapter = new KafkaCloudEventAdapter(
			options,
			new KafkaCloudEventOptions { PartitioningStrategy = KafkaPartitioningStrategy.CorrelationId },
			NullLogger<KafkaCloudEventAdapter>.Instance);
	}

	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new KafkaCloudEventAdapter(
				null!,
				new KafkaCloudEventOptions(),
				NullLogger<KafkaCloudEventAdapter>.Instance));
	}

	[Fact]
	public async Task ConvertBinaryAndRoundTripWithoutDispatchMetadata()
	{
		// Arrange
		var cloudEvent = new CloudEvent(CloudEventsSpecVersion.V1_0)
		{
			Type = "orders.created",
			Source = new Uri("https://source.excalibur.io"),
			Id = "event-1",
			Data = "payload",
			DataContentType = "text/plain",
		};
		cloudEvent["attempt"] = "2";

		// Act
		var message = await _adapter.ToTransportMessageAsync(cloudEvent, CloudEventMode.Binary, CancellationToken.None);
		var roundTrip = await _adapter.FromTransportMessageAsync(message, CancellationToken.None);

		// Assert
		message.Headers.TryGetLastBytes("ce-specversion", out _).ShouldBeTrue();
		message.Headers.TryGetLastBytes("ce-id", out _).ShouldBeTrue();
		message.Headers.TryGetLastBytes("ce-attempt", out var attemptBytes).ShouldBeTrue();
		Encoding.UTF8.GetString(attemptBytes!).ShouldBe("2");

		roundTrip.Type.ShouldBe("orders.created");
		roundTrip.Id.ShouldBe("event-1");
		roundTrip["attempt"]?.ToString().ShouldBe("2");
	}

	[Fact]
	public async Task UseCorrelationIdAsPartitionKeyWhenConfigured()
	{
		// Arrange
		var cloudEvent = new CloudEvent(CloudEventsSpecVersion.V1_0)
		{
			Type = "orders.created",
			Source = new Uri("https://source.excalibur.io"),
			Id = "event-2",
			Data = "payload",
		};
		cloudEvent["correlationid"] = "corr-123";

		// Act
		var message = await _adapter.ToTransportMessageAsync(cloudEvent, CloudEventMode.Binary, CancellationToken.None);

		// Assert
		message.Key.ShouldBe("corr-123");
	}

	[Fact]
	public async Task DetectStructuredModeFromJsonPayloadWithoutContentTypeHeader()
	{
		// Arrange
		var message = new Message<string, string>
		{
			Headers = new Headers(),
			Value = """{"specversion":"1.0","type":"orders.created","source":"https://source","id":"1"}""",
		};

		// Act
		var mode = await KafkaCloudEventAdapter.TryDetectModeAsync(message, CancellationToken.None);

		// Assert
		mode.ShouldBe(CloudEventMode.Structured);
	}

	[Fact]
	public async Task DetectBinaryModeFromRequiredHeaders()
	{
		// Arrange
		var cloudEvent = new CloudEvent(CloudEventsSpecVersion.V1_0)
		{
			Type = "orders.created",
			Source = new Uri("https://source.excalibur.io"),
			Id = "event-3",
			Data = "payload",
		};
		var message = await _adapter.ToTransportMessageAsync(cloudEvent, CloudEventMode.Binary, CancellationToken.None);

		// Act
		var mode = await KafkaCloudEventAdapter.TryDetectModeAsync(message, CancellationToken.None);

		// Assert
		mode.ShouldBe(CloudEventMode.Binary);
	}

	[Fact]
	public async Task ThrowJsonReaderExceptionWhenHeadersAreIncompleteAndPayloadIsNotJson()
	{
		// Arrange
		var message = new Message<string, string>
		{
			Headers = new Headers
			{
				new("ce-specversion", Encoding.UTF8.GetBytes("1.0")),
				new("ce-type", Encoding.UTF8.GetBytes("orders.created")),
				new("ce-source", Encoding.UTF8.GetBytes("https://source.excalibur.io")),
			},
			Value = "not-json",
		};

		// Act / Assert
		await Should.ThrowAsync<JsonException>(() =>
			_adapter.FromTransportMessageAsync(message, CancellationToken.None));
	}
}

#pragma warning restore IL2026
#pragma warning restore IL3050
