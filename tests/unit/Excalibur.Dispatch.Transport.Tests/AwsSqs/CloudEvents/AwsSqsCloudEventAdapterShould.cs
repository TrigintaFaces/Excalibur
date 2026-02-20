// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IL2026 // RequiresUnreferencedCode
#pragma warning disable IL3050 // RequiresDynamicCode

using Amazon.SQS.Model;

using CloudNative.CloudEvents;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Options.CloudEvents;
using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.CloudEvents;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class AwsSqsCloudEventAdapterShould
{
	private readonly AwsSqsCloudEventAdapter _adapter;

	public AwsSqsCloudEventAdapterShould()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new CloudEventOptions
		{
			DefaultSource = new Uri("https://test.excalibur.io"),
			DefaultMode = CloudEventMode.Structured,
		});

		_adapter = new AwsSqsCloudEventAdapter(
			options,
			NullLogger<AwsSqsCloudEventAdapter>.Instance);
	}

	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new AwsSqsCloudEventAdapter(
				null!, NullLogger<AwsSqsCloudEventAdapter>.Instance));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new CloudEventOptions());

		Should.Throw<ArgumentNullException>(() =>
			new AwsSqsCloudEventAdapter(options, null!));
	}

	[Fact]
	public void ExposeOptions()
	{
		_adapter.Options.ShouldNotBeNull();
		_adapter.Options.DefaultSource.ShouldNotBeNull();
		_adapter.Options.DefaultSource.Host.ShouldBe("test.excalibur.io");
	}

	[Fact]
	public async Task ConvertCloudEventToStructuredSqsMessage()
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
		request.MessageBody.ShouldNotBeNullOrWhiteSpace();
		request.MessageAttributes.ShouldContainKey("contentType");
		request.MessageAttributes["contentType"].StringValue
			.ShouldContain("cloudevents");
	}

	[Fact]
	public async Task ConvertCloudEventToBinarySqsMessage()
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
				(SendMessageRequest)null!, CancellationToken.None));
	}

	[Fact]
	public async Task ConvertToSqsMessageWithQueueUrl()
	{
		// Arrange
		var cloudEvent = new CloudEvent
		{
			Type = "test.event",
			Source = new Uri("https://source.example.com"),
			Id = "test-id-4",
			Data = "queue test",
		};

		// Act
		var request = await _adapter.ToSqsMessageAsync(
			cloudEvent, "https://sqs.us-east-1.amazonaws.com/123/test-queue",
			CancellationToken.None);

		// Assert
		request.QueueUrl.ShouldBe("https://sqs.us-east-1.amazonaws.com/123/test-queue");
	}

	[Fact]
	public async Task ThrowWhenQueueUrlIsEmpty()
	{
		var cloudEvent = new CloudEvent
		{
			Type = "test.event",
			Source = new Uri("https://source.example.com"),
			Id = "test-id-5",
		};

		await Should.ThrowAsync<ArgumentException>(
			() => _adapter.ToSqsMessageAsync(
				cloudEvent, "", CancellationToken.None));
	}

	[Fact]
	public async Task DetectStructuredModeFromContentType()
	{
		// Arrange
		var message = new Message
		{
			MessageAttributes = new Dictionary<string, MessageAttributeValue>
			{
				["contentType"] = new() { DataType = "String", StringValue = "application/cloudevents+json" },
			},
			Body = "{}",
		};

		// Act
		var mode = await AwsSqsCloudEventAdapter.TryDetectMode(message, CancellationToken.None);

		// Assert
		mode.ShouldBe(CloudEventMode.Structured);
	}

	[Fact]
	public async Task DetectBinaryModeFromAttributes()
	{
		// Arrange
		var message = new Message
		{
			MessageAttributes = new Dictionary<string, MessageAttributeValue>
			{
				["ce-specversion"] = new() { DataType = "String", StringValue = "1.0" },
				["ce-type"] = new() { DataType = "String", StringValue = "test.event" },
				["ce-source"] = new() { DataType = "String", StringValue = "https://source.example.com" },
				["ce-id"] = new() { DataType = "String", StringValue = "test-id" },
			},
			Body = "test body",
		};

		// Act
		var mode = await AwsSqsCloudEventAdapter.TryDetectMode(message, CancellationToken.None);

		// Assert
		mode.ShouldBe(CloudEventMode.Binary);
	}

	[Fact]
	public async Task DetectStructuredModeFromJsonBody()
	{
		// Arrange — body looks like CloudEvents JSON but no content type attribute
		var message = new Message
		{
			MessageAttributes = new Dictionary<string, MessageAttributeValue>(),
			Body = """{"specversion":"1.0","type":"test","source":"https://src","id":"1"}""",
		};

		// Act
		var mode = await AwsSqsCloudEventAdapter.TryDetectMode(message, CancellationToken.None);

		// Assert
		mode.ShouldBe(CloudEventMode.Structured);
	}

	[Fact]
	public async Task ReturnNullModeWhenNotCloudEvents()
	{
		// Arrange
		var message = new Message
		{
			MessageAttributes = new Dictionary<string, MessageAttributeValue>(),
			Body = "just plain text",
		};

		// Act
		var mode = await AwsSqsCloudEventAdapter.TryDetectMode(message, CancellationToken.None);

		// Assert
		mode.ShouldBeNull();
	}

	[Fact]
	public async Task ThrowWhenMessageIsNullForTryDetectMode()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => AwsSqsCloudEventAdapter.TryDetectMode(
				(Message)null!, CancellationToken.None).AsTask());
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

		// Act — serialize to SQS message
		var sqsRequest = await _adapter.ToTransportMessageAsync(
			original, CloudEventMode.Structured, CancellationToken.None);

		// Deserialize back from SQS message
		var deserialized = await _adapter.FromTransportMessageAsync(
			sqsRequest, CancellationToken.None);

		// Assert
		deserialized.Type.ShouldBe("test.roundtrip");
		deserialized.Source.ShouldBe(new Uri("https://source.example.com"));
		deserialized.Id.ShouldBe("roundtrip-1");
	}

	[Fact]
	public async Task ConvertFromSqsMessageWithBinaryMode()
	{
		// Arrange
		var sqsMessage = new Message
		{
			MessageAttributes = new Dictionary<string, MessageAttributeValue>
			{
				["ce-specversion"] = new() { DataType = "String", StringValue = "1.0" },
				["ce-type"] = new() { DataType = "String", StringValue = "test.binary" },
				["ce-source"] = new() { DataType = "String", StringValue = "https://source.example.com" },
				["ce-id"] = new() { DataType = "String", StringValue = "binary-1" },
				["ce-datacontenttype"] = new() { DataType = "String", StringValue = "text/plain" },
			},
			Body = "binary body content",
		};

		// Act
		var cloudEvent = await _adapter.FromSqsMessageAsync(sqsMessage, CancellationToken.None);

		// Assert
		cloudEvent.Type.ShouldBe("test.binary");
		cloudEvent.Id.ShouldBe("binary-1");
	}

	[Fact]
	public async Task ThrowWhenFromSqsMessageWithNullMessage()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _adapter.FromSqsMessageAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ConvertBatchToSqsMessages()
	{
		// Arrange
		var events = new[]
		{
			new CloudEvent
			{
				Type = "test.batch.1",
				Source = new Uri("https://source.example.com"),
				Id = "batch-1",
				Data = "batch item 1",
			},
			new CloudEvent
			{
				Type = "test.batch.2",
				Source = new Uri("https://source.example.com"),
				Id = "batch-2",
				Data = "batch item 2",
			},
		};

		// Act
		var batch = await _adapter.ToBatchSqsMessageAsync(
			events, "https://sqs.us-east-1.amazonaws.com/123/test-queue",
			CancellationToken.None);

		// Assert
		batch.QueueUrl.ShouldBe("https://sqs.us-east-1.amazonaws.com/123/test-queue");
		batch.Entries.Count.ShouldBe(2);
		batch.Entries[0].Id.ShouldBe("0");
		batch.Entries[1].Id.ShouldBe("1");
	}

	[Fact]
	public async Task ThrowWhenBatchEventsIsNull()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _adapter.ToBatchSqsMessageAsync(
				null!, "https://sqs.example.com/queue", CancellationToken.None));
	}

	[Fact]
	public async Task ThrowWhenBatchQueueUrlIsEmpty()
	{
		var events = Array.Empty<CloudEvent>();

		await Should.ThrowAsync<ArgumentException>(
			() => _adapter.ToBatchSqsMessageAsync(
				events, "", CancellationToken.None));
	}
}

#pragma warning restore IL2026
#pragma warning restore IL3050
