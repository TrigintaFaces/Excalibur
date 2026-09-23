// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Confluent.Kafka;

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka;

/// <summary>
/// A requeue names only work that is still owed, and asking twice is the same as asking once.
/// </summary>
/// <remarks>
/// <para>
/// A requeue that accepted an offset already settled would put it in the redelivery set permanently: its
/// replay is refused as already handed out, so nothing ever takes it back out, and every later requeue
/// seeks back to it and replays an ever larger range. The opposite mistake -- refusing an offset that is
/// already awaiting redelivery -- turns a retried requeue whose seek threw into a stall: the offset stays
/// owed with no seek pending.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class KafkaPartitionProgressRequeueShould
{
	private static readonly TopicPartition Partition0 = new("orders", new Partition(0));

	/// <summary>
	/// SAFETY. A settled offset is not owed, so requeuing it changes nothing.
	/// </summary>
	/// <remarks>RED against a requeue that adds any offset it is given: the next seek target becomes 10.</remarks>
	[Fact]
	public void RefuseToRequeueASettledOffset_SoItCannotPinTheSeekTarget()
	{
		var progress = new KafkaPartitionProgress();
		var g10 = Begin(progress, 10);
		var g11 = Begin(progress, 11);
		progress.TrySettle(Partition0, 10, g10, out var committed).ShouldBeTrue();
		committed.ShouldBe(11);

		progress.TryRequeue(Partition0, 10, g10, out _).ShouldBe(KafkaRequeueOutcome.NotOutstanding);

		progress.TryRequeue(Partition0, 11, g11, out var seekOffset).ShouldBe(KafkaRequeueOutcome.Requeued);
		seekOffset.ShouldBe(11, "a settled offset must not become the lowest offset awaiting redelivery");
		progress.TryGetPosition(Partition0, out var position).ShouldBeTrue();
		position.ShouldBe(11);
	}

	/// <summary>
	/// LIVENESS. A requeue whose seek failed can be asked again and still gets a seek target.
	/// </summary>
	/// <remarks>RED against a requeue that refuses any offset not currently in someone's hands.</remarks>
	[Fact]
	public void RequeueIdempotently_SoARetriedSeekStillHasATarget()
	{
		var progress = new KafkaPartitionProgress();
		var g10 = Begin(progress, 10);

		progress.TryRequeue(Partition0, 10, g10, out var first).ShouldBe(KafkaRequeueOutcome.Requeued);
		progress.TryRequeue(Partition0, 10, g10, out var second).ShouldBe(KafkaRequeueOutcome.Requeued);

		second.ShouldBe(first);
		first.ShouldBe(10);
	}

	/// <summary>
	/// SAFETY. An offset this tenure never handed out is refused, and refused differently from a settled one.
	/// </summary>
	/// <remarks>
	/// RED against a tracker that reports every unowed offset as settled: the receiver maps "settled" to an
	/// idempotent success, so a handle for work nobody delivered would be confirmed as settled.
	/// </remarks>
	[Fact]
	public void RefuseToRequeueAnOffsetNeverDelivered_WithoutCallingItSettled()
	{
		var progress = new KafkaPartitionProgress();
		var g10 = Begin(progress, 10);

		progress.TryRequeue(Partition0, 15, g10, out _).ShouldBe(KafkaRequeueOutcome.NeverDelivered);
		progress.TryGetPosition(Partition0, out var position).ShouldBeTrue();
		position.ShouldBe(10);
	}

	/// <summary>
	/// SAFETY. Once a partition is revoked, a receipt from its tenure is stale, not settled.
	/// </summary>
	/// <remarks>RED against a revoke that clears the tenure without ending it: the requeue reads "never delivered".</remarks>
	[Fact]
	public void TreatARequeueAfterARevokeAsStale()
	{
		var progress = new KafkaPartitionProgress();
		var g10 = Begin(progress, 10);

		progress.OnPartitionsRevoked([Partition0]);

		progress.TryRequeue(Partition0, 10, g10, out _).ShouldBe(KafkaRequeueOutcome.StaleGeneration);
		progress.TrySettle(Partition0, 10, g10, out _).ShouldBeFalse();
	}

	private static long Begin(KafkaPartitionProgress progress, long offset)
	{
		progress.TryBeginDelivery(Partition0, offset, out var generation).ShouldBeTrue();
		return generation;
	}
}
