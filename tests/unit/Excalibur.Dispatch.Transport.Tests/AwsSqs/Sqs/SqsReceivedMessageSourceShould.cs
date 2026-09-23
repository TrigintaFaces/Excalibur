// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

#pragma warning disable CA2012 // Use ValueTasks correctly (FakeItEasy stores ValueTask)

using Amazon.SQS;
using Amazon.SQS.Model;

using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Sqs;

/// <summary>
/// TransportReceivedMessage.Source is the source queue or subscription a message came from. The pull
/// receiver reported the message's own broker ID there, so two messages from one queue claimed two
/// different sources and no consumer could group or route by origin.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Transport")]
public sealed class SqsReceivedMessageSourceShould : IAsyncDisposable
{
	private const string QueueUrl = "https://sqs.us-east-1.amazonaws.com/123456789/orders-queue";
	private const string OtherQueueUrl = "https://sqs.us-east-1.amazonaws.com/123456789/shipments-queue";

	private readonly IAmazonSQS _fakeSqs = A.Fake<IAmazonSQS>();
	private readonly SqsTransportReceiver _receiver;

	public SqsReceivedMessageSourceShould() =>
		_receiver = new SqsTransportReceiver(_fakeSqs, QueueUrl, NullLogger<SqsTransportReceiver>.Instance);

	public async ValueTask DisposeAsync()
	{
		await _receiver.DisposeAsync();
		_fakeSqs.Dispose();
	}

	/// <summary>
	/// SAFETY, and the exact reported defect: two different messages from one queue must agree on their
	/// source while keeping their own distinct identities.
	/// </summary>
	[Fact]
	public async Task ReportTheSameSourceForTwoMessagesFromOneQueue()
	{
		RespondWith(QueueUrl, ("msg-a", "receipt-a"), ("msg-b", "receipt-b"));

		var messages = await _receiver.ReceiveAsync(10, CancellationToken.None);

		messages.Count.ShouldBe(2);
		messages[0].Source.ShouldBe(QueueUrl);
		messages[1].Source.ShouldBe(QueueUrl);

		messages[0].Id.ShouldBe("msg-a");
		messages[1].Id.ShouldBe("msg-b");
		messages[0].Id.ShouldNotBe(messages[1].Id);
		messages[0].Source.ShouldNotBe(messages[0].Id);
	}

	/// <summary>
	/// LIVENESS, and the second-queue control. Reporting a constant would satisfy the arm above; a
	/// receiver configured for a different queue must report that queue, so Source really does track the
	/// configured origin.
	/// </summary>
	[Fact]
	public async Task ReportTheConfiguredQueueOfWhicheverReceiverReceivedTheMessage()
	{
		await using var otherReceiver = new SqsTransportReceiver(
			_fakeSqs,
			OtherQueueUrl,
			NullLogger<SqsTransportReceiver>.Instance);

		RespondWith(QueueUrl, ("msg-a", "receipt-a"));
		var fromOrders = await _receiver.ReceiveAsync(1, CancellationToken.None);

		RespondWith(OtherQueueUrl, ("msg-a", "receipt-a"));
		var fromShipments = await otherReceiver.ReceiveAsync(1, CancellationToken.None);

		fromOrders[0].Source.ShouldBe(QueueUrl);
		fromShipments[0].Source.ShouldBe(OtherQueueUrl);
		fromOrders[0].Source.ShouldNotBe(fromShipments[0].Source);
	}

	/// <summary>
	/// SAFETY. Settlement identity must survive the change: the receipt handle and the broker message ID
	/// stay in ProviderData, which is what acknowledgement and rejection use.
	/// </summary>
	[Fact]
	public async Task PreserveSettlementIdentityInProviderData()
	{
		RespondWith(QueueUrl, ("msg-a", "receipt-a"), ("msg-b", "receipt-b"));

		var messages = await _receiver.ReceiveAsync(10, CancellationToken.None);

		messages[0].ProviderData["sqs.receipt_handle"].ShouldBe("receipt-a");
		messages[0].ProviderData["sqs.message_id"].ShouldBe("msg-a");
		messages[1].ProviderData["sqs.receipt_handle"].ShouldBe("receipt-b");
		messages[1].ProviderData["sqs.message_id"].ShouldBe("msg-b");

		A.CallTo(() => _fakeSqs.DeleteMessageAsync(A<DeleteMessageRequest>._, A<CancellationToken>._))
			.Returns(new DeleteMessageResponse());

		await _receiver.AcknowledgeAsync(messages[1], CancellationToken.None);

		A.CallTo(() => _fakeSqs.DeleteMessageAsync(
			A<DeleteMessageRequest>.That.Matches(r => r.ReceiptHandle == "receipt-b" && r.QueueUrl == QueueUrl),
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	/// <summary>
	/// SAFETY. The push path already reported the queue; this holds it there, so the two receive paths
	/// cannot drift apart again.
	/// </summary>
	[Fact]
	public async Task ReportTheQueueOnTheSubscriberPathToo()
	{
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
						Messages = [WireMessage("msg-a", "receipt-a")],
					});
				}

				cts.Cancel();
				return Task.FromResult(new ReceiveMessageResponse { Messages = [] });
			});

		await using var subscriber = new SqsTransportSubscriber(
			_fakeSqs,
			"logical-source-name",
			QueueUrl,
			new AwsSqsVisibilityHeartbeatOptions(),
			NullLogger<SqsTransportSubscriber>.Instance);

		TransportReceivedMessage? delivered = null;
		await subscriber.SubscribeAsync(
			(msg, _) =>
			{
				delivered ??= msg;
				return Task.FromResult(MessageAction.Acknowledge);
			},
			cts.Token);

		delivered.ShouldNotBeNull();
		delivered!.Source.ShouldBe(QueueUrl);
		delivered.Id.ShouldBe("msg-a");
	}

	private static Message WireMessage(string messageId, string receiptHandle) =>
		new()
		{
			MessageId = messageId,
			ReceiptHandle = receiptHandle,
			Body = "{}",
			MessageAttributes = new Dictionary<string, MessageAttributeValue>(StringComparer.Ordinal),
			Attributes = new Dictionary<string, string>(StringComparer.Ordinal)
			{
				["ApproximateReceiveCount"] = "1",
			},
		};

	private void RespondWith(string queueUrl, params (string MessageId, string ReceiptHandle)[] messages) =>
		A.CallTo(() => _fakeSqs.ReceiveMessageAsync(
				A<ReceiveMessageRequest>.That.Matches(r => r.QueueUrl == queueUrl),
				A<CancellationToken>._))
			.Returns(new ReceiveMessageResponse
			{
				Messages = [.. messages.Select(m => WireMessage(m.MessageId, m.ReceiptHandle))],
			});
}
