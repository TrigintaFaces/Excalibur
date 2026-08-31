// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Azure.Messaging.EventHubs.Producer;

using Excalibur.Dispatch.Transport.Azure;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus;

/// <summary>
/// The Event Hubs producer client and message bus were registered with TryAddSingleton BY TYPE, which
/// de-duplicates: a second named Event Hubs transport contributed no registration and both names resolved
/// the first transport's producer/bus, silently sending every named transport to the first transport's
/// Event Hub. Named options (<see cref="AzureEventHubsNamedOptionsShould"/>) do not close this -- the
/// producer client and message bus read the options they were constructed with, not the options a
/// consumer might look up later.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Transport")]
public sealed class AzureEventHubsNamedClientsShould
{
	[Fact]
	public async Task ResolveASeparateProducerClientPerTransportName()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddAzureEventHubsTransport("orders", hub => hub
			.ConnectionString("Endpoint=sb://orders.servicebus.windows.net/;SharedAccessKeyName=k;SharedAccessKey=v")
			.EventHubName("orders-hub"));

		_ = services.AddAzureEventHubsTransport("audit", hub => hub
			.ConnectionString("Endpoint=sb://audit.servicebus.windows.net/;SharedAccessKeyName=k;SharedAccessKey=v")
			.EventHubName("audit-hub"));

		await using var provider = services.BuildServiceProvider();

		var orders = provider.GetRequiredKeyedService<EventHubProducerClient>("orders");
		var audit = provider.GetRequiredKeyedService<EventHubProducerClient>("audit");

		// Pre-fix, TryAddSingleton-by-type meant this second lookup returned the SAME instance as the
		// first, silently pointed at "orders-hub".
		orders.ShouldNotBeSameAs(audit);
		orders.EventHubName.ShouldBe("orders-hub");
		audit.EventHubName.ShouldBe("audit-hub");
	}

	[Fact]
	public async Task ResolveASeparateMessageBusPerTransportName()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddPluggableSerialization();

		_ = services.AddAzureEventHubsTransport("orders", hub => hub
			.ConnectionString("Endpoint=sb://orders.servicebus.windows.net/;SharedAccessKeyName=k;SharedAccessKey=v")
			.EventHubName("orders-hub"));

		_ = services.AddAzureEventHubsTransport("audit", hub => hub
			.ConnectionString("Endpoint=sb://audit.servicebus.windows.net/;SharedAccessKeyName=k;SharedAccessKey=v")
			.EventHubName("audit-hub"));

		await using var provider = services.BuildServiceProvider();

		var orders = provider.GetRequiredKeyedService<AzureEventHubMessageBus>("orders");
		var audit = provider.GetRequiredKeyedService<AzureEventHubMessageBus>("audit");

		orders.ShouldNotBeSameAs(audit);

		// The bus resolved for "orders" wraps the "orders" producer client, never the other name's.
		provider.GetRequiredKeyedService<EventHubProducerClient>("orders").EventHubName.ShouldBe("orders-hub");
		provider.GetRequiredKeyedService<EventHubProducerClient>("audit").EventHubName.ShouldBe("audit-hub");
	}
}
