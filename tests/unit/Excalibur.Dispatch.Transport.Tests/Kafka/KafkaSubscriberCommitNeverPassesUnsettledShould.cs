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
/// The subscriber's committed offset is the contiguous prefix of settled work: it never passes a message
/// that has not reached a decision.
/// </summary>
/// <remarks>
/// <para>
/// The subscriber used to commit each message's own offset plus one as soon as that message was settled.
/// Because Kafka offsets are positional, settling a later message committed past an earlier one still
/// owed -- which is how a handler that threw lost its message. Routing settlement through the same
/// progress tracker the receiver uses makes that commit impossible to produce: the tracker only ever
/// yields the contiguous prefix.
/// </para>
/// <para>
/// The fake client hands back the next queued record regardless of any seek, which is exactly the shape a
/// later concurrent consume loop would present: a later message settled while an earlier one is still
/// owed. Only the Kafka client is faked; the tracker is real.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class KafkaSubscriberCommitNeverPassesUnsettledShould
{
	private const string Topic = "orders";

	/// <summary>
	/// SAFETY. A message whose handler throws is redelivered before anything after it is committed.
	/// </summary>
	/// <remarks>
	/// RED against a subscriber that does not seek back on a throw: it moves on to message 1, commits 2 when that
	/// is acknowledged, and message 0 is lost. The fake log honours seeks, as a real consumer does, so the
	/// redelivery is observed in the order a broker would produce it.
	/// </remarks>
	[Fact]
	public async Task HoldTheCommit_UntilEveryEarlierMessageHasSettled()
	{
		var commits = new List<long>();
		var consumer = FakeConsumer([Record(0), Record(1)], commits);
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

		var firstZero = true;
		var settled = 0;
		var subscriber = CreateSubscriber(consumer);

		await subscriber.SubscribeAsync(
			(message, _) =>
			{
				var offset = (long)message.ProviderData[TransportOrderingMetadata.KafkaOffsetKey];
				if (offset == 0 && firstZero)
				{
					firstZero = false;
					throw new InvalidOperationException("handler failure on offset 0");
				}

				if (++settled == 2)
				{
					cts.Cancel();
				}

				return Task.FromResult(MessageAction.Acknowledge);
			},
			cts.Token);

		commits.ShouldBe(
			[1L, 2L],
			"offset 0 must be redelivered and settled before anything after it is committed; committing 2 first "
				+ "would pass a message that had not reached a decision");
	}

	/// <summary>
	/// LIVENESS. In-order settlement still advances the committed position message by message.
	/// </summary>
	/// <remarks>
	/// Without this arm, a subscriber that never committed at all would satisfy the safety arm above.
	/// </remarks>
	[Fact]
	public async Task AdvanceTheCommit_WhenMessagesSettleInOrder()
	{
		var commits = new List<long>();
		var consumer = FakeConsumer([Record(0), Record(1)], commits);
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

		var settled = 0;
		await CreateSubscriber(consumer).SubscribeAsync(
			(_, _) =>
			{
				if (++settled == 2)
				{
					cts.Cancel();
				}

				return Task.FromResult(MessageAction.Acknowledge);
			},
			cts.Token);

		commits.ShouldBe([1L, 2L]);
	}

	private static KafkaConsumeResult Record(long offset) =>
		new()
		{
			Topic = Topic,
			Partition = new Partition(0),
			Offset = new Offset(offset),
			Message = new Message<string, byte[]> { Key = $"m{offset}", Value = new byte[4] },
		};

	private static KafkaTransportSubscriber CreateSubscriber(IConsumer<string, byte[]> consumer) =>
		new(consumer, Topic, NullLogger<KafkaTransportSubscriber>.Instance, 1024, false, new KafkaPartitionProgress());

	/// <summary>
	/// A single-partition log with a read cursor: each consume returns the record at the cursor and advances it,
	/// and a seek moves the cursor, as a real consumer does.
	/// </summary>
	private static IConsumer<string, byte[]> FakeConsumer(IReadOnlyList<KafkaConsumeResult> log, List<long> commits)
	{
		var consumer = A.Fake<IConsumer<string, byte[]>>();
		var cursor = 0;

		A.CallTo<KafkaConsumeResult?>(() => consumer.Consume(A<CancellationToken>._))
			.ReturnsLazily(() => cursor < log.Count ? log[cursor++] : null);

		A.CallTo(() => consumer.Seek(A<TopicPartitionOffset>._))
			.Invokes((TopicPartitionOffset target) =>
			{
				var index = log.ToList().FindIndex(r => r.Offset.Value >= target.Offset.Value);
				cursor = index < 0 ? log.Count : index;
			});

		A.CallTo(() => consumer.Commit(A<IEnumerable<TopicPartitionOffset>>._))
			.Invokes((IEnumerable<TopicPartitionOffset> offsets) => commits.AddRange(offsets.Select(o => o.Offset.Value)));

		return consumer;
	}
}
