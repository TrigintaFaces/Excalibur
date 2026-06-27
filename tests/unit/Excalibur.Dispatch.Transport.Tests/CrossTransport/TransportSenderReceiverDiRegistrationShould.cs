// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.CrossTransport;

/// <summary>
/// Keystone reachability lock for bead <c>kek7vm</c> (sprint 855): each transport's
/// <c>AddXxxTransport()</c> DI entry point MUST register the rich <see cref="ITransportSender"/> and
/// <see cref="ITransportReceiver"/> as keyed singletons (keyed by transport name, matching the
/// sibling <see cref="ITransportSubscriber"/> registration), so the previously-orphaned rich
/// <c>*TransportSender</c>/<c>*TransportReceiver</c> classes are reachable on the registered path
/// rather than advertised-but-inert.
/// </summary>
/// <remarks>
/// <para>
/// Authored independently of the impl (<c>issue-remediation-protocol</c>), against committed mainline
/// after the SA-ruled (A) keyed-reachability keystone landed (<c>130e95da6</c>). SA's ruling is that
/// keyed reachability = keystone-complete; the Lane-A children (<c>ne79ro</c>/<c>abyfxr</c>/<c>fjtok4</c>)
/// separately prove the capability is exercised on the live broker (NFR-3, real-infra).
/// </para>
/// <para>
/// <b>Descriptor-level, not resolution:</b> the keyed factory constructs the rich type from broker
/// collaborators (<c>IProducer</c>/<c>IConsumer</c>/etc.), so resolving it would require a live broker.
/// This lock asserts the keyed <i>registration</i> exists (the reachability contract kek7vm delivers)
/// without instantiating it — mirroring <c>TransportSubscriberDiRegistrationShould</c>.
/// </para>
/// <para>
/// <b>Non-vacuity (RED on the pre-kek7vm surface):</b> before <c>130e95da6</c> no
/// <c>TryAddKeyedSingleton&lt;ITransportSender&gt;</c>/<c>&lt;ITransportReceiver&gt;</c> existed on the
/// <c>AddXxxTransport</c> path, so these keyed descriptors are absent and every assertion fails.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
[Trait("Pattern", "TRANSPORT")]
public sealed class TransportSenderReceiverDiRegistrationShould : UnitTestBase
{
	private static bool HasKeyedSingleton(IServiceCollection services, Type serviceType, string key) =>
		services.Any(d =>
			d.ServiceType == serviceType &&
			d.ServiceKey is string k && k == key &&
			d.Lifetime == ServiceLifetime.Singleton);

	[Fact]
	public void Kafka_RegistersKeyedSenderAndReceiver()
	{
		var services = new ServiceCollection();

		_ = services.AddKafkaTransport("kafka", kafka =>
			kafka.BootstrapServers("localhost:9092").ConfigureConsumer(c => c.GroupId("test-group")));

		HasKeyedSingleton(services, typeof(ITransportSender), "kafka")
			.ShouldBeTrue("Kafka should register a keyed ITransportSender (kek7vm reachability)");
		HasKeyedSingleton(services, typeof(ITransportReceiver), "kafka")
			.ShouldBeTrue("Kafka should register a keyed ITransportReceiver (kek7vm reachability)");
	}

	[Fact]
	public void AzureServiceBus_RegistersKeyedSenderAndReceiver()
	{
		var services = new ServiceCollection();

		_ = services.AddAzureServiceBusTransport("azure-sb", sb =>
			sb.ConnectionString("Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=test"));

		HasKeyedSingleton(services, typeof(ITransportSender), "azure-sb")
			.ShouldBeTrue("Azure Service Bus should register a keyed ITransportSender (kek7vm reachability)");
		HasKeyedSingleton(services, typeof(ITransportReceiver), "azure-sb")
			.ShouldBeTrue("Azure Service Bus should register a keyed ITransportReceiver (kek7vm reachability)");
	}

	[Fact]
	public void RabbitMQ_RegistersKeyedSenderAndReceiver()
	{
		var services = new ServiceCollection();

		_ = services.AddRabbitMQTransport("rabbitmq", rmq =>
			rmq.HostName("localhost").Port(5672).Credentials("guest", "guest"));

		HasKeyedSingleton(services, typeof(ITransportSender), "rabbitmq")
			.ShouldBeTrue("RabbitMQ should register a keyed ITransportSender (kek7vm reachability)");
		HasKeyedSingleton(services, typeof(ITransportReceiver), "rabbitmq")
			.ShouldBeTrue("RabbitMQ should register a keyed ITransportReceiver (kek7vm reachability)");
	}

	[Fact]
	public void AwsSqs_RegistersKeyedSenderAndReceiver()
	{
		var services = new ServiceCollection();

		_ = services.AddAwsSqsTransport("sqs", sqs => sqs.UseRegion("us-east-1"));

		HasKeyedSingleton(services, typeof(ITransportSender), "sqs")
			.ShouldBeTrue("AWS SQS should register a keyed ITransportSender (kek7vm reachability)");
		HasKeyedSingleton(services, typeof(ITransportReceiver), "sqs")
			.ShouldBeTrue("AWS SQS should register a keyed ITransportReceiver (kek7vm reachability)");
	}

	[Fact]
	public void GooglePubSub_RegistersKeyedSenderAndReceiver()
	{
		var services = new ServiceCollection();

		_ = services.AddGooglePubSubTransport("pubsub", pubsub =>
			pubsub.ProjectId("test-project").TopicId("test-topic").SubscriptionId("test-subscription"));

		HasKeyedSingleton(services, typeof(ITransportSender), "pubsub")
			.ShouldBeTrue("Google PubSub should register a keyed ITransportSender (kek7vm reachability)");
		HasKeyedSingleton(services, typeof(ITransportReceiver), "pubsub")
			.ShouldBeTrue("Google PubSub should register a keyed ITransportReceiver (kek7vm reachability)");
	}

	// --- kek7vm symmetric capability lock (PdM 17163 / gate-full-guard-suite "both directions") ---
	// The sender is registered iff a TopicId is configured; the receiver iff a SubscriptionId is configured
	// (GooglePubSubTransportServiceCollectionExtensions.RegisterTransportSenderReceiver:333/345). These two
	// arms make "register an unconfigured capability" inexpressible in BOTH directions — and pin the exact
	// regression that broke S855 (subscriber-only built `new TopicName(projectId, null)` → ArgumentNullException
	// at registration). RED on the pre-guard surface: subscriber-only THREW on `AddGooglePubSubTransport`.

	[Fact]
	public void GooglePubSub_SubscriberOnly_RegistersReceiverNotSender_AndResolvesClean()
	{
		var services = new ServiceCollection();

		// Subscriber-only config: ProjectId + SubscriptionId, NO TopicId. This is the exact shape that threw
		// before the guard (sender registration unconditionally built a TopicName from a null TopicId).
		Should.NotThrow(() => services.AddGooglePubSubTransport("pubsub", pubsub =>
			pubsub.ProjectId("test-project").SubscriptionId("test-subscription")));

		HasKeyedSingleton(services, typeof(ITransportSender), "pubsub")
			.ShouldBeFalse("Subscriber-only GooglePubSub (no TopicId) must NOT register a keyed ITransportSender (kek7vm capability symmetry; this is the registration that threw pre-guard)");
		HasKeyedSingleton(services, typeof(ITransportReceiver), "pubsub")
			.ShouldBeTrue("Subscriber-only GooglePubSub (SubscriptionId configured) must register a keyed ITransportReceiver");
	}

	[Fact]
	public void GooglePubSub_PublisherOnly_RegistersSenderNotReceiver()
	{
		var services = new ServiceCollection();

		// Publisher-only config: ProjectId + TopicId, NO SubscriptionId — the mirror of the case above.
		Should.NotThrow(() => services.AddGooglePubSubTransport("pubsub", pubsub =>
			pubsub.ProjectId("test-project").TopicId("test-topic")));

		HasKeyedSingleton(services, typeof(ITransportSender), "pubsub")
			.ShouldBeTrue("Publisher-only GooglePubSub (TopicId configured) must register a keyed ITransportSender");
		HasKeyedSingleton(services, typeof(ITransportReceiver), "pubsub")
			.ShouldBeFalse("Publisher-only GooglePubSub (no SubscriptionId) must NOT register a keyed ITransportReceiver (kek7vm capability symmetry)");
	}

	[Fact]
	public void AllFiveTransports_RegisterKeyedSenderAndReceiver()
	{
		var services = new ServiceCollection();

		_ = services.AddKafkaTransport("kafka", k =>
			k.BootstrapServers("localhost:9092").ConfigureConsumer(c => c.GroupId("g")));
		_ = services.AddAzureServiceBusTransport("azure-sb", sb =>
			sb.ConnectionString("Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=test"));
		_ = services.AddRabbitMQTransport("rabbitmq", rmq =>
			rmq.HostName("localhost").Credentials("guest", "guest"));
		_ = services.AddAwsSqsTransport("sqs", sqs => sqs.UseRegion("us-east-1"));
		_ = services.AddGooglePubSubTransport("pubsub", p =>
			p.ProjectId("test-project").TopicId("test-topic").SubscriptionId("test-sub"));

		var senderKeys = services
			.Where(d => d.ServiceType == typeof(ITransportSender) && d.ServiceKey is string)
			.Select(d => (string)d.ServiceKey!)
			.OrderBy(k => k, StringComparer.Ordinal)
			.ToList();
		var receiverKeys = services
			.Where(d => d.ServiceType == typeof(ITransportReceiver) && d.ServiceKey is string)
			.Select(d => (string)d.ServiceKey!)
			.OrderBy(k => k, StringComparer.Ordinal)
			.ToList();

		string[] expected = ["azure-sb", "kafka", "pubsub", "rabbitmq", "sqs"];
		senderKeys.ShouldBe(expected);
		receiverKeys.ShouldBe(expected);
	}
}
