// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

#pragma warning disable CA2012 // FakeItEasy .Returns() stores ValueTask/Task

using Confluent.Kafka;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Kafka;

using Microsoft.Extensions.Logging.Abstractions;

using KafkaConsumeResult = global::Confluent.Kafka.ConsumeResult<string, byte[]>;

namespace Excalibur.Dispatch.Transport.Tests.Kafka;

/// <summary>
/// A record whose header carries a null value is an ordinary message, and a commit recorded from a previous
/// tenure does not hold back the current one.
/// </summary>
/// <remarks>
/// Kafka allows a header with a null value. Decoding one used to throw during conversion, so a perfectly
/// valid message could never be delivered on any attempt.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class KafkaNullHeaderAndStaleCommitShould
{
	private const string Topic = "orders";
	private static readonly TopicPartition Partition0 = new(Topic, new Partition(0));

	/// <summary>
	/// LIVENESS. The receiver delivers a record carrying a null-valued header, and keeps the other headers.
	/// </summary>
	/// <remarks>RED against a conversion that decodes every header value: it throws on the null one.</remarks>
	[Fact]
	public async Task DeliverAMessageWithANullValuedHeader_FromTheReceiver()
	{
		var consumer = A.Fake<IConsumer<string, byte[]>>();
		var queue = new Queue<KafkaConsumeResult?>([RecordWithNullHeader(0)]);
		A.CallTo<KafkaConsumeResult?>(() => consumer.Consume(A<TimeSpan>._))
			.ReturnsLazily(() => queue.Count > 0 ? queue.Dequeue() : null);

		var receiver = new KafkaTransportReceiver(consumer, Topic, NullLogger<KafkaTransportReceiver>.Instance, 1024, false, null);

		var messages = await receiver.ReceiveAsync(10, CancellationToken.None);

		var message = messages.ShouldHaveSingleItem();
		message.Properties["trace"].ShouldBe("abc");
		message.Properties.ContainsKey("empty").ShouldBeFalse("a null-valued header has no value to carry");
	}

	/// <summary>
	/// LIVENESS. The subscriber hands the same record to its handler.
	/// </summary>
	[Fact]
	public async Task DeliverAMessageWithANullValuedHeader_FromTheSubscriber()
	{
		var consumer = A.Fake<IConsumer<string, byte[]>>();
		var queue = new Queue<KafkaConsumeResult>([RecordWithNullHeader(0)]);
		A.CallTo<KafkaConsumeResult?>(() => consumer.Consume(A<CancellationToken>._))
			.ReturnsLazily(() => queue.Count > 0 ? queue.Dequeue() : null);

		var subscriber = new KafkaTransportSubscriber(
			consumer, Topic, NullLogger<KafkaTransportSubscriber>.Instance, 1024, false, new KafkaPartitionProgress());
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		TransportReceivedMessage? handled = null;

		await subscriber.SubscribeAsync(
			(message, _) =>
			{
				handled = message;
				cts.Cancel();
				return Task.FromResult(MessageAction.Acknowledge);
			},
			cts.Token);

		_ = handled.ShouldNotBeNull("the message must reach the handler");
		handled.Properties["trace"].ShouldBe("abc");
	}

	/// <summary>
	/// SAFETY. A commit the broker accepted for a previous tenure does not suppress the current tenure's commits.
	/// </summary>
	/// <remarks>
	/// RED against a commit record that ignores generation: the late acknowledgement of position 10 makes the
	/// new tenure treat position 6 as already held, so it never commits it.
	/// </remarks>
	[Fact]
	public void IgnoreACommitAcknowledgedForAPreviousTenure()
	{
		var progress = new KafkaPartitionProgress();
		progress.OnPartitionsAssigned([Partition0]);
		progress.TryBeginDelivery(Partition0, 9, out var oldTenure).ShouldBeTrue();
		progress.TrySettle(Partition0, 9, oldTenure, out var oldCommit).ShouldBeTrue();
		oldCommit.ShouldBe(10);

		progress.OnPartitionsRevoked([Partition0]);
		progress.OnPartitionsAssigned([Partition0]);
		progress.TryBeginDelivery(Partition0, 5, out var newTenure).ShouldBeTrue();

		// The old tenure's commit is acknowledged late, after the partition came back.
		progress.OnCommitted(Partition0, oldCommit, oldTenure);

		progress.TrySettle(Partition0, 5, newTenure, out var newCommit).ShouldBeTrue("the new tenure must still commit");
		newCommit.ShouldBe(6);
	}

	private static KafkaConsumeResult RecordWithNullHeader(long offset)
	{
		var headers = new Headers
		{
			new Header("trace", "abc"u8.ToArray()),
			new Header("empty", null),
		};

		return new KafkaConsumeResult
		{
			Topic = Topic,
			Partition = new Partition(0),
			Offset = new Offset(offset),
			Message = new Message<string, byte[]> { Key = $"m{offset}", Value = new byte[4], Headers = headers },
		};
	}
}
