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
/// Whether a message goes out singly or in a batch is decided by how many happen to be in flight, not by
/// the caller -- so the two paths must put the same envelope on the wire. These arms compare the two
/// mappings against each other rather than against a list of field names, because a list only catches the
/// fields someone remembered to add to it.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Transport")]
public sealed class SqsBatchEnvelopeParityShould : IAsyncDisposable
{
	private const string QueueUrl = "https://sqs.us-east-1.amazonaws.com/123456789/test-queue.fifo";

	private readonly IAmazonSQS _fakeSqs = A.Fake<IAmazonSQS>();
	private readonly SqsTransportSender _sender;
	private readonly SqsTransportReceiver _receiver;

	public SqsBatchEnvelopeParityShould()
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
	/// SAFETY, and the property that actually matters: for the same message the batch entry and the
	/// single-send request carry the same attributes, the same body and the same FIFO hints. A field
	/// added to one mapping and forgotten in the other fails here without anyone updating this test.
	/// </summary>
	[Fact]
	public async Task PutTheSameEnvelopeOnTheWireAsTheSingleSendPath()
	{
		var message = FullyPopulatedMessage();

		var single = await CaptureSingleAsync(message);
		var entry = await CaptureFirstBatchEntryAsync(message);

		entry.MessageBody.ShouldBe(single.MessageBody);
		entry.MessageGroupId.ShouldBe(single.MessageGroupId);
		entry.MessageDeduplicationId.ShouldBe(single.MessageDeduplicationId);

		AttributePairs(entry.MessageAttributes).ShouldBe(AttributePairs(single.MessageAttributes), ignoreOrder: true);
	}

	/// <summary>
	/// SAFETY, named explicitly. The reported defect was that a batch entry copied only Properties, so a
	/// message whose metadata lives in the top-level fields lost all of it. These are the four fields
	/// that were dropped.
	/// </summary>
	[Fact]
	public async Task CarryTopLevelMetadataWhenPropertiesAreEmpty()
	{
		var message = new TransportMessage
		{
			Body = Encoding.UTF8.GetBytes("{}"),
			ContentType = "application/json",
			MessageType = "OrderCreated",
			CorrelationId = "corr-1",
			CausationId = "cause-1",
		};

		message.Properties.Count.ShouldBe(0);

		var entry = await CaptureFirstBatchEntryAsync(message);

		entry.MessageAttributes.ShouldNotBeNull();
		entry.MessageAttributes!["content-type"].StringValue.ShouldBe("application/json");
		entry.MessageAttributes["message-type"].StringValue.ShouldBe("OrderCreated");
		entry.MessageAttributes["correlation-id"].StringValue.ShouldBe("corr-1");
		entry.MessageAttributes["causation-id"].StringValue.ShouldBe("cause-1");
	}

	/// <summary>
	/// LIVENESS. The batch envelope is not merely present on the wire -- the receiver reconstructs the
	/// routing and correlation fields from it, which is the reason the omission mattered.
	/// </summary>
	[Fact]
	public async Task RoundTripBatchMetadataBackThroughTheReceiver()
	{
		var entry = await CaptureFirstBatchEntryAsync(FullyPopulatedMessage());

		A.CallTo(() => _fakeSqs.ReceiveMessageAsync(A<ReceiveMessageRequest>._, A<CancellationToken>._))
			.Returns(new ReceiveMessageResponse
			{
				Messages =
				[
					new Message
					{
						MessageId = "sqs-1",
						ReceiptHandle = "rh-1",
						Body = entry.MessageBody,
						MessageAttributes = entry.MessageAttributes!,
						Attributes = new Dictionary<string, string>(StringComparer.Ordinal),
					},
				],
			});

		var received = (await _receiver.ReceiveAsync(1, CancellationToken.None))[0];

		received.ContentType.ShouldBe("application/json");
		received.MessageType.ShouldBe("OrderCreated");
		received.CorrelationId.ShouldBe("corr-1");
		received.Properties["causation-id"].ShouldBe("cause-1");
		received.Properties["tenant"].ShouldBe("acme");
	}

	/// <summary>
	/// SAFETY. Parity must hold across every chunk, not only the first: a batch larger than the SQS limit
	/// of ten is split, and the entries in the later chunks go through the same mapping.
	/// </summary>
	[Fact]
	public async Task HoldParityAcrossChunkBoundaries()
	{
		var messages = new List<TransportMessage>();
		for (var i = 0; i < 25; i++)
		{
			var message = FullyPopulatedMessage();
			message.CorrelationId = $"corr-{i}";
			messages.Add(message);
		}

		var captured = new List<SendMessageBatchRequest>();
		A.CallTo(() => _fakeSqs.SendMessageBatchAsync(A<SendMessageBatchRequest>._, A<CancellationToken>._))
			.Invokes((SendMessageBatchRequest req, CancellationToken _) => captured.Add(req))
			.ReturnsLazily((SendMessageBatchRequest req, CancellationToken _) => Task.FromResult(
				new SendMessageBatchResponse
				{
					Successful = [.. req.Entries.ConvertAll(e =>
						new SendMessageBatchResultEntry { Id = e.Id, MessageId = $"sqs-{e.Id}" })],
					Failed = [],
				}));

		_ = await _sender.SendBatchAsync(messages, CancellationToken.None);

		captured.Count.ShouldBe(3);
		var allEntries = captured.SelectMany(r => r.Entries).ToList();
		allEntries.Count.ShouldBe(25);

		for (var i = 0; i < 25; i++)
		{
			var entry = allEntries.Find(e => e.Id == i.ToString(System.Globalization.CultureInfo.InvariantCulture));
			entry.ShouldNotBeNull();
			entry!.MessageAttributes!["correlation-id"].StringValue.ShouldBe($"corr-{i}");
			entry.MessageAttributes["content-type"].StringValue.ShouldBe("application/json");
		}
	}

	/// <summary>
	/// SAFETY. A custom property may not impersonate a reserved attribute: the top-level metadata is
	/// authoritative on both paths, and both must resolve the conflict the same way.
	/// </summary>
	[Fact]
	public async Task ResolveAReservedAttributeConflictIdenticallyOnBothPaths()
	{
		var message = new TransportMessage
		{
			Body = Encoding.UTF8.GetBytes("{}"),
			ContentType = "application/json",
			Properties = new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["content-type"] = "text/plain",
			},
		};

		var single = await CaptureSingleAsync(message);
		var entry = await CaptureFirstBatchEntryAsync(message);

		single.MessageAttributes!["content-type"].StringValue.ShouldBe("application/json");
		entry.MessageAttributes!["content-type"].StringValue.ShouldBe("application/json");
	}

	private static TransportMessage FullyPopulatedMessage() =>
		new()
		{
			Body = Encoding.UTF8.GetBytes("{\"order\":1}"),
			ContentType = "application/json",
			MessageType = "OrderCreated",
			CorrelationId = "corr-1",
			CausationId = "cause-1",
			Properties = new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["tenant"] = "acme",
				["dispatch.ordering.key"] = "group-1",
				["dispatch.deduplication.id"] = "dedup-1",
			},
		};

	private static List<KeyValuePair<string, string?>> AttributePairs(
		Dictionary<string, MessageAttributeValue>? attributes) =>
		attributes is null
			? []
			: [.. attributes.Select(kv => new KeyValuePair<string, string?>(kv.Key, kv.Value.StringValue))];

	private async Task<SendMessageRequest> CaptureSingleAsync(TransportMessage message)
	{
		SendMessageRequest? captured = null;
		A.CallTo(() => _fakeSqs.SendMessageAsync(A<SendMessageRequest>._, A<CancellationToken>._))
			.Invokes((SendMessageRequest req, CancellationToken _) => captured = req)
			.Returns(new SendMessageResponse { MessageId = "sqs-1" });

		_ = await _sender.SendAsync(message, CancellationToken.None);

		captured.ShouldNotBeNull();
		return captured!;
	}

	private async Task<SendMessageBatchRequestEntry> CaptureFirstBatchEntryAsync(TransportMessage message)
	{
		SendMessageBatchRequest? captured = null;
		A.CallTo(() => _fakeSqs.SendMessageBatchAsync(A<SendMessageBatchRequest>._, A<CancellationToken>._))
			.Invokes((SendMessageBatchRequest req, CancellationToken _) => captured = req)
			.Returns(new SendMessageBatchResponse
			{
				Successful = [new SendMessageBatchResultEntry { Id = "0", MessageId = "sqs-1" }],
				Failed = [],
			});

		_ = await _sender.SendBatchAsync([message], CancellationToken.None);

		captured.ShouldNotBeNull();
		return captured!.Entries[0];
	}
}
