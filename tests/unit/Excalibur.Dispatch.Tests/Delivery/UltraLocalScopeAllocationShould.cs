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
		// The property consumers actually pay for. This is the arm that fails today.
		using var provider = Build();
		var dispatcher = provider.GetRequiredService<IDispatcher>();
		var direct = dispatcher.ShouldBeAssignableTo<IDirectLocalDispatcher>();

		// Warm every one-time cost off the measurement: plan resolution, the scope verdict cache, JIT.
		for (var i = 0; i < 50; i++)
		{
			await direct!.DispatchLocalAsync(new ScopeProbeCommand { Value = i }, CancellationToken.None);
		}

		const int Iterations = 100;
		var before = GC.GetAllocatedBytesForCurrentThread();
		for (var i = 0; i < Iterations; i++)
		{
			await direct!.DispatchLocalAsync(new ScopeProbeCommand { Value = i }, CancellationToken.None);
		}

		var perDispatch = (GC.GetAllocatedBytesForCurrentThread() - before) / (double)Iterations;

		// The published figure for this path is 24 B. A created IServiceScope and its scoped provider cost
		// several hundred bytes, so the bound separates "no scope" from "a scope per dispatch" with room to
		// spare rather than pinning an exact number that ordinary churn would flap.
		perDispatch.ShouldBeLessThan(
			128,
			$"the ultra-local path allocated {perDispatch:F0} B per dispatch for a handler that provably "
			+ "needs no scope. Scope-correct resolution must stay off handlers whose dependency graph cannot "
			+ "reach a scoped service, or every consumer pays for a scope none of their handlers required");
	}
}
