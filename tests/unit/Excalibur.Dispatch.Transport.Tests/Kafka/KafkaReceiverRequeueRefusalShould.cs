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
/// A requeue the receiver cannot arrange is reported to the caller, and the receipt survives until the
/// decision has actually been made.
/// </summary>
/// <remarks>
/// <para>
/// <c>ITransportReceiver.RejectAsync</c> treats <c>requeue</c> as the outcome the caller requires and forbids
/// reporting success for an outcome that was not delivered. The receiver used to drop the receipt before
/// deciding whether it could seek, so a requeue for a message whose partition had since been reassigned
/// arranged nothing and returned normally.
/// </para>
/// <para>
/// The stale generation is produced with the real <see cref="KafkaPartitionProgress"/>, not a fake of it:
/// the arm binds the receiver's use of the tracker's own decision. Only the Kafka client is faked, and the
/// assertions are on what reaches it.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class KafkaReceiverRequeueRefusalShould
{
	private const string Topic = "orders";

	private static readonly TopicPartition Partition0 = new(Topic, new Partition(0));

	/// <summary>
	/// SAFETY. A requeue that arrives after its partition was reassigned is refused out loud.
	/// </summary>
	/// <remarks>
	/// RED against dropping the receipt first: that order returned normally, having sought nothing.
	/// </remarks>
	[Fact]
	public async Task ReportTheRefusal_WhenTheRequeueArrivesAfterItsPartitionWasReassigned()
	{
		var seeks = new List<long>();
		var progress = new KafkaPartitionProgress();
		var receiver = CreateReceiver(FakeConsumer([Record(0), null], seeks), progress);

		var received = await receiver.ReceiveAsync(10, CancellationToken.None);

		progress.OnPartitionsRevoked([Partition0]);
		progress.OnPartitionsAssigned([Partition0]);

		var refusal = await Should.ThrowAsync<TransportSettlementException>(
			() => receiver.RejectAsync(received[0], "retry", requeue: true, CancellationToken.None));

		refusal.Retryability.ShouldBe(
			SettlementRetryability.Permanent,
			"the receipt belongs to a tenure that has ended, so repeating the call can only fail the same way");
		refusal.RedeliveryExpectation.ShouldBe(
			TransportRedeliveryExpectation.Unspecified,
			"the ended tenure cannot establish the broker outcome for the new owner");
		seeks.ShouldBeEmpty("a consumer that no longer owns the partition must not seek it");
	}

	/// <summary>
	/// LIVENESS. A requeue inside the current tenure still seeks back and returns normally.
	/// </summary>
	/// <remarks>
	/// Without this arm, a receiver that refused every requeue would satisfy the safety arm above.
	/// </remarks>
	[Fact]
	public async Task SeekBackAndReturnNormally_WhenTheRequeueIsInTheCurrentTenure()
	{
		var seeks = new List<long>();
		var receiver = CreateReceiver(FakeConsumer([Record(0), null], seeks), new KafkaPartitionProgress());

		var received = await receiver.ReceiveAsync(10, CancellationToken.None);

		await receiver.RejectAsync(received[0], "retry", requeue: true, CancellationToken.None);

		seeks.ShouldBe([0L]);
	}

	/// <summary>
	/// SAFETY. A seek that fails leaves the receipt in place, so the caller's retry can still act on it.
	/// </summary>
	/// <remarks>
	/// RED against dropping the receipt first: the retry then fails with "not in the receipt cache" for a
	/// message that was never settled.
	/// </remarks>
	[Fact]
	public async Task KeepTheReceipt_WhenTheSeekFails_SoTheRetryCanSucceed()
	{
		var seeks = new List<long>();
		var consumer = FakeConsumer([Record(0), null], seeks);
		var failNextSeek = true;
		var cause = new KafkaException(ErrorCode.Local_State);
		A.CallTo(() => consumer.Seek(A<TopicPartitionOffset>._))
			.Invokes((TopicPartitionOffset tpo) =>
			{
				if (failNextSeek)
				{
					failNextSeek = false;
					throw cause;
				}

				seeks.Add(tpo.Offset.Value);
			});

		var receiver = CreateReceiver(consumer, new KafkaPartitionProgress());
		var received = await receiver.ReceiveAsync(10, CancellationToken.None);

		var failure = await Should.ThrowAsync<TransportSettlementException>(
			() => receiver.RejectAsync(received[0], "retry", requeue: true, CancellationToken.None));
		failure.TransportName.ShouldBe("Kafka");
		failure.Retryability.ShouldBe(SettlementRetryability.Unspecified);
		failure.RedeliveryExpectation.ShouldBe(TransportRedeliveryExpectation.Unspecified);
		failure.InnerException.ShouldBeSameAs(cause);

		await receiver.RejectAsync(received[0], "retry", requeue: true, CancellationToken.None);

		seeks.ShouldBe([0L], "the retry found its receipt and sought back");
	}

	[Fact]
	public async Task RefuseOpposingAcknowledgment_AfterPausedRequeueSucceeds()
	{
		var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		using var release = new ManualResetEventSlim();
		var acknowledgeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var commitEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var consumer = FakeConsumer([Record(7), null], []);
		A.CallTo(() => consumer.Seek(A<TopicPartitionOffset>._)).Invokes(() =>
		{
			entered.TrySetResult();
			if (!release.Wait(TimeSpan.FromSeconds(10)))
				{
					throw new TimeoutException("The paused native operation was not released by its test owner.");
				}
		});
		A.CallTo(() => consumer.Commit(A<IEnumerable<TopicPartitionOffset>>._)).Invokes(() => commitEntered.TrySetResult());
		var receiver = CreateReceiver(consumer, new KafkaPartitionProgress());
		var message = (await receiver.ReceiveAsync(1, CancellationToken.None))[0];
		var requeue = Task.Factory.StartNew(() => receiver.RejectAsync(message, "retry", true, CancellationToken.None),
			CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
		Task<TransportSettlementException>? opposing = null;
		try
		{
			await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
			opposing = Task.Factory.StartNew(async () =>
			{
				acknowledgeStarted.TrySetResult();
				return await Should.ThrowAsync<TransportSettlementException>(() => receiver.AcknowledgeAsync(message, CancellationToken.None));
			}, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
			await acknowledgeStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
			try
			{
				await commitEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
				throw new InvalidOperationException("Acknowledgment committed a receipt while its requeue Seek was still in flight.");
			}
			catch (TimeoutException)
			{
				// An opposing settlement must wait until the receipt's native owner finishes.
			}
		}
		finally
		{
			release.Set();
			await requeue.WaitAsync(TimeSpan.FromSeconds(5));
			if (opposing is not null)
			{
				var refusal = await opposing.WaitAsync(TimeSpan.FromSeconds(5));
				refusal.Retryability.ShouldBe(SettlementRetryability.Permanent);
				refusal.RedeliveryExpectation.ShouldBe(TransportRedeliveryExpectation.Unspecified);
			}
		}
		A.CallTo(() => consumer.Commit(A<IEnumerable<TopicPartitionOffset>>._)).MustNotHaveHappened();
		A.CallTo(() => consumer.Seek(A<TopicPartitionOffset>._)).MustHaveHappenedOnceExactly();
	}

	[Theory]
	[InlineData(false, false)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task RefuseAnOldDelivery_AfterTheSameOffsetWasRedelivered(bool reject, bool requeue)
	{
		var seeks = new List<long>();
		var consumer = FakeConsumer([Record(7), Record(7)], seeks);
		var receiver = CreateReceiver(consumer, new KafkaPartitionProgress());
		var original = (await receiver.ReceiveAsync(1, CancellationToken.None))[0];
		await receiver.RejectAsync(original, "retry", true, CancellationToken.None);
		var redelivered = (await receiver.ReceiveAsync(1, CancellationToken.None))[0];
		redelivered.ProviderData["kafka.receipt_handle"].ShouldBe(original.ProviderData["kafka.receipt_handle"],
			"the native topic, partition, offset and assignment generation remain unchanged");
		var refusal = await Should.ThrowAsync<TransportSettlementException>(() => reject
			? receiver.RejectAsync(original, "delayed old delivery", requeue, CancellationToken.None)
			: receiver.AcknowledgeAsync(original, CancellationToken.None));
		refusal.TransportName.ShouldBe("Kafka");
		refusal.Retryability.ShouldBe(SettlementRetryability.Permanent);
		refusal.RedeliveryExpectation.ShouldBe(TransportRedeliveryExpectation.Unspecified);
		A.CallTo(() => consumer.Commit(A<IEnumerable<TopicPartitionOffset>>._)).MustNotHaveHappened();
		seeks.ShouldBe([7L], "the obsolete delivery must neither requeue nor remove the replacement receipt");
		await receiver.AcknowledgeAsync(redelivered, CancellationToken.None);
		A.CallTo(() => consumer.Commit(A<IEnumerable<TopicPartitionOffset>>.That.Matches(offsets =>
			offsets.Count() == 1 && offsets.Single().Offset.Value == 8))).MustHaveHappenedOnceExactly();
		seeks.ShouldBe([7L]);
	}

	private static KafkaConsumeResult Record(long offset) =>
		new()
		{
			Topic = Topic,
			Partition = new Partition(0),
			Offset = new Offset(offset),
			Message = new Message<string, byte[]> { Key = $"m{offset}", Value = new byte[4] },
		};

	private static KafkaTransportReceiver CreateReceiver(
		IConsumer<string, byte[]> consumer,
		KafkaPartitionProgress progress) =>
		new(consumer, Topic, NullLogger<KafkaTransportReceiver>.Instance, 1024, false, progress);

	private static IConsumer<string, byte[]> FakeConsumer(IEnumerable<KafkaConsumeResult?> results, List<long> seeks)
	{
		var consumer = A.Fake<IConsumer<string, byte[]>>();
		var queue = new Queue<KafkaConsumeResult?>(results);

		A.CallTo<KafkaConsumeResult?>(() => consumer.Consume(A<TimeSpan>._))
			.ReturnsLazily(() => queue.Count > 0 ? queue.Dequeue() : null);

		A.CallTo(() => consumer.Seek(A<TopicPartitionOffset>._))
			.Invokes((TopicPartitionOffset tpo) => seeks.Add(tpo.Offset.Value));

		return consumer;
	}
}
