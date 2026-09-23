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
/// Only the subscription's own cancellation ends the consume loop. A handler that cancels work on a token
/// it owns is a failed handler, not a reason to stop consuming.
/// </summary>
/// <remarks>
/// A per-attempt timeout or an HTTP client's own deadline throws an <see cref="OperationCanceledException"/>
/// carrying THAT token. Treating it as the subscription's cancellation ended the subscription silently:
/// the consumer stopped, the message was neither settled nor redelivered, and nothing was logged as an
/// error.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class KafkaSubscriberHandlerCancellationShould
{
	private const string Topic = "orders";
	private static readonly TopicPartition Partition0 = new(Topic, new Partition(0));

	/// <summary>
	/// LIVENESS. A handler that throws a cancellation on its OWN token does not stop the subscription: the
	/// message is sought back for redelivery and the loop keeps consuming.
	/// </summary>
	/// <remarks>
	/// RED against a loop that tests the exception's own token: the subscription ends on the first handler
	/// timeout and offset 11 is never delivered.
	/// </remarks>
	[Fact]
	public async Task KeepConsuming_WhenAHandlerCancelsOnItsOwnToken()
	{
		using var cts = new CancellationTokenSource();
		var log = new List<KafkaConsumeResult> { Record(10), Record(11) };
		var cursor = 0;
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

		var delivered = new List<long>();
		var handlerOwnedTimeout = 0;

		var progress = new KafkaPartitionProgress();
		progress.OnPartitionsAssigned([Partition0]);
		var subscriber = new KafkaTransportSubscriber(
			consumer, Topic, NullLogger<KafkaTransportSubscriber>.Instance, 1024, false, progress);

		await subscriber.SubscribeAsync(
			(message, _) =>
			{
				var offset = (long)message.ProviderData[TransportOrderingMetadata.KafkaOffsetKey];
				delivered.Add(offset);

				// The first delivery of offset 10 times out on a token the HANDLER owns.
				if (offset == 10 && handlerOwnedTimeout++ == 0)
				{
					using var handlerTimeout = new CancellationTokenSource();
					handlerTimeout.Cancel();
					handlerTimeout.Token.ThrowIfCancellationRequested();
				}

				return Task.FromResult(MessageAction.Acknowledge);
			},
			cts.Token);

		delivered.ShouldContain(11, "a handler's own timeout must not end the subscription");
		delivered.Count(o => o == 10).ShouldBeGreaterThan(1, "the timed-out message is sought back and redelivered");
	}

	/// <summary>
	/// SAFETY. Cancelling the SUBSCRIPTION still stops the loop, so the fix does not make the subscription
	/// unstoppable.
	/// </summary>
	[Fact]
	public async Task StopConsuming_WhenTheSubscriptionItselfIsCancelled()
	{
		using var cts = new CancellationTokenSource();
		var consumer = A.Fake<IConsumer<string, byte[]>>();
		var delivered = 0;
		A.CallTo<KafkaConsumeResult?>(() => consumer.Consume(A<CancellationToken>._))
			.ReturnsLazily(() => Record(10 + delivered));

		var progress = new KafkaPartitionProgress();
		progress.OnPartitionsAssigned([Partition0]);
		var subscriber = new KafkaTransportSubscriber(
			consumer, Topic, NullLogger<KafkaTransportSubscriber>.Instance, 1024, false, progress);

		await subscriber.SubscribeAsync(
			(_, _) =>
			{
				delivered++;
				cts.Cancel();
				cts.Token.ThrowIfCancellationRequested();
				return Task.FromResult(MessageAction.Acknowledge);
			},
			cts.Token);

		delivered.ShouldBe(1, "the subscription's own cancellation ends the loop");
	}

	private static KafkaConsumeResult Record(long offset) =>
		new()
		{
			Topic = Topic,
			Partition = new Partition(0),
			Offset = new Offset(offset),
			Message = new Message<string, byte[]> { Key = $"m{offset}", Value = [1, 2, 3] },
		};
}
