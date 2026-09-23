// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Pipeline;
using Excalibur.Dispatch.Messaging;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.Configuration;

/// <summary>
/// Regression lock: the dispatch runtime state is a singleton, and the provider handed to a singleton
/// factory is the ROOT. A middleware that depends -- directly or transitively -- on a Scoped service must
/// therefore never be resolved once and held by that pipeline: that is a captive dependency. The default
/// profile's entries are registered Scoped; <c>UseMiddleware&lt;T&gt;()</c> registers Transient and the
/// pipeline walks the constructor graph, so only a middleware that actually reaches a Scoped service is
/// resolved per dispatch. A middleware that reaches nothing Scoped is held once, from the root, exactly as
/// convention middleware is in ASP.NET Core.
/// </summary>
/// <remarks>
/// <para>
/// The defect has two halves and only one of them is loud. Under
/// <see cref="ServiceProviderOptions.ValidateScopes"/> -- the ASP.NET Core Development default -- the
/// root resolution of a scoped middleware fails. With scope validation off, which is the Production
/// default, nothing fails and one middleware graph silently serves every request, sharing whatever
/// scoped dependencies it captured.
/// </para>
/// <para>
/// Each arm pairs a safety assertion with a liveness one, because "the dispatch did not throw" is also
/// satisfied by a pipeline that composed nothing -- which is exactly what the pre-fix code does under
/// scope validation: every entry of the default profile is Optional, so the root-resolution failure is
/// caught, logged at Warning and skipped, and the host dispatches through an empty pipeline.
/// </para>
/// <para>
/// Composed through the real registration path (<c>AddDispatch(configure)</c> ->
/// <c>BuildServiceProvider</c> -> <c>GetRequiredService&lt;IDispatcher&gt;</c>), because the defect is
/// in what the container hands the pipeline; a hand-built pipeline cannot express it.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Configuration")]
public sealed class ScopedMiddlewareIsNotCapturedBySingletonPipelineShould
{
	[Fact]
	public async Task ComposeTheDefaultProfile_WhenScopeValidationIsOn()
	{
		var handler = new ProbeCommandHandler();
		var provider = BuildProvider(
			handler,
			validateScopes: true,
			static dispatch => dispatch.ConfigurePipeline(
				"Default",
				static pipeline => pipeline.UseProfile(DefaultPipelineProfiles.Default)));

		// Liveness: the four default-profile middleware must actually be in the composed pipeline.
		// Pre-fix this is false -- each one fails to resolve from the root under scope validation and
		// is skipped as Optional, leaving an empty pipeline behind a host that starts cleanly.
		var invoker = (DispatchMiddlewareInvoker)provider.GetRequiredService<IDispatchMiddlewareInvoker>();
		invoker.HasMiddleware.ShouldBeTrue();

		// Safety: and the dispatch itself still completes rather than surfacing the captive-dependency
		// InvalidOperationException to the caller.
		using var scope = provider.CreateScope();
		var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
		var context = new MessageContext(new ProbeCommand(), scope.ServiceProvider);

		var result = await dispatcher.DispatchAsync(new ProbeCommand(), context, CancellationToken.None);

		_ = result.ShouldNotBeNull();
		handler.HandledCount.ShouldBe(1);
	}

	[Fact]
	public async Task RunAScopedGlobalMiddleware_WhenScopeValidationIsOn()
	{
		var recorder = new ScopeProbeRecorder();
		var handler = new ProbeCommandHandler();
		var provider = BuildProvider(
			handler,
			validateScopes: true,
			static dispatch => dispatch.UseMiddleware<ScopeProbeMiddleware>(),
			recorder);

		using var scope = provider.CreateScope();
		var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
		var context = new MessageContext(new ProbeCommand(), scope.ServiceProvider);

		// Safety: UseMiddleware<T>() registers Required, so the pre-fix build collects the root
		// resolution failure and throws rather than skipping it.
		var result = await dispatcher.DispatchAsync(new ProbeCommand(), context, CancellationToken.None);

		// Liveness, both halves.
		_ = result.ShouldNotBeNull();
		handler.HandledCount.ShouldBe(1);
		recorder.Instances.Count.ShouldBe(1);
	}

	[Fact]
	public async Task ResolveAFreshMiddlewareInstancePerScope_RatherThanOneSharedByEveryDispatch()
	{
		var recorder = new ScopeProbeRecorder();
		var handler = new ProbeCommandHandler();

		// Scope validation OFF: the Production configuration, where the captive dependency does not
		// fail and is therefore invisible unless something asserts on instance identity.
		var provider = BuildProvider(
			handler,
			validateScopes: false,
			static dispatch => dispatch.UseMiddleware<ScopeProbeMiddleware>(),
			recorder);

		await DispatchInFreshScopeAsync(provider);
		await DispatchInFreshScopeAsync(provider);

		// Liveness first: two dispatches, two middleware invocations.
		handler.HandledCount.ShouldBe(2);
		recorder.Instances.Count.ShouldBe(2);

		// Safety: one scoped middleware instance shared between two scopes IS the captive dependency --
		// its own scoped dependencies (a store, a unit of work, a tenant context) are shared with it.
		recorder.Instances[0].ShouldNotBeSameAs(recorder.Instances[1]);
	}

	[Fact]
	public async Task HoldOneInstanceOfAMiddlewareThatDependsOnNothingScoped()
	{
		var recorder = new ScopeProbeRecorder();
		var handler = new ProbeCommandHandler();
		var provider = BuildProvider(
			handler,
			validateScopes: true,
			static dispatch => dispatch.UseMiddleware<RootSafeProbeMiddleware>(),
			recorder);

		await DispatchInFreshScopeAsync(provider);
		await DispatchInFreshScopeAsync(provider);

		// Liveness: both dispatches ran the middleware and the handler.
		handler.HandledCount.ShouldBe(2);
		recorder.Instances.Count.ShouldBe(2);

		// The fix: a middleware reaching nothing Scoped is not given a scope per dispatch; one instance
		// serves the pipeline, as convention middleware does in ASP.NET Core. Pre-fix every UseMiddleware
		// registration was Scoped, so this was a fresh instance (and a fresh DI scope) per dispatch.
		recorder.Instances[0].ShouldBeSameAs(recorder.Instances[1]);
	}

	[Fact]
	public async Task NotDisposeAHeldMiddlewareWhenTheCompositionScopeEnds()
	{
		var recorder = new ScopeProbeRecorder();
		var handler = new ProbeCommandHandler();
		var provider = BuildProvider(
			handler,
			validateScopes: true,
			static dispatch => dispatch.UseMiddleware<RootSafeProbeMiddleware>(),
			recorder);

		// Pipeline composition resolves middleware inside a scope it disposes on return. A held
		// (root-safe) middleware must come from the ROOT, or the composition scope disposes it and every
		// later dispatch invokes a disposed instance.
		await DispatchInFreshScopeAsync(provider);
		await DispatchInFreshScopeAsync(provider);

		handler.HandledCount.ShouldBe(2);
		recorder.Instances.Cast<RootSafeProbeMiddleware>().ShouldAllBe(static m => !m.IsDisposed);
	}

	[Fact]
	public async Task ShareOneScopedInstanceBetweenMiddlewareAndHandler_WithinOneDispatch()
	{
		var recorder = new ScopeProbeRecorder();
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton(recorder);
		_ = services.AddScoped<ScopedProbeDependency>();
		_ = services.AddScoped<IActionHandler<ProbeCommand>, ScopedDependencyRecordingHandler>();
		_ = services.AddDispatch(static dispatch => dispatch.UseMiddleware<ScopeProbeMiddleware>());
		var provider = services.BuildServiceProvider(new ServiceProviderOptions
		{
			ValidateScopes = true,
			ValidateOnBuild = true,
		});

		await DispatchInFreshScopeAsync(provider);
		await DispatchInFreshScopeAsync(provider);

		// Liveness: both halves ran in both dispatches.
		recorder.Dependencies.Count.ShouldBe(2);
		recorder.HandlerDependencies.Count.ShouldBe(2);

		// Within one dispatch the middleware and the handler see ONE scoped instance -- a transaction
		// middleware and its handler must share a unit of work -- and across dispatches they do not.
		recorder.Dependencies[0].ShouldBeSameAs(recorder.HandlerDependencies[0]);
		recorder.Dependencies[1].ShouldBeSameAs(recorder.HandlerDependencies[1]);
		recorder.Dependencies[0].ShouldNotBeSameAs(recorder.Dependencies[1]);
	}

	[Fact]
	public async Task HonourAConsumersOwnScopedRegistrationOfTheMiddleware()
	{
		var recorder = new ScopeProbeRecorder();
		var handler = new ProbeCommandHandler();

		// The consumer registers the middleware Scoped themselves; UseMiddleware must not override it.
		var provider = BuildProvider(
			handler,
			validateScopes: true,
			static dispatch => dispatch.UseMiddleware<RootSafeProbeMiddleware>(),
			recorder,
			static services => services.AddScoped<RootSafeProbeMiddleware>());

		await DispatchInFreshScopeAsync(provider);
		await DispatchInFreshScopeAsync(provider);

		handler.HandledCount.ShouldBe(2);
		recorder.Instances[0].ShouldNotBeSameAs(recorder.Instances[1]);
	}

	private static async Task DispatchInFreshScopeAsync(IServiceProvider provider)
	{
		using var scope = provider.CreateScope();
		var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
		var context = new MessageContext(new ProbeCommand(), scope.ServiceProvider);
		_ = await dispatcher.DispatchAsync(new ProbeCommand(), context, CancellationToken.None);
	}

	private static ServiceProvider BuildProvider(
		ProbeCommandHandler handler,
		bool validateScopes,
		Action<IDispatchBuilder> configure,
		ScopeProbeRecorder? recorder = null,
		Action<IServiceCollection>? preRegister = null)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton(recorder ?? new ScopeProbeRecorder());
		_ = services.AddSingleton<IActionHandler<ProbeCommand>>(handler);
		_ = services.AddSingleton(handler);
		_ = services.AddScoped<ScopedProbeDependency>();
		preRegister?.Invoke(services);

		_ = services.AddDispatch(configure);

		return services.BuildServiceProvider(new ServiceProviderOptions
		{
			ValidateScopes = validateScopes,
			ValidateOnBuild = validateScopes,
		});
	}

	private sealed class ProbeCommand : IDispatchAction;

	private sealed class ProbeCommandHandler : IActionHandler<ProbeCommand>
	{
		private int _handledCount;

		public int HandledCount => Volatile.Read(ref _handledCount);

		public Task HandleAsync(ProbeCommand action, CancellationToken cancellationToken)
		{
			_ = Interlocked.Increment(ref _handledCount);
			return Task.CompletedTask;
		}
	}

	private sealed class ScopeProbeRecorder
	{
		private readonly ConcurrentQueue<object> _instances = new();

		public IReadOnlyList<object> Instances => [.. _instances];

		private readonly ConcurrentQueue<object> _dependencies = new();
		private readonly ConcurrentQueue<object> _handlerDependencies = new();

		public IReadOnlyList<object> Dependencies => [.. _dependencies];

		public IReadOnlyList<object> HandlerDependencies => [.. _handlerDependencies];

		public void Record(object instance) => _instances.Enqueue(instance);

		public void RecordDependency(object dependency) => _dependencies.Enqueue(dependency);

		public void RecordHandlerDependency(object dependency) => _handlerDependencies.Enqueue(dependency);
	}

	/// <summary>A Scoped service: the thing a captive middleware would wrongly share across scopes.</summary>
	private sealed class ScopedProbeDependency;

	private sealed class ScopeProbeMiddleware(ScopeProbeRecorder recorder, ScopedProbeDependency dependency)
		: IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			recorder.Record(this);
			recorder.RecordDependency(dependency);
			return nextDelegate(message, context, cancellationToken);
		}
	}

	/// <summary>Depends on nothing Scoped, so the pipeline may hold one instance for its lifetime.</summary>
	private sealed class RootSafeProbeMiddleware(ScopeProbeRecorder recorder) : IDispatchMiddleware, IDisposable
	{
		public bool IsDisposed { get; private set; }

		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			ObjectDisposedException.ThrowIf(IsDisposed, this);
			recorder.Record(this);
			return nextDelegate(message, context, cancellationToken);
		}

		public void Dispose() => IsDisposed = true;
	}

	/// <summary>A handler that records the Scoped dependency it was given, to compare with the middleware's.</summary>
	private sealed class ScopedDependencyRecordingHandler(ScopeProbeRecorder recorder, ScopedProbeDependency dependency)
		: IActionHandler<ProbeCommand>
	{
		public Task HandleAsync(ProbeCommand action, CancellationToken cancellationToken)
		{
			recorder.RecordHandlerDependency(dependency);
			return Task.CompletedTask;
		}
	}
}
