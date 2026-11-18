// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Transport.Tests.CrossTransport;

/// <summary>
/// Verifies multi-provider keyed DI registration patterns across transports.
/// </summary>
/// <remarks>
/// Sprint 698 T.5 (9vk07): Ensures named transport registration configures
/// per-instance options and keyed service resolution.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class MultiProviderKeyedDiShould
{
	[Fact]
	public void RegisterOptionsForRabbitMQTransport()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		services.AddRabbitMQTransport("orders", rmq => rmq.ConnectionString("amqps://app-user:app-password@orders-host:5671/"));

		using var provider = services.BuildServiceProvider();

		// Assert against the factory the transport actually uses, without dialing a broker.
		var factory = provider.GetRequiredService<global::RabbitMQ.Client.IConnectionFactory>()
			.ShouldBeOfType<global::RabbitMQ.Client.ConnectionFactory>();
		factory.HostName.ShouldBe("orders-host");
		factory.Port.ShouldBe(5671);
		factory.UserName.ShouldBe("app-user");
		factory.Ssl.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void RejectDuplicateTransportNames()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act -- same name twice should throw (transport names are unique in TransportRegistry)
		services.AddRabbitMQTransport("events", rmq => rmq.ConnectionString("amqp://localhost"));

		// Assert
		Should.Throw<InvalidOperationException>(() =>
			services.AddRabbitMQTransport("events", rmq => rmq.ConnectionString("amqp://localhost")));
	}

	[Fact]
	public void RegisterTransportSubscriberForNamedTransport()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		services.AddRabbitMQTransport("subscriber-test", rmq => rmq.ConnectionString("amqp://localhost"));

		// Assert -- ITransportSubscriber registered for the transport
		services.ShouldContain(sd => sd.ServiceType == typeof(ITransportSubscriber));
	}

	[Fact]
	public void RegisterMultipleSubscribersForDifferentNames()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		services.AddRabbitMQTransport("sub-a", rmq => rmq.ConnectionString("amqp://host-a"));
		services.AddRabbitMQTransport("sub-b", rmq => rmq.ConnectionString("amqp://host-b"));

		// Assert -- multiple subscriber registrations
		var subscriberCount = services.Count(sd => sd.ServiceType == typeof(ITransportSubscriber));
		subscriberCount.ShouldBeGreaterThanOrEqualTo(2);
	}

	[Fact]
	public void RegisterKeyedServicesForMultiTransportResolution()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		services.AddRabbitMQTransport("keyed-a", rmq => rmq.ConnectionString("amqp://host-a"));
		services.AddRabbitMQTransport("keyed-b", rmq => rmq.ConnectionString("amqp://host-b"));

		// Assert -- keyed services registered with transport name keys
		var keyedA = services.Where(sd => sd.IsKeyedService && "keyed-a".Equals(sd.ServiceKey)).ToList();
		keyedA.Count.ShouldBeGreaterThan(0);
		var keyedB = services.Where(sd => sd.IsKeyedService && "keyed-b".Equals(sd.ServiceKey)).ToList();
		keyedB.Count.ShouldBeGreaterThan(0);
	}
}
