// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly — FakeItEasy .Returns() stores ValueTask

using System.Text;

using Amazon.SQS;
using Amazon.SQS.Model;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.DeadLetterQueue;

/// <summary>
/// Unit tests for <see cref="DlqProcessor"/> — the real AWS SQS DLQ implementation (S523.6).
/// Tests both the SQS-specific <see cref="IDlqManager"/> interface and the transport-agnostic
/// <see cref="IDeadLetterQueueManager"/> interface.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DlqProcessorShould : IDisposable
{
	private readonly IAmazonSQS _sqsClient;
	private readonly ILogger<DlqProcessor> _logger;
	private readonly IRetryStrategy _retryStrategy;
	private readonly IErrorTracker _errorTracker;
	private readonly DlqProcessor _sut;

	private static readonly Uri DlqUrl = new("https://sqs.us-east-1.amazonaws.com/123456789/my-queue-dlq");
	private static readonly Uri SourceQueueUrl = new("https://sqs.us-east-1.amazonaws.com/123456789/my-queue");

	public DlqProcessorShould()
	{
		_sqsClient = A.Fake<IAmazonSQS>();
		_logger = A.Fake<ILogger<DlqProcessor>>();
		_retryStrategy = A.Fake<IRetryStrategy>();
		_errorTracker = A.Fake<IErrorTracker>();

		var processorOptions = Microsoft.Extensions.Options.Options.Create(new DlqProcessorOptions { BatchSize = 10 });
		var dlqOptions = Microsoft.Extensions.Options.Options.Create(new DlqOptions { DeadLetterQueueUrl = DlqUrl });

		_sut = new DlqProcessor(_sqsClient, _logger, processorOptions, dlqOptions, _retryStrategy, _errorTracker);
	}

	#region ProcessMessageAsync Tests

	[Fact]
	public async Task ProcessMessageAsync_WhenRetryAllowed_DeletesFromDlq()
	{
		// Arrange
		A.CallTo(() => _retryStrategy.ShouldRetry(A<int>._, A<Exception>._)).Returns(true);
		var message = CreateDlqMessage();

		// Act
		var result = await _sut.ProcessMessageAsync(message, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
		result.Action.ShouldBe(DlqAction.Redriven);
		A.CallTo(() => _sqsClient.DeleteMessageAsync(
			A<DeleteMessageRequest>.That.Matches(r =>
				r.QueueUrl == DlqUrl.ToString() &&
				r.ReceiptHandle == "receipt-handle-1"),
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ProcessMessageAsync_WhenRetryExhausted_ReturnsSkipped()
	{
		// Arrange
		A.CallTo(() => _retryStrategy.ShouldRetry(A<int>._, A<Exception>._)).Returns(false);
		var message = CreateDlqMessage(attemptCount: 5);

		// Act
		var result = await _sut.ProcessMessageAsync(message, CancellationToken.None);

		// Assert
		result.Success.ShouldBeFalse();
		result.Action.ShouldBe(DlqAction.Skipped);
	}

	[Fact]
	public async Task ProcessMessageAsync_ThrowsOnNullMessage()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.ProcessMessageAsync(null!, CancellationToken.None));
	}

	#endregion

	#region MoveToDeadLetterQueueAsync Tests

	[Fact]
	public async Task MoveToDeadLetterQueueAsync_SendsMessageWithAttributes()
	{
		// Arrange
		var message = CreateDlqMessage();

		// Act
		var result = await _sut.MoveToDeadLetterQueueAsync(message, "Processing failed", CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
		A.CallTo(() => _sqsClient.SendMessageAsync(
			A<SendMessageRequest>.That.Matches(r =>
				r.QueueUrl == DlqUrl.ToString() &&
				r.MessageBody == "test body" &&
				r.MessageAttributes.ContainsKey("dlq_reason") &&
				r.MessageAttributes.ContainsKey("dlq_moved_at")),
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task MoveToDeadLetterQueueAsync_WhenNoDlqConfigured_ReturnsFalse()
	{
		// Arrange - create processor with no DLQ URL
		var sut = CreateProcessorWithoutDlqUrl();
		var message = CreateDlqMessage();

		// Act
		var result = await sut.MoveToDeadLetterQueueAsync(message, "fail", CancellationToken.None);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task MoveToDeadLetterQueueAsync_PreservesOriginalAttributes()
	{
		// Arrange
		var message = CreateDlqMessage();
		message.Attributes["custom-key"] = "custom-value";

		// Act
		await _sut.MoveToDeadLetterQueueAsync(message, "test reason", CancellationToken.None);

		// Assert
		A.CallTo(() => _sqsClient.SendMessageAsync(
			A<SendMessageRequest>.That.Matches(r =>
				r.MessageAttributes.ContainsKey("custom-key")),
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	#endregion

	#region RedriveMessagesAsync Tests

	[Fact]
	public async Task RedriveMessagesAsync_MovesMessagesToSourceQueue()
	{
		// Arrange
		var sqsMessages = new List<Message>
		{
			CreateSqsMessage("msg-1", SourceQueueUrl.ToString()),
		};
		A.CallTo(() => _sqsClient.ReceiveMessageAsync(A<ReceiveMessageRequest>._, A<CancellationToken>._))
			.Returns(new ReceiveMessageResponse { Messages = sqsMessages });

		// Act
		var count = await _sut.RedriveMessagesAsync(maxMessages: 10, cancellationToken: CancellationToken.None);

		// Assert
		count.ShouldBe(1);
		A.CallTo(() => _sqsClient.SendMessageAsync(
			A<SendMessageRequest>.That.Matches(r => r.QueueUrl == SourceQueueUrl.ToString()),
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => _sqsClient.DeleteMessageAsync(
			A<DeleteMessageRequest>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RedriveMessagesAsync_WhenNoDlqConfigured_ReturnsZero()
	{
		var sut = CreateProcessorWithoutDlqUrl();
		var count = await sut.RedriveMessagesAsync(maxMessages: 10, cancellationToken: CancellationToken.None);
		count.ShouldBe(0);
	}

	[Fact]
	public async Task RedriveMessagesAsync_WhenNoSourceQueueUrl_SkipsMessage()
	{
		// Arrange - message without source queue URL attribute
		var sqsMessages = new List<Message>
		{
			new()
			{
				MessageId = "msg-no-source",
				Body = "test",
				ReceiptHandle = "receipt-1",
				MessageAttributes = new Dictionary<string, MessageAttributeValue>(),
			},
		};
		A.CallTo(() => _sqsClient.ReceiveMessageAsync(A<ReceiveMessageRequest>._, A<CancellationToken>._))
			.Returns(new ReceiveMessageResponse { Messages = sqsMessages });

		// Act
		var count = await _sut.RedriveMessagesAsync(maxMessages: 10, cancellationToken: CancellationToken.None);

		// Assert - message skipped, not redriven
		count.ShouldBe(0);
	}

	#endregion

	#region GetStatisticsAsync Tests (IDlqManager)

	[Fact]
	public async Task GetStatisticsAsync_ReturnsQueueAttributes()
	{
		// Arrange
		A.CallTo(() => _sqsClient.GetQueueAttributesAsync(A<GetQueueAttributesRequest>._, A<CancellationToken>._))
			.Returns(new GetQueueAttributesResponse
			{
				Attributes = new Dictionary<string, string>
				{
					["ApproximateNumberOfMessages"] = "15",
					["ApproximateNumberOfMessagesNotVisible"] = "3",
				},
			});

		// Act
		var stats = await _sut.GetStatisticsAsync(CancellationToken.None);

		// Assert
		stats.TotalMessages.ShouldBe(18); // 15 + 3
	}

	[Fact]
	public async Task GetStatisticsAsync_WhenNoDlqConfigured_ReturnsEmptyStats()
	{
		var sut = CreateProcessorWithoutDlqUrl();
		var stats = await sut.GetStatisticsAsync(CancellationToken.None);
		stats.TotalMessages.ShouldBe(0);
	}

	#endregion

	#region PurgeMessagesAsync Tests

	[Fact]
	public async Task PurgeMessagesAsync_CallsPurgeQueueApi()
	{
		// Arrange
		A.CallTo(() => _sqsClient.GetQueueAttributesAsync(A<GetQueueAttributesRequest>._, A<CancellationToken>._))
			.Returns(new GetQueueAttributesResponse
			{
				Attributes = new Dictionary<string, string>
				{
					["ApproximateNumberOfMessages"] = "5",
					["ApproximateNumberOfMessagesNotVisible"] = "0",
				},
			});

		// Act
		var purgedCount = await _sut.PurgeMessagesAsync(CancellationToken.None);

		// Assert
		purgedCount.ShouldBe(5);
		A.CallTo(() => _sqsClient.PurgeQueueAsync(
			A<PurgeQueueRequest>.That.Matches(r => r.QueueUrl == DlqUrl.ToString()),
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	#endregion

	#region IDeadLetterQueueManager.MoveToDeadLetterAsync Tests

	[Fact]
	public async Task IDeadLetterQueueManager_MoveToDeadLetterAsync_ConvertsTransportMessage()
	{
		// Arrange
		IDeadLetterQueueManager manager = _sut;
		var transportMessage = new TransportMessage
		{
			Id = "cloud-msg-1",
			Body = Encoding.UTF8.GetBytes("cloud body"),
		};

		// Act
		var result = await manager.MoveToDeadLetterAsync(transportMessage, "Timeout exceeded", null, CancellationToken.None);

		// Assert
		result.ShouldBe("cloud-msg-1");
		A.CallTo(() => _sqsClient.SendMessageAsync(
			A<SendMessageRequest>.That.Matches(r => r.QueueUrl == DlqUrl.ToString()),
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task IDeadLetterQueueManager_GetStatisticsAsync_ReturnsDeadLetterStatistics()
	{
		// Arrange
		IDeadLetterQueueManager manager = _sut;
		A.CallTo(() => _sqsClient.GetQueueAttributesAsync(A<GetQueueAttributesRequest>._, A<CancellationToken>._))
			.Returns(new GetQueueAttributesResponse
			{
				Attributes = new Dictionary<string, string>
				{
					["ApproximateNumberOfMessages"] = "7",
					["ApproximateNumberOfMessagesNotVisible"] = "2",
				},
			});

		// Act
		var stats = await manager.GetStatisticsAsync(CancellationToken.None);

		// Assert
		stats.MessageCount.ShouldBe(7);
		stats.GeneratedAt.ShouldNotBe(default);
	}

	[Fact]
	public async Task IDeadLetterQueueManager_PurgeDeadLetterQueueAsync_DelegatesToPurgeMessages()
	{
		// Arrange
		IDeadLetterQueueManager manager = _sut;
		A.CallTo(() => _sqsClient.GetQueueAttributesAsync(A<GetQueueAttributesRequest>._, A<CancellationToken>._))
			.Returns(new GetQueueAttributesResponse
			{
				Attributes = new Dictionary<string, string>
				{
					["ApproximateNumberOfMessages"] = "3",
					["ApproximateNumberOfMessagesNotVisible"] = "0",
				},
			});

		// Act
		var count = await manager.PurgeDeadLetterQueueAsync(CancellationToken.None);

		// Assert
		count.ShouldBe(3);
	}

	#endregion

	#region Helpers

	private static DlqMessage CreateDlqMessage(int attemptCount = 0)
	{
		return new DlqMessage
		{
			MessageId = "msg-1",
			Body = "test body",
			ReceiptHandle = "receipt-handle-1",
			SourceQueueUrl = SourceQueueUrl,
			AttemptCount = attemptCount,
		};
	}

	private static Message CreateSqsMessage(string messageId, string sourceQueueUrl)
	{
		return new Message
		{
			MessageId = messageId,
			Body = "sqs body",
			ReceiptHandle = "receipt-" + messageId,
			MessageAttributes = new Dictionary<string, MessageAttributeValue>
			{
				["dlq_original_queue_url"] = new()
				{
					DataType = "String",
					StringValue = sourceQueueUrl,
				},
			},
		};
	}

	private DlqProcessor CreateProcessorWithoutDlqUrl()
	{
		var noDlqOptions = Microsoft.Extensions.Options.Options.Create(new DlqOptions { DeadLetterQueueUrl = null });
		var processorOptions = Microsoft.Extensions.Options.Options.Create(new DlqProcessorOptions { BatchSize = 10 });
		return new DlqProcessor(_sqsClient, _logger, processorOptions, noDlqOptions, _retryStrategy, _errorTracker);
	}

	#endregion

	public void Dispose()
	{
		_sut.Dispose();
		_sqsClient.Dispose();
	}
}
