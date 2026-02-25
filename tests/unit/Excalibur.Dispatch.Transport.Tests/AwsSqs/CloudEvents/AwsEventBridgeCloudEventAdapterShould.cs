// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IL2026 // RequiresUnreferencedCode
#pragma warning disable IL3050 // RequiresDynamicCode

using Amazon.EventBridge.Model;

using CloudNative.CloudEvents;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Options.CloudEvents;
using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.CloudEvents;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class AwsEventBridgeCloudEventAdapterShould
{
	private readonly AwsEventBridgeCloudEventAdapter _adapter;

	public AwsEventBridgeCloudEventAdapterShould()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new CloudEventOptions
		{
			DefaultSource = new Uri("https://test.excalibur.io"),
			DefaultMode = CloudEventMode.Structured,
		});

		_adapter = new AwsEventBridgeCloudEventAdapter(
			options,
			NullLogger<AwsEventBridgeCloudEventAdapter>.Instance);
	}

	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new AwsEventBridgeCloudEventAdapter(
				null!, NullLogger<AwsEventBridgeCloudEventAdapter>.Instance));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new CloudEventOptions());

		Should.Throw<ArgumentNullException>(() =>
			new AwsEventBridgeCloudEventAdapter(options, null!));
	}

	[Fact]
	public void ExposeOptions()
	{
		_adapter.Options.ShouldNotBeNull();
		_adapter.Options.DefaultSource.ShouldNotBeNull();
	}

	[Fact]
	public async Task ConvertCloudEventToEventBridgeEntry()
	{
		// Arrange
		var cloudEvent = new CloudEvent
		{
			Type = "test.event",
			Source = new Uri("https://source.example.com"),
			Id = "test-id-1",
			Data = "test data",
			DataContentType = "text/plain",
		};

		// Act
		var entry = await _adapter.ToTransportMessageAsync(
			cloudEvent, CloudEventMode.Structured, CancellationToken.None);

		// Assert
		entry.ShouldNotBeNull();
		entry.DetailType.ShouldBe("test.event");
		entry.Detail.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task ThrowWhenCloudEventIsNull()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _adapter.ToTransportMessageAsync(
				null!, CloudEventMode.Structured, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowWhenTransportMessageIsNullForFromTransport()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _adapter.FromTransportMessageAsync(
				(PutEventsRequestEntry)null!, CancellationToken.None));
	}

	[Fact]
	public async Task DetectStructuredModeFromJsonWithSpecVersion()
	{
		// Arrange
		var entry = new PutEventsRequestEntry
		{
			Detail = """{"specversion":"1.0","type":"test","id":"1","source":"https://src"}""",
		};

		// Act
		var mode = await AwsEventBridgeCloudEventAdapter.TryDetectMode(entry, CancellationToken.None);

		// Assert
		mode.ShouldBe(CloudEventMode.Structured);
	}

	[Fact]
	public async Task DetectBinaryModeWhenDetailIsEmpty()
	{
		// Arrange
		var entry = new PutEventsRequestEntry
		{
			Detail = "",
		};

		// Act
		var mode = await AwsEventBridgeCloudEventAdapter.TryDetectMode(entry, CancellationToken.None);

		// Assert
		mode.ShouldBe(CloudEventMode.Binary);
	}

	[Fact]
	public async Task DetectBinaryModeWhenDetailIsNotCloudEventsJson()
	{
		// Arrange
		var entry = new PutEventsRequestEntry
		{
			Detail = """{"key":"value"}""",
		};

		// Act
		var mode = await AwsEventBridgeCloudEventAdapter.TryDetectMode(entry, CancellationToken.None);

		// Assert
		mode.ShouldBe(CloudEventMode.Binary);
	}

	[Fact]
	public async Task DetectBinaryModeWhenDetailIsInvalidJson()
	{
		// Arrange
		var entry = new PutEventsRequestEntry
		{
			Detail = "not json at all",
		};

		// Act
		var mode = await AwsEventBridgeCloudEventAdapter.TryDetectMode(entry, CancellationToken.None);

		// Assert
		mode.ShouldBe(CloudEventMode.Binary);
	}

	[Fact]
	public async Task ThrowWhenMessageIsNullForTryDetectMode()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => AwsEventBridgeCloudEventAdapter.TryDetectMode(
				(PutEventsRequestEntry)null!, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ConvertToEventBridgeEventWithBusName()
	{
		// Arrange
		var cloudEvent = new CloudEvent
		{
			Type = "test.event",
			Source = new Uri("https://source.example.com"),
			Id = "test-id-2",
			Data = "test data",
		};

		// Act
		var entry = await _adapter.ToEventBridgeEventAsync(
			cloudEvent, "my-event-bus", CancellationToken.None);

		// Assert
		entry.EventBusName.ShouldBe("my-event-bus");
	}

	[Fact]
	public async Task ThrowWhenEventBusNameIsEmpty()
	{
		var cloudEvent = new CloudEvent
		{
			Type = "test.event",
			Source = new Uri("https://source.example.com"),
			Id = "test-id-3",
		};

		await Should.ThrowAsync<ArgumentException>(
			() => _adapter.ToEventBridgeEventAsync(
				cloudEvent, "", CancellationToken.None));
	}

	[Fact]
	public async Task RoundTripStructuredCloudEvent()
	{
		// Arrange
		var original = new CloudEvent
		{
			Type = "test.roundtrip",
			Source = new Uri("https://source.example.com"),
			Id = "roundtrip-1",
			Data = "round trip data",
			DataContentType = "text/plain",
		};

		// Act — serialize to EventBridge entry
		var entry = await _adapter.ToTransportMessageAsync(
			original, CloudEventMode.Structured, CancellationToken.None);

		// Deserialize back
		var deserialized = await _adapter.FromTransportMessageAsync(
			entry, CancellationToken.None);

		// Assert
		deserialized.Type.ShouldBe("test.roundtrip");
		deserialized.Id.ShouldBe("roundtrip-1");
	}

	[Fact]
	public async Task IncludeSubjectInResources()
	{
		// Arrange
		var cloudEvent = new CloudEvent
		{
			Type = "test.event",
			Source = new Uri("https://source.example.com"),
			Id = "test-id-4",
			Subject = "my-subject",
			Data = "test data",
		};

		// Act
		var entry = await _adapter.ToTransportMessageAsync(
			cloudEvent, CloudEventMode.Structured, CancellationToken.None);

		// Assert
		entry.Resources.ShouldContain("my-subject");
	}
}

#pragma warning restore IL2026
#pragma warning restore IL3050
