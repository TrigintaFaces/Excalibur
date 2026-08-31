// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Azure.Messaging.ServiceBus;

using Excalibur.Dispatch.Transport.Azure;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus;

/// <summary>
/// Locks for the Azure Service Bus options collapse. The transport used to carry a builder-facing model
/// that was translated into a separate runtime model field by field; the difference between what the
/// public model promised and what the runtime could consume went missing in that translation. There is
/// now one model, and the builder writes into the instance the options system serves.
///
/// These assert the property that matters — a value set through the public fluent API is observable at
/// the seam that consumes it — rather than that any particular copy still happens.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Transport")]
public sealed class AzureServiceBusOptionsCollapseShould
{
	private static AzureServiceBusOptions Resolve(IServiceProvider provider, string name)
		=> provider.GetRequiredService<IOptionsMonitor<AzureServiceBusOptions>>().Get(name);

	[Fact]
	public void CarryEveryFluentlyConfiguredValueToTheResolvedNamedOptions()
	{
		var services = new ServiceCollection();

		_ = services.AddAzureServiceBusTransport("orders", sb => sb
			.ConnectionString("Endpoint=sb://example.servicebus.windows.net/;SharedAccessKeyName=k;SharedAccessKey=v")
			.TransportType(ServiceBusTransportType.AmqpWebSockets)
			.ConfigureSender(sender => sender.DefaultEntityName = "orders-topic")
			.ConfigureProcessor(processor =>
			{
				processor.MaxConcurrentCalls = 20;
				processor.PrefetchCount = 7;
				processor.RequiresSession = true;
			})
			.MapEntity<string>("string-topic"));

		using var provider = services.BuildServiceProvider();
		var options = Resolve(provider, "orders");

		options.Name.ShouldBe("orders");
		options.TransportType.ShouldBe(ServiceBusTransportType.AmqpWebSockets);
		options.Sender.DefaultEntityName.ShouldBe("orders-topic");
		options.Processor.MaxConcurrentCalls.ShouldBe(20);
		options.Processor.PrefetchCount.ShouldBe(7);
		options.Processor.RequiresSession.ShouldBeTrue();

		// MapEntity is the one builder call that is not a plain assignment — it appends to a routing
		// table, which is why it survived the collapse while the pure setters did not.
		options.EntityMappings.ShouldContainKeyAndValue(typeof(string), "string-topic");
	}

	[Fact]
	public void KeepTwoNamedTransportsIndependent()
	{
		var services = new ServiceCollection();

		_ = services.AddAzureServiceBusTransport("a", sb => sb
			.ConnectionString("Endpoint=sb://a.servicebus.windows.net/;SharedAccessKeyName=k;SharedAccessKey=v")
			.ConfigureSender(s => s.DefaultEntityName = "queue-a")
			.ConfigureProcessor(p => p.MaxConcurrentCalls = 1));

		_ = services.AddAzureServiceBusTransport("b", sb => sb
			.ConnectionString("Endpoint=sb://b.servicebus.windows.net/;SharedAccessKeyName=k;SharedAccessKey=v")
			.ConfigureSender(s => s.DefaultEntityName = "queue-b")
			.ConfigureProcessor(p => p.MaxConcurrentCalls = 2));

		using var provider = services.BuildServiceProvider();

		// The options were registered UNNAMED before the collapse, so the second registration silently
		// overwrote the first and both transports ran on one configuration. The XML example on
		// AddAzureServiceBusTransport demonstrates exactly this two-transport scenario.
		Resolve(provider, "a").Sender.DefaultEntityName.ShouldBe("queue-a");
		Resolve(provider, "a").Processor.MaxConcurrentCalls.ShouldBe(1);
		Resolve(provider, "b").Sender.DefaultEntityName.ShouldBe("queue-b");
		Resolve(provider, "b").Processor.MaxConcurrentCalls.ShouldBe(2);
	}

	[Fact]
	public void ReachProcessorSettingsThatOnlyTheSdkProjectionConsumes()
	{
		var services = new ServiceCollection();

		_ = services.AddAzureServiceBusTransport("tuned", sb => sb
			.ConnectionString("Endpoint=sb://example.servicebus.windows.net/;SharedAccessKeyName=k;SharedAccessKey=v")
			.ConfigureSender(s => s.DefaultEntityName = "tuned-queue")
			.ConfigureProcessor(p =>
			{
				p.AutoCompleteMessages = false;
				p.ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete;
				p.MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(3);
			}));

		using var provider = services.BuildServiceProvider();
		var options = Resolve(provider, "tuned");

		// The projection onto the SDK's own processor options is the one translation that survives,
		// because it crosses a boundary we do not own. Assert it is fed the configured values.
		var sdk = AzureServiceBusTransportServiceCollectionExtensions.BuildProcessorOptions(options.Processor);

		sdk.AutoCompleteMessages.ShouldBeFalse();
		sdk.ReceiveMode.ShouldBe(ServiceBusReceiveMode.ReceiveAndDelete);
		sdk.MaxAutoLockRenewalDuration.ShouldBe(TimeSpan.FromMinutes(3));
	}

	[Fact]
	public void RejectAConfigurationWithNoTargetEntity()
	{
		var services = new ServiceCollection();

		_ = services.AddAzureServiceBusTransport("nameless", sb => sb
			.ConnectionString("Endpoint=sb://example.servicebus.windows.net/;SharedAccessKeyName=k;SharedAccessKey=v"));

		using var provider = services.BuildServiceProvider();

		// Liveness's counterpart: validation must still REJECT the incomplete configuration. A collapse
		// that quietly stopped validating would pass every "the value arrives" assertion above.
		_ = Should.Throw<OptionsValidationException>(() => Resolve(provider, "nameless"));
	}
}
