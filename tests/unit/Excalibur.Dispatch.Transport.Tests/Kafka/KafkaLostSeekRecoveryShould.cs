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
/// A requeued offset whose seek was lost is sought back to when the fetch position passes it, and one that no
/// longer exists in the log is eventually released rather than pinning the partition forever.
/// </summary>
/// <remarks>
/// Before this, a requeue whose seek threw left the offset owed with nothing ever replaying it: the committed
/// position froze on it for the rest of the tenure, and a restart replayed everything after it.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class KafkaLostSeekRecoveryShould
{
	private const string Topic = "orders";
	private static readonly TopicPartition Partition0 = new(Topic, new Partition(0));

	/// <summary>
	/// LIVENESS. The fetch position passing an owed offset asks for a seek back to it, and the passing record is
	/// not delivered, because it will be fetched again after the seek.
	/// </summary>
	/// <remarks>RED against a tracker that does not look for owed offsets below the record it is given.</remarks>
	[Fact]
	public void AskForASeekBack_WhenTheFetchPassesAnOwedOffset()
	{
		var progress = Delivered(10, 11);
		progress.TryRequeue(Partition0, 10, 0, out _).ShouldBe(KafkaRequeueOutcome.Requeued);

		var passed = progress.BeginDelivery(Partition0, 12);
		passed.Deliver.ShouldBeFalse();
		passed.SeekTo.ShouldBe(10);

		var replay = progress.BeginDelivery(Partition0, 10);
		replay.Deliver.ShouldBeTrue("the owed offset is redelivered once the seek back takes effect");
		replay.SeekTo.ShouldBeNull();
	}

	/// <summary>
	/// LIVENESS. An owed offset that two seeks back could not find is released, and the position moves past it.
	/// </summary>
	/// <remarks>
	/// Compaction can remove a requeued record before its replay. RED against a tracker that never releases it:
	/// every fetch asks for the same seek again, forever.
	/// </remarks>
	[Fact]
	public void ReleaseAnOwedOffset_AfterTwoSeeksBackEachReturnALaterRecord()
	{
		var progress = Delivered(10, 11);
		progress.TryRequeue(Partition0, 10, 0, out _).ShouldBe(KafkaRequeueOutcome.Requeued);
		progress.TrySettle(Partition0, 11, 0, out _).ShouldBeFalse("10 is still owed, so nothing commits yet");

		progress.BeginDelivery(Partition0, 12).SeekTo.ShouldBe(10);   // the seek was lost
		progress.NoteSeekIssued(Partition0, 10);
		progress.BeginDelivery(Partition0, 12).SeekTo.ShouldBe(10);   // first seek back: 10 is not there
		progress.NoteSeekIssued(Partition0, 10);

		var released = progress.BeginDelivery(Partition0, 12);        // second seek back: still not there
		released.AbandonedOffset.ShouldBe(10);
		released.Deliver.ShouldBeTrue("with 10 released, 12 is the next record to hand out");

		progress.TrySettle(Partition0, 12, released.Generation, out var commit).ShouldBeTrue();
		commit.ShouldBe(13);
	}

	/// <summary>
	/// SAFETY. One seek back that returns a later record is not enough to give up: if the owed record then
	/// arrives, it is delivered and stays owed until settled.
	/// </summary>
	/// <remarks>
	/// Releasing an offset that still exists would let the position pass a message that was never redelivered.
	/// RED against a release after a single observation.
	/// </remarks>
	[Fact]
	public void KeepAnOwedOffset_WhenItArrivesAfterOneMiss()
	{
		var progress = Delivered(10, 11);
		progress.TryRequeue(Partition0, 10, 0, out _).ShouldBe(KafkaRequeueOutcome.Requeued);

		progress.BeginDelivery(Partition0, 12).SeekTo.ShouldBe(10);
		progress.NoteSeekIssued(Partition0, 10);
		var miss = progress.BeginDelivery(Partition0, 12);
		miss.SeekTo.ShouldBe(10);
		miss.AbandonedOffset.ShouldBeNull();
		progress.NoteSeekIssued(Partition0, 10);

		var arrived = progress.BeginDelivery(Partition0, 10);
		arrived.Deliver.ShouldBeTrue();
		progress.TryGetPosition(Partition0, out var position).ShouldBeTrue();
		position.ShouldBe(10, "10 is in a handler's hands, so the position cannot pass it");
	}

	/// <summary>
	/// SAFETY. Seeks back that were never performed prove nothing, however many records pass the owed offset.
	/// </summary>
	/// <remarks>
	/// RED against counting every record past the owed offset: two seeks that threw would release an offset that
	/// was never re-fetched, and the position would pass a message that was never redelivered.
	/// </remarks>
	[Fact]
	public void NeverReleaseAnOwedOffset_OnSeeksThatWereNotPerformed()
	{
		var progress = Delivered(10, 11);
		progress.TryRequeue(Partition0, 10, 0, out _).ShouldBe(KafkaRequeueOutcome.Requeued);

		for (var offset = 12L; offset < 20; offset++)
		{
			var decision = progress.BeginDelivery(Partition0, offset);
			decision.SeekTo.ShouldBe(10);
			decision.AbandonedOffset.ShouldBeNull($"no seek back was performed before offset {offset}");
			decision.Deliver.ShouldBeFalse();
		}

		progress.TryGetPosition(Partition0, out var position).ShouldBeTrue();
		position.ShouldBe(10, "10 is still owed");
	}

	/// <summary>
	/// SAFETY, through the receiver: seeks back that throw never release the owed offset.
	/// </summary>
	/// <remarks>RED against a receiver that reports the seek as performed before, or without, it succeeding.</remarks>
	[Fact]
	public async Task NeverReleaseAnOwedOffset_FromTheReceiver_WhenEverySeekFails()
	{
		var consumer = A.Fake<IConsumer<string, byte[]>>();
		var queue = new Queue<KafkaConsumeResult?>([Record(10), Record(11), Record(12), Record(13), Record(14), Record(15)]);
		A.CallTo<KafkaConsumeResult?>(() => consumer.Consume(A<TimeSpan>._))
			.ReturnsLazily(() => queue.Count > 0 ? queue.Dequeue() : null);
		A.CallTo(() => consumer.Seek(A<TopicPartitionOffset>._)).Throws(new KafkaException(ErrorCode.Local_State));

		var progress = new KafkaPartitionProgress();
		progress.OnPartitionsAssigned([Partition0]);
		var receiver = new KafkaTransportReceiver(consumer, Topic, NullLogger<KafkaTransportReceiver>.Instance, 1024, false, progress);

		var first = await receiver.ReceiveAsync(2, CancellationToken.None);
		_ = await Should.ThrowAsync<KafkaException>(
			() => receiver.RejectAsync(first[0], "retry", requeue: true, CancellationToken.None));

		var next = await receiver.ReceiveAsync(10, CancellationToken.None);

		next.ShouldBeEmpty("nothing past the owed offset may be handed out while its seek back keeps failing");
		progress.TryGetPosition(Partition0, out var position).ShouldBeTrue();
		position.ShouldBe(10);
	}

	/// <summary>
	/// WIRING. Through the receiver: a requeue whose seek threw is recovered by the next fetch, which seeks back.
	/// </summary>
	/// <remarks>RED against a receiver that ignores the tracker's request to seek back.</remarks>
	[Fact]
	public async Task SeekBackFromTheReceiver_AfterARequeueWhoseSeekFailed()
	{
		var seeks = new List<long>();
		var failSeek = true;
		var consumer = A.Fake<IConsumer<string, byte[]>>();
		var queue = new Queue<KafkaConsumeResult?>([Record(10), Record(11), Record(12)]);
		A.CallTo<KafkaConsumeResult?>(() => consumer.Consume(A<TimeSpan>._))
			.ReturnsLazily(() => queue.Count > 0 ? queue.Dequeue() : null);
		A.CallTo(() => consumer.Seek(A<TopicPartitionOffset>._))
			.Invokes((TopicPartitionOffset tpo) =>
			{
				if (failSeek)
				{
					failSeek = false;
					throw new KafkaException(ErrorCode.Local_State);
				}

				seeks.Add(tpo.Offset.Value);
			});

		var progress = new KafkaPartitionProgress();
		progress.OnPartitionsAssigned([Partition0]);
		var receiver = new KafkaTransportReceiver(consumer, Topic, NullLogger<KafkaTransportReceiver>.Instance, 1024, false, progress);

		var first = await receiver.ReceiveAsync(2, CancellationToken.None);
		first.Count.ShouldBe(2);
		_ = await Should.ThrowAsync<KafkaException>(
			() => receiver.RejectAsync(first[0], "retry", requeue: true, CancellationToken.None));

		var next = await receiver.ReceiveAsync(10, CancellationToken.None);

		seeks.ShouldBe([10L], "the fetch of 12 must seek back to the owed offset 10");
		next.ShouldBeEmpty("12 is not handed out; it will be fetched again after the seek");
	}

	/// <summary>
	/// LIVENESS, through the receiver: when the seeks back succeed but the owed record is gone, it is released and
	/// the partition moves on.
	/// </summary>
	/// <remarks>
	/// The fake returns later records after every seek, as a compacted log does. RED against a receiver that never
	/// reports its successful seeks: the tracker can then never conclude anything and asks for the same seek forever.
	/// </remarks>
	[Fact]
	public async Task ReleaseACompactedOffset_FromTheReceiver_WhenItsSeeksSucceed()
	{
		var consumer = A.Fake<IConsumer<string, byte[]>>();
		var queue = new Queue<KafkaConsumeResult?>([Record(10), Record(11), Record(12), Record(13), Record(14)]);
		A.CallTo<KafkaConsumeResult?>(() => consumer.Consume(A<TimeSpan>._))
			.ReturnsLazily(() => queue.Count > 0 ? queue.Dequeue() : null);

		var progress = new KafkaPartitionProgress();
		progress.OnPartitionsAssigned([Partition0]);
		var receiver = new KafkaTransportReceiver(consumer, Topic, NullLogger<KafkaTransportReceiver>.Instance, 1024, false, progress);

		var first = await receiver.ReceiveAsync(2, CancellationToken.None);
		await receiver.RejectAsync(first[0], "retry", requeue: true, CancellationToken.None);

		var next = await receiver.ReceiveAsync(10, CancellationToken.None);

		var delivered = next.ShouldHaveSingleItem("with offset 10 released, the next surviving record is handed out");
		delivered.ProviderData[TransportOrderingMetadata.KafkaOffsetKey].ShouldBe(14L);
	}

	private static KafkaPartitionProgress Delivered(params long[] offsets)
	{
		// A fresh tracker is at generation 0, the generation the arms present when they settle and requeue.
		var progress = new KafkaPartitionProgress();
		foreach (var offset in offsets)
		{
			progress.BeginDelivery(Partition0, offset).Deliver.ShouldBeTrue();
		}

		return progress;
	}

	private static KafkaConsumeResult Record(long offset) =>
		new()
		{
			Topic = Topic,
			Partition = new Partition(0),
			Offset = new Offset(offset),
			Message = new Message<string, byte[]> { Key = $"m{offset}", Value = new byte[4] },
		};
}
