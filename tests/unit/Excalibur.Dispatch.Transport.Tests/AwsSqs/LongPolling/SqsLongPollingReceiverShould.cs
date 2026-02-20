// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // FakeItEasy .Returns() stores ValueTask

using Amazon.SQS;
using Amazon.SQS.Model;

using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.Logging.Abstractions;

using AwsPollingStatus = Excalibur.Dispatch.Transport.Aws.PollingStatus;
using IAwsLongPollingStrategy = Excalibur.Dispatch.Transport.ILongPollingStrategy;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.LongPolling;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class SqsLongPollingReceiverShould : IDisposable
{
	private readonly IAmazonSQS _sqsClient;
	private readonly IAwsLongPollingStrategy _strategy;
	private readonly IPollingMetricsCollector _metricsCollector;
	private readonly LongPollingConfiguration _config;
	private readonly SqsLongPollingReceiver _receiver;

	public SqsLongPollingReceiverShould()
	{
		_sqsClient = A.Fake<IAmazonSQS>();
		_strategy = A.Fake<IAwsLongPollingStrategy>();
		_metricsCollector = A.Fake<IPollingMetricsCollector>();
		_config = new LongPollingConfiguration
		{
			QueueUrl = new Uri("https://sqs.us-east-1.amazonaws.com/123456789/test-queue"),
			MaxMessagesPerReceive = 10,
		};

		A.CallTo(() => _strategy.CalculateOptimalWaitTimeAsync())
			.Returns(new ValueTask<TimeSpan>(TimeSpan.FromSeconds(5)));

		_receiver = new SqsLongPollingReceiver(
			_sqsClient,
			_strategy,
			_metricsCollector,
			_config,
			NullLogger<SqsLongPollingReceiver>.Instance);
	}

	[Fact]
	public void ThrowWhenSqsClientIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new SqsLongPollingReceiver(
			null!, _strategy, _metricsCollector, _config, NullLogger<SqsLongPollingReceiver>.Instance));
	}

	[Fact]
	public void ThrowWhenStrategyIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new SqsLongPollingReceiver(
			_sqsClient, null!, _metricsCollector, _config, NullLogger<SqsLongPollingReceiver>.Instance));
	}

	[Fact]
	public void ThrowWhenMetricsCollectorIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new SqsLongPollingReceiver(
			_sqsClient, _strategy, null!, _config, NullLogger<SqsLongPollingReceiver>.Instance));
	}

	[Fact]
	public void ThrowWhenConfigurationIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new SqsLongPollingReceiver(
			_sqsClient, _strategy, _metricsCollector, null!, NullLogger<SqsLongPollingReceiver>.Instance));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new SqsLongPollingReceiver(
			_sqsClient, _strategy, _metricsCollector, _config, null!));
	}

	[Fact]
	public void HaveInactiveStatusByDefault()
	{
		_receiver.Status.ShouldBe(AwsPollingStatus.Inactive);
	}

	[Fact]
	public async Task ReceiveMessagesFromQueue()
	{
		// Arrange
		var messages = new List<Message> { new() { MessageId = "msg-1" } };
		A.CallTo(() => _sqsClient.ReceiveMessageAsync(A<ReceiveMessageRequest>._, A<CancellationToken>._))
			.Returns(new ReceiveMessageResponse { Messages = messages });

		// Act
		var result = await _receiver.ReceiveMessagesAsync(
			"https://sqs.us-east-1.amazonaws.com/123456789/test-queue",
			CancellationToken.None);

		// Assert
		result.Count.ShouldBe(1);
		result[0].MessageId.ShouldBe("msg-1");
	}

	[Fact]
	public async Task ThrowWhenQueueUrlIsEmpty()
	{
		await Should.ThrowAsync<ArgumentException>(
			() => _receiver.ReceiveMessagesAsync("", CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ThrowWhenQueueUrlIsNull()
	{
		await Should.ThrowAsync<ArgumentException>(
			() => _receiver.ReceiveMessagesAsync(null!, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task RecordMetricsOnReceive()
	{
		// Arrange
		A.CallTo(() => _sqsClient.ReceiveMessageAsync(A<ReceiveMessageRequest>._, A<CancellationToken>._))
			.Returns(new ReceiveMessageResponse { Messages = [new Message { MessageId = "msg-1" }] });

		// Act
		await _receiver.ReceiveMessagesAsync(
			"https://sqs.us-east-1.amazonaws.com/123456789/test-queue",
			CancellationToken.None);

		// Assert — strategy should record the result
		A.CallTo(() => _strategy.RecordReceiveResultAsync(1, A<TimeSpan>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => _metricsCollector.RecordPollingAttempt(1, A<TimeSpan>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RecordErrorOnReceiveFailure()
	{
		// Arrange
		A.CallTo(() => _sqsClient.ReceiveMessageAsync(A<ReceiveMessageRequest>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("SQS error"));

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			() => _receiver.ReceiveMessagesAsync(
				"https://sqs.us-east-1.amazonaws.com/123456789/test-queue",
				CancellationToken.None).AsTask());

		A.CallTo(() => _metricsCollector.RecordPollingError(A<Exception>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DeleteMessageSuccessfully()
	{
		// Arrange
		A.CallTo(() => _sqsClient.DeleteMessageAsync(A<DeleteMessageRequest>._, A<CancellationToken>._))
			.Returns(new DeleteMessageResponse());

		// Act
		await _receiver.DeleteMessageAsync(
			"https://sqs.us-east-1.amazonaws.com/123456789/test-queue",
			"receipt-handle-1",
			CancellationToken.None);

		// Assert
		A.CallTo(() => _sqsClient.DeleteMessageAsync(A<DeleteMessageRequest>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DeleteMessagesInBatches()
	{
		// Arrange
		A.CallTo(() => _sqsClient.DeleteMessageBatchAsync(A<DeleteMessageBatchRequest>._, A<CancellationToken>._))
			.Returns(new DeleteMessageBatchResponse
			{
				Successful = [new DeleteMessageBatchResultEntry { Id = "0" }],
				Failed = [],
			});

		// Act
		await _receiver.DeleteMessagesAsync(
			"https://sqs.us-east-1.amazonaws.com/123456789/test-queue",
			["handle-1", "handle-2"],
			CancellationToken.None);

		// Assert
		A.CallTo(() => _sqsClient.DeleteMessageBatchAsync(A<DeleteMessageBatchRequest>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SkipDeleteWhenNoHandlesProvided()
	{
		// Act
		await _receiver.DeleteMessagesAsync(
			"https://sqs.us-east-1.amazonaws.com/123456789/test-queue",
			Array.Empty<string>(),
			CancellationToken.None);

		// Assert — should not call SQS at all
		A.CallTo(() => _sqsClient.DeleteMessageBatchAsync(A<DeleteMessageBatchRequest>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task ReturnStatistics()
	{
		// Act
		var stats = await _receiver.GetStatisticsAsync();

		// Assert
		stats.TotalReceiveOperations.ShouldBe(0);
		stats.TotalMessagesReceived.ShouldBe(0);
		stats.TotalMessagesDeleted.ShouldBe(0);
		stats.PollingStatus.ShouldBe(AwsPollingStatus.Inactive);
	}

	[Fact]
	public async Task StartSetStatusToActive()
	{
		// Act
		await _receiver.StartAsync(CancellationToken.None);

		// Assert
		_receiver.Status.ShouldBe(AwsPollingStatus.Active);
	}

	[Fact]
	public async Task StopWhenNotActive()
	{
		// Act — stopping when already inactive should be a no-op
		await _receiver.StopAsync(CancellationToken.None);

		// Assert
		_receiver.Status.ShouldBe(AwsPollingStatus.Inactive);
	}

	[Fact]
	public void DisposeWithoutThrowing()
	{
		// Act & Assert
		_receiver.Dispose();
		_receiver.Dispose();
	}

	[Fact]
	public async Task SkipVisibilityOptimizationWhenDisabled()
	{
		// Arrange — config has EnableVisibilityTimeoutOptimization = false by default
		_config.EnableVisibilityTimeoutOptimization = false;

		// Act
		await _receiver.OptimizeVisibilityTimeoutAsync(
			"https://sqs.us-east-1.amazonaws.com/123456789/test-queue",
			"receipt-handle",
			TimeSpan.FromSeconds(5),
			CancellationToken.None);

		// Assert — should not call SQS
		A.CallTo(() => _sqsClient.ChangeMessageVisibilityAsync(
			A<ChangeMessageVisibilityRequest>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task ThrowWhenStartContinuousPollingWithNullHandler()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _receiver.StartContinuousPollingAsync(
				"https://sqs.example.com/queue",
				(Func<Message, CancellationToken, ValueTask>)null!,
				CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ThrowWhenStartContinuousPollingBatchWithEmptyUrl()
	{
		Func<IReadOnlyList<Message>, CancellationToken, ValueTask> handler = (_, _) => ValueTask.CompletedTask;

		await Should.ThrowAsync<ArgumentException>(
			() => _receiver.StartContinuousPollingAsync(
				"", handler, CancellationToken.None).AsTask());
	}

	public void Dispose()
	{
		_receiver.Dispose();
	}
}

#pragma warning restore CA2012
