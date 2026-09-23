// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Confluent.Kafka;

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka;

/// <summary>
/// Locks the offset arithmetic that decides where a Kafka consumer is allowed to commit.
/// </summary>
/// <remarks>
/// <para>
/// A Kafka commit is one number per partition meaning "everything below this is dealt with", so the only
/// position that can honestly be committed is the contiguous terminal prefix. These arms bind that: a
/// commit may never cross an offset that is still owed, may never move backwards, and — the liveness
/// half, without which a tracker that refuses every commit would pass — must advance as soon as the
/// earlier work settles.
/// </para>
/// <para>
/// The arithmetic lives in a type with no Kafka client in it precisely so these cases are decidable
/// without a broker. The real-broker arms that prove the same properties end to end are in the
/// integration suite; these are the fast, exhaustive-on-boundaries half.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class KafkaPartitionProgressShould
{
	private static readonly TopicPartition Tp = new("orders", new Partition(0));
	private static readonly TopicPartition Other = new("orders", new Partition(1));

	private static KafkaPartitionProgress Assigned()
	{
		var progress = new KafkaPartitionProgress();
		progress.OnPartitionsAssigned([Tp, Other]);
		return progress;
	}

	private static long Deliver(KafkaPartitionProgress progress, long offset, TopicPartition? partition = null)
	{
		progress.TryBeginDelivery(partition ?? Tp, offset, out var generation).ShouldBeTrue();
		return generation;
	}

	// ---- Safety: a commit never crosses work that is still owed ----

	[Fact]
	public void NotCommit_WhenAnEarlierOffsetIsStillOutstanding()
	{
		// The reproduction the guarantee exists for: offset 5 is in a handler, offset 6 turns out to be
		// unprocessable and settles immediately. Committing 7 here would tell the broker 5 was handled,
		// and a restart would resume past it.
		var progress = Assigned();
		var generation = Deliver(progress, 5);
		_ = Deliver(progress, 6);

		progress.TrySettle(Tp, 6, generation, out _).ShouldBeFalse();
	}

	[Fact]
	public void Commit_OnceTheOutstandingEarlierOffsetSettles()
	{
		// Liveness partner to the arm above: refusing every commit also satisfies "never crosses owed
		// work", so the prefix must be shown to move the moment the gap closes.
		var progress = Assigned();
		var generation = Deliver(progress, 5);
		_ = Deliver(progress, 6);
		progress.TrySettle(Tp, 6, generation, out _).ShouldBeFalse();

		progress.TrySettle(Tp, 5, generation, out var commit).ShouldBeTrue();
		commit.ShouldBe(7); // 5 and 6 are both terminal, so the next offset to read is 7
	}

	[Fact]
	public void NotCommit_WhenAcknowledgedOutOfOrder_UntilThePrefixCloses()
	{
		var progress = Assigned();
		var generation = Deliver(progress, 0);
		_ = Deliver(progress, 1);
		_ = Deliver(progress, 2);

		progress.TrySettle(Tp, 2, generation, out _).ShouldBeFalse();
		progress.TrySettle(Tp, 1, generation, out _).ShouldBeFalse();

		progress.TrySettle(Tp, 0, generation, out var commit).ShouldBeTrue();
		commit.ShouldBe(3); // all three drain in one step once the frontier is terminal
	}

	[Fact]
	public void NotRegress_WhenALaterSettleFollowsAHigherCommit()
	{
		var progress = Assigned();
		var generation = Deliver(progress, 0);
		_ = Deliver(progress, 1);

		progress.TrySettle(Tp, 0, generation, out var first).ShouldBeTrue();
		first.ShouldBe(1);
		progress.OnCommitted(Tp, first, generation);

		progress.TrySettle(Tp, 1, generation, out var second).ShouldBeTrue();
		second.ShouldBe(2);
		second.ShouldBeGreaterThan(first);
	}

	[Fact]
	public void NotCommitTwice_ForAPositionAlreadyAccepted()
	{
		var progress = Assigned();
		var generation = Deliver(progress, 0);
		progress.TrySettle(Tp, 0, generation, out var commit).ShouldBeTrue();
		progress.OnCommitted(Tp, commit, generation);

		// Settling an offset the prefix has already passed must not re-emit the same position.
		progress.TrySettle(Tp, 0, generation, out _).ShouldBeFalse();
	}

	[Fact]
	public void TrackPartitionsIndependently()
	{
		// One partition's stalled prefix must not hold back another's; a Kafka commit is per-partition.
		var progress = Assigned();
		var generation = Deliver(progress, 10);
		_ = Deliver(progress, 11);
		_ = Deliver(progress, 40, Other);

		progress.TrySettle(Tp, 11, generation, out _).ShouldBeFalse();
		progress.TrySettle(Other, 40, generation, out var commit).ShouldBeTrue();
		commit.ShouldBe(41);
	}

	// ---- Retry: a requeue makes progress inside the same healthy consumer ----

	[Fact]
	public void SeekToTheRequeuedOffset_AndNotLetThePrefixPassIt()
	{
		var progress = Assigned();
		var generation = Deliver(progress, 4);
		_ = Deliver(progress, 5);

		progress.TryRequeue(Tp, 4, generation, out var seek).ShouldBe(KafkaRequeueOutcome.Requeued);
		seek.ShouldBe(4);

		// A requeued offset is never terminal, so settling the one above it cannot move the position.
		progress.TrySettle(Tp, 5, generation, out _).ShouldBeFalse();
	}

	[Fact]
	public void SeekToTheLowestRequeuedOffset_WhenSeveralAreRequeued()
	{
		var progress = Assigned();
		var generation = Deliver(progress, 4);
		_ = Deliver(progress, 5);

		progress.TryRequeue(Tp, 5, generation, out var first).ShouldBe(KafkaRequeueOutcome.Requeued);
		first.ShouldBe(5);

		// Requeuing the earlier offset afterwards must pull the seek back, not leave it ahead — a seek
		// that only ever moved forward would strand 4.
		progress.TryRequeue(Tp, 4, generation, out var second).ShouldBe(KafkaRequeueOutcome.Requeued);
		second.ShouldBe(4);
	}

	[Fact]
	public void RedeliverARequeuedOffset_ButNotOneAlreadySettledOrStillInFlight()
	{
		var progress = Assigned();
		var generation = Deliver(progress, 0);
		_ = Deliver(progress, 1);
		_ = Deliver(progress, 2);

		progress.TrySettle(Tp, 2, generation, out _).ShouldBeFalse(); // 2 is terminal, gap at 0 and 1
		progress.TryRequeue(Tp, 0, generation, out _).ShouldBe(KafkaRequeueOutcome.Requeued);

		// The seek replays 0, 1 and 2. Only 0 was requeued.
		progress.TryBeginDelivery(Tp, 0, out _).ShouldBeTrue();
		progress.TryBeginDelivery(Tp, 1, out _).ShouldBeFalse(); // still in a handler from the first delivery
		progress.TryBeginDelivery(Tp, 2, out _).ShouldBeFalse(); // already terminal
	}

	[Fact]
	public void CommitPastARequeuedOffset_OnlyAfterTheRetrySucceeds()
	{
		// Liveness partner to the two arms above: a requeue that blocks the prefix forever is not a retry.
		var progress = Assigned();
		var generation = Deliver(progress, 4);
		_ = Deliver(progress, 5);
		progress.TrySettle(Tp, 5, generation, out _).ShouldBeFalse();
		progress.TryRequeue(Tp, 4, generation, out _).ShouldBe(KafkaRequeueOutcome.Requeued);

		_ = Deliver(progress, 4); // redelivered by the seek
		progress.TrySettle(Tp, 4, generation, out var commit).ShouldBeTrue();
		commit.ShouldBe(6);
	}

	// ---- Ownership: a stale generation cannot settle the new tenure ----

	[Fact]
	public void RefuseASettleCarryingAPreviousGeneration()
	{
		var progress = Assigned();
		var stale = Deliver(progress, 7);

		progress.OnPartitionsRevoked([Tp]);
		progress.OnPartitionsAssigned([Tp]);

		progress.TrySettle(Tp, 7, stale, out _).ShouldBeFalse();
		progress.TryRequeue(Tp, 7, stale, out _).ShouldBe(KafkaRequeueOutcome.StaleGeneration);
	}

	[Fact]
	public void AcceptASettleUnderTheCurrentGeneration_AfterAReassignment()
	{
		// Liveness partner: refusing everything after a rebalance would also satisfy the arm above.
		var progress = Assigned();
		_ = Deliver(progress, 7);
		progress.OnPartitionsRevoked([Tp]);
		progress.OnPartitionsAssigned([Tp]);

		var current = Deliver(progress, 7);
		progress.TrySettle(Tp, 7, current, out var commit).ShouldBeTrue();
		commit.ShouldBe(8);
	}

	[Fact]
	public void RestartThePositionFromTheFirstOffsetOfTheNewTenure()
	{
		// The new owner resumes from the broker's committed position, which may be anywhere; the tracker
		// must adopt whatever it is first handed rather than assuming the previous tenure's frontier.
		var progress = Assigned();
		_ = Deliver(progress, 100);
		progress.OnPartitionsRevoked([Tp]);
		progress.OnPartitionsAssigned([Tp]);

		var current = Deliver(progress, 42);
		progress.TrySettle(Tp, 42, current, out var commit).ShouldBeTrue();
		commit.ShouldBe(43);
	}

	// ---- "Not determined" is a state, distinct from offset zero ----

	[Fact]
	public void ReportNoPosition_BeforeAnythingIsFetched()
	{
		var progress = Assigned();

		// An untouched partition has no position. Reporting 0 would claim the consumer is at the start of
		// the partition, which is a different and possibly false statement.
		progress.TryGetPosition(Tp, out var position).ShouldBeFalse();
		position.ShouldBeNull();
	}

	[Fact]
	public void ReportZero_WhenOffsetZeroIsGenuinelyTheFrontier()
	{
		var progress = Assigned();
		_ = Deliver(progress, 0);

		progress.TryGetPosition(Tp, out var position).ShouldBeTrue();
		position.ShouldBe(0L);
	}

	[Fact]
	public void ReportOutstandingWork_WhileMessagesAreUnsettled()
	{
		var progress = Assigned();
		var generation = Deliver(progress, 0);
		_ = Deliver(progress, 1);
		progress.Outstanding.ShouldBe(2);

		progress.TrySettle(Tp, 0, generation, out _).ShouldBeTrue();
		progress.Outstanding.ShouldBe(1);
	}

	[Fact]
	public void DistinguishARefusedSettleFromOneThatSimplyDidNotMoveThePrefix()
	{
		// Both return "nothing to commit". Reporting the first as a successful acknowledgment would hide a
		// rebalance from the operator, so the two must be separable.
		var progress = Assigned();
		var generation = Deliver(progress, 0);
		_ = Deliver(progress, 1);

		progress.IsCurrentGeneration(Tp, generation).ShouldBeTrue();
		progress.TrySettle(Tp, 1, generation, out _).ShouldBeFalse(); // held by the gap at 0, not refused

		progress.OnPartitionsRevoked([Tp]);
		progress.OnPartitionsAssigned([Tp]);
		progress.IsCurrentGeneration(Tp, generation).ShouldBeFalse();
	}

	[Fact]
	public void IgnoreASettleForAnUntrackedPartition()
	{
		var progress = new KafkaPartitionProgress();

		progress.TrySettle(Tp, 0, generation: 0, out _).ShouldBeFalse();
		progress.TryRequeue(Tp, 0, generation: 0, out _).ShouldBe(KafkaRequeueOutcome.StaleGeneration);
		progress.TryGetPosition(Tp, out _).ShouldBeFalse();
		progress.IsCurrentGeneration(Tp, 0).ShouldBeFalse();
	}

	[Fact]
	public void RejectNullArguments()
	{
		var progress = new KafkaPartitionProgress();

		_ = Should.Throw<ArgumentNullException>(() => progress.TryBeginDelivery(null!, 0, out _));
		_ = Should.Throw<ArgumentNullException>(() => progress.TrySettle(null!, 0, 0, out _));
		_ = Should.Throw<ArgumentNullException>(() => progress.IsCurrentGeneration(null!, 0));
		_ = Should.Throw<ArgumentNullException>(() => progress.TryRequeue(null!, 0, 0, out _));
		_ = Should.Throw<ArgumentNullException>(() => progress.OnCommitted(null!, 0, 0));
		_ = Should.Throw<ArgumentNullException>(() => progress.TryGetPosition(null!, out _));
		_ = Should.Throw<ArgumentNullException>(() => progress.OnPartitionsAssigned(null!));
		_ = Should.Throw<ArgumentNullException>(() => progress.OnPartitionsRevoked(null!));
	}
}
