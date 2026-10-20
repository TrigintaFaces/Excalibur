// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

#pragma warning disable CA2012 // Use ValueTasks correctly (FakeItEasy stores ValueTask)

using Amazon.SQS;
using Amazon.SQS.Model;

using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Sqs;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Platform")]
[Trait("Pattern", "TRANSPORT")]
public sealed class SqsTransportReceiverShould : IAsyncDisposable
{
	private readonly IAmazonSQS _fakeSqs = A.Fake<IAmazonSQS>();
	private readonly SqsTransportReceiver _receiver;

	public SqsTransportReceiverShould()
	{
		_receiver = new SqsTransportReceiver(
			_fakeSqs,
			"https://sqs.us-east-1.amazonaws.com/123456789/test-queue",
			NullLogger<SqsTransportReceiver>.Instance);
	}

	public async ValueTask DisposeAsync()
	{
		await _receiver.DisposeAsync();
		_fakeSqs.Dispose();
	}

	[Fact]
	public void ExposeSource()
	{
		// Assert
		_receiver.Source.ShouldBe("https://sqs.us-east-1.amazonaws.com/123456789/test-queue");
	}

	[Fact]
	public async Task ReceiveMessagesSuccessfully()
	{
		// Arrange
		A.CallTo(() => _fakeSqs.ReceiveMessageAsync(A<ReceiveMessageRequest>._, A<CancellationToken>._))
			.Returns(new ReceiveMessageResponse
			{
				Messages =
				[
					new Message
					{
						MessageId = "sqs-msg-1",
						Body = "Hello World",
						ReceiptHandle = "receipt-1",
						MessageAttributes = new Dictionary<string, MessageAttributeValue>
						{
							["content-type"] = new() { DataType = "String", StringValue = "text/plain" },
							["correlation-id"] = new() { DataType = "String", StringValue = "corr-1" },
						},
						Attributes = new Dictionary<string, string>
						{
							["ApproximateReceiveCount"] = "1",
							["SentTimestamp"] = "1700000000000",
						},
					},
				],
			});

		// Act
		var messages = await _receiver.ReceiveAsync(10, CancellationToken.None);

		// Assert
		messages.Count.ShouldBe(1);
		messages[0].Id.ShouldBe("sqs-msg-1");
		messages[0].ContentType.ShouldBe("text/plain");
		messages[0].CorrelationId.ShouldBe("corr-1");
		messages[0].DeliveryCount.ShouldBe(1);
	}

	[Fact]
	public async Task ReturnEmptyListWhenNoMessages()
	{
		// Arrange
		A.CallTo(() => _fakeSqs.ReceiveMessageAsync(A<ReceiveMessageRequest>._, A<CancellationToken>._))
			.Returns(new ReceiveMessageResponse { Messages = [] });

		// Act
		var messages = await _receiver.ReceiveAsync(10, CancellationToken.None);

		// Assert
		messages.ShouldBeEmpty();
	}

	[Fact]
	public async Task ReturnEmptyListWhenMessagesIsNull()
	{
		// Arrange
		A.CallTo(() => _fakeSqs.ReceiveMessageAsync(A<ReceiveMessageRequest>._, A<CancellationToken>._))
			.Returns(new ReceiveMessageResponse { Messages = null });

		// Act
		var messages = await _receiver.ReceiveAsync(10, CancellationToken.None);

		// Assert
		messages.ShouldBeEmpty();
	}

	[Fact]
	public async Task AcknowledgeMessageByDeletingIt()
	{
		// Arrange
		var received = CreateReceivedMessage("msg-1", "receipt-handle-1");

		// Act
		await _receiver.AcknowledgeAsync(received, CancellationToken.None);

		// Assert
		A.CallTo(() => _fakeSqs.DeleteMessageAsync(
			A<DeleteMessageRequest>.That.Matches(r =>
				r.ReceiptHandle == "receipt-handle-1"),
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ThrowWhenAcknowledgeMessageIsNull()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_receiver.AcknowledgeAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task RejectWithRequeueByChangingVisibility()
	{
		// Arrange
		var received = CreateReceivedMessage("msg-1", "receipt-handle-1");

		// Act
		await _receiver.RejectAsync(received, "test reason", requeue: true, CancellationToken.None);

		// Assert
		A.CallTo(() => _fakeSqs.ChangeMessageVisibilityAsync(
			A<ChangeMessageVisibilityRequest>.That.Matches(r =>
				r.ReceiptHandle == "receipt-handle-1" &&
				r.VisibilityTimeout == 0),
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RejectWithoutRequeueByDeletingMessage()
	{
		// Arrange
		var received = CreateReceivedMessage("msg-1", "receipt-handle-1");

		// Act
		await _receiver.RejectAsync(received, "poison message", requeue: false, CancellationToken.None);

		// Assert
		A.CallTo(() => _fakeSqs.DeleteMessageAsync(
			A<DeleteMessageRequest>.That.Matches(r =>
				r.ReceiptHandle == "receipt-handle-1"),
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ThrowWhenRejectMessageIsNull()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_receiver.RejectAsync(null!, "reason", true, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowWhenReceiptHandleIsMissing()
	{
		// Arrange
		var received = new TransportReceivedMessage
		{
			Id = "msg-no-handle",
			Body = Array.Empty<byte>(),
			ProviderData = new Dictionary<string, object>(),
		};

		// Act & Assert
		var thrown = await Should.ThrowAsync<TransportSettlementException>(() =>
			_receiver.AcknowledgeAsync(received, CancellationToken.None));
		thrown.Retryability.ShouldBe(SettlementRetryability.Permanent);
	}

	[Fact]
	public void ReturnSqsClientFromGetService()
	{
		// Act
		var service = _receiver.GetService(typeof(IAmazonSQS));

		// Assert
		service.ShouldBeSameAs(_fakeSqs);
	}

	[Fact]
	public void ReturnNullForUnknownServiceType()
	{
		// Act
		var service = _receiver.GetService(typeof(string));

		// Assert
		service.ShouldBeNull();
	}

	[Fact]
	public void ThrowWhenGetServiceTypeIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			_receiver.GetService(null!));
	}

	[Fact]
	public async Task DisposeIdempotently()
	{
		// Act
		await _receiver.DisposeAsync();
		await _receiver.DisposeAsync();

		// Assert - no exception
	}

	[Fact]
	public void ThrowWhenConstructedWithNullSqsClient()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new SqsTransportReceiver(null!, "source", NullLogger<SqsTransportReceiver>.Instance));
	}

	[Fact]
	public void ThrowWhenConstructedWithNullSource()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new SqsTransportReceiver(_fakeSqs, null!, NullLogger<SqsTransportReceiver>.Instance));
	}

	[Fact]
	public void ThrowWhenConstructedWithNullLogger()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new SqsTransportReceiver(_fakeSqs, "source", null!));
	}

	[Fact]
	public void ClampWaitTimeSeconds()
	{
		// Act - should not throw; clamps internally to 0-20
		var receiver = new SqsTransportReceiver(_fakeSqs, "source", NullLogger<SqsTransportReceiver>.Instance, waitTimeSeconds: 100);

		// Assert - no exception, clamped internally
		receiver.Source.ShouldBe("source");
	}

	[Fact]
	public async Task ExtractDeliveryCountFromApproximateReceiveCount()
	{
		// Arrange
		A.CallTo(() => _fakeSqs.ReceiveMessageAsync(A<ReceiveMessageRequest>._, A<CancellationToken>._))
			.Returns(new ReceiveMessageResponse
			{
				Messages =
				[
					new Message
					{
						MessageId = "msg-rc",
						Body = "test",
						ReceiptHandle = "rh-1",
						Attributes = new Dictionary<string, string>
						{
							["ApproximateReceiveCount"] = "5",
						},
					},
				],
			});

		// Act
		var messages = await _receiver.ReceiveAsync(1, CancellationToken.None);

		// Assert
		messages[0].DeliveryCount.ShouldBe(5);
	}

	[Fact]
	public async Task ExtractEnqueuedTimeFromSentTimestamp()
	{
		// Arrange
		A.CallTo(() => _fakeSqs.ReceiveMessageAsync(A<ReceiveMessageRequest>._, A<CancellationToken>._))
			.Returns(new ReceiveMessageResponse
			{
				Messages =
				[
					new Message
					{
						MessageId = "msg-ts",
						Body = "test",
						ReceiptHandle = "rh-2",
						Attributes = new Dictionary<string, string>
						{
							["SentTimestamp"] = "1700000000000",
						},
					},
				],
			});

		// Act
		var messages = await _receiver.ReceiveAsync(1, CancellationToken.None);

		// Assert
		messages[0].EnqueuedAt.ShouldBe(DateTimeOffset.FromUnixTimeMilliseconds(1700000000000));
	}

	[Fact]
	public async Task StoreReceiptHandleInProviderData()
	{
		// Arrange
		A.CallTo(() => _fakeSqs.ReceiveMessageAsync(A<ReceiveMessageRequest>._, A<CancellationToken>._))
			.Returns(new ReceiveMessageResponse
			{
				Messages =
				[
					new Message
					{
						MessageId = "msg-pd",
						Body = "test",
						ReceiptHandle = "my-receipt-handle",
					},
				],
			});

		// Act
		var messages = await _receiver.ReceiveAsync(1, CancellationToken.None);

		// Assert
		messages[0].ProviderData["sqs.receipt_handle"].ShouldBe("my-receipt-handle");
		messages[0].ProviderData["sqs.message_id"].ShouldBe("msg-pd");
	}

	[Fact]
	public async Task RethrowOnReceiveError()
	{
		// Arrange
		A.CallTo(() => _fakeSqs.ReceiveMessageAsync(A<ReceiveMessageRequest>._, A<CancellationToken>._))
			.Throws(new AmazonSQSException("Access denied"));

		// Act & Assert
		await Should.ThrowAsync<AmazonSQSException>(() =>
			_receiver.ReceiveAsync(10, CancellationToken.None));
	}

	[Fact]
	public async Task WrapAcknowledgeErrorWithPortableUncertainty()
	{
		// Arrange
		var received = CreateReceivedMessage("msg-err", "receipt-err");
		var native = new AmazonSQSException("Delete failed");
		A.CallTo(() => _fakeSqs.DeleteMessageAsync(A<DeleteMessageRequest>._, A<CancellationToken>._))
			.Throws(native);

		// Act & Assert
		var thrown = await Should.ThrowAsync<TransportSettlementException>(() =>
			_receiver.AcknowledgeAsync(received, CancellationToken.None));
		AssertNativeFailure(thrown);
		thrown.InnerException.ShouldBeSameAs(native);
	}

	[Fact]
	public async Task WrapRejectErrorWithPortableUncertainty()
	{
		// Arrange
		var received = CreateReceivedMessage("msg-rej-err", "receipt-rej");
		var native = new AmazonSQSException("Visibility change failed");
		A.CallTo(() => _fakeSqs.ChangeMessageVisibilityAsync(A<ChangeMessageVisibilityRequest>._, A<CancellationToken>._))
			.Throws(native);

		// Act & Assert
		var thrown = await Should.ThrowAsync<TransportSettlementException>(() =>
			_receiver.RejectAsync(received, "reason", true, CancellationToken.None));
		AssertNativeFailure(thrown);
		thrown.InnerException.ShouldBeSameAs(native);
	}


	[Theory]
	[InlineData("acknowledge", false)]
	[InlineData("reject", false)]
	[InlineData("requeue", false)]
	[InlineData("acknowledge", true)]
	[InlineData("reject", true)]
	[InlineData("requeue", true)]
	public async Task WrapNativeTimeoutDespiteUnrelatedCallerCancellation(string action, bool cancelCaller)
	{
		using var caller = new CancellationTokenSource();
		if (cancelCaller) caller.Cancel();
		var native = new OperationCanceledException("native settlement timeout");
		A.CallTo(() => _fakeSqs.DeleteMessageAsync(A<DeleteMessageRequest>._, A<CancellationToken>._))
			.Returns(Task.FromException<DeleteMessageResponse>(native));
		A.CallTo(() => _fakeSqs.ChangeMessageVisibilityAsync(A<ChangeMessageVisibilityRequest>._, A<CancellationToken>._))
			.Returns(Task.FromException<ChangeMessageVisibilityResponse>(native));
		var received = CreateReceivedMessage("msg-timeout", "receipt-timeout");

		var thrown = await Should.ThrowAsync<TransportSettlementException>(() => Settle(received, action, caller.Token));

		AssertNativeFailure(thrown);
		thrown.InnerException.ShouldBeSameAs(native);
		received.ProviderData["sqs.receipt_handle"].ShouldBe("receipt-timeout");
	}

	[Theory]
	[InlineData("acknowledge")]
	[InlineData("reject")]
	[InlineData("requeue")]
	public async Task CompleteSettlementWithIndependentTokenWhenCallerIsCanceled(string action)
	{
		using var caller = new CancellationTokenSource();
		caller.Cancel();
		var observed = CancellationToken.None;
		A.CallTo(() => _fakeSqs.DeleteMessageAsync(A<DeleteMessageRequest>._, A<CancellationToken>._))
			.Invokes((DeleteMessageRequest _, CancellationToken token) => observed = token)
			.Returns(new DeleteMessageResponse());
		A.CallTo(() => _fakeSqs.ChangeMessageVisibilityAsync(A<ChangeMessageVisibilityRequest>._, A<CancellationToken>._))
			.Invokes((ChangeMessageVisibilityRequest _, CancellationToken token) => observed = token)
			.Returns(new ChangeMessageVisibilityResponse());

		await Settle(CreateReceivedMessage("msg-canceled", "receipt-canceled"), action, caller.Token);

		observed.ShouldNotBe(caller.Token);
		observed.CanBeCanceled.ShouldBeTrue();
		observed.IsCancellationRequested.ShouldBeFalse();
	}

	[Theory]
	[InlineData("acknowledge", false)]
	[InlineData("reject", false)]
	[InlineData("requeue", false)]
	[InlineData("acknowledge", true)]
	[InlineData("reject", true)]
	[InlineData("requeue", true)]
	public async Task RejectInvalidReceiptWithoutNativeIo(string action, bool malformed)
	{
		var message = new TransportReceivedMessage { Id = "invalid-receipt" };
		if (malformed) message.ProviderData["sqs.receipt_handle"] = 123;

		var thrown = await Should.ThrowAsync<TransportSettlementException>(() => Settle(message, action, CancellationToken.None));

		thrown.TransportName.ShouldBe("Sqs");
		thrown.RedeliveryExpectation.ShouldBe(TransportRedeliveryExpectation.Unspecified);
		thrown.Retryability.ShouldBe(SettlementRetryability.Permanent);
		A.CallTo(() => _fakeSqs.DeleteMessageAsync(A<DeleteMessageRequest>._, A<CancellationToken>._)).MustNotHaveHappened();
		A.CallTo(() => _fakeSqs.ChangeMessageVisibilityAsync(A<ChangeMessageVisibilityRequest>._, A<CancellationToken>._)).MustNotHaveHappened();
	}

	[Theory]
	[InlineData("acknowledge")]
	[InlineData("reject")]
	[InlineData("requeue")]
	public async Task PreserveExistingPortableSettlementFailureIdentity(string action)
	{
		var sentinel = new TransportSettlementException("already classified")
		{
			TransportName = "Sqs",
			RedeliveryExpectation = TransportRedeliveryExpectation.Expected,
			Retryability = SettlementRetryability.Permanent,
		};
		A.CallTo(() => _fakeSqs.DeleteMessageAsync(A<DeleteMessageRequest>._, A<CancellationToken>._))
			.Returns(Task.FromException<DeleteMessageResponse>(sentinel));
		A.CallTo(() => _fakeSqs.ChangeMessageVisibilityAsync(A<ChangeMessageVisibilityRequest>._, A<CancellationToken>._))
			.Returns(Task.FromException<ChangeMessageVisibilityResponse>(sentinel));

		var thrown = await Should.ThrowAsync<TransportSettlementException>(() =>
			Settle(CreateReceivedMessage("sentinel-msg", "sentinel-receipt"), action, CancellationToken.None));

		thrown.ShouldBeSameAs(sentinel);
	}

	private Task Settle(TransportReceivedMessage message, string action, CancellationToken token) =>
		action == "acknowledge" ? _receiver.AcknowledgeAsync(message, token)
			: _receiver.RejectAsync(message, "test reason", action == "requeue", token);

	private static void AssertNativeFailure(TransportSettlementException thrown)
	{
		thrown.TransportName.ShouldBe("Sqs");
		thrown.RedeliveryExpectation.ShouldBe(TransportRedeliveryExpectation.Unspecified);
		thrown.Retryability.ShouldBe(SettlementRetryability.Unspecified);
		thrown.InnerException.ShouldNotBeNull();
	}

	private static TransportReceivedMessage CreateReceivedMessage(string id, string receiptHandle) =>
		new()
		{
			Id = id,
			Body = "test"u8.ToArray(),
			ProviderData = new Dictionary<string, object>
			{
				["sqs.receipt_handle"] = receiptHandle,
				["sqs.message_id"] = id,
			},
		};
}
