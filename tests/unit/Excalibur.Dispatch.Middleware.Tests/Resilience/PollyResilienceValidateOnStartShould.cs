// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Threading.Tasks;

using Excalibur.Dispatch.Resilience.Polly;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Author≠impl fail-fast lock for Sprint 848 Lane O2 (<c>a2vhwf</c>): <c>AddPollyResilience</c> must wire
/// <c>AddOptions&lt;T&gt;().ValidateOnStart()</c> for <see cref="TimeoutManagerOptions"/>,
/// <see cref="GracefulDegradationOptions"/>, and <see cref="DistributedCircuitBreakerOptions"/>
/// <b>unconditionally</b> — not only on the <c>IConfiguration</c> path. Only <c>.Bind(configuration)</c>
/// may be gated behind <c>configuration != null</c>.
/// </summary>
/// <remarks>
/// RED on the pre-fix parent: <c>ValidateOnStart()</c> sat inside the <c>if (configuration != null)</c>
/// block, so the convenience overload (<c>AddPollyResilience()</c>, <c>configuration == null</c>) left the
/// validators dead — an invalid options value was silently accepted and <c>host.StartAsync()</c> did NOT
/// throw. GREEN once <c>ValidateOnStart()</c> is called unconditionally for all three option types.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class PollyResilienceValidateOnStartShould
{
	private static IHost BuildHost(Action<IServiceCollection> configureServices)
		=> new HostBuilder()
			.ConfigureServices((_, services) =>
			{
				services.AddLogging();
				configureServices(services);
			})
			.Build();

	[Fact]
	public async Task ThrowAtStartup_OnConvenienceOverload_WhenTimeoutOptionsInvalid()
	{
		// The convenience overload passes configuration == null: this is the exact path whose
		// ValidateOnStart wiring the pre-fix code gated away, leaving validation dead.
		using var host = BuildHost(services =>
		{
			services.AddPollyResilience();
			services.PostConfigure<TimeoutManagerOptions>(o => o.SlowOperationThreshold = 2.0); // [Range(0.0, 1.0)] -> invalid
		});

		await Should.ThrowAsync<OptionsValidationException>(() => host.StartAsync());
	}

	[Fact]
	public async Task StartCleanly_OnConvenienceOverload_WhenOptionsAreValid()
	{
		using var host = BuildHost(services => services.AddPollyResilience());

		// Valid defaults must still start (fail-fast is unconditional, but must not be over-strict).
		await host.StartAsync();
		await host.StopAsync();
	}
}
