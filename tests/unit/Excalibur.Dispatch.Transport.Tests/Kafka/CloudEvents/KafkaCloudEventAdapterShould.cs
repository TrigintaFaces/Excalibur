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

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
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
		message.Headers.TryGetLastBytes("ce_specversion", out _).ShouldBeTrue();
		message.Headers.TryGetLastBytes("ce_id", out _).ShouldBeTrue();
		message.Headers.TryGetLastBytes("ce_attempt", out var attemptBytes).ShouldBeTrue();
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
				new("ce_specversion", Encoding.UTF8.GetBytes("1.0")),
				new("ce_type", Encoding.UTF8.GetBytes("orders.created")),
				new("ce_source", Encoding.UTF8.GetBytes("https://source.excalibur.io")),
			},
			Value = "not-json",
		};

		// Act / Assert
		await Should.ThrowAsync<JsonException>(() =>
			_adapter.FromTransportMessageAsync(message, CancellationToken.None));
	}

	[Fact]
	public async Task RejectUnsupportedSpecVersionOnBinaryDecodeInsteadOfSilentlyCoercingToV1()
	{
		// Regression lock (pa4e1x): the binary decode must resolve the specversion from the header via the
		// CNCF SDK and reject an unknown one — the prior ternary returned V1_0 on BOTH branches, silently
		// accepting any/invalid specversion. RED on that pre-fix code (it would decode "9.9" as V1_0).
		var message = new Message<string, string>
		{
			Headers = new Headers
			{
				new("ce_specversion", Encoding.UTF8.GetBytes("9.9")),
				new("ce_type", Encoding.UTF8.GetBytes("orders.created")),
				new("ce_source", Encoding.UTF8.GetBytes("https://source.excalibur.io")),
				new("ce_id", Encoding.UTF8.GetBytes("evt-1")),
			},
			Value = """{"value":"payload"}""",
		};

		var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
			_adapter.FromTransportMessageAsync(message, CancellationToken.None));
		ex.Message.ShouldContain("9.9");
	}
}

#pragma warning restore IL2026
#pragma warning restore IL3050
