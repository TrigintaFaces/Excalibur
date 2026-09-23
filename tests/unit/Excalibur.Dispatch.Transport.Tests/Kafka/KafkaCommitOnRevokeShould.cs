// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Confluent.Kafka;

using Excalibur.Dispatch.Transport.Kafka;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.Kafka;

/// <summary>
/// A rebalance publishes the position the tracker holds — the lowest offset still owed — as an EXPLICIT
/// offset, so the partition's next owner never resumes past work that was still in flight.
/// </summary>
/// <remarks>
/// An argument-less commit publishes librdkafka's stored offsets, which is a different claim: it is equal
/// to the tracked position only while nothing else writes the store, so the guarantee rested on a property
/// of the whole composition rather than on the call. These arms bind the call itself.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class KafkaCommitOnRevokeShould
{
	private const string Topic = "orders";
	private static readonly TopicPartition Partition0 = new(Topic, new Partition(0));

	/// <summary>
	/// SAFETY. With work still in flight the revoke commits the lowest offset still owed, never a position
	/// past it.
	/// </summary>
	/// <remarks>
	/// RED against a revoke that commits the consumer's stored offsets with an argument-less call: nothing
	/// names offset 11, and an unsettled offset can be committed past.
	/// </remarks>
	[Fact]
	public void CommitTheLowestOwedOffset_WhenPartitionsAreRevokedWithWorkInFlight()
	{
		var consumer = A.Fake<IConsumer<string, byte[]>>();
		var committed = new List<TopicPartitionOffset>();
		A.CallTo(() => consumer.Commit(A<IEnumerable<TopicPartitionOffset>>._))
			.Invokes((IEnumerable<TopicPartitionOffset> offsets) => committed.AddRange(offsets));

		var progress = new KafkaPartitionProgress();
		progress.OnPartitionsAssigned([Partition0]);
		progress.BeginDelivery(Partition0, 10).Deliver.ShouldBeTrue();
		progress.BeginDelivery(Partition0, 11).Deliver.ShouldBeTrue();

		// 10 is finished; 11 is still inside a handler.
		progress.TrySettle(Partition0, 10, generation: 1, out _).ShouldBeTrue();

		KafkaConsumerRebalance.CommitOnRevoke(consumer, [Partition0], progress, NullLogger.Instance);

		var offset = committed.ShouldHaveSingleItem();
		offset.TopicPartition.ShouldBe(Partition0);
		offset.Offset.Value.ShouldBe(11, "11 is still owed, so the next owner must resume at it");
		A.CallTo(() => consumer.Commit()).MustNotHaveHappened();
	}

	/// <summary>
	/// LIVENESS. A revoke with nothing in flight still publishes the settled prefix, so the fix is not
	/// merely a refusal to commit.
	/// </summary>
	/// <remarks>RED against a revoke that commits nothing, which would redeliver settled work forever.</remarks>
	[Fact]
	public void CommitTheSettledPrefix_WhenPartitionsAreRevokedWithNothingInFlight()
	{
		var consumer = A.Fake<IConsumer<string, byte[]>>();
		var committed = new List<TopicPartitionOffset>();
		A.CallTo(() => consumer.Commit(A<IEnumerable<TopicPartitionOffset>>._))
			.Invokes((IEnumerable<TopicPartitionOffset> offsets) => committed.AddRange(offsets));

		var progress = new KafkaPartitionProgress();
		progress.OnPartitionsAssigned([Partition0]);
		progress.BeginDelivery(Partition0, 10).Deliver.ShouldBeTrue();
		progress.BeginDelivery(Partition0, 11).Deliver.ShouldBeTrue();
		progress.TrySettle(Partition0, 10, generation: 1, out _).ShouldBeTrue();
		progress.TrySettle(Partition0, 11, generation: 1, out _).ShouldBeTrue();

		KafkaConsumerRebalance.CommitOnRevoke(consumer, [Partition0], progress, NullLogger.Instance);

		committed.ShouldHaveSingleItem().Offset.Value.ShouldBe(12, "everything through 11 is settled");
	}

	/// <summary>
	/// SAFETY. With nothing settled, the revoke commits the in-flight offset ITSELF -- the resume point --
	/// never a position past it.
	/// </summary>
	/// <remarks>
	/// The committed position is the lowest offset still owed, so a message inside a handler is where the
	/// next owner starts. RED against a revoke that publishes the stored offsets, which name one PAST every
	/// message handed out.
	/// </remarks>
	[Fact]
	public void CommitTheInFlightOffsetItself_WhenNothingHasSettled()
	{
		var consumer = A.Fake<IConsumer<string, byte[]>>();
		var committed = new List<TopicPartitionOffset>();
		A.CallTo(() => consumer.Commit(A<IEnumerable<TopicPartitionOffset>>._))
			.Invokes((IEnumerable<TopicPartitionOffset> offsets) => committed.AddRange(offsets));

		var progress = new KafkaPartitionProgress();
		progress.OnPartitionsAssigned([Partition0]);
		progress.BeginDelivery(Partition0, 10).Deliver.ShouldBeTrue();

		KafkaConsumerRebalance.CommitOnRevoke(consumer, [Partition0], progress, NullLogger.Instance);

		committed.ShouldHaveSingleItem().Offset.Value.ShouldBe(10,
			"offset 10 is still inside a handler, so the next owner must resume AT it, not after it");
	}

	/// <summary>
	/// SAFETY. A partition this tenure never fetched from commits nothing: there is no position it earned,
	/// and publishing one would name work this consumer never did.
	/// </summary>
	/// <remarks>RED against a revoke that commits a position for a partition with no delivered work.</remarks>
	[Fact]
	public void CommitNothing_ForAPartitionThisTenureNeverFetchedFrom()
	{
		var consumer = A.Fake<IConsumer<string, byte[]>>();

		var progress = new KafkaPartitionProgress();
		progress.OnPartitionsAssigned([Partition0]);

		KafkaConsumerRebalance.CommitOnRevoke(consumer, [Partition0], progress, NullLogger.Instance);

		A.CallTo(() => consumer.Commit(A<IEnumerable<TopicPartitionOffset>>._)).MustNotHaveHappened();
		A.CallTo(() => consumer.Commit()).MustNotHaveHappened();
	}
}
