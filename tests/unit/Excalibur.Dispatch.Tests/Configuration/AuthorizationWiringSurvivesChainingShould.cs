// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Middleware.Auth;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Dispatch.Tests.Configuration;

/// <summary>
/// Wiring authorization while its feature is disabled must fail loudly however the pipeline was composed.
/// </summary>
/// <remarks>
/// The composition explicitly asked for authorization and the flag that activates the stage is off, so the
/// synthesizer drops the stage and authorization-required messages reach their handlers unauthorized. The
/// original guard ran inside the registration call, which could only see middleware configured before it
/// returned. Once AddDispatch hands the builder back, configuring afterwards is the normal shape, and a
/// registration-time guard cannot see it -- so the check has to read the finished composition at start-up.
/// </remarks>
public sealed class AuthorizationWiringSurvivesChainingShould
{
	private static async Task<Exception?> StartHostAsync(ServiceProvider provider)
	{
		try
		{
			foreach (var hosted in provider.GetServices<IHostedService>())
			{
				await hosted.StartAsync(CancellationToken.None);
			}

			return null;
		}
		catch (InvalidOperationException ex)
		{
			return ex;
		}
	}

	[Fact]
	public async Task FailStartupWhenAuthorizationIsChainedOnWithTheFeatureDisabled()
	{
		// SAFETY. This is the arm that was RED: the middleware is added after the registration-time guard
		// has already run, so nothing caught it and the misconfiguration reached a running host.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(static d => d.WithOptions(static o => o.Features.EnableAuthorization = false))
			.UseAuthorization();

		using var provider = services.BuildServiceProvider();

		var failure = await StartHostAsync(provider);

		failure.ShouldNotBeNull(
			"authorization was wired while its feature is disabled, so the stage would be dropped and "
			+ "authorization-required messages would reach handlers unauthorized");
		failure.Message.ShouldContain("EnableAuthorization");
	}

	[Fact]
	public async Task StartNormallyWhenAuthorizationIsChainedOnWithTheFeatureEnabled()
	{
		// LIVENESS. Without this arm a guard that simply always threw would satisfy the arm above while
		// making every authorized composition unstartable.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(static d => d.WithOptions(static o => o.Features.EnableAuthorization = true))
			.UseAuthorization();

		using var provider = services.BuildServiceProvider();

		(await StartHostAsync(provider)).ShouldBeNull(
			"a correctly wired authorization pipeline must start");
	}

	[Fact]
	public async Task StartNormallyWhenAuthorizationWasNeverWired()
	{
		// LIVENESS. The guard is opt-in by construction: a composition that never asked for authorization
		// must not be required to enable the feature.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(static d => d.WithOptions(static o => o.Features.EnableAuthorization = false));

		using var provider = services.BuildServiceProvider();

		(await StartHostAsync(provider)).ShouldBeNull(
			"no authorization middleware was registered, so disabling the feature is a deliberate opt-out");
	}
}
