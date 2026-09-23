// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text;

using Confluent.Kafka;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Kafka;

using Microsoft.Extensions.Logging.Abstractions;

using Tests.Shared.Fixtures;
using Tests.Shared.Infrastructure;

namespace Excalibur.Dispatch.Integration.Tests.Transport.Kafka;

/// <summary>
/// Proves, against a real broker, that the Kafka receiver's committed position never crosses work that
/// has not reached a terminal state — and that it still advances when the work finishes.
/// </summary>
/// <remarks>
/// <para>
/// The property under test is only meaningful against a real broker: what makes an uncommitted message
/// survive is that a <em>new consumer in the same group</em> resumes from the position the broker holds.
/// A faked client cannot exhibit that, so these arms produce to a real topic, settle through the real
/// receiver, drop the consumer without a graceful close, and re-read with a fresh consumer in the same
/// group. The assertion is on the message bodies the second consumer receives — the observable effect —
/// not on which methods were called.
/// </para>
/// <para>
/// Docker is a hard requirement here rather than a skip condition. These arms exist because the defect
/// they cover was invisible to every test that did not talk to a broker; a skip would restore exactly
/// that blindness while reporting success.
/// </para>
/// </remarks>
[Collection(ContainerCollections.Kafka)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait("Database", "Kafka")]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class KafkaOffsetProgressIntegrationShould
{
	private static readonly TimeSpan ReceiveTimeout = TestTimeouts.Scale(TimeSpan.FromSeconds(30));

	/// <summary>Small enough that a deliberately oversized body is rejected before materialization.</summary>
	private const int MaxPayloadBytes = 64;

	private readonly KafkaContainerFixture _fixture;

	public KafkaOffsetProgressIntegrationShould(KafkaContainerFixture fixture) => _fixture = fixture;

	private void RequireKafka() =>
		_fixture.DockerAvailable.ShouldBeTrue(
			_fixture.InitializationError
			?? "A real Kafka broker is required: these arms prove offset-commit behaviour that only a broker exhibits.");

	// ---- The reproduction: an oversized message must not carry the position past an unhandled one ----

	[Fact]
	public async Task NotLoseAMessageNoHandlerSaw_WhenTheNextOneIsOversized()
	{
		RequireKafka();
		var topic = $"progress-oversized-{Guid.NewGuid():N}";
		var group = $"progress-group-{Guid.NewGuid():N}";
		await ProduceAsync(topic, [("m0", 8), ("m1", 512), ("m2", 8)]);

		// First tenure: m0 and m2 are handed out, m1 is oversized and settles during the poll. Nothing is
		// acknowledged, so the broker must still be holding the position at the start of the partition.
		await using (var first = CreateReceiver(topic, group, out var consumer))
		{
			var batch = await ReceiveAtLeastAsync(first, 2);
			batch.Select(m => m.Id).ShouldBe(["m0", "m2"]);
			consumer.Close(); // a close, not a commit: EnableAutoCommit is off and nothing was stored
		}

		// Second tenure, same group: m0 was never handled, so it must come back.
		await using var second = CreateReceiver(topic, group, out _);
		var redelivered = await ReceiveAtLeastAsync(second, 1);

		redelivered.Select(m => m.Id).ShouldContain("m0");
	}

	[Fact]
	public async Task AdvancePastBoth_OnceTheEarlierMessageIsAcknowledged()
	{
		// Liveness partner. A receiver that never commits satisfies the arm above and makes no progress at
		// all; acknowledging m0 must carry the position past the oversized m1 as well.
		RequireKafka();
		var topic = $"progress-advance-{Guid.NewGuid():N}";
		var group = $"progress-group-{Guid.NewGuid():N}";
		await ProduceAsync(topic, [("m0", 8), ("m1", 512), ("m2", 8)]);

		await using (var first = CreateReceiver(topic, group, out var consumer))
		{
			var batch = await ReceiveAtLeastAsync(first, 2);
			await first.AcknowledgeAsync(batch[0], TestContext.Current.CancellationToken); // m0
			consumer.Close();
		}

		await using var second = CreateReceiver(topic, group, out _);
		var remaining = await ReceiveAtLeastAsync(second, 1);

		// m0 is settled and m1 is terminal, so only m2 is left. Neither of the first two comes back.
		remaining.Select(m => m.Id).ShouldBe(["m2"]);
	}

	// ---- Out-of-order acknowledgment ----

	[Fact]
	public async Task RedeliverAnUnacknowledgedMessage_WhenALaterOneWasAcknowledgedFirst()
	{
		RequireKafka();
		var topic = $"progress-ooo-{Guid.NewGuid():N}";
		var group = $"progress-group-{Guid.NewGuid():N}";
		await ProduceAsync(topic, [("m0", 8), ("m1", 8), ("m2", 8)]);

		await using (var first = CreateReceiver(topic, group, out var consumer))
		{
			var batch = await ReceiveAtLeastAsync(first, 3);
			await first.AcknowledgeAsync(batch[2], TestContext.Current.CancellationToken); // m2 finishes first
			await first.AcknowledgeAsync(batch[1], TestContext.Current.CancellationToken); // then m1
			consumer.Close();
		}

		// m0 never reached a terminal state, so the position cannot have passed it — all three come back.
		await using var second = CreateReceiver(topic, group, out _);
		var redelivered = await ReceiveAtLeastAsync(second, 3);

		redelivered.Select(m => m.Id).ShouldBe(["m0", "m1", "m2"]);
	}

	[Fact]
	public async Task AdvancePastAllOfThem_WhenTheGapFinallyCloses()
	{
		RequireKafka();
		var topic = $"progress-ooo-close-{Guid.NewGuid():N}";
		var group = $"progress-group-{Guid.NewGuid():N}";
		await ProduceAsync(topic, [("m0", 8), ("m1", 8), ("m2", 8)]);

		await using (var first = CreateReceiver(topic, group, out var consumer))
		{
			var batch = await ReceiveAtLeastAsync(first, 3);
			await first.AcknowledgeAsync(batch[2], TestContext.Current.CancellationToken);
			await first.AcknowledgeAsync(batch[1], TestContext.Current.CancellationToken);
			await first.AcknowledgeAsync(batch[0], TestContext.Current.CancellationToken);
			consumer.Close();
		}

		await ProduceAsync(topic, [("m3", 8)]);

		await using var second = CreateReceiver(topic, group, out _);
		var remaining = await ReceiveAtLeastAsync(second, 1);

		remaining.Select(m => m.Id).ShouldBe(["m3"]);
	}

	// ---- Rejection without requeue is terminal, and still bound by the prefix ----

	[Fact]
	public async Task NotLoseAnOutstandingMessage_WhenALaterOneIsRejectedToTheDeadLetterPath()
	{
		RequireKafka();
		var topic = $"progress-reject-{Guid.NewGuid():N}";
		var group = $"progress-group-{Guid.NewGuid():N}";
		await ProduceAsync(topic, [("m0", 8), ("m1", 8)]);

		await using (var first = CreateReceiver(topic, group, out var consumer))
		{
			var batch = await ReceiveAtLeastAsync(first, 2);
			await first.RejectAsync(batch[1], "poison", requeue: false, TestContext.Current.CancellationToken);
			consumer.Close();
		}

		await using var second = CreateReceiver(topic, group, out _);
		var redelivered = await ReceiveAtLeastAsync(second, 2);

		redelivered.Select(m => m.Id).ShouldBe(["m0", "m1"]);
	}

	// ---- Requeue retries inside the same healthy consumer ----

	[Fact]
	public async Task RedeliverARequeuedMessage_WithoutRestartingOrRebalancing()
	{
		// The consumer stays up and keeps polling throughout, which is precisely the case in which a
		// session timeout never fires and therefore redelivers nothing.
		RequireKafka();
		var topic = $"progress-requeue-{Guid.NewGuid():N}";
		var group = $"progress-group-{Guid.NewGuid():N}";
		await ProduceAsync(topic, [("m0", 8)]);

		await using var receiver = CreateReceiver(topic, group, out _);
		var first = await ReceiveAtLeastAsync(receiver, 1);
		await receiver.RejectAsync(first[0], "transient", requeue: true, TestContext.Current.CancellationToken);

		var retry = await ReceiveAtLeastAsync(receiver, 1);

		retry.Select(m => m.Id).ShouldBe(["m0"]);
	}

	[Fact]
	public async Task CommitPastARequeuedMessage_OnlyAfterItsRetrySucceeds()
	{
		RequireKafka();
		var topic = $"progress-requeue-ack-{Guid.NewGuid():N}";
		var group = $"progress-group-{Guid.NewGuid():N}";
		await ProduceAsync(topic, [("m0", 8), ("m1", 8)]);

		await using (var first = CreateReceiver(topic, group, out var consumer))
		{
			var batch = await ReceiveAtLeastAsync(first, 2);
			await first.AcknowledgeAsync(batch[1], TestContext.Current.CancellationToken); // m1 terminal, m0 not
			await first.RejectAsync(batch[0], "transient", requeue: true, TestContext.Current.CancellationToken);

			var retry = await ReceiveAtLeastAsync(first, 1);
			retry.Select(m => m.Id).ShouldBe(["m0"]); // only the requeued one replays, not the settled m1
			await first.AcknowledgeAsync(retry[0], TestContext.Current.CancellationToken);
			consumer.Close();
		}

		await ProduceAsync(topic, [("m2", 8)]);

		await using var second = CreateReceiver(topic, group, out _);
		var remaining = await ReceiveAtLeastAsync(second, 1);

		remaining.Select(m => m.Id).ShouldBe(["m2"]);
	}

	// ---- Ownership across a real revocation ----

	[Fact]
	public async Task NotSettleTheNewOwnersPosition_WithAReceiptFromBeforeTheRebalance()
	{
		RequireKafka();
		var topic = $"progress-rebalance-{Guid.NewGuid():N}";
		var group = $"progress-group-{Guid.NewGuid():N}";
		await ProduceAsync(topic, [("m0", 8), ("m1", 8)]);

		var progress = new KafkaPartitionProgress();
		await using (var receiver = CreateReceiver(topic, group, out var consumer, progress))
		{
			var beforeRebalance = await ReceiveAtLeastAsync(receiver, 2);

			// A real revocation and re-assignment of the same partition, through the same handlers the
			// composed transport wires. The receipts above belong to the tenure that just ended; the broker
			// rewinds the new tenure to the last committed position.
			var assignment = consumer.Assignment;
			assignment.ShouldNotBeEmpty();
			progress.OnPartitionsRevoked(assignment);
			progress.OnPartitionsAssigned(assignment);
			consumer.Seek(new TopicPartitionOffset(assignment[0], Offset.Beginning));

			// The new tenure handles m0 and is holding at m1. That is what makes the straggler dangerous:
			// honouring it would carry the position past m1, which this owner has not processed.
			var afterRebalance = await ReceiveAtLeastAsync(receiver, 2);
			await receiver.AcknowledgeAsync(afterRebalance[0], TestContext.Current.CancellationToken);

			await receiver.AcknowledgeAsync(beforeRebalance[1], TestContext.Current.CancellationToken);
			consumer.Close();
		}

		// The stale acknowledgment must not have moved the broker's position: m1 is still owed.
		await using var second = CreateReceiver(topic, group, out _);
		var redelivered = await ReceiveAtLeastAsync(second, 1);

		redelivered.Select(m => m.Id).ShouldBe(["m1"]);
	}

	[Fact]
	public async Task SettleNormallyAfterAReassignment_UnderTheCurrentGeneration()
	{
		// Liveness partner: refusing every settlement after a rebalance would satisfy the arm above while
		// wedging the consumer permanently.
		RequireKafka();
		var topic = $"progress-rebalance-live-{Guid.NewGuid():N}";
		var group = $"progress-group-{Guid.NewGuid():N}";
		await ProduceAsync(topic, [("m0", 8), ("m1", 8)]);

		var progress = new KafkaPartitionProgress();
		await using (var receiver = CreateReceiver(topic, group, out var consumer, progress))
		{
			_ = await ReceiveAtLeastAsync(receiver, 2);

			var assignment = consumer.Assignment;
			progress.OnPartitionsRevoked(assignment);
			progress.OnPartitionsAssigned(assignment);
			consumer.Seek(new TopicPartitionOffset(assignment[0], Offset.Beginning));

			var afterRebalance = await ReceiveAtLeastAsync(receiver, 2);
			foreach (var message in afterRebalance)
			{
				await receiver.AcknowledgeAsync(message, TestContext.Current.CancellationToken);
			}

			consumer.Close();
		}

		await ProduceAsync(topic, [("m2", 8)]);

		await using var second = CreateReceiver(topic, group, out _);
		var remaining = await ReceiveAtLeastAsync(second, 1);

		remaining.Select(m => m.Id).ShouldBe(["m2"]);
	}

	// ---- Helpers ----

	private async Task ProduceAsync(string topic, IReadOnlyList<(string Id, int Bytes)> messages)
	{
		var config = new ProducerConfig { BootstrapServers = _fixture.BootstrapServers, Acks = Acks.All };
		using var producer = new ProducerBuilder<string, byte[]>(config).Build();

		foreach (var (id, bytes) in messages)
		{
			var headers = new Headers { { "message-id", Encoding.UTF8.GetBytes(id) } };
			_ = await producer.ProduceAsync(
				topic,
				new Message<string, byte[]> { Key = id, Value = new byte[bytes], Headers = headers },
				TestContext.Current.CancellationToken);
		}

		_ = producer.Flush(TestTimeouts.Scale(TimeSpan.FromSeconds(10)));
	}

	private KafkaTransportReceiver CreateReceiver(
		string topic,
		string groupId,
		out IConsumer<string, byte[]> consumer,
		KafkaPartitionProgress? progress = null)
	{
		var config = new ConsumerConfig
		{
			BootstrapServers = _fixture.BootstrapServers,
			GroupId = groupId,
			AutoOffsetReset = AutoOffsetReset.Earliest,
			EnableAutoCommit = false,
		};

		consumer = new ConsumerBuilder<string, byte[]>(config).Build();
		consumer.Subscribe(topic);
		return new KafkaTransportReceiver(
			consumer,
			topic,
			NullLogger<KafkaTransportReceiver>.Instance,
			MaxPayloadBytes,
			decodeConfluentFraming: false,
			progress);
	}

	/// <summary>
	/// Polls until <paramref name="count"/> messages have been received or the timeout elapses. A single
	/// poll can return fewer than expected while the group is still being assigned its partitions.
	/// </summary>
	private static async Task<IReadOnlyList<TransportReceivedMessage>> ReceiveAtLeastAsync(
		ITransportReceiver receiver,
		int count)
	{
		var received = new List<TransportReceivedMessage>();
		var deadline = DateTimeOffset.UtcNow + ReceiveTimeout;

		while (received.Count < count && DateTimeOffset.UtcNow < deadline)
		{
			received.AddRange(await receiver.ReceiveAsync(10, TestContext.Current.CancellationToken));
		}

		received.Count.ShouldBeGreaterThanOrEqualTo(
			count,
			$"expected at least {count} message(s) within {ReceiveTimeout}, received {received.Count}");
		return received;
	}
}
