// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

#pragma warning disable CA2012 // FakeItEasy .Returns() stores ValueTask/Task

using Confluent.Kafka;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Kafka;

using Microsoft.Extensions.Logging;

using KafkaConsumeResult = global::Confluent.Kafka.ConsumeResult<string, byte[]>;

namespace Excalibur.Dispatch.Transport.Tests.Kafka;

/// <summary>
/// A broker commit that fails after the handler succeeded is a settlement failure, not a handler failure.
/// </summary>
/// <remarks>
/// The handler's work is done; only the committed position is unpersisted, and the next settle on the
/// partition commits it again. Treating the failure as a thrown handler would seek back and run the work a
/// second time in this process, on top of whatever the broker redelivers after a restart.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class KafkaSubscriberCommitFailureIsNotAHandlerFailureShould
{
	private const string Topic = "orders";

	/// <summary>
	/// SAFETY. The failed commit is reported as a failed commit, the message is not sought back, and the
	/// next settle re-commits the position.
	/// </summary>
	/// <remarks>
	/// RED against a subscriber that settles inside the handler's try: the commit failure is logged as a
	/// handler failure and routed to the redelivery path.
	/// </remarks>
	[Fact]
	public async Task ReportTheFailedCommit_AndRecommitOnTheNextSettle()
	{
		var commits = new List<long>();
		var commitCalls = 0;
		var consumer = A.Fake<IConsumer<string, byte[]>>();
		var queue = new Queue<KafkaConsumeResult>([Record(0), Record(1)]);
		A.CallTo<KafkaConsumeResult?>(() => consumer.Consume(A<CancellationToken>._))
			.ReturnsLazily(() => queue.Count > 0 ? queue.Dequeue() : null);
		A.CallTo(() => consumer.Commit(A<IEnumerable<TopicPartitionOffset>>._))
			.Invokes((IEnumerable<TopicPartitionOffset> offsets) =>
			{
				if (++commitCalls == 1)
				{
					throw new KafkaException(ErrorCode.RequestTimedOut);
				}

				commits.AddRange(offsets.Select(o => o.Offset.Value));
			});

		var logger = new EventCapturingLogger<KafkaTransportSubscriber>();
		var subscriber = new KafkaTransportSubscriber(consumer, Topic, logger, 1024, false, new KafkaPartitionProgress());
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		var handled = 0;

		await subscriber.SubscribeAsync(
			(_, _) =>
			{
				if (++handled == 2)
				{
					cts.Cancel();
				}

				return Task.FromResult(MessageAction.Acknowledge);
			},
			cts.Token);

		logger.EventIds.ShouldContain(KafkaEventId.TransportSubscriberSettlementCommitFailed, "the failed commit must be reported as a settlement failure");
		logger.EventIds.ShouldNotContain(KafkaEventId.TransportSubscriberError, "the handler succeeded; nothing it did failed");
		A.CallTo(() => consumer.Seek(A<TopicPartitionOffset>._)).MustNotHaveHappened();
		commits.ShouldBe([2L], "the next settle commits the position the failed commit left unpersisted");
	}

	/// <summary>
	/// LIVENESS. A settlement failure that is not a broker error still leaves the subscription running.
	/// </summary>
	/// <remarks>
	/// RED against a subscriber whose settlement catch admits only broker errors: the first failure ends the
	/// loop and the second message is never handled.
	/// </remarks>
	[Fact]
	public async Task KeepSubscribing_WhenSettlementFailsForAnyReason()
	{
		var commitCalls = 0;
		var consumer = A.Fake<IConsumer<string, byte[]>>();
		var queue = new Queue<KafkaConsumeResult>([Record(0), Record(1)]);
		A.CallTo<KafkaConsumeResult?>(() => consumer.Consume(A<CancellationToken>._))
			.ReturnsLazily(() => queue.Count > 0 ? queue.Dequeue() : null);
		A.CallTo(() => consumer.Commit(A<IEnumerable<TopicPartitionOffset>>._))
			.Invokes(() =>
			{
				if (++commitCalls == 1)
				{
					throw new ObjectDisposedException("consumer");
				}
			});

		var logger = new EventCapturingLogger<KafkaTransportSubscriber>();
		var subscriber = new KafkaTransportSubscriber(consumer, Topic, logger, 1024, false, new KafkaPartitionProgress());
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		var handled = 0;

		await subscriber.SubscribeAsync(
			(_, _) =>
			{
				if (++handled == 2)
				{
					cts.Cancel();
				}

				return Task.FromResult(MessageAction.Acknowledge);
			},
			cts.Token);

		handled.ShouldBe(2, "the loop must survive a settlement failure and handle the next message");
		logger.EventIds.ShouldContain(KafkaEventId.TransportSubscriberSettlementCommitFailed);
	}

	private static KafkaConsumeResult Record(long offset) =>
		new()
		{
			Topic = Topic,
			Partition = new Partition(0),
			Offset = new Offset(offset),
			Message = new Message<string, byte[]> { Key = $"m{offset}", Value = new byte[4] },
		};

	private sealed class EventCapturingLogger<T> : ILogger<T>
	{
		public List<int> EventIds { get; } = [];

		public IDisposable? BeginScope<TState>(TState state)
			where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(
			LogLevel logLevel,
			EventId eventId,
			TState state,
			Exception? exception,
			Func<TState, Exception?, string> formatter)
		{
			lock (EventIds)
			{
				EventIds.Add(eventId.Id);
			}
		}
	}
}
