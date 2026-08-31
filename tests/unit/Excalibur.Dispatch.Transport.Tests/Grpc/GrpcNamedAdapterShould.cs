// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Grpc;

using Grpc.Net.Client;

namespace Excalibur.Dispatch.Transport.Tests.Grpc;

/// <summary>
/// <see cref="GrpcTransportAdapter"/> was registered with TryAddSingleton BY TYPE while its channel and
/// sender were already correctly keyed by transport name -- so a second named gRPC transport contributed
/// no adapter registration and both names resolved the first transport's adapter, silently sending every
/// named transport's traffic through the first transport's channel.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Transport")]
public sealed class GrpcNamedAdapterShould
{
	[Fact]
	public async Task ResolveASeparateAdapterPerTransportName()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddGrpcTransport("orders", o => o.ServerAddress = "https://orders.local:5001");
		_ = services.AddGrpcTransport("audit", o => o.ServerAddress = "https://audit.local:5001");

		await using var provider = services.BuildServiceProvider();

		var orders = provider.GetRequiredKeyedService<GrpcTransportAdapter>("orders");
		var audit = provider.GetRequiredKeyedService<GrpcTransportAdapter>("audit");

		// Pre-fix, TryAddSingleton-by-type meant this second lookup returned the SAME instance as the
		// first, silently pointed at "orders.local".
		orders.ShouldNotBeSameAs(audit);

		var ordersChannel = provider.GetRequiredKeyedService<GrpcChannel>("orders");
		var auditChannel = provider.GetRequiredKeyedService<GrpcChannel>("audit");
		ordersChannel.Target.ShouldBe("orders.local:5001");
		auditChannel.Target.ShouldBe("audit.local:5001");
	}

	[Fact]
	public async Task ExposeTheKeyedAdapterAsITransportAdapterAndITransportHealthCheckerUnderTheSameKey()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddGrpcTransport("orders", o => o.ServerAddress = "https://orders.local:5001");
		_ = services.AddGrpcTransport("audit", o => o.ServerAddress = "https://audit.local:5001");

		await using var provider = services.BuildServiceProvider();

		var ordersAdapter = provider.GetRequiredKeyedService<GrpcTransportAdapter>("orders");
		var ordersAsTransportAdapter = provider.GetRequiredKeyedService<ITransportAdapter>("orders");
		var ordersAsHealthChecker = provider.GetRequiredKeyedService<ITransportHealthChecker>("orders");

		ordersAsTransportAdapter.ShouldBeSameAs(ordersAdapter);
		ordersAsHealthChecker.ShouldBeSameAs(ordersAdapter);
	}
}
