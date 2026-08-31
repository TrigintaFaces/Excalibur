// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Validation;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.Delivery;

/// <summary>
/// The context-less dispatch overloads must not skip configured middleware.
/// </summary>
/// <remarks>
/// Those overloads take an ultra-local path that resolves the handler and invokes it directly. The path
/// is a sound optimisation when nothing is configured and a silent hole when something is: no validation,
/// no authorization, no tenant identity, and failures returned as a result rather than thrown, so nothing
/// reports that a stage did not run. The guard asks whether skipping is safe; these arms prove it asks,
/// and that it still permits the optimisation when it genuinely is.
/// </remarks>
public sealed class ContextLessDispatchRunsMiddlewareShould
{
	private sealed record ProbeAction : IDispatchAction;

	private static ServiceProvider Build(bool withMiddleware)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		// Both arms go through AddDispatch, so the only difference between them is whether a stage is
		// configured. AddDispatchPipeline alone does not register IHandlerRegistry, so it cannot resolve
		// an IDispatcher at all -- a "bare" container built that way fails before reaching any assertion.
		_ = services.AddDispatch(dispatch =>
		{
			if (withMiddleware)
			{
				// The real registration path, not a stand-in: UseValidation registers the validation
				// services and puts ValidationMiddleware in the pipeline, which is exactly what a consumer
				// configures and expects to take effect.
				_ = dispatch.UseValidation();
			}
		});

		return services.BuildServiceProvider();
	}

	[Fact]
	public void NotAllowBypassWhenMiddlewareIsConfigured()
	{
		// SAFETY. Configuring a stage must make it run. Before the guard, this path was chosen from the
		// caller's shape alone -- an ambient context and a capable dispatcher -- and never asked whether
		// anything was configured, so a registered validator was simply never invoked.
		using var provider = Build(withMiddleware: true);
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var direct = dispatcher.ShouldBeAssignableTo<IDirectLocalDispatcher>();

		direct!.CanBypassMiddlewareFor(typeof(ProbeAction)).ShouldBeFalse(
			"middleware is configured for this message, so the ultra-local path must not be taken; if this "
			+ "is true the context-less overloads invoke the handler with no pipeline and nothing reports it");
	}

	[Fact]
	public void StillAllowBypassWhenNothingIsConfigured()
	{
		// LIVENESS. The guard must not cost the optimisation it protects. Without this arm, a change that
		// simply always returned false would pass the safety arm above while quietly removing a fast path
		// the framework's throughput claims depend on.
		using var provider = Build(withMiddleware: false);
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var direct = dispatcher.ShouldBeAssignableTo<IDirectLocalDispatcher>();

		var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
		var invoker = dispatcher.GetType().GetField("_concreteMiddlewareInvoker", flags)?.GetValue(dispatcher);
		var count = invoker?.GetType().GetField("_middlewareCount", flags)?.GetValue(invoker);
		var nonRouting = invoker?.GetType().GetField("_hasAnyNonRoutingMiddleware", flags)?.GetValue(invoker);

		direct!.CanBypassMiddlewareFor(typeof(ProbeAction)).ShouldBeTrue(
			$"no middleware is configured, so skipping the pipeline changes nothing observable and the "
			+ $"ultra-local path should still be available. Bare container holds _middlewareCount={count}, "
			+ $"_hasAnyNonRoutingMiddleware={nonRouting}");
	}
}
