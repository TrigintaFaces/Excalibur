// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.RabbitMQ;

using Microsoft.Extensions.Options;

using Tests.Shared.Categories;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ;

/// <summary>
/// Asserts the observable effect of the exchange and queue name prefixes: the names the framework
/// addresses at runtime carry the prefix, so the declared topology and the addressed topology cannot
/// drift apart.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
[Trait("Pattern", "TRANSPORT")]
public sealed class RabbitMqTopologyPrefixShould : UnitTestBase
{
	[Fact]
	public void ApplyThePrefixToTheNamesTheRuntimeAddresses()
	{
		var services = new ServiceCollection();

		_ = services.AddRabbitMQTransport("prefixed", rmq => rmq
			.ConnectionString("amqp://app:s3cret@localhost:5672/")
			.WithExchangePrefix("myapp-")
			.WithQueuePrefix("myapp-")
			.ConfigureExchange(e => e.Name("events"))
			.ConfigureQueue(q => q.Name("orders"))
			.ConfigureBinding(b => b.Exchange("events").Queue("orders").RoutingKey("#")));

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptionsMonitor<RabbitMqOptions>>().Get("prefixed");

		// The runtime options are mapped from the FIRST configured exchange and queue, so a prefix that
		// is applied to the declared topology but not here would address an entity that was never declared.
		options.Exchange.ShouldBe("myapp-events");
		options.Queue.QueueName.ShouldBe("myapp-orders");
	}

	[Fact]
	public void LeaveNamesAloneWhenNoPrefixIsConfigured()
	{
		var services = new ServiceCollection();

		_ = services.AddRabbitMQTransport("plain", rmq => rmq
			.ConnectionString("amqp://app:s3cret@localhost:5672/")
			.ConfigureExchange(e => e.Name("events"))
			.ConfigureQueue(q => q.Name("orders")));

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptionsMonitor<RabbitMqOptions>>().Get("plain");

		options.Exchange.ShouldBe("events");
		options.Queue.QueueName.ShouldBe("orders");
	}
}
