// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Tests.Messaging;

/// <summary>
/// Regression tests for ADR-335: a singleton dispatcher must resolve scoped handlers (and handlers with
/// scoped dependencies) from a dependency-injection scope, never from the captured root provider. Before
/// the fix these dispatches failed with "Cannot resolve scoped service '…' from root provider".
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
public sealed class ScopedHandlerResolutionShould
{
	[Fact]
	public async Task ResolveScopedHandlerDispatchedViaBareTypedOverload()
	{
		// Arrange — scoped handler + scoped dependency; dispatch with NO context (the bug repro).
		using var provider = BuildProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		// Act — bare overload: no MessageContext, no ambient scope.
		var result = await dispatcher
			.DispatchAsync<ScopedQuery, string>(new ScopedQuery(), CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeTrue($"Error: {result.ErrorMessage}; Problem: {result.ProblemDetails?.Detail}");
		result.ReturnValue.ShouldBe("scoped-ok");
	}

	[Fact]
	public async Task ResolveScopedHandlerDispatchedViaBareNoResponseOverload()
	{
		// Arrange
		ScopedCommandHandler.Reset();
		using var provider = BuildProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		// Act — bare no-response overload.
		var result = await dispatcher
			.DispatchAsync(new ScopedCommand(), CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeTrue($"Error: {result.ErrorMessage}; Problem: {result.ProblemDetails?.Detail}");
		ScopedCommandHandler.InvocationCount.ShouldBe(1);
	}

	[Fact]
	public async Task ResolveScopedHandlerDispatchedViaContextOverload()
	{
		// Arrange — context path already resolves via context.RequestServices; verify no regression.
		using var provider = BuildProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();
		using var scope = provider.CreateScope();
		var message = new ScopedQuery();
		var context = new MessageContext(message, scope.ServiceProvider);

		// Act
		var result = await dispatcher
			.DispatchAsync<ScopedQuery, string>(message, context, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeTrue($"Error: {result.ErrorMessage}; Problem: {result.ProblemDetails?.Detail}");
		result.ReturnValue.ShouldBe("scoped-ok");
	}

	[Fact]
	public async Task CreateFreshScopePerDispatchAndDisposeScopedDependency_WhenNoAmbientScope()
	{
		// Arrange — worker scenario: no IDispatchAmbientScopeAccessor registered.
		var tracker = new DisposalTracker();
		using var provider = BuildProvider(services => _ = services.AddSingleton(tracker));
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		// Act
		var result = await dispatcher
			.DispatchAsync<ScopedQuery, string>(new ScopedQuery(), CancellationToken.None)
			.ConfigureAwait(false);

		// Assert — the per-dispatch scope was disposed, disposing its scoped dependency exactly once.
		result.Succeeded.ShouldBeTrue($"Error: {result.ErrorMessage}; Problem: {result.ProblemDetails?.Detail}");
		tracker.DisposeCount.ShouldBe(1, "the freshly created scope must be disposed after the handler completes");
	}

	[Fact]
	public async Task BorrowAmbientScopeAndNotDisposeIt_WhenAccessorProvidesOne()
	{
		// Arrange — simulate a hosting integration that supplies the ambient (request) scope.
		var tracker = new DisposalTracker();
		var ambient = new MutableAmbientScopeAccessor();
		using var provider = BuildProvider(services =>
		{
			_ = services.AddSingleton(tracker);
			_ = services.AddSingleton<IDispatchAmbientScopeAccessor>(ambient);
		});
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		using var requestScope = provider.CreateScope();
		ambient.Current = requestScope.ServiceProvider;

		// Act
		var result = await dispatcher
			.DispatchAsync<ScopedQuery, string>(new ScopedQuery(), CancellationToken.None)
			.ConfigureAwait(false);

		// Assert — handler resolved from the borrowed ambient scope; core did NOT dispose it.
		result.Succeeded.ShouldBeTrue($"Error: {result.ErrorMessage}; Problem: {result.ProblemDetails?.Detail}");
		tracker.DisposeCount.ShouldBe(0, "a borrowed ambient scope must never be disposed by the dispatcher");

		// The ambient scope (and its scoped dependency) is disposed by its owner — here, the test.
		requestScope.Dispose();
		tracker.DisposeCount.ShouldBe(1);
	}

	[Fact]
	public async Task NotCreateScopeForTransientHandler_DispatchedViaBareOverload()
	{
		// Arrange — transient handler with no scoped dependency: must keep using the root-resolvable path.
		using var provider = BuildProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		// Act
		var result = await dispatcher
			.DispatchAsync<TransientQuery, string>(new TransientQuery(), CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeTrue($"Error: {result.ErrorMessage}; Problem: {result.ProblemDetails?.Detail}");
		result.ReturnValue.ShouldBe("transient-ok");
	}

	[Fact]
	public async Task ResolveTransientHandlerWithScopedDependency_ViaBareOverload()
	{
		// Arrange — the common default case: handler registered Transient (as AddHandlersFromAssembly
		// does) but depending on a Scoped service. Constructor inspection must detect the scoped dep.
		var tracker = new DisposalTracker();
		using var provider = BuildProvider(services =>
		{
			_ = services.AddSingleton(tracker);
			_ = services.AddTransient<IActionHandler<TransientWithScopedDepQuery, string>, TransientWithScopedDepHandler>();
		});
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		// Act
		var result = await dispatcher
			.DispatchAsync<TransientWithScopedDepQuery, string>(new TransientWithScopedDepQuery(), CancellationToken.None)
			.ConfigureAwait(false);

		// Assert — resolves via a fresh scope and disposes the scoped dependency.
		result.Succeeded.ShouldBeTrue($"Error: {result.ErrorMessage}; Problem: {result.ProblemDetails?.Detail}");
		result.ReturnValue.ShouldBe("scoped-ok");
		tracker.DisposeCount.ShouldBe(1);
	}

	[Fact]
	public async Task IsolateScopesAcrossConcurrentBareDispatches()
	{
		// Arrange
		using var provider = BuildProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		// Act — many concurrent bare dispatches of a scoped handler.
		var tasks = Enumerable.Range(0, 64)
			.Select(_ => dispatcher.DispatchAsync<ScopedQuery, string>(new ScopedQuery(), CancellationToken.None))
			.ToArray();
		var results = await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert — all succeed, no cross-dispatch state bleed.
		results.ShouldAllBe(r => r.Succeeded);
		results.ShouldAllBe(r => r.ReturnValue == "scoped-ok");
	}

	private static ServiceProvider BuildProvider(Action<IServiceCollection>? configure = null)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Scoped dependency the scoped handler consumes — proves resolution happens in a real scope.
		_ = services.AddScoped<ScopedDependency>();

		// Scoped handlers (the captive-dependency case).
		_ = services.AddScoped<IActionHandler<ScopedQuery, string>, ScopedQueryHandler>();
		_ = services.AddScoped<IActionHandler<ScopedCommand>, ScopedCommandHandler>();

		// Transient handler with no scoped dependency — the root-resolvable hot path.
		_ = services.AddTransient<IActionHandler<TransientQuery, string>, TransientQueryHandler>();

		configure?.Invoke(services);

		_ = services.AddDispatchPipeline();
		_ = services.AddDispatchHandlers();

		var provider = services.BuildServiceProvider(new ServiceProviderOptions
		{
			ValidateScopes = true,
		});

		_ = provider.GetRequiredKeyedService<IMessageBus>("Local");
		return provider;
	}

	private sealed class DisposalTracker
	{
		private int _disposeCount;
		public int DisposeCount => Volatile.Read(ref _disposeCount);
		public void MarkDisposed() => Interlocked.Increment(ref _disposeCount);
	}

	private sealed class ScopedDependency(DisposalTracker? tracker = null) : IDisposable
	{
		public string Marker => "scoped-ok";
		public void Dispose() => tracker?.MarkDisposed();
	}

	private sealed class MutableAmbientScopeAccessor : IDispatchAmbientScopeAccessor
	{
		public IServiceProvider? Current { get; set; }
		public IServiceProvider? CurrentServiceProvider => Current;
	}

	private sealed class ScopedQuery : IDispatchAction<string>
	{
		public object Body => this;
		public Guid Id { get; } = Guid.NewGuid();
		public string MessageId => Id.ToString();
		public string MessageType => GetType().FullName ?? GetType().Name;
		public MessageKinds Kind => MessageKinds.Action;
		public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
	}

	private sealed class ScopedQueryHandler(ScopedDependency dependency) : IActionHandler<ScopedQuery, string>
	{
		public Task<string> HandleAsync(ScopedQuery action, CancellationToken cancellationToken)
			=> Task.FromResult(dependency.Marker);
	}

	private sealed class ScopedCommand : IDispatchAction
	{
		public object Body => this;
		public Guid Id { get; } = Guid.NewGuid();
		public string MessageId => Id.ToString();
		public string MessageType => GetType().FullName ?? GetType().Name;
		public MessageKinds Kind => MessageKinds.Action;
		public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
	}

	private sealed class ScopedCommandHandler(ScopedDependency dependency) : IActionHandler<ScopedCommand>
	{
		private static int s_invocationCount;
		public static int InvocationCount => Volatile.Read(ref s_invocationCount);
		public static void Reset() => Interlocked.Exchange(ref s_invocationCount, 0);

		public Task HandleAsync(ScopedCommand action, CancellationToken cancellationToken)
		{
			_ = dependency.Marker;
			_ = Interlocked.Increment(ref s_invocationCount);
			return Task.CompletedTask;
		}
	}

	private sealed class TransientWithScopedDepQuery : IDispatchAction<string>
	{
		public object Body => this;
		public Guid Id { get; } = Guid.NewGuid();
		public string MessageId => Id.ToString();
		public string MessageType => GetType().FullName ?? GetType().Name;
		public MessageKinds Kind => MessageKinds.Action;
		public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
	}

	private sealed class TransientWithScopedDepHandler(ScopedDependency dependency)
		: IActionHandler<TransientWithScopedDepQuery, string>
	{
		public Task<string> HandleAsync(TransientWithScopedDepQuery action, CancellationToken cancellationToken)
			=> Task.FromResult(dependency.Marker);
	}

	private sealed class TransientQuery : IDispatchAction<string>
	{
		public object Body => this;
		public Guid Id { get; } = Guid.NewGuid();
		public string MessageId => Id.ToString();
		public string MessageType => GetType().FullName ?? GetType().Name;
		public MessageKinds Kind => MessageKinds.Action;
		public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
	}

	private sealed class TransientQueryHandler : IActionHandler<TransientQuery, string>
	{
		public Task<string> HandleAsync(TransientQuery action, CancellationToken cancellationToken)
			=> Task.FromResult("transient-ok");
	}
}
