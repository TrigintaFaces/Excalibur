// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Middleware.Resilience;
using Excalibur.Dispatch.Options.Resilience;

namespace Excalibur.Dispatch.Tests.DependencyInjection;

/// <summary>
/// Locks that middleware added to the builder <c>AddDispatch</c> returns reaches the pipeline, on both
/// overloads, exactly once, in the order it was added.
/// </summary>
/// <remarks>
/// <para>
/// The defect: <c>AddDispatch(params Assembly[])</c> returned a builder whose <c>UseMiddleware&lt;T&gt;()</c>
/// -- and so every <c>Use*()</c> extension built on it -- accepted the call, returned itself for chaining and
/// changed nothing. That overload composed its pipeline only from middleware registered as
/// <see cref="IDispatchMiddleware"/>, while <c>UseMiddleware</c> records into the builder's composition,
/// which only <c>AddDispatch(configure)</c> ever turned into a pipeline. Every observable signal said the
/// middleware was configured, and it never ran.
/// </para>
/// <para>
/// Every arm here dispatches a real message through a provider built by the production registration path
/// and asserts what actually executed. None inspects a descriptor list: the defect was a registration that
/// existed and did nothing, which is exactly what a descriptor assertion cannot see.
/// </para>
/// <para>
/// SAFETY -- chained middleware runs (red against the defect). LIVENESS -- it runs ONCE, so a fix that
/// seats a middleware on two paths fails; middleware registered the ordinary way still runs; and the
/// configure-lambda route is unchanged. ORDERING -- registration order is preserved, and both orders are
/// asserted so the arm cannot pass by coincidence. GENERALITY -- a framework <c>Use*()</c> extension, not
/// only the seam, is exercised.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait(TraitNames.Component, TestComponents.DependencyInjection)]
public sealed class MiddlewareChainedOffAddDispatchShould
{
	private static readonly Assembly HandlerAssembly = typeof(MiddlewareChainedOffAddDispatchShould).Assembly;

	[Fact]
	public async Task RunMiddlewareChainedOffTheAssemblyOverload()
	{
		var services = NewServices();
		_ = services.AddDispatch(HandlerAssembly).UseMiddleware<FirstProbeMiddleware>();

		var outcome = await DispatchOnceAsync(services).ConfigureAwait(true);

		outcome.Trace.ShouldBe(
			[nameof(FirstProbeMiddleware)],
			"a middleware added to the builder AddDispatch(Assembly[]) returns must be in the pipeline it composes.");
		outcome.HandlerInvocations.ShouldBe(1);
	}

	[Fact]
	public async Task RunMiddlewareChainedOffTheAssemblyOverload_InRegistrationOrder()
	{
		var forward = NewServices();
		_ = forward.AddDispatch(HandlerAssembly)
			.UseMiddleware<FirstProbeMiddleware>()
			.UseMiddleware<SecondProbeMiddleware>();

		var reversed = NewServices();
		_ = reversed.AddDispatch(HandlerAssembly)
			.UseMiddleware<SecondProbeMiddleware>()
			.UseMiddleware<FirstProbeMiddleware>();

		(await DispatchOnceAsync(forward).ConfigureAwait(true)).Trace.ShouldBe(
			[nameof(FirstProbeMiddleware), nameof(SecondProbeMiddleware)]);

		// The reverse composition is what makes the arm above a statement about ORDER rather than about a
		// fixed sequence that happens to match.
		(await DispatchOnceAsync(reversed).ConfigureAwait(true)).Trace.ShouldBe(
			[nameof(SecondProbeMiddleware), nameof(FirstProbeMiddleware)]);
	}

	[Fact]
	public async Task ComposeTheSamePipelineOnBothOverloads_ForTheSameUseSequence()
	{
		var chained = NewServices();
		_ = chained.AddDispatch(HandlerAssembly)
			.UseMiddleware<SecondProbeMiddleware>()
			.UseMiddleware<FirstProbeMiddleware>();

		var configured = NewServices();
		_ = configured.AddDispatch(dispatch => dispatch
			.AddHandlersFromAssembly(HandlerAssembly)
			.UseMiddleware<SecondProbeMiddleware>()
			.UseMiddleware<FirstProbeMiddleware>());

		var fromChained = await DispatchOnceAsync(chained).ConfigureAwait(true);
		var fromConfigured = await DispatchOnceAsync(configured).ConfigureAwait(true);

		fromConfigured.Trace.ShouldBe([nameof(SecondProbeMiddleware), nameof(FirstProbeMiddleware)]);
		fromChained.Trace.ShouldBe(
			fromConfigured.Trace,
			"the two overloads must compose the same pipeline for the same Use*() sequence.");
		fromChained.HandlerInvocations.ShouldBe(fromConfigured.HandlerInvocations);
	}

	[Fact]
	public async Task KeepMiddlewareChainedAfterTheConfigureLambda_AfterTheLambdasOwn()
	{
		var services = NewServices();
		_ = services
			.AddDispatch(dispatch => dispatch
				.AddHandlersFromAssembly(HandlerAssembly)
				.UseMiddleware<SecondProbeMiddleware>())
			.UseMiddleware<FirstProbeMiddleware>();

		var outcome = await DispatchOnceAsync(services).ConfigureAwait(true);

		outcome.Trace.ShouldBe(
			[nameof(SecondProbeMiddleware), nameof(FirstProbeMiddleware)],
			"middleware added after AddDispatch(configure) returns must follow the lambda's own, each exactly once.");
	}

	[Fact]
	public async Task RunTheConfigureLambdasMiddlewareExactlyOnce()
	{
		// The regression arm for the configure route, which already composed its pipeline and must not
		// start composing it twice now that the assembly route shares the same composition.
		var services = NewServices();
		_ = services.AddDispatch(dispatch => dispatch
			.AddHandlersFromAssembly(HandlerAssembly)
			.UseMiddleware<FirstProbeMiddleware>());

		var outcome = await DispatchOnceAsync(services).ConfigureAwait(true);

		outcome.Trace.ShouldBe([nameof(FirstProbeMiddleware)]);
		outcome.HandlerInvocations.ShouldBe(1);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task RunAMiddlewareRegisteredBothWaysExactlyOnce(bool viaAssemblyOverload)
	{
		// The same type placed through the builder AND registered as an IDispatchMiddleware. Both
		// registrations reach the composed pipeline; the composition deduplicates by concrete type. A fix
		// that bridged the two paths by seating the middleware on each would run it twice here.
		var services = NewServices();
		var builder = viaAssemblyOverload
			? services.AddDispatch(HandlerAssembly)
			: services.AddDispatch(dispatch => dispatch.AddHandlersFromAssembly(HandlerAssembly));

		_ = builder.UseMiddleware<FirstProbeMiddleware>();

		// Registered directly as an IDispatchMiddleware, with the same lifetime UseMiddleware gives it.
		_ = services.AddScoped<IDispatchMiddleware, FirstProbeMiddleware>();

		var outcome = await DispatchOnceAsync(services).ConfigureAwait(true);

		outcome.Trace.ShouldBe([nameof(FirstProbeMiddleware)]);
		outcome.HandlerInvocations.ShouldBe(1);
	}

	[Fact]
	public async Task ComposeOnePipeline_WhenTheAssemblyOverloadIsFollowedByTheConfigureOverload()
	{
		// Both overloads over one collection -- a host scanning its own assembly, then a package calling
		// AddDispatch(configure). The assembly overload now registers the composition first; the later
		// configure call must continue that composition, not start a second one or be skipped.
		var services = NewServices();
		_ = services.AddDispatch(HandlerAssembly).UseMiddleware<FirstProbeMiddleware>();
		_ = services.AddDispatch(dispatch => dispatch.UseMiddleware<SecondProbeMiddleware>());

		var outcome = await DispatchOnceAsync(services).ConfigureAwait(true);

		outcome.Trace.ShouldBe([nameof(FirstProbeMiddleware), nameof(SecondProbeMiddleware)]);
		outcome.HandlerInvocations.ShouldBe(1);
	}

	[Fact]
	public async Task StillRunMiddlewareRegisteredAsIDispatchMiddlewareAfterTheAssemblyOverload()
	{
		// The ordinary registration path this overload always served. It must survive the overload
		// composing its pipeline from the builder.
		var services = NewServices();
		_ = services.AddDispatch(HandlerAssembly);
		_ = services.AddDispatchMiddleware<SecondProbeMiddleware>();

		var outcome = await DispatchOnceAsync(services).ConfigureAwait(true);

		outcome.Trace.ShouldBe([nameof(SecondProbeMiddleware)]);
		outcome.HandlerInvocations.ShouldBe(1);
	}

	[Fact]
	public async Task DispatchThroughAnEmptyPipeline_WhenNothingIsChained()
	{
		// Liveness for the arms above: with nothing chained the pipeline still delivers, and no probe runs,
		// so a probe that recorded unconditionally could not satisfy them.
		var services = NewServices();
		_ = services.AddDispatch(HandlerAssembly);

		var outcome = await DispatchOnceAsync(services).ConfigureAwait(true);

		outcome.Trace.ShouldBeEmpty();
		outcome.HandlerInvocations.ShouldBe(1);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task RetryTheHandler_WhenUseRetryIsChainedOffEitherOverload(bool viaAssemblyOverload)
	{
		// A framework Use*() extension rather than the seam itself, so the arms above are not the only
		// evidence that an extension built on UseMiddleware reaches the pipeline.
		var services = NewServices();
		_ = services.Configure<RetryOptions>(static options =>
		{
			options.BaseDelay = TimeSpan.FromMilliseconds(1);
			options.MaxDelay = TimeSpan.FromMilliseconds(1);
			options.UseJitter = false;
		});

		var builder = viaAssemblyOverload
			? services.AddDispatch(HandlerAssembly)
			: services.AddDispatch(dispatch => dispatch.AddHandlersFromAssembly(HandlerAssembly));
		_ = builder.UseRetry();

		var recorder = new FailOnceActionHandler.Recorder();
		_ = services.AddSingleton(recorder);

		await using var provider = Build(services);
		var result = await DispatchAsync(provider, new FailOnceAction()).ConfigureAwait(true);

		recorder.Invocations.ShouldBe(
			2,
			"UseRetry() places the retry middleware, so a transient first failure must be retried once.");
		result.Succeeded.ShouldBeTrue(result.ErrorMessage ?? "the retried dispatch failed");
	}

	private static ServiceCollection NewServices()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton<ProbeTrace>();
		_ = services.AddSingleton<ProbeActionHandler.Recorder>();
		return services;
	}

	private static ServiceProvider Build(ServiceCollection services) =>
		// Scope validation ON: UseMiddleware registers middleware Scoped, and a composition that resolved it
		// from the root provider would be refused here rather than silently capturing it.
		services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = false });

	private static async Task<(IReadOnlyList<string> Trace, int HandlerInvocations)> DispatchOnceAsync(
		ServiceCollection services)
	{
		await using var provider = Build(services);

		var result = await DispatchAsync(provider, new ProbeAction()).ConfigureAwait(true);
		result.Succeeded.ShouldBeTrue(result.ErrorMessage ?? "dispatch failed");

		return (
			provider.GetRequiredService<ProbeTrace>().Snapshot(),
			provider.GetRequiredService<ProbeActionHandler.Recorder>().Invocations);
	}

	private static Task<IMessageResult> DispatchAsync(IServiceProvider provider, IDispatchAction action)
	{
		var dispatcher = provider.GetRequiredService<IDispatcher>();
		var context = provider.GetRequiredService<IMessageContextFactory>().CreateContext();
		context.MessageId = Guid.NewGuid().ToString("N");

		return dispatcher.DispatchAsync(action, context, TestContext.Current.CancellationToken);
	}

	internal sealed class ProbeTrace
	{
		private readonly List<string> _entries = [];
		private readonly Lock _gate = new();

		public void Record(string entry)
		{
			lock (_gate)
			{
				_entries.Add(entry);
			}
		}

		public IReadOnlyList<string> Snapshot()
		{
			lock (_gate)
			{
				return [.. _entries];
			}
		}
	}

	internal abstract class ProbeMiddlewareBase(ProbeTrace trace) : IDispatchMiddleware
	{
		// One shared stage, so the pipeline's stage sort is a tie and the order observed is registration
		// order -- the property under test.
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(nextDelegate);

			if (message is ProbeAction)
			{
				trace.Record(GetType().Name);
			}

			return nextDelegate(message, context, cancellationToken);
		}
	}

	internal sealed class FirstProbeMiddleware(ProbeTrace trace) : ProbeMiddlewareBase(trace);

	internal sealed class SecondProbeMiddleware(ProbeTrace trace) : ProbeMiddlewareBase(trace);

	internal sealed class ProbeAction : IDispatchAction;

	internal sealed class ProbeActionHandler(ProbeActionHandler.Recorder recorder) : IActionHandler<ProbeAction>
	{
		public Task HandleAsync(ProbeAction action, CancellationToken cancellationToken)
		{
			recorder.Record();
			return Task.CompletedTask;
		}

		internal sealed class Recorder
		{
			private int _invocations;

			public int Invocations => Volatile.Read(ref _invocations);

			public void Record() => _ = Interlocked.Increment(ref _invocations);
		}
	}

	internal sealed class FailOnceAction : IDispatchAction;

	internal sealed class FailOnceActionHandler(FailOnceActionHandler.Recorder recorder) : IActionHandler<FailOnceAction>
	{
		public Task HandleAsync(FailOnceAction action, CancellationToken cancellationToken)
		{
			// Transient by the default failure classifier, so the retry middleware retries it.
			return recorder.Record() == 1
				? throw new TimeoutException("first attempt fails transiently")
				: Task.CompletedTask;
		}

		internal sealed class Recorder
		{
			private int _invocations;

			public int Invocations => Volatile.Read(ref _invocations);

			public int Record() => Interlocked.Increment(ref _invocations);
		}
	}
}
