// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Configuration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Configuration;

/// <summary>
/// Empirical regression lock for gziqqa: the legacy <c>AddDispatch(Assembly[])</c> params route
/// deliberately never calls <c>DispatchBuilder.Build()</c> (so later <c>AddDispatchMiddleware&lt;T&gt;()</c>
/// calls stay visible to the legacy <c>GetServices&lt;IDispatchMiddleware&gt;()</c> discovery path), and
/// <c>DispatchOptionsValidator</c>/the other start-up validators used to be registered ONLY inline inside
/// <c>AddDispatch(configure)</c> -- so a composition built through the params route validated nothing at
/// start-up.
/// </summary>
/// <remarks>
/// Deliberately empirical, not a registration assertion: checking <c>GetServices&lt;IValidateOptions&lt;
/// DispatchOptions&gt;&gt;()</c> for a non-empty result proves the validator is IN THE CONTAINER, not that
/// resolving <see cref="IOptions{TOptions}"/> actually runs it. This injects an invalid
/// <see cref="DispatchOptions.MaxConcurrency"/> and asserts the OBSERVABLE effect: resolving
/// <c>IOptions&lt;DispatchOptions&gt;.Value</c> throws (safety), while a valid configuration resolves
/// cleanly (liveness) -- so a regression that silently drops the registration again fails this lock even
/// though the composition otherwise looks fine.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Configuration")]
public sealed class AddDispatchAssembliesRegistrationParityShould
{
	[Fact]
	public void ThrowOnResolve_WhenDispatchOptionsAreInvalid_AndComposedThroughAddDispatchAssemblies()
	{
		// Arrange: the legacy params route, with no assemblies to scan (so no handler ambiguity confuses
		// the failure), plus a configuration registered AFTER AddDispatch(...) so it wins the composed
		// Options pipeline -- proving the fault is injected independently of any internal default.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(Array.Empty<System.Reflection.Assembly>());
		_ = services.Configure<DispatchOptions>(static o => o.MaxConcurrency = 0);

		var provider = services.BuildServiceProvider();

		// Assert (safety): if DispatchOptionsValidator was never registered by this route,
		// IOptions<DispatchOptions>.Value resolves the invalid configuration silently instead of throwing --
		// which is exactly the pre-fix registration-parity gap.
		var ex = Should.Throw<OptionsValidationException>(
			() => provider.GetRequiredService<IOptions<DispatchOptions>>().Value);
		ex.Message.ShouldContain(nameof(DispatchOptions.MaxConcurrency));
	}

	[Fact]
	public void ResolveCleanly_WhenDispatchOptionsAreValid_AndComposedThroughAddDispatchAssemblies()
	{
		// Liveness arm: the same wiring must not turn into a blanket rejection -- a valid configuration
		// still resolves.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(Array.Empty<System.Reflection.Assembly>());
		_ = services.Configure<DispatchOptions>(static o => o.MaxConcurrency = 4);

		var provider = services.BuildServiceProvider();

		var options = provider.GetRequiredService<IOptions<DispatchOptions>>().Value;

		_ = options.ShouldNotBeNull();
		options.MaxConcurrency.ShouldBe(4);
	}

	[Fact]
	public void PreserveFeaturesAndCrossCuttingConfiguredBeforeAddDispatch_9bhptg()
	{
		// Regression lock for 9bhptg: DispatchBuilder.RegisterOptions() used to assign
		// `opt.Features = _options.Features` / `opt.CrossCutting = _options.CrossCutting` wholesale --
		// swapping the reference discarded ANY property a consumer's own Configure<DispatchOptions>()
		// had already set on those objects, silently, the moment AddDispatch ran RegisterOptions(). This is
		// the exact pattern docs-site/docs/performance/auto-freeze.md and
		// docs/performance/performance-optimization-guide.md teach consumers to use.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.Configure<DispatchOptions>(static o =>
		{
			o.Features.EnableCorrelation = false;
			o.CrossCutting.Performance.AutoFreezeOnStart = false;
		});
		_ = services.AddDispatch(Array.Empty<System.Reflection.Assembly>());

		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<DispatchOptions>>().Value;

		options.Features.EnableCorrelation.ShouldBeFalse();
		options.CrossCutting.Performance.AutoFreezeOnStart.ShouldBeFalse();
	}

	[Fact]
	public void PreserveAutoPromoteStatelessHandlersToSingletonConfiguredBeforeAddDispatch_9bhptg()
	{
		// Same defect, the specific property that regressed the "Notification to 3 handlers" benchmark:
		// AddDispatch(params Assembly[]) itself defaults this flag to true via a Configure<DispatchOptions>
		// call registered BEFORE RegisterOptions() runs -- so the wholesale swap clobbered the framework's
		// own default, not merely a consumer's.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(Array.Empty<System.Reflection.Assembly>());

		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<DispatchOptions>>().Value;

		options.CrossCutting.Performance.AutoPromoteStatelessHandlersToSingleton.ShouldBeTrue();
	}
}
