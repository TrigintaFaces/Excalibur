// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly (FakeItEasy stores ValueTask)

using Amazon.SQS;
using Amazon.SQS.Model;

using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Sqs;

[Trait("Category", "Unit")]
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
		var cts = new CancellationTokenSource();
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

		// Assert
		processedIds.ShouldContain("msg-sub-1");
		A.CallTo(() => _fakeSqs.DeleteMessageAsync(A<DeleteMessageRequest>._, A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task RejectMessageByDeletingIt()
	{
		// Arrange
		var cts = new CancellationTokenSource();
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

		// Assert - Reject deletes the message
		A.CallTo(() => _fakeSqs.DeleteMessageAsync(
			A<DeleteMessageRequest>.That.Matches(r => r.ReceiptHandle == "rh-rej"),
			A<CancellationToken>._)).MustHaveHappened();
	}

	[Fact]
	public async Task RequeueMessageByChangingVisibility()
	{
		// Arrange
		var cts = new CancellationTokenSource();
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

		// Assert
		A.CallTo(() => _fakeSqs.ChangeMessageVisibilityAsync(
			A<ChangeMessageVisibilityRequest>.That.Matches(r =>
				r.ReceiptHandle == "rh-req" && r.VisibilityTimeout == 0),
			A<CancellationToken>._)).MustHaveHappened();
	}

	[Fact]
	public async Task HandleHandlerExceptionByChangingVisibility()
	{
		// Arrange
		var cts = new CancellationTokenSource();
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

		// Assert - should change visibility to 0 for retry
		A.CallTo(() => _fakeSqs.ChangeMessageVisibilityAsync(
			A<ChangeMessageVisibilityRequest>.That.Matches(r =>
				r.ReceiptHandle == "rh-err" && r.VisibilityTimeout == 0),
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
			new SqsTransportSubscriber(null!, "source", "queueUrl", NullLogger<SqsTransportSubscriber>.Instance));
	}

	[Fact]
	public void ThrowWhenConstructedWithNullSource()
	{
		Should.Throw<ArgumentNullException>(() =>
			new SqsTransportSubscriber(_fakeSqs, null!, "queueUrl", NullLogger<SqsTransportSubscriber>.Instance));
	}

	[Fact]
	public void ThrowWhenConstructedWithNullQueueUrl()
	{
		Should.Throw<ArgumentNullException>(() =>
			new SqsTransportSubscriber(_fakeSqs, "source", null!, NullLogger<SqsTransportSubscriber>.Instance));
	}

	[Fact]
	public void ThrowWhenConstructedWithNullLogger()
	{
		Should.Throw<ArgumentNullException>(() =>
			new SqsTransportSubscriber(_fakeSqs, "source", "queueUrl", null!));
	}

	[Fact]
	public async Task SkipEmptyPollsAndContinue()
	{
		// Arrange
		var cts = new CancellationTokenSource();
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
			TimeSpan.FromSeconds(5));
		// Assert - should have polled multiple times
		callCount.ShouldBeGreaterThan(1);
	}
}
