// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.Configuration;

/// <summary>
/// A dispatch composition must stay continuable after the call that started it returns.
/// </summary>
/// <remarks>
/// The configuration a builder accumulates used to live in that builder's own fields, while the pipeline
/// registered in the container read the fields of the FIRST builder. So configuration applied afterwards --
/// by a chained call, or by any code that obtained a builder over the same service collection a second time
/// -- accumulated into objects nothing resolved. It did not throw and it did not log; the middleware simply
/// never ran. These arms pin the fix and the fast path it must not cost.
/// </remarks>
public sealed class DispatchCompositionIsContinuableShould
{
	private static int ConfiguredMiddlewareCount(IDispatcher dispatcher)
	{
		var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
		var invoker = dispatcher.GetType().GetField("_concreteMiddlewareInvoker", flags)?.GetValue(dispatcher);
		var count = invoker?.GetType().GetField("_middlewareCount", flags)?.GetValue(invoker);

		count.ShouldNotBeNull("the invoker's middleware count is the observable this test reads; if the field "
			+ "was renamed the arms below would silently stop measuring anything");

		return (int)count;
	}

	[Fact]
	public void ApplyMiddlewareConfiguredAfterAddDispatchReturns()
	{
		// SAFETY. This is the arm that was RED: the count was 0 because the second builder accumulated
		// into its own list while the registered pipeline read the first builder's.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(static _ => { });

		using (var continued = new DispatchBuilder(services))
		{
			_ = continued.UseMiddleware<ProbeMiddleware>();
		}

		using var provider = services.BuildServiceProvider();

		ConfiguredMiddlewareCount(provider.GetRequiredService<IDispatcher>()).ShouldBe(
			1,
			"middleware configured after AddDispatch returned must reach the pipeline; a 0 here means the "
			+ "composition was silently discarded, which is how this defect presented");
	}

	[Fact]
	public void ApplyMiddlewareChainedDirectlyOffTheReturnedBuilder()
	{
		// The point of the whole change: AddDispatch hands back the builder, so a component is configured by
		// chaining rather than by minting another top-level AddDispatchXxx extension on IServiceCollection.
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddDispatch(static _ => { }).UseMiddleware<ProbeMiddleware>();

		using var provider = services.BuildServiceProvider();

		ConfiguredMiddlewareCount(provider.GetRequiredService<IDispatcher>()).ShouldBe(1);
	}

	[Fact]
	public void ApplyMiddlewareConfiguredInsideTheConfigureAction()
	{
		// The already-supported path. It shares the state plumbing, so it is pinned alongside.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(static d => d.UseMiddleware<ProbeMiddleware>());

		using var provider = services.BuildServiceProvider();

		ConfiguredMiddlewareCount(provider.GetRequiredService<IDispatcher>()).ShouldBe(1);
	}

	[Fact]
	public void StillConfigureNoMiddlewareWhenTheConsumerConfiguredNone()
	{
		// LIVENESS. Without this arm, sharing state in a way that leaked middleware into every composition
		// would satisfy the safety arm above while destroying the no-middleware fast path.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(static _ => { });

		using var provider = services.BuildServiceProvider();

		ConfiguredMiddlewareCount(provider.GetRequiredService<IDispatcher>()).ShouldBe(
			0,
			"a consumer who configured no middleware must still get the empty pipeline");
	}

	[Fact]
	public void ResolveTheSameTransportBindingRegistryTheBuilderConfigures()
	{
		// The same root cause seen from the transport side, and it has to be measured on the REAL path.
		// AddDispatchPipeline TryAdd-registers TransportBindingRegistry as a TYPE, which makes the builder's
		// TryAddSingleton of its own instance a no-op, so the container activated a SECOND empty registry.
		// A binding registered through the builder then resolved from nothing -- and a binding that resolves
		// from nothing does not fail, it simply never matches.
		//
		// Note what this arm must NOT do: constructing a bare DispatchBuilder skips AddDispatchPipeline, so
		// no type registration exists, the instance registration succeeds, and the arm passes against the
		// defect. It has to go through AddDispatch.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(static _ => { });

		var builderSide = DispatchBuilderState.GetOrAdd(services).BindingRegistry;
		builderSide.RegisterPendingTransportReference("probe-transport");

		using var provider = services.BuildServiceProvider();
		var resolved = provider.GetRequiredService<TransportBindingRegistry>();

		resolved.GetPendingTransportReferences().ShouldContain(
			"probe-transport",
			"the registry the container serves must be the one the builder configures; if it is a second "
			+ "instance every builder-registered binding is invisible to whatever resolves it");
	}

	private sealed class ProbeMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => null;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate next,
			CancellationToken cancellationToken) => next(message, context, cancellationToken);
	}
}
