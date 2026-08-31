// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.EventBridge;

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.EventBridge;

/// <summary>
/// The EventBridge client and message bus were registered with TryAddSingleton BY TYPE, which
/// de-duplicates: a second named EventBridge transport contributed no registration and both names
/// resolved the first transport's client/bus, silently sending every named transport to the first
/// transport's event bus.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class AwsEventBridgeNamedClientsShould
{
	[Fact]
	public async Task ResolveASeparateClientPerTransportName()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddAwsEventBridgeTransport("orders", eb => eb
			.EventBusName("orders-bus")
			.Region("us-east-1"));

		_ = services.AddAwsEventBridgeTransport("analytics", eb => eb
			.EventBusName("analytics-bus")
			.Region("us-west-2"));

		await using var provider = services.BuildServiceProvider();

		var orders = (AmazonEventBridgeClient)provider.GetRequiredKeyedService<IAmazonEventBridge>("orders");
		var analytics = (AmazonEventBridgeClient)provider.GetRequiredKeyedService<IAmazonEventBridge>("analytics");

		// Pre-fix, TryAddSingleton-by-type meant this second lookup returned the SAME instance as the
		// first, silently pointed at "us-east-1".
		orders.ShouldNotBeSameAs(analytics);
		orders.Config.RegionEndpoint.SystemName.ShouldBe("us-east-1");
		analytics.Config.RegionEndpoint.SystemName.ShouldBe("us-west-2");
	}

	[Fact]
	public async Task ResolveASeparateMessageBusPerTransportName()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddPluggableSerialization();

		_ = services.AddAwsEventBridgeTransport("orders", eb => eb.EventBusName("orders-bus").Region("us-east-1"));
		_ = services.AddAwsEventBridgeTransport("analytics", eb => eb.EventBusName("analytics-bus").Region("us-west-2"));

		await using var provider = services.BuildServiceProvider();

		var orders = provider.GetRequiredKeyedService<AwsEventBridgeMessageBus>("orders");
		var analytics = provider.GetRequiredKeyedService<AwsEventBridgeMessageBus>("analytics");

		orders.ShouldNotBeSameAs(analytics);
	}
}
