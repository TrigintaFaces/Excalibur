// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Confluent.Kafka;

using Excalibur.Dispatch.Transport.Kafka;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using KafkaConsumeResult = global::Confluent.Kafka.ConsumeResult<string, byte[]>;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.DeadLetter;

/// <summary>
/// Reading the dead-letter queue does not consume it: the committed position moves only when the caller
/// reports what it has taken ownership of.
/// </summary>
/// <remarks>
/// A dead-letter queue holds the last copy of a message. Committing as the messages are handed out meant a
/// caller that threw, crashed, or simply dropped the returned list lost them with no redelivery and no
/// further copy anywhere.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class KafkaDeadLetterCommitOwnershipShould
{
	private const string Topic = "orders.dead-letter";

	/// <summary>
	/// SAFETY. Reading returns the messages and commits nothing, so a caller that fails still sees them.
	/// </summary>
	/// <remarks>RED against a read that commits the batch before the caller has done anything with it.</remarks>
	[Fact]
	public void CommitNothing_WhenMessagesAreOnlyRead()
	{
		var consumer = Consumer(Record(10), Record(11));
		using var reader = new KafkaDeadLetterConsumer(consumer, Options(), NullLogger<KafkaDeadLetterConsumer>.Instance);

		var messages = reader.Consume(Topic, maxMessages: 10, CancellationToken.None);

		messages.Count.ShouldBe(2);
		A.CallTo(() => consumer.Commit(A<IEnumerable<TopicPartitionOffset>>._)).MustNotHaveHappened();
		A.CallTo(() => consumer.Commit()).MustNotHaveHappened();
		A.CallTo(() => consumer.StoreOffset(A<TopicPartitionOffset>._)).MustNotHaveHappened();
	}

	/// <summary>
	/// LIVENESS. A caller that reports ownership advances the position past exactly those messages: one
	/// past the highest processed offset per partition.
	/// </summary>
	/// <remarks>RED against a commit that never happens, which would re-read the same messages forever.</remarks>
	[Fact]
	public void CommitOnePastTheHighestProcessedOffset_PerPartition()
	{
		var consumer = Consumer(Record(10), Record(11), Record(4, partition: 1));
		using var reader = new KafkaDeadLetterConsumer(consumer, Options(), NullLogger<KafkaDeadLetterConsumer>.Instance);
		var committed = new List<TopicPartitionOffset>();
		A.CallTo(() => consumer.Commit(A<IEnumerable<TopicPartitionOffset>>._))
			.Invokes((IEnumerable<TopicPartitionOffset> offsets) => committed.AddRange(offsets));

		var messages = reader.Consume(Topic, maxMessages: 10, CancellationToken.None);
		reader.CommitProcessed(messages);

		committed.Count.ShouldBe(2, "one position per partition");
		committed.Single(o => o.Partition.Value == 0).Offset.Value.ShouldBe(12);
		committed.Single(o => o.Partition.Value == 1).Offset.Value.ShouldBe(5);
	}

	/// <summary>
	/// SAFETY. Only what the caller processed is committed, so a message it could not handle is read again.
	/// </summary>
	/// <remarks>RED against a commit that advances past the whole batch regardless of the caller's outcome.</remarks>
	[Fact]
	public void CommitOnlyWhatTheCallerProcessed()
	{
		var consumer = Consumer(Record(10), Record(11), Record(12));
		using var reader = new KafkaDeadLetterConsumer(consumer, Options(), NullLogger<KafkaDeadLetterConsumer>.Instance);
		var committed = new List<TopicPartitionOffset>();
		A.CallTo(() => consumer.Commit(A<IEnumerable<TopicPartitionOffset>>._))
			.Invokes((IEnumerable<TopicPartitionOffset> offsets) => committed.AddRange(offsets));

		var messages = reader.Consume(Topic, maxMessages: 10, CancellationToken.None);

		// The caller persisted the first message and failed on the rest.
		reader.CommitProcessed(messages.Take(1));

		committed.ShouldHaveSingleItem().Offset.Value.ShouldBe(11,
			"offsets 11 and 12 were never taken, so they must be read again");
	}

	private static IConsumer<string, byte[]> Consumer(params KafkaConsumeResult[] records)
	{
		var consumer = A.Fake<IConsumer<string, byte[]>>();
		var queue = new Queue<KafkaConsumeResult?>(records);
		A.CallTo<KafkaConsumeResult?>(() => consumer.Consume(A<TimeSpan>._))
			.ReturnsLazily(() => queue.Count > 0 ? queue.Dequeue() : null);
		return consumer;
	}

	private static IOptions<KafkaDeadLetterOptions> Options() =>
		Microsoft.Extensions.Options.Options.Create(new KafkaDeadLetterOptions());

	private static KafkaConsumeResult Record(long offset, int partition = 0) =>
		new()
		{
			Topic = Topic,
			Partition = new Partition(partition),
			Offset = new Offset(offset),
			Message = new Message<string, byte[]>
			{
				Key = $"m{offset}",
				Value = [1, 2, 3],
				Headers = [],
				Timestamp = new Timestamp(DateTimeOffset.UtcNow),
			},
		};
}
