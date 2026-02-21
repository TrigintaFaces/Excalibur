// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly (FakeItEasy stores ValueTask)

using System.Text;

using Amazon.SQS;
using Amazon.SQS.Model;

using Excalibur.Dispatch.Transport.Aws;
using Excalibur.Dispatch.Transport.Diagnostics;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Sqs;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class SqsTransportSenderShould : IAsyncDisposable
{
	private readonly IAmazonSQS _fakeSqs = A.Fake<IAmazonSQS>();
	private readonly SqsTransportSender _sender;

	public SqsTransportSenderShould()
	{
		_sender = new SqsTransportSender(
			_fakeSqs,
			"https://sqs.us-east-1.amazonaws.com/123456789/test-queue",
			NullLogger<SqsTransportSender>.Instance);
	}

	public async ValueTask DisposeAsync()
	{
		await _sender.DisposeAsync();
		_fakeSqs.Dispose();
	}

	[Fact]
	public void ExposeDestination()
	{
		// Assert
		_sender.Destination.ShouldBe("https://sqs.us-east-1.amazonaws.com/123456789/test-queue");
	}

	[Fact]
	public async Task SendMessageSuccessfully()
	{
		// Arrange
		var message = CreateTestMessage();
		A.CallTo(() => _fakeSqs.SendMessageAsync(A<SendMessageRequest>._, A<CancellationToken>._))
			.Returns(new SendMessageResponse { MessageId = "sqs-msg-1" });

		// Act
		var result = await _sender.SendAsync(message, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		result.MessageId.ShouldBe("sqs-msg-1");
	}

	[Fact]
	public async Task ReturnFailureWhenSendThrows()
	{
		// Arrange
		var message = CreateTestMessage();
		A.CallTo(() => _fakeSqs.SendMessageAsync(A<SendMessageRequest>._, A<CancellationToken>._))
			.Throws(new AmazonSQSException("Service unavailable") { ErrorCode = "ServiceUnavailable" });

		// Act
		var result = await _sender.SendAsync(message, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		result.Error.ShouldNotBeNull();
		result.Error!.IsRetryable.ShouldBeTrue();
	}

	[Fact]
	public async Task ThrowWhenSendAsyncMessageIsNull()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_sender.SendAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task SendBatchSuccessfully()
	{
		// Arrange
		var messages = new List<TransportMessage>
		{
			CreateTestMessage("msg-1"),
			CreateTestMessage("msg-2"),
		};

		A.CallTo(() => _fakeSqs.SendMessageBatchAsync(A<SendMessageBatchRequest>._, A<CancellationToken>._))
			.Returns(new SendMessageBatchResponse
			{
				Successful =
				[
					new SendMessageBatchResultEntry { Id = "0", MessageId = "sqs-1" },
					new SendMessageBatchResultEntry { Id = "1", MessageId = "sqs-2" },
				],
				Failed = [],
			});

		// Act
		var result = await _sender.SendBatchAsync(messages, CancellationToken.None);

		// Assert
		result.TotalMessages.ShouldBe(2);
		result.SuccessCount.ShouldBe(2);
		result.FailureCount.ShouldBe(0);
	}

	[Fact]
	public async Task SendBatchWithPartialFailure()
	{
		// Arrange
		var messages = new List<TransportMessage>
		{
			CreateTestMessage("msg-1"),
			CreateTestMessage("msg-2"),
		};

		A.CallTo(() => _fakeSqs.SendMessageBatchAsync(A<SendMessageBatchRequest>._, A<CancellationToken>._))
			.Returns(new SendMessageBatchResponse
			{
				Successful =
				[
					new SendMessageBatchResultEntry { Id = "0", MessageId = "sqs-1" },
				],
				Failed =
				[
					new BatchResultErrorEntry { Id = "1", Code = "ERR", Message = "Failed", SenderFault = true },
				],
			});

		// Act
		var result = await _sender.SendBatchAsync(messages, CancellationToken.None);

		// Assert
		result.TotalMessages.ShouldBe(2);
		result.SuccessCount.ShouldBe(1);
		result.FailureCount.ShouldBe(1);
	}

	[Fact]
	public async Task ReturnEmptyBatchResultForEmptyMessages()
	{
		// Arrange
		var messages = new List<TransportMessage>();

		// Act
		var result = await _sender.SendBatchAsync(messages, CancellationToken.None);

		// Assert
		result.TotalMessages.ShouldBe(0);
		result.SuccessCount.ShouldBe(0);
		result.FailureCount.ShouldBe(0);
	}

	[Fact]
	public async Task ThrowWhenSendBatchAsyncMessagesIsNull()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_sender.SendBatchAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task FlushCompletesImmediately()
	{
		// Act
		await _sender.FlushAsync(CancellationToken.None);

		// Assert - no exception thrown, SQS sends are immediate
	}

	[Fact]
	public void ReturnSqsClientFromGetService()
	{
		// Act
		var service = _sender.GetService(typeof(IAmazonSQS));

		// Assert
		service.ShouldBeSameAs(_fakeSqs);
	}

	[Fact]
	public void ReturnNullForUnknownServiceType()
	{
		// Act
		var service = _sender.GetService(typeof(string));

		// Assert
		service.ShouldBeNull();
	}

	[Fact]
	public void ThrowWhenGetServiceTypeIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			_sender.GetService(null!));
	}

	[Fact]
	public async Task DisposeIdempotently()
	{
		// Act
		await _sender.DisposeAsync();
		await _sender.DisposeAsync(); // second call should be no-op

		// Assert - no exception thrown
	}

	[Fact]
	public async Task MapOrderingKeyToMessageGroupId()
	{
		// Arrange
		var message = CreateTestMessage();
		message.Properties[TransportTelemetryConstants.PropertyKeys.OrderingKey] = "group-1";

		A.CallTo(() => _fakeSqs.SendMessageAsync(A<SendMessageRequest>._, A<CancellationToken>._))
			.Returns(new SendMessageResponse { MessageId = "sqs-fifo-1" });

		// Act
		await _sender.SendAsync(message, CancellationToken.None);

		// Assert
		A.CallTo(() => _fakeSqs.SendMessageAsync(
			A<SendMessageRequest>.That.Matches(r => r.MessageGroupId == "group-1"),
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task MapDeduplicationIdToMessageDeduplicationId()
	{
		// Arrange
		var message = CreateTestMessage();
		message.Properties[TransportTelemetryConstants.PropertyKeys.DeduplicationId] = "dedup-1";

		A.CallTo(() => _fakeSqs.SendMessageAsync(A<SendMessageRequest>._, A<CancellationToken>._))
			.Returns(new SendMessageResponse { MessageId = "sqs-dedup-1" });

		// Act
		await _sender.SendAsync(message, CancellationToken.None);

		// Assert
		A.CallTo(() => _fakeSqs.SendMessageAsync(
			A<SendMessageRequest>.That.Matches(r => r.MessageDeduplicationId == "dedup-1"),
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task MapDelaySecondsProperty()
	{
		// Arrange
		var message = CreateTestMessage();
		message.Properties[TransportTelemetryConstants.PropertyKeys.DelaySeconds] = "60";

		A.CallTo(() => _fakeSqs.SendMessageAsync(A<SendMessageRequest>._, A<CancellationToken>._))
			.Returns(new SendMessageResponse { MessageId = "sqs-delayed-1" });

		// Act
		await _sender.SendAsync(message, CancellationToken.None);

		// Assert
		A.CallTo(() => _fakeSqs.SendMessageAsync(
			A<SendMessageRequest>.That.Matches(r => r.DelaySeconds == 60),
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ClampDelaySecondsToMaximum900()
	{
		// Arrange
		var message = CreateTestMessage();
		message.Properties[TransportTelemetryConstants.PropertyKeys.DelaySeconds] = "1200";

		A.CallTo(() => _fakeSqs.SendMessageAsync(A<SendMessageRequest>._, A<CancellationToken>._))
			.Returns(new SendMessageResponse { MessageId = "sqs-clamped" });

		// Act
		await _sender.SendAsync(message, CancellationToken.None);

		// Assert
		A.CallTo(() => _fakeSqs.SendMessageAsync(
			A<SendMessageRequest>.That.Matches(r => r.DelaySeconds == 900),
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task AddContentTypeAsMessageAttribute()
	{
		// Arrange
		var message = CreateTestMessage();
		message.ContentType = "application/json";

		A.CallTo(() => _fakeSqs.SendMessageAsync(A<SendMessageRequest>._, A<CancellationToken>._))
			.Returns(new SendMessageResponse { MessageId = "sqs-ct" });

		// Act
		await _sender.SendAsync(message, CancellationToken.None);

		// Assert
		A.CallTo(() => _fakeSqs.SendMessageAsync(
			A<SendMessageRequest>.That.Matches(r =>
				r.MessageAttributes.ContainsKey("content-type") &&
				r.MessageAttributes["content-type"].StringValue == "application/json"),
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task AddCorrelationIdAsMessageAttribute()
	{
		// Arrange
		var message = CreateTestMessage();
		message.CorrelationId = "corr-123";

		A.CallTo(() => _fakeSqs.SendMessageAsync(A<SendMessageRequest>._, A<CancellationToken>._))
			.Returns(new SendMessageResponse { MessageId = "sqs-corr" });

		// Act
		await _sender.SendAsync(message, CancellationToken.None);

		// Assert
		A.CallTo(() => _fakeSqs.SendMessageAsync(
			A<SendMessageRequest>.That.Matches(r =>
				r.MessageAttributes.ContainsKey("correlation-id") &&
				r.MessageAttributes["correlation-id"].StringValue == "corr-123"),
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task HandleNonTransientErrors()
	{
		// Arrange
		var message = CreateTestMessage();
		A.CallTo(() => _fakeSqs.SendMessageAsync(A<SendMessageRequest>._, A<CancellationToken>._))
			.Throws(new AmazonSQSException("Bad request") { ErrorCode = "InvalidParameterValue" });

		// Act
		var result = await _sender.SendAsync(message, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		result.Error.ShouldNotBeNull();
		result.Error!.IsRetryable.ShouldBeFalse();
	}

	[Fact]
	public async Task HandleBatchExceptionGracefully()
	{
		// Arrange
		var messages = new List<TransportMessage>
		{
			CreateTestMessage("msg-1"),
			CreateTestMessage("msg-2"),
			CreateTestMessage("msg-3"),
		};

		A.CallTo(() => _fakeSqs.SendMessageBatchAsync(A<SendMessageBatchRequest>._, A<CancellationToken>._))
			.Throws(new AmazonSQSException("Batch failure") { ErrorCode = "InternalError" });

		// Act
		var result = await _sender.SendBatchAsync(messages, CancellationToken.None);

		// Assert
		result.TotalMessages.ShouldBe(3);
		result.SuccessCount.ShouldBe(0);
		result.FailureCount.ShouldBe(3);
		result.Duration.ShouldNotBeNull();
		result.Duration!.Value.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
	}

	[Fact]
	public void ThrowWhenConstructedWithNullSqsClient()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new SqsTransportSender(null!, "queue-url", NullLogger<SqsTransportSender>.Instance));
	}

	[Fact]
	public void ThrowWhenConstructedWithNullDestination()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new SqsTransportSender(_fakeSqs, null!, NullLogger<SqsTransportSender>.Instance));
	}

	[Fact]
	public void ThrowWhenConstructedWithNullLogger()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new SqsTransportSender(_fakeSqs, "queue-url", null!));
	}

	[Fact]
	public async Task ParseSequenceNumberFromResponse()
	{
		// Arrange
		var message = CreateTestMessage();
		A.CallTo(() => _fakeSqs.SendMessageAsync(A<SendMessageRequest>._, A<CancellationToken>._))
			.Returns(new SendMessageResponse { MessageId = "sqs-fifo", SequenceNumber = "12345" });

		// Act
		var result = await _sender.SendAsync(message, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		result.SequenceNumber.ShouldBe(12345L);
	}

	private static TransportMessage CreateTestMessage(string? id = null) =>
		new()
		{
			Id = id ?? Guid.NewGuid().ToString(),
			Body = Encoding.UTF8.GetBytes("test payload"),
			ContentType = null,
			MessageType = null,
			CorrelationId = null,
		};
}
