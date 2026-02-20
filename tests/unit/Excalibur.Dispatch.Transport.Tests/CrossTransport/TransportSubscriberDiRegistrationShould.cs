// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.CrossTransport;

/// <summary>
/// Verifies that all 5 transports register <see cref="ITransportSubscriber"/> as a keyed singleton
/// when calling their <c>AddXxxTransport()</c> DI entry point.
/// <para>
/// Sprint 531 (S531.11, bd-68kf2): ITransportSubscriber DI registration tests.
/// Each transport registers a keyed singleton composed with <c>TransportSubscriberBuilder.UseTelemetry().Build()</c>.
/// </para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Transport")]
[Trait("Pattern", "TRANSPORT")]
public sealed class TransportSubscriberDiRegistrationShould : UnitTestBase
{
	[Fact]
	public void Kafka_RegistersKeyedITransportSubscriber()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddKafkaTransport("kafka", kafka =>
		{
			_ = kafka.BootstrapServers("localhost:9092")
				.ConfigureConsumer(consumer => consumer.GroupId("test-group"));
		});

		// Assert
		services.Any(d =>
			d.ServiceType == typeof(ITransportSubscriber) &&
			d.ServiceKey is string key && key == "kafka" &&
			d.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue(
				"Kafka should register ITransportSubscriber as a keyed singleton");
	}

	[Fact]
	public void AzureServiceBus_RegistersKeyedITransportSubscriber()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddAzureServiceBusTransport("azure-sb", sb =>
		{
			_ = sb.ConnectionString("Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=test");
		});

		// Assert
		services.Any(d =>
			d.ServiceType == typeof(ITransportSubscriber) &&
			d.ServiceKey is string key && key == "azure-sb" &&
			d.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue(
				"Azure Service Bus should register ITransportSubscriber as a keyed singleton");
	}

	[Fact]
	public void RabbitMQ_RegistersKeyedITransportSubscriber()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddRabbitMQTransport("rabbitmq", rmq =>
		{
			_ = rmq.HostName("localhost")
				.Port(5672)
				.Credentials("guest", "guest");
		});

		// Assert
		services.Any(d =>
			d.ServiceType == typeof(ITransportSubscriber) &&
			d.ServiceKey is string key && key == "rabbitmq" &&
			d.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue(
				"RabbitMQ should register ITransportSubscriber as a keyed singleton");
	}

	[Fact]
	public void AwsSqs_RegistersKeyedITransportSubscriber()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddAwsSqsTransport("sqs", sqs =>
		{
			_ = sqs.UseRegion("us-east-1");
		});

		// Assert
		services.Any(d =>
			d.ServiceType == typeof(ITransportSubscriber) &&
			d.ServiceKey is string key && key == "sqs" &&
			d.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue(
				"AWS SQS should register ITransportSubscriber as a keyed singleton");
	}

	[Fact]
	public void GooglePubSub_RegistersKeyedITransportSubscriber_WhenSubscriptionConfigured()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddGooglePubSubTransport("pubsub", pubsub =>
		{
			_ = pubsub.ProjectId("test-project")
				.SubscriptionId("test-subscription");
		});

		// Assert
		services.Any(d =>
			d.ServiceType == typeof(ITransportSubscriber) &&
			d.ServiceKey is string key && key == "pubsub" &&
			d.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue(
				"Google PubSub should register ITransportSubscriber as a keyed singleton when SubscriptionId is configured");
	}

	[Fact]
	public void GooglePubSub_DoesNotRegisterITransportSubscriber_WhenNoSubscriptionConfigured()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddGooglePubSubTransport("pubsub", pubsub =>
		{
			_ = pubsub.ProjectId("test-project");
			// No SubscriptionId — subscriber registration should be skipped
		});

		// Assert
		services.Any(d =>
			d.ServiceType == typeof(ITransportSubscriber) &&
			d.ServiceKey is string key && key == "pubsub").ShouldBeFalse(
				"Google PubSub should NOT register ITransportSubscriber when SubscriptionId is not configured");
	}

	[Fact]
	public void AllTransports_UseCorrectServiceKey()
	{
		// Arrange
		var services = new ServiceCollection();
		var customName = "my-custom-transport";

		// Act — register each transport with a custom name
		_ = services.AddKafkaTransport(customName, kafka =>
			kafka.BootstrapServers("localhost:9092").ConfigureConsumer(c => c.GroupId("g")));
		_ = services.AddAzureServiceBusTransport(customName + "-sb", sb =>
			sb.ConnectionString("Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=test"));
		_ = services.AddRabbitMQTransport(customName + "-rmq", rmq =>
			rmq.HostName("localhost").Credentials("guest", "guest"));
		_ = services.AddAwsSqsTransport(customName + "-sqs", sqs =>
			sqs.UseRegion("us-east-1"));
		_ = services.AddGooglePubSubTransport(customName + "-pubsub", pubsub =>
			pubsub.ProjectId("test-project").SubscriptionId("test-sub"));

		// Assert — each transport's subscriber should be keyed by the provided name
		var subscriberRegistrations = services
			.Where(d => d.ServiceType == typeof(ITransportSubscriber) && d.ServiceKey is string)
			.Select(d => (string)d.ServiceKey!)
			.OrderBy(k => k, StringComparer.Ordinal)
			.ToList();

		subscriberRegistrations.Count.ShouldBe(5,
			$"Expected 5 ITransportSubscriber registrations, found: {string.Join(", ", subscriberRegistrations)}");

		subscriberRegistrations.ShouldContain(customName);
		subscriberRegistrations.ShouldContain(customName + "-sb");
		subscriberRegistrations.ShouldContain(customName + "-rmq");
		subscriberRegistrations.ShouldContain(customName + "-sqs");
		subscriberRegistrations.ShouldContain(customName + "-pubsub");
	}
}
