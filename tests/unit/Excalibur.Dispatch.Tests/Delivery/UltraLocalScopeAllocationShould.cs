// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.Delivery;

/// <summary>
/// The ultra-local dispatch path must not pay for a dependency-injection scope when the handler provably
/// does not need one.
/// </summary>
/// <remarks>
/// <para>
/// Scope-correct handler resolution exists to fix a real defect: a singleton message bus resolving a scoped
/// handler from the captured root container throws "Cannot resolve scoped service from root provider". The
/// fix creates a scope. <see cref="HandlerScopeResolver"/> is supposed to keep that cost off handlers that
/// cannot capture anything scoped — a handler registered Singleton, or one whose constructor dependency
/// graph never reaches a Scoped service. That is the pay-for-what-you-use half.
/// </para>
/// <para>
/// These arms bind both halves against a handler with NO constructor dependencies at all, which is the
/// clearest possible case for "no scope required": there is nothing to capture. The allocation arm is the
/// load-bearing one — allocation is deterministic, unlike timing, and was byte-identical across a loaded
/// and an idle host on every row of the comparative suite.
/// </para>
/// </remarks>
public sealed class UltraLocalScopeAllocationShould
{
	private sealed record ScopeProbeCommand : IDispatchAction
	{
		public int Value { get; init; }
	}

	/// <summary>A handler with no constructor dependencies: nothing scoped is reachable from it.</summary>
	private sealed class ScopeProbeCommandHandler : IActionHandler<ScopeProbeCommand>
	{
		public Task HandleAsync(ScopeProbeCommand message, CancellationToken cancellationToken) =>
			Task.CompletedTask;
	}

	private static ServiceProvider Build()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Mirrors the published ultra-local benchmark container: the Direct profile with every optional
		// feature off. If this configuration needs a scope, no configuration avoids one.
		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.ConfigurePipeline(
				"DirectLocal",
				pipeline => pipeline.UseProfile(DefaultPipelineProfiles.Direct));
			_ = dispatch.WithOptions(options =>
			{
				options.UseLightMode = true;
				options.EnablePipelineSynthesis = false;
				options.Features.EnableMetrics = false;
				options.Features.EnableAuthorization = false;
				options.Features.ValidateMessageSchemas = false;
				options.Features.EnableVersioning = false;
				options.Features.EnableMultiTenancy = false;
				options.Features.EnableTransactions = false;
			});
		});

		_ = services.AddTransient<ScopeProbeCommandHandler>();
		_ = services.AddTransient<IActionHandler<ScopeProbeCommand>, ScopeProbeCommandHandler>();

		return services.BuildServiceProvider();
	}

	/// <summary>A handler whose constructor reaches a scoped service, so resolving it REQUIRES a scope.</summary>
	private sealed class ScopedDependencyCommandHandler(IScopedDependency dependency)
		: IActionHandler<ScopeProbeCommand>
	{
		public Task HandleAsync(ScopeProbeCommand message, CancellationToken cancellationToken)
		{
			_ = dependency.Value;
			return Task.CompletedTask;
		}
	}

	private interface IScopedDependency
	{
		int Value { get; }
	}

	private sealed class ScopedDependency : IScopedDependency
	{
		public int Value => 1;
	}

	/// <summary>
	/// The same container except the handler takes a scoped dependency, so the resolver must create a
	/// scope per dispatch. This is the configuration the counting arm below must go RED against.
	/// </summary>


	[Fact]
	public void ReportNoScopeRequiredForADependencyFreeTransientHandler()
	{
		// The premise the fast path rests on. A transient handler with a parameterless constructor has an
		// empty dependency graph, so the walk has nothing to find and the verdict must be root-safe. If this
		// is true and the dispatch still allocates a scope, the resolver's verdict is not being consulted.
		using var provider = Build();
		var resolver = new HandlerScopeResolver(provider);

		resolver.CanCreateScope.ShouldBeTrue(
			"Microsoft DI registers IServiceScopeFactory by default, so the resolver must be able to create "
			+ "a scope -- otherwise RequiresScope short-circuits to false and this arm proves nothing");

		resolver.RequiresScope(typeof(ScopeProbeCommandHandler)).ShouldBeFalse(
			"the handler is registered Transient and takes no constructor arguments, so no scoped service is "
			+ "reachable from it and resolving it from the root container cannot capture anything");
	}

	[Fact]
	public async Task NotAllocateAScopePerDispatchWhenTheHandlerNeedsNone()
	{
		// The property observed directly. A byte total cannot express it: the dispatch path legitimately
		// allocates for reasons that have nothing to do with scoping (an ambient ExecutionContext copy, a
		// result Task), so a ceiling on the total fails for costs this lock was never about while a scope
		// could hide under a generous one. ScopePathProbe counts entries into the scope-taking branch.
		var probe = new ScopePathProbe();
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch();
		_ = services.AddSingleton<IDispatchAmbientScopeAccessor>(probe);
		_ = services.AddTransient<ScopeProbeCommandHandler>();
		_ = services.AddTransient<IActionHandler<ScopeProbeCommand>, ScopeProbeCommandHandler>();
		using (var provider = services.BuildServiceProvider())
		{
			var dispatcher = provider.GetRequiredService<IDispatcher>();
			for (var i = 0; i < 100; i++)
			{
				_ = await dispatcher.DispatchAsync(new ScopeProbeCommand { Value = i }, CancellationToken.None);
			}

			probe.ScopePathEntries.ShouldBe(
				0,
				$"100 dispatches of a dependency-free handler entered the scope-taking branch "
				+ $"{probe.ScopePathEntries} times. Scope-correct resolution must stay off handlers whose "
				+ "dependency graph cannot reach a scoped service, or every consumer pays for a scope none "
				+ "of their handlers required");
		}

		// LIVENESS. Without this the arm above is passed by a resolver that never scopes for anyone, and
		// by a probe wired to nothing. The same probe, the same assertion, must be able to move.
		var scopedProbe = new ScopePathProbe();
		var scopedServices = new ServiceCollection();
		_ = scopedServices.AddLogging();
		_ = scopedServices.AddDispatch();
		_ = scopedServices.AddSingleton<IDispatchAmbientScopeAccessor>(scopedProbe);
		_ = scopedServices.AddScoped<IScopedDependency, ScopedDependency>();
		_ = scopedServices.AddTransient<IActionHandler<ScopeProbeCommand>, ScopedDependencyCommandHandler>();
		using (var scopedProvider = scopedServices.BuildServiceProvider())
		{
			var dispatcher = scopedProvider.GetRequiredService<IDispatcher>();
			for (var i = 0; i < 10; i++)
			{
				_ = await dispatcher.DispatchAsync(new ScopeProbeCommand { Value = i }, CancellationToken.None);
			}

			scopedProbe.ScopePathEntries.ShouldBeGreaterThan(
				0,
				"a handler whose constructor reaches a scoped service MUST drive the dispatch into the "
				+ "scope-taking branch; if this is zero the probe observes nothing and the arm above "
				+ "proves nothing");
		}
	}
}
