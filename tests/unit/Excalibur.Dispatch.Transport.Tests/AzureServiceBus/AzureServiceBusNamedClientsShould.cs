// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Azure.Messaging.ServiceBus;

using Excalibur.Dispatch.Transport.Azure;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus;

/// <summary>
/// The Service Bus client and message bus were registered with TryAddSingleton BY TYPE, which
/// de-duplicates: a second named Service Bus transport contributed no registration and both names
/// resolved the first transport's client/bus, silently sending every named transport to the first
/// transport's namespace.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Transport")]
public sealed class AzureServiceBusNamedClientsShould
{
	[Fact]
	public async Task ResolveASeparateClientPerTransportName()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddAzureServiceBusTransport("orders", sb => sb
			.ConnectionString("Endpoint=sb://orders.servicebus.windows.net/;SharedAccessKeyName=k;SharedAccessKey=v"));

		_ = services.AddAzureServiceBusTransport("audit", sb => sb
			.ConnectionString("Endpoint=sb://audit.servicebus.windows.net/;SharedAccessKeyName=k;SharedAccessKey=v"));

		await using var provider = services.BuildServiceProvider();

		var orders = provider.GetRequiredKeyedService<ServiceBusClient>("orders");
		var audit = provider.GetRequiredKeyedService<ServiceBusClient>("audit");

		// Pre-fix, TryAddSingleton-by-type meant this second lookup returned the SAME instance as the
		// first, silently pointed at "orders.servicebus.windows.net".
		orders.ShouldNotBeSameAs(audit);
		orders.FullyQualifiedNamespace.ShouldBe("orders.servicebus.windows.net");
		audit.FullyQualifiedNamespace.ShouldBe("audit.servicebus.windows.net");

		await orders.DisposeAsync();
		await audit.DisposeAsync();
	}

	[Fact]
	public async Task ResolveASeparateMessageBusPerTransportName()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddPluggableSerialization();

		_ = services.AddAzureServiceBusTransport("orders", sb => sb
			.ConnectionString("Endpoint=sb://orders.servicebus.windows.net/;SharedAccessKeyName=k;SharedAccessKey=v")
			.ConfigureSender(sender => sender.DefaultEntityName = "orders-queue"));

		_ = services.AddAzureServiceBusTransport("audit", sb => sb
			.ConnectionString("Endpoint=sb://audit.servicebus.windows.net/;SharedAccessKeyName=k;SharedAccessKey=v")
			.ConfigureSender(sender => sender.DefaultEntityName = "audit-queue"));

		await using var provider = services.BuildServiceProvider();

		var orders = provider.GetRequiredKeyedService<AzureServiceBusMessageBus>("orders");
		var audit = provider.GetRequiredKeyedService<AzureServiceBusMessageBus>("audit");

		orders.ShouldNotBeSameAs(audit);
	}
}
