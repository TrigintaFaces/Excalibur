// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

#pragma warning disable CA2012 // FakeItEasy .Returns() stores ValueTask/Task

using Confluent.Kafka;

using Excalibur.Dispatch.Transport.Kafka;

using Microsoft.Extensions.Logging.Abstractions;

using KafkaConsumeResult = global::Confluent.Kafka.ConsumeResult<string, byte[]>;

namespace Excalibur.Dispatch.Transport.Tests.Kafka;

/// <summary>
/// Binds the receiver's settlement path to the contiguous-prefix rule, at the seam where the offsets
/// actually reach the Kafka client.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="KafkaPartitionProgressShould"/> locks the arithmetic in isolation; these arms lock that the
/// receiver routes every settlement through it, because a correct tracker the receiver bypasses protects
/// nothing. They assert on the offsets handed to <c>Commit</c> and <c>Seek</c>, which is the observable
/// effect on the broker.
/// </para>
/// <para>
/// A faked client is the right instrument only for the arithmetic and the wiring — it returns whatever it
/// is told and cannot exhibit a real rebalance or the broker's own commit handling. The arms that prove
/// the guarantee survives a crash, a real revocation and a real reassignment run against a broker in
/// <c>KafkaOffsetProgressIntegrationShould</c>.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class KafkaReceiverContiguousProgressShould
{
	private const string Topic = "orders";
	private const int MaxPayloadBytes = 8;

	private static KafkaConsumeResult Record(string key, long offset, int payloadBytes = 4) =>
		new()
		{
			Topic = Topic,
			Partition = new Partition(0),
			Offset = new Offset(offset),
			Message = new Message<string, byte[]> { Key = key, Value = new byte[payloadBytes] },
		};

	private static KafkaTransportReceiver CreateReceiver(
		IConsumer<string, byte[]> consumer,
		KafkaPartitionProgress? progress = null) =>
		new(consumer, Topic, NullLogger<KafkaTransportReceiver>.Instance, MaxPayloadBytes, false, progress);

	/// <summary>Queues poll results and records every offset the receiver commits or seeks to.</summary>
	private static IConsumer<string, byte[]> FakeConsumer(
		IEnumerable<KafkaConsumeResult?> results,
		List<long> commits,
		List<long>? seeks = null)
	{
		var consumer = A.Fake<IConsumer<string, byte[]>>();
		var queue = new Queue<KafkaConsumeResult?>(results);

		// Explicit nullable generic: Consume's declared return is ConsumeResult<..>?, but generic inference
		// over the expression drops the annotation, so the non-nullable overload binds.
		A.CallTo<KafkaConsumeResult?>(() => consumer.Consume(A<TimeSpan>._))
			.ReturnsLazily(() => queue.Count > 0 ? queue.Dequeue() : null);

		A.CallTo(() => consumer.Commit(A<IEnumerable<TopicPartitionOffset>>._))
			.Invokes((IEnumerable<TopicPartitionOffset> offsets) => commits.AddRange(offsets.Select(o => o.Offset.Value)));

		A.CallTo(() => consumer.Seek(A<TopicPartitionOffset>._))
			.Invokes((TopicPartitionOffset tpo) => seeks?.Add(tpo.Offset.Value));

		return consumer;
	}

	// ---- The reproduction: a valid offset followed by an oversized one ----

	[Fact]
	public async Task NotCommitPastAMessageStillInAHandler_WhenTheNextOneIsOversized()
	{
		// Offset 0 is valid and handed to the caller. Offset 1 is oversized and can never be processed, so
		// it settles during the poll. Committing 2 at that moment would declare offset 0 handled, and a
		// restart would resume at 2 — losing a message no handler ever saw.
		var commits = new List<long>();
		var consumer = FakeConsumer([Record("m0", 0), Record("m1", 1, payloadBytes: 64), null], commits);

		var messages = await CreateReceiver(consumer).ReceiveAsync(10, CancellationToken.None);

		messages.Count.ShouldBe(1);
		messages[0].Id.ShouldBe("m0");
		commits.ShouldBeEmpty();
	}

	[Fact]
	public async Task CommitPastBothOfThem_OnceTheValidMessageIsAcknowledged()
	{
		// Liveness partner: a receiver that never commits also satisfies the arm above. The oversized
		// message is genuinely terminal, so acknowledging offset 0 must carry the position past both.
		var commits = new List<long>();
		var consumer = FakeConsumer([Record("m0", 0), Record("m1", 1, payloadBytes: 64), null], commits);
		var receiver = CreateReceiver(consumer);

		var messages = await receiver.ReceiveAsync(10, CancellationToken.None);
		await receiver.AcknowledgeAsync(messages[0], CancellationToken.None);

		commits.ShouldBe([2]);
	}

	// ---- Out-of-order settlement ----

	[Fact]
	public async Task NotCommit_WhenALaterMessageIsAcknowledgedFirst()
	{
		var commits = new List<long>();
		var consumer = FakeConsumer([Record("m0", 0), Record("m1", 1), Record("m2", 2), null], commits);
		var receiver = CreateReceiver(consumer);

		var messages = await receiver.ReceiveAsync(10, CancellationToken.None);
		await receiver.AcknowledgeAsync(messages[2], CancellationToken.None);

		commits.ShouldBeEmpty();
	}

	[Fact]
	public async Task CommitTheWholePrefixAtOnce_WhenTheGapCloses()
	{
		var commits = new List<long>();
		var consumer = FakeConsumer([Record("m0", 0), Record("m1", 1), Record("m2", 2), null], commits);
		var receiver = CreateReceiver(consumer);

		var messages = await receiver.ReceiveAsync(10, CancellationToken.None);
		await receiver.AcknowledgeAsync(messages[2], CancellationToken.None);
		await receiver.AcknowledgeAsync(messages[1], CancellationToken.None);
		await receiver.AcknowledgeAsync(messages[0], CancellationToken.None);

		// One commit, at the far end, and never an intermediate one that crossed unfinished work.
		commits.ShouldBe([3]);
	}

	[Fact]
	public async Task NeverEmitACommitLowerThanOneAlreadyEmitted()
	{
		var commits = new List<long>();
		var consumer = FakeConsumer([Record("m0", 0), Record("m1", 1), Record("m2", 2), null], commits);
		var receiver = CreateReceiver(consumer);

		var messages = await receiver.ReceiveAsync(10, CancellationToken.None);
		await receiver.AcknowledgeAsync(messages[0], CancellationToken.None);
		await receiver.AcknowledgeAsync(messages[2], CancellationToken.None);
		await receiver.AcknowledgeAsync(messages[1], CancellationToken.None);

		commits.ShouldBe([1, 3]);
		commits.ShouldBe(commits.Order().ToList());
	}

	// ---- Rejection without requeue is terminal, and still bound by the prefix ----

	[Fact]
	public async Task NotCommitPastAnOutstandingMessage_WhenALaterOneIsRejected()
	{
		var commits = new List<long>();
		var consumer = FakeConsumer([Record("m0", 0), Record("m1", 1), null], commits);
		var receiver = CreateReceiver(consumer);

		var messages = await receiver.ReceiveAsync(10, CancellationToken.None);
		await receiver.RejectAsync(messages[1], "poison", requeue: false, CancellationToken.None);

		commits.ShouldBeEmpty();
	}

	// ---- Requeue makes progress inside the same healthy consumer ----

	[Fact]
	public async Task SeekBackToARequeuedMessage_RatherThanWaitForASessionTimeout()
	{
		// A healthy consumer that keeps polling is never redelivered anything by a session timeout, so a
		// requeue that only drops its bookkeeping retries nothing at all.
		var commits = new List<long>();
		var seeks = new List<long>();
		var consumer = FakeConsumer([Record("m0", 0), null], commits, seeks);
		var receiver = CreateReceiver(consumer);

		var messages = await receiver.ReceiveAsync(10, CancellationToken.None);
		await receiver.RejectAsync(messages[0], "transient", requeue: true, CancellationToken.None);

		seeks.ShouldBe([0]);
		commits.ShouldBeEmpty();
	}

	[Fact]
	public async Task RedeliverARequeuedMessage_AndCommitOnceTheRetrySucceeds()
	{
		var commits = new List<long>();
		var seeks = new List<long>();
		var consumer = FakeConsumer([Record("m0", 0), null, Record("m0", 0), null], commits, seeks);
		var receiver = CreateReceiver(consumer);

		var first = await receiver.ReceiveAsync(10, CancellationToken.None);
		await receiver.RejectAsync(first[0], "transient", requeue: true, CancellationToken.None);

		var retry = await receiver.ReceiveAsync(10, CancellationToken.None);
		retry.Count.ShouldBe(1);
		await receiver.AcknowledgeAsync(retry[0], CancellationToken.None);

		seeks.ShouldBe([0]);
		commits.ShouldBe([1]);
	}

	[Fact]
	public async Task NotHandOutAnAlreadySettledMessage_WhenASeekReplaysIt()
	{
		// The seek rewinds the whole partition, so the poll after a requeue replays offsets that already
		// settled. Handing those to the caller again would duplicate finished work.
		var commits = new List<long>();
		var seeks = new List<long>();
		var consumer = FakeConsumer(
			[Record("m0", 0), Record("m1", 1), null, Record("m0", 0), Record("m1", 1), null],
			commits,
			seeks);
		var receiver = CreateReceiver(consumer);

		var first = await receiver.ReceiveAsync(10, CancellationToken.None);
		await receiver.AcknowledgeAsync(first[1], CancellationToken.None); // offset 1 terminal, 0 still owed
		await receiver.RejectAsync(first[0], "transient", requeue: true, CancellationToken.None);

		var replay = await receiver.ReceiveAsync(10, CancellationToken.None);

		replay.Count.ShouldBe(1);
		replay[0].Id.ShouldBe("m0");
	}

	// ---- Ownership ----

	[Fact]
	public async Task RefuseToSettleAReceiptIssuedBeforeARebalance()
	{
		// The straggler must be able to do damage if it is honoured, or the arm proves nothing: the new
		// tenure has finished offset 0 and is holding at 1, so accepting an old-tenure settlement for
		// offset 1 would carry the position to 2 over work this owner never did.
		var commits = new List<long>();
		var progress = new KafkaPartitionProgress();
		var partition = new TopicPartition(Topic, new Partition(0));
		var consumer = FakeConsumer(
			[Record("m0", 0), Record("m1", 1), null, Record("m0", 0), null],
			commits);
		var receiver = CreateReceiver(consumer, progress);

		var beforeRebalance = await receiver.ReceiveAsync(10, CancellationToken.None);

		// The partition moves to another member and comes back: a new tenure, a new generation. The
		// broker rewinds it to the last committed position, so offset 0 is delivered again.
		progress.OnPartitionsRevoked([partition]);
		progress.OnPartitionsAssigned([partition]);

		var afterRebalance = await receiver.ReceiveAsync(10, CancellationToken.None);
		await receiver.AcknowledgeAsync(afterRebalance[0], CancellationToken.None);
		commits.ShouldBe([1L]);

		// The straggler: offset 1, acknowledged under the tenure that ended.
		await receiver.AcknowledgeAsync(beforeRebalance[1], CancellationToken.None);

		commits.ShouldBe([1L]); // unchanged — the new owner still owes offset 1
	}

	[Fact]
	public async Task SettleNormallyUnderTheNewGeneration()
	{
		// Liveness partner: refusing every settlement after a rebalance would also satisfy the arm above.
		var commits = new List<long>();
		var progress = new KafkaPartitionProgress();
		var consumer = FakeConsumer([Record("m0", 0), null, Record("m0", 0), null], commits);
		var receiver = CreateReceiver(consumer, progress);

		_ = await receiver.ReceiveAsync(10, CancellationToken.None);
		progress.OnPartitionsRevoked([new TopicPartition(Topic, new Partition(0))]);
		progress.OnPartitionsAssigned([new TopicPartition(Topic, new Partition(0))]);

		var afterRebalance = await receiver.ReceiveAsync(10, CancellationToken.None);
		afterRebalance.Count.ShouldBe(1);
		await receiver.AcknowledgeAsync(afterRebalance[0], CancellationToken.None);

		commits.ShouldBe([1]);
	}

	// ---- Commit failure ----

	[Fact]
	public async Task RetryTheCommitOnTheNextSettle_WhenTheBrokerRejectsIt()
	{
		// A Kafka commit names an absolute position, so a later commit subsumes a failed earlier one. What
		// must not happen is the failure silently retiring the prefix it did not manage to record.
		var commits = new List<long>();
		var consumer = A.Fake<IConsumer<string, byte[]>>();
		var queue = new Queue<KafkaConsumeResult?>([Record("m0", 0), Record("m1", 1), null]);
		A.CallTo<KafkaConsumeResult?>(() => consumer.Consume(A<TimeSpan>._))
			.ReturnsLazily(() => queue.Count > 0 ? queue.Dequeue() : null);

		var failFirst = true;
		A.CallTo(() => consumer.Commit(A<IEnumerable<TopicPartitionOffset>>._))
			.Invokes((IEnumerable<TopicPartitionOffset> offsets) =>
			{
				if (failFirst)
				{
					failFirst = false;
					throw new KafkaException(ErrorCode.Local_Transport);
				}

				commits.AddRange(offsets.Select(o => o.Offset.Value));
			});

		var receiver = CreateReceiver(consumer);
		var messages = await receiver.ReceiveAsync(10, CancellationToken.None);

		_ = await Should.ThrowAsync<KafkaException>(
			() => receiver.AcknowledgeAsync(messages[0], CancellationToken.None));

		await receiver.AcknowledgeAsync(messages[1], CancellationToken.None);

		// The surviving commit covers both offsets: the position the failed attempt would have recorded is
		// subsumed, not lost.
		commits.ShouldBe([2]);
	}

	[Fact]
	public async Task ThrowWhenAcknowledgingAnUnknownReceipt()
	{
		var commits = new List<long>();
		var consumer = FakeConsumer([Record("m0", 0), null], commits);
		var receiver = CreateReceiver(consumer);

		var messages = await receiver.ReceiveAsync(10, CancellationToken.None);
		await receiver.AcknowledgeAsync(messages[0], CancellationToken.None);

		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => receiver.AcknowledgeAsync(messages[0], CancellationToken.None));
	}

	[Fact]
	public async Task RedeliverAMessageTheFailedBatchNeverReturned()
	{
		// A poll that throws after some messages were built discards them: the caller never receives that
		// batch. They are already recorded as dispatched, so without handing them back the partition would
		// stall on work nobody holds — the commit position could never move again.
		var commits = new List<long>();
		var seeks = new List<long>();
		var consumer = A.Fake<IConsumer<string, byte[]>>();
		var polls = 0;
		A.CallTo<KafkaConsumeResult?>(() => consumer.Consume(A<TimeSpan>._)).ReturnsLazily(() =>
		{
			polls++;
			return polls switch
			{
				1 => Record("m0", 0),
				2 => throw new KafkaException(ErrorCode.Local_Transport),
				3 => Record("m0", 0),
				_ => null,
			};
		});
		A.CallTo(() => consumer.Commit(A<IEnumerable<TopicPartitionOffset>>._))
			.Invokes((IEnumerable<TopicPartitionOffset> offsets) => commits.AddRange(offsets.Select(o => o.Offset.Value)));
		A.CallTo(() => consumer.Seek(A<TopicPartitionOffset>._))
			.Invokes((TopicPartitionOffset tpo) => seeks.Add(tpo.Offset.Value));

		var receiver = CreateReceiver(consumer);
		_ = await Should.ThrowAsync<KafkaException>(() => receiver.ReceiveAsync(10, CancellationToken.None));

		seeks.ShouldBe([0]);

		var retry = await receiver.ReceiveAsync(10, CancellationToken.None);
		retry.Select(m => m.Id).ShouldBe(["m0"]);
		await receiver.AcknowledgeAsync(retry[0], CancellationToken.None);
		commits.ShouldBe([1]);
	}

	[Fact]
	public async Task ExposeItsProgressTracker_SoTheRebalanceHandlersShareIt()
	{
		// The rebalance handlers are the only place that observes a revoke and the assignment after it, so
		// generation fencing only works if they and the receiver hold the same tracker.
		var progress = new KafkaPartitionProgress();
		var receiver = CreateReceiver(FakeConsumer([null], []), progress);

		receiver.GetService(typeof(KafkaPartitionProgress)).ShouldBeSameAs(progress);
	}
}
