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
/// Proves, against a real broker and a real consumer-group rebalance, that the committed position never
/// moves past messages that are still inside handlers — and that it still advances once they finish.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect.</b> librdkafka defaults <c>enable.auto.offset.store=true</c>, which records offset+1
/// for every message at the moment <c>Consume</c> hands it to the application, before any handler has
/// run. The partition-revocation handler commits the consumer's STORED offsets, so a rebalance while
/// work was in flight committed a position that was never true. The next tenure resumed past those
/// offsets and no member ever fetched them again. Nothing throws, nothing is dead-lettered, nothing is
/// logged; the messages are simply gone if the handler does not finish.
/// </para>
/// <para>
/// <b>Why these arms build the consumer through the production path.</b> The sibling suite's helper
/// hand-builds a <see cref="ConsumerConfig"/> and wires no rebalance handler, which is right for what it
/// tests and would make THIS suite vacuous twice over: the hand-built config never receives the
/// <c>enable.auto.offset.store=false</c> that is half the fix, and with no revoke handler
/// <c>CommitOnRevoke</c> — the thing under test — never executes at all. Both arms therefore go through
/// <see cref="KafkaConsumerConfigBuilder"/> and <see cref="KafkaConsumerRebalance"/>, exactly as the DI
/// registration does, so the mechanism being asserted is the shipped one.
/// </para>
/// <para>
/// <b>Why a real broker is not negotiable here.</b> A faked consumer returns what it was told; it has no
/// offset store, no group coordinator and no rebalance, so it can exhibit none of this. Docker is a hard
/// requirement rather than a skip condition, because a skip would restore precisely the blindness that
/// let this ship — the suite would report success while testing nothing.
/// </para>
/// </remarks>
[Collection(ContainerCollections.Kafka)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait("Database", "Kafka")]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class KafkaRebalanceOffsetStoreIntegrationShould
{
	private static readonly TimeSpan ReceiveTimeout = TestTimeouts.Scale(TimeSpan.FromSeconds(30));
	private static readonly TimeSpan RebalanceTimeout = TestTimeouts.Scale(TimeSpan.FromSeconds(60));

	private readonly KafkaContainerFixture _fixture;

	public KafkaRebalanceOffsetStoreIntegrationShould(KafkaContainerFixture fixture) => _fixture = fixture;

	private void RequireKafka() =>
		_fixture.DockerAvailable.ShouldBeTrue(
			_fixture.InitializationError
			?? "A real Kafka broker is required: a rebalance and an offset store are broker behaviour, and a fake has neither.");

	/// <summary>
	/// SAFETY, and the reproduction. Messages handed to handlers that have not finished must survive a
	/// rebalance. RED before the fix: auto-offset-store had already recorded a position past all three,
	/// so the revoke handler committed it and the second tenure resumed past them.
	/// </summary>
	[Fact]
	public async Task NotCommitPastMessagesStillInFlight_WhenAPartitionIsRevokedByARebalance()
	{
		RequireKafka();
		var topic = $"rebalance-inflight-{Guid.NewGuid():N}";
		var group = $"rebalance-group-{Guid.NewGuid():N}";
		await CreateTopicAsync(topic, partitions: 2);
		await ProduceAsync(topic, ["m0", "m1", "m2"]);

		await using (var receiver = CreateProductionReceiver(topic, group, out var consumer))
		{
			// All three are handed out. NOTHING is settled: every one is "inside a handler" that has not
			// returned, which is the state the defect requires and the state a single-instance consumer
			// test never reaches.
			var handedOut = await ReceiveAtLeastAsync(receiver, 3);
			handedOut.Count.ShouldBe(3);

			// A second member joining the group is a real rebalance: the coordinator revokes the
			// partition from this consumer, which runs CommitOnRevoke.
			await ForceRebalanceAsync(topic, group, consumer);

			consumer.Close();
		}

		// A fresh member of the same group resumes from whatever the BROKER holds. That is the observable
		// effect and the only one that matters: if the position moved past unfinished work, these
		// messages are unreachable to every member of the group, forever.
		var survivors = await ReadCommittedTailAsync(topic, group, expected: 3);

		survivors.ShouldBe(["m0", "m1", "m2"]);
	}

	/// <summary>
	/// LIVENESS. Refusing to commit anything would satisfy the arm above perfectly. Once the work IS
	/// settled, a rebalance must carry the position past it, or the fix has traded message loss for
	/// unbounded redelivery.
	/// </summary>
	[Fact]
	public async Task StillCommitTheSettledPrefix_WhenAPartitionIsRevokedWithNoWorkInFlight()
	{
		RequireKafka();
		var topic = $"rebalance-settled-{Guid.NewGuid():N}";
		var group = $"rebalance-group-{Guid.NewGuid():N}";
		await CreateTopicAsync(topic, partitions: 2);
		await ProduceAsync(topic, ["m0", "m1", "m2"]);

		await using (var receiver = CreateProductionReceiver(topic, group, out var consumer))
		{
			var handedOut = await ReceiveAtLeastAsync(receiver, 3);
			handedOut.Count.ShouldBe(3);

			foreach (var message in handedOut)
			{
				await receiver.AcknowledgeAsync(message, CancellationToken.None);
			}

			await ForceRebalanceAsync(topic, group, consumer);
			consumer.Close();
		}

		var redelivered = await ReadCommittedTailAsync(topic, group, expected: 0);

		redelivered.ShouldBeEmpty();
	}

	// ---- helpers -------------------------------------------------------------------------------

	/// <summary>
	/// Builds the consumer the way the DI registration does — the shipped config builder plus the
	/// revoke/assign handlers — so both halves of the fix are actually in the loop.
	/// </summary>
	private KafkaTransportReceiver CreateProductionReceiver(
		string topic,
		string groupId,
		out IConsumer<string, byte[]> consumer)
	{
		var config = BuildProductionConfig(groupId);

		var progress = new KafkaPartitionProgress();
		var builder = new ConsumerBuilder<string, byte[]>(config);
		KafkaConsumerRebalance.Configure(builder, NullLogger.Instance, progress);

		consumer = builder.Build();
		consumer.Subscribe(topic);

		return new KafkaTransportReceiver(
			consumer,
			topic,
			NullLogger<KafkaTransportReceiver>.Instance,
			maxPayloadBytes: null,
			decodeConfluentFraming: false,
			progress);
	}

	/// <summary>
	/// The consumer configuration the shipped DI registration produces, which is where
	/// <c>enable.auto.offset.store=false</c> comes from. Both group members are built from it so the
	/// broker sees one consistent group.
	/// </summary>
	private ConsumerConfig BuildProductionConfig(string groupId)
	{
		var options = new KafkaOptions
		{
			BootstrapServers = _fixture.BootstrapServers,
			ConsumerGroup = groupId,

			// The throwaway container speaks plaintext. The production security posture refuses that by
			// default, which is correct and is itself a sign this arm goes through the shipped
			// configuration path rather than around it.
			RequireTls = false,
		};
		options.Consumer.AutoOffsetReset = "earliest";
		options.Consumer.EnableAutoCommit = false;
		options.Consumer.SessionTimeoutMs = 10000;

		return KafkaConsumerConfigBuilder.Build(options);
	}

	/// <summary>
	/// Causes a genuine group rebalance by adding a second member, then polls the incumbent so its
	/// revocation handler actually runs — librdkafka delivers rebalance callbacks on Consume, so a
	/// consumer that is never polled never learns it was revoked.
	/// </summary>
	private async Task ForceRebalanceAsync(string topic, string groupId, IConsumer<string, byte[]> incumbent)
	{
		// Built through the SAME production path as the incumbent. Two members of one group must agree
		// on the assignment strategy and group protocol; a hand-built joiner does not, and the broker
		// rejects it with "Inconsistent group protocol" before any rebalance can occur. Configuring it
		// identically is also the more faithful shape: this is a second instance of the same application.
		using var joiner = new ConsumerBuilder<string, byte[]>(BuildProductionConfig(groupId)).Build();
		joiner.Subscribe(topic);

		var deadline = DateTime.UtcNow + RebalanceTimeout;
		var revoked = false;

		while (DateTime.UtcNow < deadline && !revoked)
		{
			// Poll BOTH: the joiner drives the coordinator to start the rebalance, and the incumbent has
			// to be polled for its revoke callback to fire.
			_ = joiner.Consume(TimeSpan.FromMilliseconds(250));
			_ = incumbent.Consume(TimeSpan.FromMilliseconds(250));

			// With TWO partitions and two members the coordinator must hand the joiner one of them, so
			// this becomes true exactly when the group has actually rebalanced. With a single partition
			// it may not: the incumbent can be revoked and immediately reassigned the same partition,
			// and the transient is invisible to a poll-based observer. The detector has to be something
			// that LATCHES, or the arm reports "no rebalance" for a rebalance that did happen.
			revoked = joiner.Assignment.Count > 0;

			await Task.Yield();
		}

		revoked.ShouldBeTrue(
			"the rebalance never happened, so this arm proved nothing about the revoke path — "
			+ "treat this as an inconclusive run, not a pass.");

		joiner.Close();
	}

	/// <summary>
	/// Creates the topic explicitly rather than relying on broker auto-creation, which yields a single
	/// partition and makes the rebalance unobservable.
	/// </summary>
	private async Task CreateTopicAsync(string topic, int partitions)
	{
		// Fully qualified: this package ships its own TopicSpecification, so the unqualified name is
		// ambiguous here and the compiler is right to say so.
		using var admin = new AdminClientBuilder(
			new AdminClientConfig { BootstrapServers = _fixture.BootstrapServers }).Build();

		await admin.CreateTopicsAsync(
		[
			new global::Confluent.Kafka.Admin.TopicSpecification
			{
				Name = topic,
				NumPartitions = partitions,
				ReplicationFactor = 1,
			},
		]);
	}

	private async Task ProduceAsync(string topic, IReadOnlyList<string> ids)
	{
		var config = new ProducerConfig { BootstrapServers = _fixture.BootstrapServers };
		using var producer = new ProducerBuilder<string, byte[]>(config).Build();

		foreach (var id in ids)
		{
			// One fixed key for every message, so all three land on the SAME partition and are therefore
			// in flight together. Spread across partitions they would settle independently and the
			// scenario under test -- a revoke while a contiguous prefix is unfinished -- would not arise.
			_ = await producer.ProduceAsync(
				topic,
				new Message<string, byte[]>
				{
					Key = "same-partition",
					Value = Encoding.UTF8.GetBytes(id),
					Headers = [new Header("message-id", Encoding.UTF8.GetBytes(id))],
				});
		}

		_ = producer.Flush(TimeSpan.FromSeconds(10));
	}

	private static async Task<IReadOnlyList<TransportReceivedMessage>> ReceiveAtLeastAsync(
		KafkaTransportReceiver receiver,
		int count)
	{
		var collected = new List<TransportReceivedMessage>();
		var deadline = DateTime.UtcNow + ReceiveTimeout;

		while (collected.Count < count && DateTime.UtcNow < deadline)
		{
			var batch = await receiver.ReceiveAsync(count - collected.Count, CancellationToken.None);
			collected.AddRange(batch);
		}

		return collected;
	}

	/// <summary>
	/// Reads what a NEW member of the same group can still see. This is the assertion that matters: it
	/// is the broker's committed position speaking, not our own bookkeeping repeated back to us.
	/// </summary>
	private async Task<IReadOnlyList<string>> ReadCommittedTailAsync(string topic, string groupId, int expected)
	{
		var config = new ConsumerConfig
		{
			BootstrapServers = _fixture.BootstrapServers,
			GroupId = groupId,
			AutoOffsetReset = AutoOffsetReset.Earliest,
			EnableAutoCommit = false,
			EnableAutoOffsetStore = false,
		};

		using var consumer = new ConsumerBuilder<string, byte[]>(config).Build();
		consumer.Subscribe(topic);

		var seen = new List<string>();

		// When nothing is expected, the absence has to be given a fair chance to appear or the arm would
		// pass simply by not waiting.
		var window = expected == 0 ? TestTimeouts.Scale(TimeSpan.FromSeconds(10)) : ReceiveTimeout;
		var deadline = DateTime.UtcNow + window;

		while (DateTime.UtcNow < deadline && (expected == 0 || seen.Count < expected))
		{
			var result = consumer.Consume(TimeSpan.FromMilliseconds(500));
			if (result?.Message is not null)
			{
				seen.Add(Encoding.UTF8.GetString(result.Message.Value));
			}

			await Task.Yield();
		}

		consumer.Close();
		return seen;
	}
}
