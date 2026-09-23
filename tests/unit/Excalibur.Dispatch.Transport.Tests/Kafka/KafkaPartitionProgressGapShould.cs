// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Confluent.Kafka;

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka;

/// <summary>
/// The committed position steps over offsets that were never delivered, and only those.
/// </summary>
/// <remarks>
/// <para>
/// Kafka offsets are not contiguous. A transaction commit or abort marker occupies an offset and is never
/// handed to the application, and compaction removes records. A tracker that advances only through
/// settled offsets halts at the first such gap for good: nothing is committed again, every restart
/// redelivers everything after the gap, and the terminal set grows without bound.
/// </para>
/// <para>
/// The invariant these arms bind: the position may pass an offset only if that offset is terminal or was
/// never delivered in this tenure. The first arm is the liveness half the tracker lacked; the second and
/// third are the safety half a careless fix would break -- a gap must not let the position pass work
/// still owed, and a seek-back replay must not be mistaken for a gap.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class KafkaPartitionProgressGapShould
{
	private static readonly TopicPartition Partition0 = new("orders", new Partition(0));

	/// <summary>
	/// LIVENESS. An offset the fetch skipped does not hold the commit.
	/// </summary>
	/// <remarks>RED against a drain that advances only through settled offsets: it stops at 12 for good.</remarks>
	[Fact]
	public void CommitPastAnOffsetThatWasNeverDelivered()
	{
		var progress = new KafkaPartitionProgress();

		var g10 = Begin(progress, 10);
		var g11 = Begin(progress, 11);
		var g13 = Begin(progress, 13); // 12 is a transaction marker: never delivered

		Settle(progress, 10, g10).ShouldBe(11);

		// 12 was never delivered, so once 11 settles the position runs to the first offset still owed: 13.
		// It must not stop at 12 (nothing is owed there) and must not pass 13 (which is still in hand).
		Settle(progress, 11, g11).ShouldBe(
			13,
			"offset 12 was never delivered, so nothing is owed for it and the commit must not stop there");
		Settle(progress, 13, g13).ShouldBe(14);
	}

	/// <summary>
	/// SAFETY. A gap after an owed offset does not let the commit pass the owed offset.
	/// </summary>
	[Fact]
	public void HoldTheCommitAtAnOwedOffset_EvenWithAGapAfterIt()
	{
		var progress = new KafkaPartitionProgress();

		var g10 = Begin(progress, 10);
		var g13 = Begin(progress, 13); // 11 and 12 never delivered

		Settle(progress, 13, g13).ShouldBeNull("offset 10 is still owed, so no commit may be emitted yet");
		Settle(progress, 10, g10).ShouldBe(14, "once 10 settles, the never-delivered range is stepped over");
	}

	/// <summary>
	/// SAFETY. A replay after a seek-back is not a gap, so the offset it replays stays owed.
	/// </summary>
	/// <remarks>
	/// The replay arrives BELOW the highest offset fetched. Treating that as a skipped range would mark a
	/// requeued message as not owed and let the commit pass it -- the loss the tracker exists to prevent.
	/// </remarks>
	[Fact]
	public void KeepARequeuedOffsetOwed_WhenItIsReplayedAfterASeekBack()
	{
		var progress = new KafkaPartitionProgress();

		var g10 = Begin(progress, 10);
		var g11 = Begin(progress, 11);

		progress.TryRequeue(Partition0, 10, g10, out var seekOffset).ShouldBe(KafkaRequeueOutcome.Requeued);
		seekOffset.ShouldBe(10);

		Settle(progress, 11, g11).ShouldBeNull("10 was requeued and is still owed");

		var replay = Begin(progress, 10);
		Settle(progress, 10, replay).ShouldBe(12);
	}

	private static long Begin(KafkaPartitionProgress progress, long offset)
	{
		progress.TryBeginDelivery(Partition0, offset, out var generation).ShouldBeTrue();
		return generation;
	}

	private static long? Settle(KafkaPartitionProgress progress, long offset, long generation)
	{
		if (!progress.TrySettle(Partition0, offset, generation, out var commitOffset))
		{
			return null;
		}

		progress.OnCommitted(Partition0, commitOffset, generation);
		return commitOffset;
	}
}
