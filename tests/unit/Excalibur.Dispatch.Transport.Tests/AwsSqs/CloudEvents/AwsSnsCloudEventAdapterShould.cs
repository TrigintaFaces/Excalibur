// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IL2026 // RequiresUnreferencedCode
#pragma warning disable IL3050 // RequiresDynamicCode

using Amazon.SimpleNotificationService.Model;

using CloudNative.CloudEvents;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Options.CloudEvents;
using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.CloudEvents;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class AwsSnsCloudEventAdapterShould
{
	private readonly AwsSnsCloudEventAdapter _adapter;

	public AwsSnsCloudEventAdapterShould()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new CloudEventOptions
		{
			DefaultSource = new Uri("https://test.excalibur.io"),
			DefaultMode = CloudEventMode.Structured,
		});

		_adapter = new AwsSnsCloudEventAdapter(
			options,
			NullLogger<AwsSnsCloudEventAdapter>.Instance);
	}

	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new AwsSnsCloudEventAdapter(
				null!, NullLogger<AwsSnsCloudEventAdapter>.Instance));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new CloudEventOptions());

		Should.Throw<ArgumentNullException>(() =>
			new AwsSnsCloudEventAdapter(options, null!));
	}

	[Fact]
	public void ExposeOptions()
	{
		_adapter.Options.ShouldNotBeNull();
		_adapter.Options.DefaultSource.ShouldNotBeNull();
		_adapter.Options.DefaultSource.Host.ShouldBe("test.excalibur.io");
	}

	[Fact]
	public async Task ConvertCloudEventToStructuredSnsMessage()
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
		var request = await _adapter.ToTransportMessageAsync(
			cloudEvent, CloudEventMode.Structured, CancellationToken.None);

		// Assert
		request.ShouldNotBeNull();
		request.Message.ShouldNotBeNullOrWhiteSpace();
		request.MessageAttributes.ShouldContainKey("contentType");
		request.MessageAttributes["contentType"].StringValue
			.ShouldContain("cloudevents");
	}

	[Fact]
	public async Task ConvertCloudEventToBinarySnsMessage()
	{
		// Arrange
		var cloudEvent = new CloudEvent
		{
			Type = "test.event",
			Source = new Uri("https://source.example.com"),
			Id = "test-id-2",
			Data = "binary test data",
			DataContentType = "text/plain",
		};

		// Act
		var request = await _adapter.ToTransportMessageAsync(
			cloudEvent, CloudEventMode.Binary, CancellationToken.None);

		// Assert
		request.ShouldNotBeNull();
		request.MessageAttributes.ShouldContainKey("ce-specversion");
		request.MessageAttributes.ShouldContainKey("ce-type");
		request.MessageAttributes.ShouldContainKey("ce-source");
		request.MessageAttributes.ShouldContainKey("ce-id");
	}

	[Fact]
	public async Task ThrowForUnsupportedMode()
	{
		// Arrange
		var cloudEvent = new CloudEvent
		{
			Type = "test.event",
			Source = new Uri("https://source.example.com"),
			Id = "test-id-3",
		};

		// Act & Assert
		await Should.ThrowAsync<NotSupportedException>(
			() => _adapter.ToTransportMessageAsync(
				cloudEvent, (CloudEventMode)99, CancellationToken.None));
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
				(PublishRequest)null!, CancellationToken.None));
	}

	[Fact]
	public async Task DetectStructuredModeFromContentType()
	{
		// Arrange
		var request = new PublishRequest
		{
			MessageAttributes = new Dictionary<string, MessageAttributeValue>
			{
				["contentType"] = new() { DataType = "String", StringValue = "application/cloudevents+json" },
			},
			Message = "{}",
		};

		// Act
		var mode = await AwsSnsCloudEventAdapter.TryDetectMode(request, CancellationToken.None);

		// Assert
		mode.ShouldBe(CloudEventMode.Structured);
	}

	[Fact]
	public async Task DetectBinaryModeFromAttributes()
	{
		// Arrange
		var request = new PublishRequest
		{
			MessageAttributes = new Dictionary<string, MessageAttributeValue>
			{
				["ce-specversion"] = new() { DataType = "String", StringValue = "1.0" },
				["ce-type"] = new() { DataType = "String", StringValue = "test.event" },
			},
			Message = "test body",
		};

		// Act
		var mode = await AwsSnsCloudEventAdapter.TryDetectMode(request, CancellationToken.None);

		// Assert
		mode.ShouldBe(CloudEventMode.Binary);
	}

	[Fact]
	public async Task DetectStructuredModeFromJsonBody()
	{
		// Arrange
		var request = new PublishRequest
		{
			MessageAttributes = new Dictionary<string, MessageAttributeValue>(),
			Message = """{"specversion":"1.0","type":"test","source":"https://src","id":"1"}""",
		};

		// Act
		var mode = await AwsSnsCloudEventAdapter.TryDetectMode(request, CancellationToken.None);

		// Assert
		mode.ShouldBe(CloudEventMode.Structured);
	}

	[Fact]
	public async Task ReturnNullModeWhenNotCloudEvents()
	{
		// Arrange
		var request = new PublishRequest
		{
			MessageAttributes = new Dictionary<string, MessageAttributeValue>(),
			Message = "just plain text",
		};

		// Act
		var mode = await AwsSnsCloudEventAdapter.TryDetectMode(request, CancellationToken.None);

		// Assert
		mode.ShouldBeNull();
	}

	[Fact]
	public async Task ThrowWhenMessageIsNullForTryDetectMode()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => AwsSnsCloudEventAdapter.TryDetectMode(
				(PublishRequest)null!, CancellationToken.None).AsTask());
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
			Time = DateTimeOffset.UtcNow,
		};

		// Act — serialize to SNS message
		var snsRequest = await _adapter.ToTransportMessageAsync(
			original, CloudEventMode.Structured, CancellationToken.None);

		// Deserialize back from SNS message
		var deserialized = await _adapter.FromTransportMessageAsync(
			snsRequest, CancellationToken.None);

		// Assert
		deserialized.Type.ShouldBe("test.roundtrip");
		deserialized.Source.ShouldBe(new Uri("https://source.example.com"));
		deserialized.Id.ShouldBe("roundtrip-1");
	}
}

#pragma warning restore IL2026
#pragma warning restore IL3050
