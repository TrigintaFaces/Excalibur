// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Grpc;

using Grpc.Net.Client;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Tests.Grpc;

/// <summary>
/// The gRPC transport is registered under a name while its runtime options were registered without one,
/// so two named gRPC transports in one container wrote the same options instance and the second silently
/// replaced the first.
/// </summary>
/// <remarks>
/// gRPC is the sharper case of this family, because the options carry the SERVER ADDRESS and the channel
/// is built from them. A shared options instance therefore did not merely mis-report a setting: it sent
/// one transport's traffic to the other transport's server, with nothing thrown and nothing logged.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Transport")]
public sealed class GrpcNamedOptionsShould
{
	private static GrpcTransportOptions Resolve(IServiceProvider provider, string name)
		=> provider.GetRequiredService<IOptionsMonitor<GrpcTransportOptions>>().Get(name);

	private static void AddTwoNamedTransports(IServiceCollection services)
	{
		_ = services.AddGrpcTransport("orders", o =>
		{
			o.ServerAddress = "https://orders.internal:5001";
			o.Destination = "orders-destination";
			o.MaxRetryAttempts = 11;
		});

		_ = services.AddGrpcTransport("audit", o =>
		{
			o.ServerAddress = "https://audit.internal:5002";
			o.Destination = "audit-destination";
			o.MaxRetryAttempts = 22;
		});
	}

	[Fact]
	public void KeepTwoNamedTransportsIndependent()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		AddTwoNamedTransports(services);

		using var provider = services.BuildServiceProvider();

		// Pre-fix every one of these read the SECOND registration's values.
		Resolve(provider, "orders").ServerAddress.ShouldBe("https://orders.internal:5001");
		Resolve(provider, "orders").Destination.ShouldBe("orders-destination");
		Resolve(provider, "orders").MaxRetryAttempts.ShouldBe(11);

		Resolve(provider, "audit").ServerAddress.ShouldBe("https://audit.internal:5002");
		Resolve(provider, "audit").Destination.ShouldBe("audit-destination");
		Resolve(provider, "audit").MaxRetryAttempts.ShouldBe(22);
	}

	[Fact]
	public void GiveEachNamedTransportAChannelPointedAtItsOwnServer()
	{
		// The arm that binds the consequence rather than the setting. Naming the options while leaving one
		// shared GrpcChannel would satisfy the options assertions above and still send both transports to
		// one server, so this asserts the address the channel actually dials.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		AddTwoNamedTransports(services);

		using var provider = services.BuildServiceProvider();

		var orders = provider.GetRequiredKeyedService<GrpcChannel>("orders");
		var audit = provider.GetRequiredKeyedService<GrpcChannel>("audit");

		orders.Target.ShouldBe("orders.internal:5001");
		audit.Target.ShouldBe("audit.internal:5002");
		orders.ShouldNotBeSameAs(audit);
	}

	[Fact]
	public async Task GiveEachNamedTransportASenderBoundToItsOwnConfiguration()
	{
		// The sender is the component a consumer actually resolves, and it takes
		// IOptions<GrpcTransportOptions> in its constructor -- which resolves the UNNAMED instance.
		// Naming the options without re-binding the sender leaves it reading the last registration while
		// the named options read correctly, so this asserts a value the sender exposes FROM its injected
		// options rather than that two keyed registrations produced two objects, which was true before the
		// fix as well.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		AddTwoNamedTransports(services);

		await using var provider = services.BuildServiceProvider();

		provider.GetRequiredKeyedService<ITransportSender>("orders").Destination.ShouldBe("orders-destination");
		provider.GetRequiredKeyedService<ITransportSender>("audit").Destination.ShouldBe("audit-destination");
	}

	[Fact]
	public void StillConfigureTheUnnamedOptionsForASingleTransportHost()
	{
		// Liveness, and the arm a careless fix breaks. The health check and the single-transport host
		// resolve GrpcChannel and IOptions<GrpcTransportOptions> without a key, so moving the registration
		// to the named overload alone would leave them on an empty options object with an empty server
		// address -- a silent failure worse than the overwrite being fixed.
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddGrpcTransport(o =>
		{
			o.ServerAddress = "https://single.internal:5000";
			o.Destination = "single-destination";
		});

		using var provider = services.BuildServiceProvider();

		var unnamed = provider.GetRequiredService<IOptions<GrpcTransportOptions>>().Value;
		unnamed.ServerAddress.ShouldBe("https://single.internal:5000");
		unnamed.Destination.ShouldBe("single-destination");

		provider.GetRequiredService<GrpcChannel>().Target.ShouldBe("single.internal:5000");

		// And the default name resolves the same configuration, so a host that reaches the options either
		// way sees one answer.
		Resolve(provider, "default").ServerAddress.ShouldBe("https://single.internal:5000");
	}
}
