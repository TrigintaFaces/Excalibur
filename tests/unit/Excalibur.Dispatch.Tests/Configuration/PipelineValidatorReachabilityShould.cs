// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Configuration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Dispatch.Tests.Configuration;

/// <summary>
/// kne2xq — reachability and bootability locks for <see cref="AuthorizationWiringPrerequisiteValidator"/>,
/// the startup guard that fails CLOSED when the selected profile declares the authorization stage but no
/// authorization middleware is resolvable. A guard that is written, documented and unreachable protects
/// nobody.
/// </summary>
/// <remarks>
/// <para>
/// The guard was registered at one site only — inside the internal <c>AddDispatchWithInfrastructure</c> —
/// so the sole public routes were <c>AddStrictDispatchPipelines</c> and <c>AddDispatchWithDurability</c>.
/// A consumer on the ordinary <c>AddDispatch(configure)</c> path never received it.
/// </para>
/// <para>
/// <b>Arm B exists because arm A is not sufficient</b>, and that distinction cost a real mistake: asserting
/// the guard is REGISTERED says nothing about whether a default application can still START once it is.
/// The first attempt at this fix registered a bundle whose empty-pipeline check then threw for every
/// ordinary composition — registration green, bootability broken. Registration and bootability are
/// different predicates and both are load-bearing.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Feature", "Authorization")]
public sealed class PipelineValidatorReachabilityShould
{
	// ARM A — reachability. Both lifecycle contracts, because the IHostedService half alone leaves the
	// guarantee inert for host-less consumers (serverless, manual BuildServiceProvider).
	[Fact]
	public void BeRegistered_UnderBothLifecycleContracts_OnTheMainstreamAddDispatchPath()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddDispatch(static _ => { });

		Implementations<IHostedService>(services).ShouldContain(
			static t => t == typeof(AuthorizationWiringPrerequisiteValidator),
			"the ordinary AddDispatch path must receive the fail-closed authorization guard");

		Implementations<IStartupPrerequisiteValidator>(services).ShouldContain(
			static t => t == typeof(AuthorizationWiringPrerequisiteValidator),
			"IHostedService alone never fires for a consumer who builds a provider without a host, so the "
			+ "guarantee would be silently inert for exactly the serverless hosts least able to afford it");
	}

	// ARM B — bootability. NO injected middleware: a default application registers none, and that is a
	// supported shape (the builder falls back to the Direct profile). If wiring the guard makes an
	// ordinary app fail to start, this is what says so.
	[Fact]
	public async Task LetAnOrdinaryApplicationStart_WithNoMiddlewareRegistered()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(static _ => { });

		using var provider = services.BuildServiceProvider();

		foreach (var hosted in provider.GetServices<IHostedService>())
		{
			await hosted.StartAsync(CancellationToken.None);
		}
	}

	// ARM B' — the host-less equivalent of arm B. Same supported shape, no host.
	[Fact]
	public void LetAnOrdinaryApplicationValidate_WithoutAHost()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(static _ => { });

		using var provider = services.BuildServiceProvider();

		_ = Should.NotThrow(() => provider.ValidateStartupGates());
	}

	private static List<Type> Implementations<TService>(IServiceCollection services) =>
		[.. services
			.Where(d => d.ServiceType == typeof(TService))
			.Select(static d => d.ImplementationType
				?? d.ImplementationInstance?.GetType()
				?? d.ImplementationFactory?.Method.ReturnType)
			.Where(static t => t is not null)
			.Select(static t => t!)];
}
