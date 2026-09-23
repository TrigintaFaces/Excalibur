// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

#pragma warning disable CA2012 // Use ValueTasks correctly (FakeItEasy stores ValueTask)

using System.Text;

using Amazon.SQS;
using Amazon.SQS.Model;

using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Sqs;

/// <summary>
/// The SQS transport exposes a byte-buffer body. These arms hold it to that: the bytes a consumer hands
/// the sender are the bytes the receiver hands back, for every byte value -- and text bodies keep going
/// on the wire as text, so existing producers and consumers are unaffected.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Transport")]
public sealed class SqsBinaryBodyRoundTripShould : IAsyncDisposable
{
	private const string QueueUrl = "https://sqs.us-east-1.amazonaws.com/123456789/test-queue";

	private readonly IAmazonSQS _fakeSqs = A.Fake<IAmazonSQS>();
	private readonly SqsTransportSender _sender;
	private readonly SqsTransportReceiver _receiver;

	public SqsBinaryBodyRoundTripShould()
	{
		_sender = new SqsTransportSender(_fakeSqs, QueueUrl, NullLogger<SqsTransportSender>.Instance);
		_receiver = new SqsTransportReceiver(_fakeSqs, QueueUrl, NullLogger<SqsTransportReceiver>.Instance);
	}

	public async ValueTask DisposeAsync()
	{
		await _sender.DisposeAsync();
		await _receiver.DisposeAsync();
		_fakeSqs.Dispose();
	}

	/// <summary>
	/// SAFETY. The exact reported defect: a body containing 0xFF is not valid UTF-8, and a lossy decode
	/// rewrites it to the replacement character EF BF BD. The round trip must return 0xFF.
	/// </summary>
	[Fact]
	public async Task PreserveAByteThatIsNotValidUtf8()
	{
		var body = new byte[] { 0x00, 0x01, 0xFF, 0xFE, 0x80 };

		var received = await RoundTripAsync(body);

		received.Body.ToArray().ShouldBe(body);
		received.Body.ToArray().ShouldNotBe(Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(body)));
	}

	/// <summary>
	/// SAFETY. Every single byte value survives, so the guarantee is about the byte-buffer contract and
	/// not about the one example in the report.
	/// </summary>
	[Fact]
	public async Task PreserveEveryByteValue()
	{
		var body = new byte[256];
		for (var i = 0; i < body.Length; i++)
		{
			body[i] = (byte)i;
		}

		var received = await RoundTripAsync(body);

		received.Body.ToArray().ShouldBe(body);
	}

	/// <summary>
	/// SAFETY. A real binary serializer payload -- MessagePack's encoding of a small map -- contains
	/// bytes that are not valid UTF-8 and must survive.
	/// </summary>
	[Fact]
	public async Task PreserveABinarySerializerPayload()
	{
		// MessagePack fixmap{"id": 0xDEADBEEF as uint32}, chosen because 0xDE/0xAD/0xBE/0xEF are an
		// invalid UTF-8 sequence.
		var body = new byte[] { 0x81, 0xA2, 0x69, 0x64, 0xCE, 0xDE, 0xAD, 0xBE, 0xEF };

		var received = await RoundTripAsync(body);

		received.Body.ToArray().ShouldBe(body);
	}

	/// <summary>
	/// LIVENESS. The fix must not be "Base64 everything": a UTF-8 text body still goes on the wire as
	/// its own text and carries no encoding attribute, so a consumer reading the queue with any other
	/// tool sees what it sees today. An implementation that encoded unconditionally would fail here.
	/// </summary>
	[Fact]
	public async Task CarryAUtf8TextBodyAsTextOnTheWire()
	{
		var text = "{\"order\":\"A-1\",\"note\":\"na\u00efve caf\u00e9 \u2713 \ud83d\ude80\"}";
		var body = Encoding.UTF8.GetBytes(text);

		var request = await CaptureSendAsync(body);

		request.MessageBody.ShouldBe(text);
		request.MessageAttributes.ShouldSatisfyAllConditions(
			() => (request.MessageAttributes is null ||
				!request.MessageAttributes.ContainsKey("dispatch-body-encoding")).ShouldBeTrue());

		var received = await RoundTripAsync(body);
		received.Body.ToArray().ShouldBe(body);
	}

	/// <summary>
	/// SAFETY. A body that is valid UTF-8 but contains a character SQS does not accept (the service
	/// takes the XML 1.0 character set) cannot be carried as text either -- SQS answers
	/// InvalidMessageContents. It must be encoded rather than offered to the broker as-is.
	/// </summary>
	[Fact]
	public async Task EncodeTextThatSqsDoesNotAccept()
	{
		var body = Encoding.UTF8.GetBytes("valid\u0000text\u0007here");

		var request = await CaptureSendAsync(body);

		request.MessageAttributes.ShouldNotBeNull();
		request.MessageAttributes!["dispatch-body-encoding"].StringValue.ShouldBe("base64");

		var received = await RoundTripAsync(body);
		received.Body.ToArray().ShouldBe(body);
	}

	/// <summary>
	/// Boundary. An empty body is carried as an empty body and comes back empty.
	/// </summary>
	[Fact]
	public async Task PreserveAnEmptyBody()
	{
		var request = await CaptureSendAsync([]);

		request.MessageBody.ShouldBe(string.Empty);

		var received = await RoundTripAsync([]);
		received.Body.Length.ShouldBe(0);
	}

	/// <summary>
	/// SAFETY. The batch path shares the single path's mapping, so a binary body sent as part of a batch
	/// is encoded the same way and survives the same round trip.
	/// </summary>
	[Fact]
	public async Task PreserveBinaryBodiesSentAsABatch()
	{
		var binary = new byte[] { 0xFF, 0x00, 0xC0 };
		var text = Encoding.UTF8.GetBytes("plain text");

		SendMessageBatchRequest? captured = null;
		A.CallTo(() => _fakeSqs.SendMessageBatchAsync(A<SendMessageBatchRequest>._, A<CancellationToken>._))
			.Invokes((SendMessageBatchRequest req, CancellationToken _) => captured = req)
			.Returns(new SendMessageBatchResponse
			{
				Successful =
				[
					new SendMessageBatchResultEntry { Id = "0", MessageId = "s-0" },
					new SendMessageBatchResultEntry { Id = "1", MessageId = "s-1" },
				],
				Failed = [],
			});

		_ = await _sender.SendBatchAsync(
			[new TransportMessage { Body = binary }, new TransportMessage { Body = text }],
			CancellationToken.None);

		captured.ShouldNotBeNull();
		var binaryEntry = captured!.Entries[0];
		var textEntry = captured.Entries[1];

		binaryEntry.MessageAttributes!["dispatch-body-encoding"].StringValue.ShouldBe("base64");
		(textEntry.MessageAttributes is null ||
			!textEntry.MessageAttributes.ContainsKey("dispatch-body-encoding")).ShouldBeTrue();

		(await ReceiveFromWireAsync(ToWireMessage(binaryEntry.MessageBody, binaryEntry.MessageAttributes)))
			.Body.ToArray().ShouldBe(binary);
		(await ReceiveFromWireAsync(ToWireMessage(textEntry.MessageBody, textEntry.MessageAttributes)))
			.Body.ToArray().ShouldBe(text);
	}

	/// <summary>
	/// SAFETY. A stale encoding label must not survive a round trip. A message received from an encoded
	/// body carries the encoding attribute in its properties; re-sending that payload as text must not
	/// re-apply the label, which would make the next receiver Base64-decode plain text.
	/// </summary>
	[Fact]
	public async Task NotInheritAStaleEncodingLabelFromProperties()
	{
		var message = new TransportMessage
		{
			Body = Encoding.UTF8.GetBytes("plain text"),
			Properties = new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["dispatch-body-encoding"] = "base64",
			},
		};

		SendMessageRequest? captured = null;
		A.CallTo(() => _fakeSqs.SendMessageAsync(A<SendMessageRequest>._, A<CancellationToken>._))
			.Invokes((SendMessageRequest req, CancellationToken _) => captured = req)
			.Returns(new SendMessageResponse { MessageId = "m" });

		_ = await _sender.SendAsync(message, CancellationToken.None);

		captured.ShouldNotBeNull();
		captured!.MessageBody.ShouldBe("plain text");
		(captured.MessageAttributes is null ||
			!captured.MessageAttributes.ContainsKey("dispatch-body-encoding")).ShouldBeTrue();

		(await ReceiveFromWireAsync(ToWireMessage(captured.MessageBody, captured.MessageAttributes)))
			.Body.ToArray().ShouldBe(Encoding.UTF8.GetBytes("plain text"));
	}

	/// <summary>
	/// SAFETY. The push path must decode identically to the pull path; otherwise the guarantee depends on
	/// which receiver a consumer happens to register.
	/// </summary>
	[Fact]
	public async Task PreserveBinaryBodiesOnTheSubscriberPath()
	{
		var body = new byte[] { 0xFF, 0x01, 0xFD };

		var request = await CaptureSendAsync(body);
		var wire = ToWireMessage(request.MessageBody, request.MessageAttributes);

		using var cts = new CancellationTokenSource();
		var callCount = 0;
		A.CallTo(() => _fakeSqs.ReceiveMessageAsync(A<ReceiveMessageRequest>._, A<CancellationToken>._))
			.ReturnsLazily(_ =>
			{
				callCount++;
				if (callCount == 1)
				{
					return Task.FromResult(new ReceiveMessageResponse { Messages = [wire] });
				}

				cts.Cancel();
				return Task.FromResult(new ReceiveMessageResponse { Messages = [] });
			});

		await using var subscriber = new SqsTransportSubscriber(
			_fakeSqs,
			"test-source",
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
		delivered!.Body.ToArray().ShouldBe(body);
	}

	private static Message ToWireMessage(string body, Dictionary<string, MessageAttributeValue>? attributes) =>
		new()
		{
			MessageId = "sqs-msg-1",
			ReceiptHandle = "receipt-1",
			Body = body,
			MessageAttributes = attributes ?? new Dictionary<string, MessageAttributeValue>(StringComparer.Ordinal),
			Attributes = new Dictionary<string, string>(StringComparer.Ordinal)
			{
				["ApproximateReceiveCount"] = "1",
			},
		};

	private async Task<SendMessageRequest> CaptureSendAsync(byte[] body)
	{
		SendMessageRequest? captured = null;
		A.CallTo(() => _fakeSqs.SendMessageAsync(A<SendMessageRequest>._, A<CancellationToken>._))
			.Invokes((SendMessageRequest req, CancellationToken _) => captured = req)
			.Returns(new SendMessageResponse { MessageId = "sqs-msg-1" });

		var result = await _sender.SendAsync(
			new TransportMessage { Body = body },
			CancellationToken.None);

		result.IsSuccess.ShouldBeTrue();
		captured.ShouldNotBeNull();
		return captured!;
	}

	private async Task<TransportReceivedMessage> ReceiveFromWireAsync(Message wire)
	{
		A.CallTo(() => _fakeSqs.ReceiveMessageAsync(A<ReceiveMessageRequest>._, A<CancellationToken>._))
			.Returns(new ReceiveMessageResponse { Messages = [wire] });

		var messages = await _receiver.ReceiveAsync(1, CancellationToken.None);
		messages.Count.ShouldBe(1);
		return messages[0];
	}

	private async Task<TransportReceivedMessage> RoundTripAsync(byte[] body)
	{
		var request = await CaptureSendAsync(body);
		return await ReceiveFromWireAsync(ToWireMessage(request.MessageBody, request.MessageAttributes));
	}
}
