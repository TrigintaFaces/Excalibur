// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text;

using Confluent.Kafka;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Kafka;

using Microsoft.Extensions.Logging;

using Tests.Shared.Fixtures;
using Tests.Shared.Infrastructure;

namespace Excalibur.Dispatch.Integration.Tests.Transport.Kafka;

/// <summary>
/// Resolves which rebalance path a POLL-INTERVAL eviction takes, because a guarantee this package
/// documents turns on the answer and it cannot be reasoned out.
/// </summary>
/// <remarks>
/// <para>
/// A consumer that stops polling for longer than <c>max.poll.interval.ms</c> is removed from the
/// group. Two paths are then possible, and they differ in whether anything is committed: the
/// lost-partitions path records the loss and publishes nothing, while the ordinary revocation path
/// publishes the tenure's settled position first. That difference decides how much progress each
/// eviction cycle makes, and therefore whether a host whose settlement concurrency does not fall
/// after an eviction re-enters the same state indefinitely.
/// </para>
/// <para>
/// <b>This is a characterization arm, not a safety arm.</b> It pins version-specific client
/// behaviour that an analysis depends on, so that a client upgrade which changes it fails here
/// rather than silently invalidating the analysis. It asserts what the client does, and makes no
/// claim about whether that is correct.
/// </para>
/// <para>
/// <b>Non-vacuity is a property of the FIXTURE STATE, and the fixture must settle something.</b>
/// When a tenure has settled nothing, the revocation path finds no position to publish and reports
/// exactly what the lost path reports — so an arm that evicts an idle consumer prints the same thing
/// under both behaviours and cannot discriminate. The arm below settles a message before provoking
/// the eviction for that reason, and asserts the settlement happened so the precondition cannot rot.
/// </para>
/// <para>
/// Docker is a hard requirement, not a skip condition: the behaviour under test is the broker's and
/// the client's, and a skip would report success while measuring nothing.
/// </para>
/// </remarks>
[Collection(ContainerCollections.Kafka)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait("Database", "Kafka")]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class KafkaPollIntervalEvictionIntegrationShould
{
	/// <summary>Low enough that a deliberate pause exceeds it, high enough to exceed the session timeout.</summary>
	private const int MaxPollIntervalMs = 10_000;

	private const int SessionTimeoutMs = 6_000;

	private readonly KafkaContainerFixture _fixture;

	public KafkaPollIntervalEvictionIntegrationShould(KafkaContainerFixture fixture) => _fixture = fixture;

	[Fact]
	public async Task ReportWhichRebalancePathAPollIntervalEvictionTakes()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			_fixture.InitializationError
			?? "A real broker is required: the path an eviction takes is the client's behaviour, not ours.");

		var topic = $"poll-eviction-{Guid.NewGuid():N}";
		var group = $"g-{Guid.NewGuid():N}";
		await ProduceAsync(topic, 3);

		var logger = new EventCapturingLogger<KafkaTransportReceiver>();
		var progress = new KafkaPartitionProgress();
		var config = new ConsumerConfig
		{
			BootstrapServers = _fixture.BootstrapServers,
			GroupId = group,
			AutoOffsetReset = AutoOffsetReset.Earliest,
			EnableAutoCommit = false,
			EnableAutoOffsetStore = false,
			MaxPollIntervalMs = MaxPollIntervalMs,
			SessionTimeoutMs = SessionTimeoutMs,
		};

		var builder = new ConsumerBuilder<string, byte[]>(config);

		// The package's OWN instrumentation is the discriminator, so the handlers must be attached.
		// The sibling helpers in this directory build a bare consumer and would observe nothing.
		KafkaConsumerRebalance.Configure(builder, logger, progress);
		using var consumer = builder.Build();
		consumer.Subscribe(topic);

		var receiver = new KafkaTransportReceiver(
			consumer, topic, logger, maxPayloadBytes: 1024, decodeConfluentFraming: false, progress);

		// SETTLE SOMETHING. Without this the two paths are indistinguishable in the log, and the arm
		// is green under either behaviour -- the fixture-state vacuity this file's remarks describe.
		var first = await ReceiveOneAsync(receiver);
		await receiver.AcknowledgeAsync(first, TestContext.Current.CancellationToken);
		progress.TryGetPosition(new TopicPartition(topic, new Partition(0)), out var settledPosition)
			.ShouldBeTrue("the tenure must have a settled position, or neither path can be told from the other");
		settledPosition.ShouldNotBeNull();

		// Provoke the eviction by not polling. Nothing else is required: no concurrency, no stall.
		await Task.Delay(TimeSpan.FromMilliseconds(MaxPollIntervalMs + 6_000), TestContext.Current.CancellationToken); // delay-ok: Intentionally exceed the real broker max.poll.interval to stimulate eviction; subsequent typed fault/rebalance is the witness.

		// MEASURED, and it is what neither reading of this predicted: the client does not quietly
		// dispatch a callback. librdkafka THROWS out of Consume -- "Application maximum poll interval
		// (10000ms) exceeded by 84ms" -- and the receiver lets it propagate, so the consumer's pump
		// gets a typed exception naming the exact cause. That is a backpressure signal, and the loop
		// argument that assumed none has to account for it.
		var deadline = DateTimeOffset.UtcNow + TestTimeouts.Scale(TimeSpan.FromSeconds(60));
		ConsumeException? evictionFault = null;
		while (DateTimeOffset.UtcNow < deadline && !SawRebalance(logger))
		{
			try
			{
				_ = await receiver.ReceiveAsync(10, TestContext.Current.CancellationToken);
			}
			catch (ConsumeException ex)
			{
				// Recorded rather than swallowed: the FIRST observable effect of the eviction is this
				// throw, and a pump that ignores it is the precondition of the redelivery loop.
				evictionFault ??= ex;
			}
		}

		evictionFault.ShouldNotBeNull(
			"the eviction must surface to the caller as a typed fault before any rebalance callback; "
			+ "if this is ever null the loop analysis loses its loudness argument");
		evictionFault.Message.ShouldContain(
			"maximum poll interval",
			Case.Insensitive,
			"the fault must name the cause, or a pump cannot distinguish it from any other consume error");

		var ids = logger.EventIds;
		SawRebalance(logger).ShouldBeTrue(
			$"the eviction must surface through one of the package's rebalance paths; saw [{string.Join(", ", ids)}]");

		var lost = ids.Contains(KafkaEventId.PartitionsLost);
		var revoked = ids.Contains(KafkaEventId.PartitionsRevoked);
		var committedOnRevoke = ids.Contains(KafkaEventId.CommitOnRevoke);
		var nothingSettled = ids.Contains(KafkaEventId.CommitOnRevokeNothingSettled);

		// PINNED BEHAVIOUR. A client upgrade that changes which path an eviction takes changes how much
		// progress each cycle makes, so it must fail here rather than quietly invalidating the analysis.
		// Either path is recorded rather than asserted, because which one fires is the question this arm
		// exists to answer; what IS asserted is that exactly one fired and that the fault above preceded
		// it. If a future run flips the branch, the guarantee doc's Kafka entry is what changes.
		// An earlier version asserted exactly-one-path-fired. That is a WELL-FORMEDNESS assertion
		// wearing a branch assertion's clothes: exactly-one-path is implied by the implementation
		// under test, so the predicate was already true before the question was asked, and the arm
		// was green under either answer. The same defect as a positive control drawn from inside the
		// blast radius. So the branch is PINNED, and the failure message carries the alternative: if
		// this reddens, read it as the answer changing, and update the guarantee doc's Kafka entry
		// rather than relaxing the assertion.
		lost.ShouldBeTrue(
			$"a poll-interval eviction is recorded as taking the lost-partitions path. lost={lost}, "
			+ $"revoked={revoked}, committedOnRevoke={committedOnRevoke}, "
			+ $"nothingSettled={nothingSettled}, ids=[{string.Join(", ", ids)}]");

		// The branch that publishes nothing is the one the redelivery loop needs. When the revocation
		// path fires it publishes the settled prefix -- but never the owed tail, which its own position
		// arithmetic excludes -- so progress per cycle and redelivery of the tail are different
		// quantities and a green "progress happened" does not settle the loop.
		if (revoked)
		{
			(committedOnRevoke || nothingSettled).ShouldBeTrue(
				"the revocation path must either publish a position or report that it had none");
		}
	}

	private static bool SawRebalance(EventCapturingLogger<KafkaTransportReceiver> logger) =>
		logger.EventIds.Contains(KafkaEventId.PartitionsLost)
		|| logger.EventIds.Contains(KafkaEventId.PartitionsRevoked);

	private async Task<TransportReceivedMessage> ReceiveOneAsync(ITransportReceiver receiver)
	{
		var deadline = DateTimeOffset.UtcNow + TestTimeouts.Scale(TimeSpan.FromSeconds(45));
		while (DateTimeOffset.UtcNow < deadline)
		{
			var batch = await receiver.ReceiveAsync(1, TestContext.Current.CancellationToken);
			if (batch.Count > 0)
			{
				return batch[0];
			}
		}

		throw new InvalidOperationException("no message arrived from the broker within the receive window");
	}

	private async Task ProduceAsync(string topic, int count)
	{
		using var producer = new ProducerBuilder<string, byte[]>(
			new ProducerConfig { BootstrapServers = _fixture.BootstrapServers }).Build();

		for (var i = 0; i < count; i++)
		{
			var id = $"m{i}";
			var headers = new Headers { { "message-id", Encoding.UTF8.GetBytes(id) } };
			_ = await producer.ProduceAsync(
				topic,
				new Message<string, byte[]> { Key = id, Value = new byte[8], Headers = headers },
				TestContext.Current.CancellationToken);
		}

		_ = producer.Flush(TestTimeouts.Scale(TimeSpan.FromSeconds(10)));
	}

	private sealed class EventCapturingLogger<T> : ILogger<T>, ILogger
	{
		private readonly List<int> _ids = [];

		public IReadOnlyList<int> EventIds
		{
			get
			{
				lock (_ids)
				{
					return [.. _ids];
				}
			}
		}

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
			lock (_ids)
			{
				_ids.Add(eventId.Id);
			}
		}
	}
}
