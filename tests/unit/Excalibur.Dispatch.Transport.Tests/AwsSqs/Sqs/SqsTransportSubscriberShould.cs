// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly (FakeItEasy stores ValueTask)

using Amazon.SQS;
using Amazon.SQS.Model;

using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Sqs;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Platform")]
public sealed class SqsTransportSubscriberShould : IAsyncDisposable
{
	private readonly IAmazonSQS _fakeSqs = A.Fake<IAmazonSQS>();
	private readonly SqsTransportSubscriber _subscriber;

	public SqsTransportSubscriberShould()
	{
		_subscriber = new SqsTransportSubscriber(
			_fakeSqs,
			"test-source",
			"https://sqs.us-east-1.amazonaws.com/123456789/test-queue",
			new AwsSqsVisibilityHeartbeatOptions(),
			NullLogger<SqsTransportSubscriber>.Instance);
	}

	public async ValueTask DisposeAsync()
	{
		await _subscriber.DisposeAsync();
		_fakeSqs.Dispose();
	}

	[Fact]
	public void ExposeSource()
	{
		// Assert
		_subscriber.Source.ShouldBe("test-source");
	}

	[Fact]
	public async Task SubscribeAndProcessMessages()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		var processedIds = new List<string>();
		var callCount = 0;

		A.CallTo(() => _fakeSqs.ReceiveMessageAsync(A<ReceiveMessageRequest>._, A<CancellationToken>._))
			.ReturnsLazily(call =>
			{
				callCount++;
				if (callCount == 1)
				{
					return Task.FromResult(new ReceiveMessageResponse
					{
						Messages =
						[
							new Message
							{
								MessageId = "msg-sub-1",
								Body = "Hello",
								ReceiptHandle = "rh-1",
							},
						],
					});
				}

				cts.Cancel();
				return Task.FromResult(new ReceiveMessageResponse { Messages = [] });
			});

		// Act
		await _subscriber.SubscribeAsync(
			(msg, ct) =>
			{
				processedIds.Add(msg.Id);
				return Task.FromResult(MessageAction.Acknowledge);
			},
			cts.Token);

		// Assert — settlement now uses batch delete (one call per receive batch), not per-message delete.
		processedIds.ShouldContain("msg-sub-1");
		A.CallTo(() => _fakeSqs.DeleteMessageBatchAsync(
			A<DeleteMessageBatchRequest>.That.Matches(r => r.Entries.Exists(e => e.ReceiptHandle == "rh-1")),
			A<CancellationToken>._)).MustHaveHappened();
		A.CallTo(() => _fakeSqs.DeleteMessageAsync(A<DeleteMessageRequest>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task RejectMessageByDeletingIt()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		var callCount = 0;

		A.CallTo(() => _fakeSqs.ReceiveMessageAsync(A<ReceiveMessageRequest>._, A<CancellationToken>._))
			.ReturnsLazily(_ =>
			{
				callCount++;
				if (callCount == 1)
				{
					return Task.FromResult(new ReceiveMessageResponse
					{
						Messages =
						[
							new Message
							{
								MessageId = "msg-rej",
								Body = "Hello",
								ReceiptHandle = "rh-rej",
							},
						],
					});
				}

				cts.Cancel();
				return Task.FromResult(new ReceiveMessageResponse { Messages = [] });
			});

		// Act
		await _subscriber.SubscribeAsync(
			(_, _) => Task.FromResult(MessageAction.Reject),
			cts.Token);

		// Assert - Reject deletes the message via batch delete
		A.CallTo(() => _fakeSqs.DeleteMessageBatchAsync(
			A<DeleteMessageBatchRequest>.That.Matches(r => r.Entries.Exists(e => e.ReceiptHandle == "rh-rej")),
			A<CancellationToken>._)).MustHaveHappened();
	}

	[Fact]
	public async Task RequeueMessageByChangingVisibility()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		var callCount = 0;

		A.CallTo(() => _fakeSqs.ReceiveMessageAsync(A<ReceiveMessageRequest>._, A<CancellationToken>._))
			.ReturnsLazily(_ =>
			{
				callCount++;
				if (callCount == 1)
				{
					return Task.FromResult(new ReceiveMessageResponse
					{
						Messages =
						[
							new Message
							{
								MessageId = "msg-req",
								Body = "retry-me",
								ReceiptHandle = "rh-req",
							},
						],
					});
				}

				cts.Cancel();
				return Task.FromResult(new ReceiveMessageResponse { Messages = [] });
			});

		// Act
		await _subscriber.SubscribeAsync(
			(_, _) => Task.FromResult(MessageAction.Requeue),
			cts.Token);

		// Assert — requeue changes visibility to 0 via batch visibility change
		A.CallTo(() => _fakeSqs.ChangeMessageVisibilityBatchAsync(
			A<ChangeMessageVisibilityBatchRequest>.That.Matches(r =>
				r.Entries.Exists(e => e.ReceiptHandle == "rh-req" && e.VisibilityTimeout == 0)),
			A<CancellationToken>._)).MustHaveHappened();
	}

	[Fact]
	public async Task HandleHandlerExceptionByChangingVisibility()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		var callCount = 0;

		A.CallTo(() => _fakeSqs.ReceiveMessageAsync(A<ReceiveMessageRequest>._, A<CancellationToken>._))
			.ReturnsLazily(_ =>
			{
				callCount++;
				if (callCount == 1)
				{
					return Task.FromResult(new ReceiveMessageResponse
					{
						Messages =
						[
							new Message
							{
								MessageId = "msg-err",
								Body = "bad-msg",
								ReceiptHandle = "rh-err",
							},
						],
					});
				}

				cts.Cancel();
				return Task.FromResult(new ReceiveMessageResponse { Messages = [] });
			});

		// Act
		await _subscriber.SubscribeAsync(
			(_, _) => throw new InvalidOperationException("Processing failed"),
			cts.Token);

		// Assert - should change visibility to 0 for retry via batch visibility change
		A.CallTo(() => _fakeSqs.ChangeMessageVisibilityBatchAsync(
			A<ChangeMessageVisibilityBatchRequest>.That.Matches(r =>
				r.Entries.Exists(e => e.ReceiptHandle == "rh-err" && e.VisibilityTimeout == 0)),
			A<CancellationToken>._)).MustHaveHappened();
	}

	[Fact]
	public async Task ThrowWhenHandlerIsNull()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_subscriber.SubscribeAsync(null!, CancellationToken.None));
	}

	[Fact]
	public void ReturnSqsClientFromGetService()
	{
		// Act
		var service = _subscriber.GetService(typeof(IAmazonSQS));

		// Assert
		service.ShouldBeSameAs(_fakeSqs);
	}

	[Fact]
	public void ReturnNullForUnknownServiceType()
	{
		// Act
		var service = _subscriber.GetService(typeof(string));

		// Assert
		service.ShouldBeNull();
	}

	[Fact]
	public void ThrowWhenGetServiceTypeIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => _subscriber.GetService(null!));
	}

	[Fact]
	public async Task DisposeIdempotently()
	{
		// Act
		await _subscriber.DisposeAsync();
		await _subscriber.DisposeAsync();

		// Assert - no exception
	}

	[Fact]
	public void ThrowWhenConstructedWithNullSqsClient()
	{
		Should.Throw<ArgumentNullException>(() =>
			new SqsTransportSubscriber(null!, "source", "queueUrl", new AwsSqsVisibilityHeartbeatOptions(), NullLogger<SqsTransportSubscriber>.Instance));
	}

	[Fact]
	public void ThrowWhenConstructedWithNullSource()
	{
		Should.Throw<ArgumentNullException>(() =>
			new SqsTransportSubscriber(_fakeSqs, null!, "queueUrl", new AwsSqsVisibilityHeartbeatOptions(), NullLogger<SqsTransportSubscriber>.Instance));
	}

	[Fact]
	public void ThrowWhenConstructedWithNullQueueUrl()
	{
		Should.Throw<ArgumentNullException>(() =>
			new SqsTransportSubscriber(_fakeSqs, "source", null!, new AwsSqsVisibilityHeartbeatOptions(), NullLogger<SqsTransportSubscriber>.Instance));
	}

	[Fact]
	public void ThrowWhenConstructedWithNullLogger()
	{
		Should.Throw<ArgumentNullException>(() =>
			new SqsTransportSubscriber(_fakeSqs, "source", "queueUrl", new AwsSqsVisibilityHeartbeatOptions(), null!));
	}

	[Fact]
	public async Task SkipEmptyPollsAndContinue()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		var callCount = 0;
		var secondPollObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		A.CallTo(() => _fakeSqs.ReceiveMessageAsync(A<ReceiveMessageRequest>._, A<CancellationToken>._))
			.ReturnsLazily(_ =>
			{
				callCount++;
				if (callCount >= 2)
				{
					secondPollObserved.TrySetResult();
				}

				if (callCount <= 2)
				{
					return Task.FromResult(new ReceiveMessageResponse { Messages = [] });
				}

				cts.Cancel();
				return Task.FromResult(new ReceiveMessageResponse { Messages = [] });
			});

		// Act
		await _subscriber.SubscribeAsync(
			(_, _) => Task.FromResult(MessageAction.Acknowledge),
			cts.Token);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			secondPollObserved.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(5)));
		// Assert - should have polled multiple times
		callCount.ShouldBeGreaterThan(1);
	}
}