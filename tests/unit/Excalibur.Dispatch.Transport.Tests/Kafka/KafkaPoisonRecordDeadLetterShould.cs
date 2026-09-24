// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

#pragma warning disable CA2012 // FakeItEasy .Returns() stores ValueTask/Task

using Confluent.Kafka;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Kafka;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using System.Text;

using KafkaConsumeResult = global::Confluent.Kafka.ConsumeResult<string, byte[]>;

namespace Excalibur.Dispatch.Transport.Tests.Kafka;

/// <summary>
/// A record that can never become a message is written to the dead-letter queue before its offset is settled,
/// and a dead-letter write that fails leaves it owed rather than skipped.
/// </summary>
/// <remarks>
/// Such a record never reaches a handler, so the dead-letter decorator never sees it. Before this, it was settled
/// with only a log line, and the copy was lost. The arms drive the oversized-payload failure; a record that fails
/// conversion for any other reason goes through the same catch.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class KafkaPoisonRecordDeadLetterShould
{
	private const string Topic = "orders";
	private const int MaxPayloadBytes = 4;
	private static readonly TopicPartition Partition0 = new(Topic, new Partition(0));

	/// <summary>
	/// SAFETY. Through the receiver: exactly one dead-letter entry, and the position is still at the poison offset
	/// while it is being written.
	/// </summary>
	/// <remarks>RED against a receiver that settles before the dead-letter write, or never writes it.</remarks>
	[Fact]
	public async Task DeadLetterAPoisonRecordOnce_BeforeTheReceiverSettlesIt()
	{
		var consumer = A.Fake<IConsumer<string, byte[]>>();
		var queue = new Queue<KafkaConsumeResult?>([Record(10, poison: true), Record(11)]);
		A.CallTo<KafkaConsumeResult?>(() => consumer.Consume(A<TimeSpan>._))
			.ReturnsLazily(() => queue.Count > 0 ? queue.Dequeue() : null);

		var progress = new KafkaPartitionProgress();
		progress.OnPartitionsAssigned([Partition0]);
		var positionsSeenByDeadLetter = new List<long?>();
		var deadLetter = A.Fake<IDeadLetterQueueManager>();
		A.CallTo(() => deadLetter.MoveToDeadLetterAsync(A<TransportMessage>._, A<string>._, A<Exception?>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				positionsSeenByDeadLetter.Add(progress.TryGetPosition(Partition0, out var p) ? p : null);
				return Task.FromResult("dlq-1");
			});

		var receiver = new KafkaTransportReceiver(
			consumer, Topic, NullLogger<KafkaTransportReceiver>.Instance, MaxPayloadBytes, false, progress, Router(deadLetter));

		var received = await receiver.ReceiveAsync(10, CancellationToken.None);

		received.ShouldHaveSingleItem().ProviderData[TransportOrderingMetadata.KafkaOffsetKey].ShouldBe(11L);
		A.CallTo(() => deadLetter.MoveToDeadLetterAsync(
				A<TransportMessage>.That.Matches(m => m.Id == "orders:0:10" && m.Body.Length == 8),
				"PayloadTooLargeException", A<Exception?>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		positionsSeenByDeadLetter.ShouldHaveSingleItem().GetValueOrDefault(10).ShouldBeLessThanOrEqualTo(10,
			"the poison offset may be settled only after its dead-letter write returned");
		progress.TryGetPosition(Partition0, out var position).ShouldBeTrue();
		position.ShouldBe(11, "once dead-lettered, the poison offset is settled");
	}

	/// <summary>
	/// SAFETY. Through the receiver: a dead-letter write that fails for a reason a RETRY COULD FIX leaves the
	/// poison offset owed and seeks back to it, so it is redelivered and routed again instead of skipped.
	/// </summary>
	/// <remarks>
	/// Scoped to a transient failure. A permanent refusal must NOT keep the offset owed -- retrying a write that
	/// can never succeed stalls the partition forever -- which the tombstone arms below bind.
	/// RED against a receiver that settles the offset whether or not the write succeeded.
	/// </remarks>
	[Fact]
	public async Task KeepAPoisonRecordOwed_WhenATransientDeadLetterWriteFails()
	{
		var consumer = A.Fake<IConsumer<string, byte[]>>();
		var queue = new Queue<KafkaConsumeResult?>([Record(10, poison: true), Record(11)]);
		A.CallTo<KafkaConsumeResult?>(() => consumer.Consume(A<TimeSpan>._))
			.ReturnsLazily(() => queue.Count > 0 ? queue.Dequeue() : null);

		var progress = new KafkaPartitionProgress();
		progress.OnPartitionsAssigned([Partition0]);
		var deadLetter = A.Fake<IDeadLetterQueueManager>();
		A.CallTo(() => deadLetter.MoveToDeadLetterAsync(A<TransportMessage>._, A<string>._, A<Exception?>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("the dead-letter broker is briefly unreachable"));

		var receiver = new KafkaTransportReceiver(
			consumer, Topic, NullLogger<KafkaTransportReceiver>.Instance, MaxPayloadBytes, false, progress, Router(deadLetter));

		_ = await Should.ThrowAsync<InvalidOperationException>(() => receiver.ReceiveAsync(10, CancellationToken.None));

		if (progress.TryGetPosition(Partition0, out var position))
		{
			position.GetValueOrDefault().ShouldBeLessThanOrEqualTo(10, "an offset that was not dead-lettered must not be committed past");
		}

		A.CallTo(() => consumer.Seek(A<TopicPartitionOffset>.That.Matches(t => t.Offset.Value == 10)))
			.MustHaveHappened();
	}

	/// <summary>
	/// SAFETY and LIVENESS. Through the subscriber: a failed dead-letter write is retried on redelivery, nothing is
	/// committed past the poison offset until a write succeeds, and then exactly one entry exists.
	/// </summary>
	/// <remarks>RED against a subscriber that settles the offset regardless of the write, or never writes it.</remarks>
	[Fact]
	public async Task RetryAFailedDeadLetterWrite_BeforeTheSubscriberCommitsPastIt()
	{
		using var cts = new CancellationTokenSource();
		var log = new List<KafkaConsumeResult> { Record(10, poison: true), Record(11) };
		var cursor = 0;
		var commits = new List<long>();
		var consumer = A.Fake<IConsumer<string, byte[]>>();
		A.CallTo<KafkaConsumeResult?>(() => consumer.Consume(A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				if (cursor < log.Count)
				{
					return log[cursor++];
				}

				cts.Cancel();
				return null;
			});
		A.CallTo(() => consumer.Seek(A<TopicPartitionOffset>._))
			.Invokes((TopicPartitionOffset target) => cursor = log.FindIndex(r => r.Offset.Value >= target.Offset.Value));
		A.CallTo(() => consumer.Commit(A<IEnumerable<TopicPartitionOffset>>._))
			.Invokes((IEnumerable<TopicPartitionOffset> offsets) => commits.AddRange(offsets.Select(o => o.Offset.Value)));

		var commitsSeenByDeadLetter = new List<long>();
		var attempts = 0;
		var deadLetter = A.Fake<IDeadLetterQueueManager>();
		A.CallTo(() => deadLetter.MoveToDeadLetterAsync(A<TransportMessage>._, A<string>._, A<Exception?>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				commitsSeenByDeadLetter.AddRange(commits);
				return ++attempts == 1
					? Task.FromException<string>(new InvalidOperationException("dead-letter topic unavailable"))
					: Task.FromResult("dlq-1");
			});

		var progress = new KafkaPartitionProgress();
		progress.OnPartitionsAssigned([Partition0]);
		var subscriber = new KafkaTransportSubscriber(
			consumer, Topic, NullLogger<KafkaTransportSubscriber>.Instance, MaxPayloadBytes, false, progress, Router(deadLetter));

		await subscriber.SubscribeAsync((_, _) => Task.FromResult(MessageAction.Acknowledge), cts.Token);

		attempts.ShouldBe(2, "the failed write is retried when the record is redelivered");
		commitsSeenByDeadLetter.ShouldAllBe(c => c <= 10, "nothing may be committed past the poison offset before it is dead-lettered");
		commits.ShouldContain(12L, "once dead-lettered, the poison offset and the record after it are committed");
	}

	/// <summary>
	/// With no dead-letter queue configured the poison record is discarded and settled, so it does not pin the
	/// partition's position.
	/// </summary>
	/// <remarks>RED against a receiver that leaves a discarded poison offset owed.</remarks>
	[Fact]
	public async Task SettleADiscardedPoisonRecord_WhenNoDeadLetterQueueIsConfigured()
	{
		var consumer = A.Fake<IConsumer<string, byte[]>>();
		var queue = new Queue<KafkaConsumeResult?>([Record(10, poison: true), Record(11)]);
		A.CallTo<KafkaConsumeResult?>(() => consumer.Consume(A<TimeSpan>._))
			.ReturnsLazily(() => queue.Count > 0 ? queue.Dequeue() : null);

		var progress = new KafkaPartitionProgress();
		progress.OnPartitionsAssigned([Partition0]);
		var receiver = new KafkaTransportReceiver(
			consumer, Topic, NullLogger<KafkaTransportReceiver>.Instance, MaxPayloadBytes, false, progress);

		_ = await receiver.ReceiveAsync(10, CancellationToken.None);

		progress.TryGetPosition(Partition0, out var position).ShouldBeTrue();
		position.ShouldBe(11);
	}

	/// <summary>
	/// LIVENESS and SAFETY. A dead-letter write that can NEVER succeed does not stall the partition: a body-less
	/// tombstone naming the position is written instead, and only then is the offset settled.
	/// </summary>
	/// <remarks>
	/// The oversized case is reachable on the shipped defaults -- the receiver rejects above 4 MiB and the
	/// dead-letter producer's ceiling is 1 MiB -- so a record that qualifies as poison by size cannot be copied.
	/// RED against a router that rethrows a permanent refusal: the partition never advances again.
	/// </remarks>
	[Fact]
	public async Task TombstoneAPoisonRecord_WhenTheDeadLetterQueuePermanentlyRefusesItsBody()
	{
		var consumer = A.Fake<IConsumer<string, byte[]>>();
		var queue = new Queue<KafkaConsumeResult?>([Record(10, poison: true), Record(11)]);
		A.CallTo<KafkaConsumeResult?>(() => consumer.Consume(A<TimeSpan>._))
			.ReturnsLazily(() => queue.Count > 0 ? queue.Dequeue() : null);

		var written = new List<TransportMessage>();
		var deadLetter = A.Fake<IDeadLetterQueueManager>();
		A.CallTo(() => deadLetter.MoveToDeadLetterAsync(A<TransportMessage>._, A<string>._, A<Exception?>._, A<CancellationToken>._))
			.ReturnsLazily((TransportMessage m, string _, Exception? _, CancellationToken _) =>
			{
				written.Add(m);
				return m.Body.IsEmpty
					? Task.FromResult("dlq-tombstone")
					: Task.FromException<string>(PermanentRefusal());
			});

		var progress = new KafkaPartitionProgress();
		progress.OnPartitionsAssigned([Partition0]);
		var receiver = new KafkaTransportReceiver(
			consumer, Topic, NullLogger<KafkaTransportReceiver>.Instance, MaxPayloadBytes, false, progress, Router(deadLetter));

		var received = await receiver.ReceiveAsync(10, CancellationToken.None);

		received.ShouldHaveSingleItem().ProviderData[TransportOrderingMetadata.KafkaOffsetKey].ShouldBe(11L);
		written.Count.ShouldBe(2, "the body is attempted first, then the tombstone");
		written[1].Body.IsEmpty.ShouldBeTrue("the body is what the dead-letter queue refused");
		written[1].Properties["dlq_body_dropped"].ShouldBe(bool.TrueString);
		written[1].Properties["dlq_body_bytes"].ShouldBe("8");
		progress.TryGetPosition(Partition0, out var position).ShouldBeTrue();
		position.ShouldBe(11, "a record that can never be copied must not pin the partition forever");
	}

	/// <summary>
	/// LIVENESS. When even the tombstone is refused the record is discarded and the partition still advances,
	/// because refusing to move would lose the record AND stop the partition.
	/// </summary>
	/// <remarks>RED against a router that rethrows when the tombstone write fails too.</remarks>
	[Fact]
	public async Task DiscardAPoisonRecord_WhenEvenItsTombstoneIsRefused()
	{
		var consumer = A.Fake<IConsumer<string, byte[]>>();
		var queue = new Queue<KafkaConsumeResult?>([Record(10, poison: true), Record(11)]);
		A.CallTo<KafkaConsumeResult?>(() => consumer.Consume(A<TimeSpan>._))
			.ReturnsLazily(() => queue.Count > 0 ? queue.Dequeue() : null);

		var deadLetter = A.Fake<IDeadLetterQueueManager>();
		A.CallTo(() => deadLetter.MoveToDeadLetterAsync(A<TransportMessage>._, A<string>._, A<Exception?>._, A<CancellationToken>._))
			.ReturnsLazily(() => Task.FromException<string>(PermanentRefusal()));

		var progress = new KafkaPartitionProgress();
		progress.OnPartitionsAssigned([Partition0]);
		var receiver = new KafkaTransportReceiver(
			consumer, Topic, NullLogger<KafkaTransportReceiver>.Instance, MaxPayloadBytes, false, progress, Router(deadLetter));

		_ = await receiver.ReceiveAsync(10, CancellationToken.None);

		progress.TryGetPosition(Partition0, out var position).ShouldBeTrue();
		position.ShouldBe(11);
	}

	/// <summary>
	/// LIVENESS. Through the subscriber: a permanently refused poison record is settled rather than sought back
	/// forever, so the committed position moves past it.
	/// </summary>
	/// <remarks>RED against a subscriber that seeks back on a refusal no retry can fix.</remarks>
	[Fact]
	public async Task CommitPastAPoisonRecord_WhenTheSubscribersDeadLetterQueuePermanentlyRefusesIt()
	{
		using var cts = new CancellationTokenSource();
		var log = new List<KafkaConsumeResult> { Record(10, poison: true), Record(11) };
		var cursor = 0;
		var commits = new List<long>();
		var consumer = A.Fake<IConsumer<string, byte[]>>();
		A.CallTo<KafkaConsumeResult?>(() => consumer.Consume(A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				if (cursor < log.Count)
				{
					return log[cursor++];
				}

				cts.Cancel();
				return null;
			});
		A.CallTo(() => consumer.Seek(A<TopicPartitionOffset>._))
			.Invokes((TopicPartitionOffset target) => cursor = log.FindIndex(r => r.Offset.Value >= target.Offset.Value));
		A.CallTo(() => consumer.Commit(A<IEnumerable<TopicPartitionOffset>>._))
			.Invokes((IEnumerable<TopicPartitionOffset> offsets) => commits.AddRange(offsets.Select(o => o.Offset.Value)));

		var deadLetter = A.Fake<IDeadLetterQueueManager>();
		A.CallTo(() => deadLetter.MoveToDeadLetterAsync(A<TransportMessage>._, A<string>._, A<Exception?>._, A<CancellationToken>._))
			.ReturnsLazily((TransportMessage m, string _, Exception? _, CancellationToken _) => m.Body.IsEmpty
				? Task.FromResult("dlq-tombstone")
				: Task.FromException<string>(PermanentRefusal()));

		var progress = new KafkaPartitionProgress();
		progress.OnPartitionsAssigned([Partition0]);
		var subscriber = new KafkaTransportSubscriber(
			consumer, Topic, NullLogger<KafkaTransportSubscriber>.Instance, MaxPayloadBytes, false, progress, Router(deadLetter));

		await subscriber.SubscribeAsync((_, _) => Task.FromResult(MessageAction.Acknowledge), cts.Token);

		commits.ShouldContain(12L, "the refused record is settled, so the position moves past it and the record after it");
	}

	/// <summary>
	/// THE PREMISE THE TOMBSTONE RESTS ON, against the REAL producer rather than a fake: a body larger than the
	/// dead-letter producer's configured ceiling is refused locally, with the permanent error code the router
	/// keys on, and no broker is contacted.
	/// </summary>
	/// <remarks>
	/// Every other arm here injects a fake dead-letter queue, so none of them can observe the real ceiling. The
	/// receiver rejects a record above 4 MiB while the shipped producer ceiling is 1 MiB, so the oversized record
	/// is by construction too big to copy. librdkafka validates the size before it sends, which is what makes the
	/// refusal permanent rather than a broker-side transient -- and is why this needs no running broker.
	/// RED if that refusal were ever transient or carried a different code: the router would then retry it forever.
	/// </remarks>
	[Fact]
	public async Task RefuseABodyAboveTheProducersCeiling_LocallyAndPermanently_WithNoBroker()
	{
		var config = new ProducerConfig
		{
			// Never resolved: the size check happens before any connection is attempted.
			BootstrapServers = "127.0.0.1:1",
			MessageMaxBytes = 1024 * 1024,
			MessageTimeoutMs = 2000,
			SocketTimeoutMs = 1000,
		};

		using var producer = new ProducerBuilder<string, byte[]>(config).Build();

		var oversized = new Message<string, byte[]> { Key = "m10", Value = new byte[2 * 1024 * 1024] };

		var thrown = await Should.ThrowAsync<ProduceException<string, byte[]>>(
			() => producer.ProduceAsync("orders.dead-letter", oversized, CancellationToken.None));

		thrown.Error.Code.ShouldBe(ErrorCode.MsgSizeTooLarge,
			"the router treats exactly this code as permanent, so the tombstone path depends on it");
		KafkaPoisonRecordRouter.IsPermanent(thrown).ShouldBeTrue(
			"the router must classify the real producer's refusal as permanent, not transient");
	}

	/// <summary>
	/// SAFETY. A record that is oversized BECAUSE OF ITS HEADERS still yields a written tombstone: the tombstone
	/// is built from a bounded field set rather than copied from the record, so it does not inherit the headers
	/// that made the body unwritable.
	/// </summary>
	/// <remarks>
	/// This is the case the tombstone exists for, and the one a copied tombstone fails: deriving it from the
	/// record carries every Kafka header onto it, so the second write is refused for the same reason as the
	/// first and the position is discarded instead of recorded. RED against a tombstone built by copy-then-empty
	/// -- the oversized header reappears in its properties and the assertion on the bounded key fails with it.
	/// </remarks>
	[Fact]
	public async Task BuildABoundedTombstone_WhenTheHeadersAreWhatMadeTheRecordTooLarge()
	{
		var consumer = A.Fake<IConsumer<string, byte[]>>();
		var bloatedHeaderValue = new string('h', 64 * 1024);
		var longKey = new string('k', 4096);
		var poisonWithFatHeaders = Record(10, poison: true);
		poisonWithFatHeaders.Message.Key = longKey;
		poisonWithFatHeaders.Message.Headers =
		[
			new Header("x-bloat", Encoding.UTF8.GetBytes(bloatedHeaderValue)),
		];

		var queue = new Queue<KafkaConsumeResult?>([poisonWithFatHeaders, Record(11)]);
		A.CallTo<KafkaConsumeResult?>(() => consumer.Consume(A<TimeSpan>._))
			.ReturnsLazily(() => queue.Count > 0 ? queue.Dequeue() : null);

		var written = new List<TransportMessage>();
		var deadLetter = A.Fake<IDeadLetterQueueManager>();
		A.CallTo(() => deadLetter.MoveToDeadLetterAsync(A<TransportMessage>._, A<string>._, A<Exception?>._, A<CancellationToken>._))
			.ReturnsLazily((TransportMessage m, string _, Exception? _, CancellationToken _) =>
			{
				written.Add(m);
				return m.Body.IsEmpty
					? Task.FromResult("dlq-tombstone")
					: Task.FromException<string>(PermanentRefusal());
			});

		var progress = new KafkaPartitionProgress();
		progress.OnPartitionsAssigned([Partition0]);
		var receiver = new KafkaTransportReceiver(
			consumer, Topic, NullLogger<KafkaTransportReceiver>.Instance, MaxPayloadBytes, false, progress, Router(deadLetter));

		_ = await receiver.ReceiveAsync(10, CancellationToken.None);

		written.Count.ShouldBe(2, "the body is attempted first, then the tombstone");
		var tombstone = written[1];
		tombstone.Properties.ShouldNotContainKey("x-bloat",
			"the tombstone is built, not copied -- inheriting the record's headers is what makes it unwritable too");
		tombstone.Properties.Values.Select(v => v is null ? 0 : v.ToString()!.Length).ShouldAllBe(length => length <= 256,
			"every value carried onto a tombstone is capped, so its size is bounded by construction");
		tombstone.Subject!.Length.ShouldBe(256, "the record key is capped rather than carried whole");
		tombstone.Properties["dlq_partition"].ShouldBe("0");
		tombstone.Properties["dlq_offset"].ShouldBe("10", "the position is the pointer back to the retained record");
		progress.TryGetPosition(Partition0, out var position).ShouldBeTrue();
		position.ShouldBe(11, "the tombstone was written, so the partition advances");
	}

	/// <summary>
	/// SAFETY. A dead-letter topic that does not exist yet leaves the record OWED: the write is retried rather
	/// than tombstoned, because an operator can fix it and several such failures are transient.
	/// </summary>
	/// <remarks>
	/// Classified permanent, an unprovisioned dead-letter topic or an ACL mid-reload would discard every poison
	/// record at poll speed -- the framework dropping a consumer's data over a configuration fault that is about
	/// to be corrected. RED against a router that treats <see cref="ErrorCode.UnknownTopicOrPart"/> as permanent:
	/// it tombstones and returns, and the record is settled instead of redelivered.
	/// </remarks>
	[Fact]
	public async Task LeaveThePoisonRecordOwed_WhenTheDeadLetterTopicDoesNotExistYet()
	{
		var written = new List<TransportMessage>();
		var deadLetter = A.Fake<IDeadLetterQueueManager>();
		A.CallTo(() => deadLetter.MoveToDeadLetterAsync(A<TransportMessage>._, A<string>._, A<Exception?>._, A<CancellationToken>._))
			.ReturnsLazily((TransportMessage m, string _, Exception? _, CancellationToken _) =>
			{
				written.Add(m);
				return Task.FromException<string>(new ProduceException<string, byte[]>(
					new Error(ErrorCode.UnknownTopicOrPart, "Broker: Unknown topic or partition"),
					new DeliveryResult<string, byte[]>()));
			});

		var thrown = await Should.ThrowAsync<ProduceException<string, byte[]>>(
			() => Router(deadLetter).RouteAsync(Record(10, poison: true), new InvalidOperationException("poison"), CancellationToken.None));

		thrown.Error.Code.ShouldBe(ErrorCode.UnknownTopicOrPart);
		written.ShouldHaveSingleItem();
		written[0].Body.IsEmpty.ShouldBeFalse(
			"no tombstone is written: the body is still writable once the topic exists, so nothing is dropped");
		KafkaPoisonRecordRouter.IsPermanent(thrown).ShouldBeFalse(
			"an operator can create the topic, so this must not be classified as impossible");
	}

	/// <summary>
	/// LIVENESS. A retryable dead-letter failure is PACED before the record is handed back, so a fault the
	/// caller re-polls into is not retried at poll speed.
	/// </summary>
	/// <remarks>
	/// Deterministic: the wait is driven by a fake clock, so the arm asserts that a wait was taken rather
	/// than measuring elapsed time. RED against a router that rethrows immediately — the task completes
	/// with no time advanced.
	/// </remarks>
	[Fact]
	public async Task PaceARetryableDeadLetterFailure_BeforeHandingTheRecordBack()
	{
		var consumer = A.Fake<IConsumer<string, byte[]>>();
		var queue = new Queue<KafkaConsumeResult?>([Record(10, poison: true)]);
		A.CallTo<KafkaConsumeResult?>(() => consumer.Consume(A<TimeSpan>._))
			.ReturnsLazily(() => queue.Count > 0 ? queue.Dequeue() : null);

		var deadLetter = A.Fake<IDeadLetterQueueManager>();
		A.CallTo(() => deadLetter.MoveToDeadLetterAsync(A<TransportMessage>._, A<string>._, A<Exception?>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("the dead-letter broker is briefly unreachable"));

		var progress = new KafkaPartitionProgress();
		progress.OnPartitionsAssigned([Partition0]);
		var clock = new FakeTimeProvider();
		var receiver = new KafkaTransportReceiver(
			consumer, Topic, NullLogger<KafkaTransportReceiver>.Instance, MaxPayloadBytes, false, progress, Router(deadLetter, clock));

		var receive = receiver.ReceiveAsync(10, CancellationToken.None);

		// Nothing is waiting on the broker here: the only thing left is the router's own pacing, and with a
		// fake clock it cannot elapse by itself.
		await Task.Delay(TimeSpan.FromMilliseconds(50), TimeProvider.System, CancellationToken.None); // delay-ok: proves the router PACES -- the pacing runs on a fake clock that cannot elapse, so real time is the only way to observe it has not completed
		receive.IsCompleted.ShouldBeFalse("the retryable failure must be paced, not handed back at poll speed");

		clock.Advance(TimeSpan.FromSeconds(10));
		_ = await Should.ThrowAsync<InvalidOperationException>(() => receive);
	}

	private static ProduceException<string, byte[]> PermanentRefusal() =>
		new(new Error(ErrorCode.MsgSizeTooLarge, "Broker: Message size too large"), new DeliveryResult<string, byte[]>());

	private static KafkaPoisonRecordRouter Router(IDeadLetterQueueManager deadLetter, TimeProvider? clock = null) =>
		new(deadLetter, meter: null, Topic, NullLogger.Instance, clock);


	private static KafkaConsumeResult Record(long offset, bool poison = false) =>
		new()
		{
			Topic = Topic,
			Partition = new Partition(0),
			Offset = new Offset(offset),
			Message = new Message<string, byte[]> { Key = $"m{offset}", Value = new byte[poison ? 8 : 1] },
		};
}
