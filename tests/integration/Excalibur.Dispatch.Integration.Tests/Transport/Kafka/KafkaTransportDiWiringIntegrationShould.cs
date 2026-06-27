// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Confluent.Kafka;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Kafka;

using Tests.Shared.Fixtures;

namespace Excalibur.Dispatch.Integration.Tests.Transport.Kafka;

/// <summary>
/// Regression lock for bd-9b8dbc: the Kafka transport advertised a subscribe/consume path
/// (<c>AddKafkaTransport</c> registers a keyed <see cref="ITransportSubscriber"/>) but the path was
/// inert at runtime — <c>KafkaTransportServiceCollectionExtensions</c> resolved
/// <c>IConsumer&lt;string, byte[]&gt;</c> from DI, yet no <c>IConsumer&lt;string, byte[]&gt;</c> was ever
/// registered, so resolving the keyed subscriber threw <see cref="InvalidOperationException"/>
/// ("no service for type ... has been registered"). The fix registers a configured
/// <c>IConsumer&lt;string, byte[]&gt;</c> (GroupId, manual-commit) and wires the subscriber
/// (<c>KafkaTransportServiceCollectionExtensions.cs:247-253</c> consumer registration;
/// <c>:377-397</c> subscriber composition).
/// <para>
/// This is an author≠impl lock: authored by TestsDeveloper, independent of the implementer.
/// It exercises the DI-wired path end-to-end against a real Kafka broker (it does NOT hand-build
/// the consumer/subscriber the way the existing <c>KafkaTransportSubscriberIntegrationShould</c>
/// /<c>KafkaTransportReceiverIntegrationShould</c> tests do — those bypass DI and so could not
/// catch this bug). The existing unit test
/// <c>TransportSubscriberDiRegistrationShould.Kafka_RegistersKeyedITransportSubscriber</c> only
/// inspects the <c>ServiceCollection</c> descriptors and never builds the provider, so it passed
/// even on the pre-fix code.
/// </para>
/// <para>
/// <strong>Non-vacuity:</strong> RED on the pre-fix wiring — resolving
/// <c>IConsumer&lt;string, byte[]&gt;</c> (and therefore the keyed <see cref="ITransportSubscriber"/>)
/// throws because no consumer is registered, failing <see cref="MapResolvesConsumerTest"/> at the
/// resolution call and <see cref="DeliversEndToEndTest"/> at subscriber resolution. The impl fix
/// (bd-9b8dbc) was committed prior to this lock; the production RED-proof is therefore the
/// reasoned pre-fix-throw above. Real-Kafka, NON-SKIPPED (per
/// <c>verify-against-real-infra-not-mock</c>): a mocked consumer would certify the broken wiring.
/// </para>
/// </summary>
[Collection(ContainerCollections.Kafka)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait("Database", "Kafka")]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class KafkaTransportDiWiringIntegrationShould
{
	private static readonly TimeSpan SubscriptionTimeout = TimeSpan.FromSeconds(30);

	private const string MapResolvesConsumerTest =
		nameof(AddKafkaTransport_RegistersResolvableConsumerAndSubscriber);

	private const string DeliversEndToEndTest =
		nameof(DiWiredSubscriber_ReceivesMessagePublishedToTopic_EndToEnd);

	private readonly KafkaContainerFixture _fixture;

	public KafkaTransportDiWiringIntegrationShould(KafkaContainerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task AddKafkaTransport_RegistersResolvableConsumerAndSubscriber()
	{
		// NON-SKIPPED: the lock must run against real infrastructure, never skip itself.
		_fixture.DockerAvailable.ShouldBeTrue(
			_fixture.InitializationError ?? "Kafka container must be available — this lock is never skipped.");

		// Arrange — wire the transport exactly the way a consumer would, through the public DI entry point.
		var transportName = $"kafka-di-{Guid.NewGuid():N}";
		var groupId = $"grp-{Guid.NewGuid():N}";

		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddKafkaTransport(transportName, kafka =>
			kafka.BootstrapServers(_fixture.BootstrapServers)
				.ConfigureConsumer(consumer => consumer
					.GroupId(groupId)
					.AutoOffsetReset(KafkaOffsetReset.Earliest)
					.EnableAutoCommit(false)));

		// The keyed subscriber is composed with a telemetry decorator that is IAsyncDisposable-only,
		// so the provider must be disposed asynchronously.
		await using var provider = services.BuildServiceProvider();

		// Act + Assert — pre-fix these resolutions throw because no IConsumer<string, byte[]> is
		// registered; the subscriber factory depends on it. This is the bug bd-9b8dbc fixed.
		var consumer = provider.GetRequiredService<IConsumer<string, byte[]>>();
		consumer.ShouldNotBeNull();

		var subscriber = provider.GetRequiredKeyedService<ITransportSubscriber>(transportName);
		subscriber.ShouldNotBeNull();
	}

	[Fact]
	public async Task DiWiredSubscriber_ReceivesMessagePublishedToTopic_EndToEnd()
	{
		// NON-SKIPPED: real-Kafka end-to-end is the only thing that proves the wiring delivers.
		_fixture.DockerAvailable.ShouldBeTrue(
			_fixture.InitializationError ?? "Kafka container must be available — this lock is never skipped.");

		// Arrange — the DI-wired subscriber's Source is the configured GroupId (see RegisterSubscriber:
		// source = ConsumerOptions?.GroupId ?? name), and SubscribeAsync subscribes to that Source as the
		// topic. Use one name for both so the message we publish lands on the topic the subscriber reads.
		var topic = $"kafka-di-e2e-{Guid.NewGuid():N}";
		var transportName = $"kafka-di-{Guid.NewGuid():N}";

		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddKafkaTransport(transportName, kafka =>
			kafka.BootstrapServers(_fixture.BootstrapServers)
				.ConfigureConsumer(consumer => consumer
					.GroupId(topic)
					.AutoOffsetReset(KafkaOffsetReset.Earliest)
					.EnableAutoCommit(false)));

		await using var provider = services.BuildServiceProvider();

		// Resolve the subscriber via the wired DI path (throws pre-fix).
		var subscriber = provider.GetRequiredKeyedService<ITransportSubscriber>(transportName);

		// Publish a message to the topic before subscribing; AutoOffsetReset.Earliest delivers it.
		var expectedBody = Encoding.UTF8.GetBytes($"di-wired-payload-{Guid.NewGuid():N}");
		await ProduceMessageAsync(topic, "di-key", "di-msg-1", expectedBody).ConfigureAwait(false);

		TransportReceivedMessage? received = null;
		using var cts = new CancellationTokenSource();
		cts.CancelAfter(SubscriptionTimeout);

		// Act — drive the consume loop through the DI-wired subscriber.
		await subscriber.SubscribeAsync(
			(msg, ct) =>
			{
				received = msg;
				cts.Cancel();
				return Task.FromResult(MessageAction.Acknowledge);
			},
			cts.Token).ConfigureAwait(false);

		await subscriber.DisposeAsync().ConfigureAwait(false);

		// Assert — the message produced to the topic was delivered through the DI-wired consumer.
		received.ShouldNotBeNull();
		received.Source.ShouldBe(topic);
		received.Id.ShouldBe("di-msg-1");
		received.Body.ToArray().ShouldBe(expectedBody);
	}

	private async Task ProduceMessageAsync(string topic, string key, string messageId, byte[] body)
	{
		var config = new ProducerConfig
		{
			BootstrapServers = _fixture.BootstrapServers,
			AllowAutoCreateTopics = true,
		};

		using var producer = new ProducerBuilder<string, byte[]>(config).Build();

		await producer.ProduceAsync(topic, new Message<string, byte[]>
		{
			Key = key,
			Value = body,
			Headers = new Headers { { "message-id", Encoding.UTF8.GetBytes(messageId) } },
		}).ConfigureAwait(false);

		producer.Flush(TimeSpan.FromSeconds(5));
	}
}
