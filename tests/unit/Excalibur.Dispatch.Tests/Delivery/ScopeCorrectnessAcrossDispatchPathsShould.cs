// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Middleware;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.Delivery;

/// <summary>
/// A handler with a scoped dependency must be resolved from a dependency-injection scope on <em>every</em>
/// dispatch path — actions, events and documents alike — not only on the gated fast paths.
/// </summary>
/// <remarks>
/// <para>
/// The published guarantee is unconditional: a handler with scoped dependencies is always resolved from a
/// scope, never the root container. Scope resolution was built action-shaped, so the paths that consult it
/// are the ultra-local and direct fast paths. A single registered middleware is enough to route an ordinary
/// action past all of them, and events and documents never consulted it at all. From a root-provider host
/// the handler then resolves its scoped dependency from the root, where it is cached for the life of the
/// process — one instance, shared by every dispatch, for a service the consumer declared per-scope.
/// </para>
/// <para>
/// <b>Every container here registers a middleware.</b> Without one the fast paths are silently what is
/// under test, and they were already correct — a lock built that way passes on the unfixed code and proves
/// nothing about the paths that carry the defect. Middleware is also the normal production configuration,
/// so this is the ordinary case rather than an exotic one.
/// </para>
/// <para>
/// Arms assert observable properties only: whether two dispatches saw the same object, and what a dispatch
/// allocates. No arm reads a lifetime, a verdict, a cache, or the shape of whatever opens the scope, so an
/// implementation that reaches the same guarantee a different way stays green.
/// </para>
/// <para>
/// Every arm runs under both handler lifetimes. The defect is lifetime-neutral, and pinning that down here
/// keeps it from being mistaken for — or absorbed into — a question about what lifetime handlers should be
/// discovered at.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ScopeCorrectnessAcrossDispatchPathsShould
{
	/// <summary>
	/// A conservative lower bound on what one created scope plus its scoped instance costs. Set well under
	/// the observed cost of a scope and well over ordinary per-dispatch churn.
	/// </summary>
	private const int ScopeCostFloorBytes = 300;

	// ---- Messages, one per dispatch path. ----

	private sealed record ScopeProbeAction : IDispatchAction;

	private sealed record ScopeProbeEvent : IDispatchEvent;

	private sealed record ScopeProbeDocument : IDispatchDocument;

	/// <summary>Reaches a handler that depends on nothing at all — the no-scope-required case.</summary>
	private sealed record DependencyFreeAction : IDispatchAction;

	// ---- The scoped dependency whose identity is the whole measurement. ----

	private interface IScopedProbe
	{
		int Id { get; }
	}

	private sealed class ScopedProbe : IScopedProbe
	{
		private static int s_next;

		public int Id { get; } = Interlocked.Increment(ref s_next);
	}

	/// <summary>
	/// Collects what each handler invocation saw. Registered Singleton so it is identical across scopes and
	/// contributes nothing to the scope verdict.
	/// </summary>
	private sealed class ScopeRecorder
	{
		private readonly List<(string Path, string Tag, IScopedProbe Probe)> _seen = [];

		public void Record(string path, string tag, IScopedProbe probe)
		{
			lock (_seen)
			{
				_seen.Add((path, tag, probe));
			}
		}

		public IReadOnlyList<(string Path, string Tag, IScopedProbe Probe)> Seen
		{
			get
			{
				lock (_seen)
				{
					return [.. _seen];
				}
			}
		}
	}

	// ---- One handler per path, each capturing the scoped instance it was handed. ----

	private sealed class ScopeProbeActionHandler(IScopedProbe probe, ScopeRecorder recorder)
		: IActionHandler<ScopeProbeAction>
	{
		public Task HandleAsync(ScopeProbeAction message, CancellationToken cancellationToken)
		{
			recorder.Record("action", "only", probe);
			return Task.CompletedTask;
		}
	}

	private sealed class ScopeProbeEventHandlerA(IScopedProbe probe, ScopeRecorder recorder)
		: IEventHandler<ScopeProbeEvent>
	{
		public Task HandleAsync(ScopeProbeEvent eventMessage, CancellationToken cancellationToken)
		{
			recorder.Record("event", "A", probe);
			return Task.CompletedTask;
		}
	}

	private sealed class ScopeProbeEventHandlerB(IScopedProbe probe, ScopeRecorder recorder)
		: IEventHandler<ScopeProbeEvent>
	{
		public Task HandleAsync(ScopeProbeEvent eventMessage, CancellationToken cancellationToken)
		{
			recorder.Record("event", "B", probe);
			return Task.CompletedTask;
		}
	}

	private sealed class ScopeProbeDocumentHandler(IScopedProbe probe, ScopeRecorder recorder)
		: IDocumentHandler<ScopeProbeDocument>
	{
		public Task HandleAsync(ScopeProbeDocument document, CancellationToken cancellationToken)
		{
			recorder.Record("document", "only", document is null ? throw new ArgumentNullException(nameof(document)) : probe);
			return Task.CompletedTask;
		}
	}

	/// <summary>Depends on nothing, so nothing scoped is reachable and no scope is owed to it.</summary>
	private sealed class DependencyFreeActionHandler : IActionHandler<DependencyFreeAction>
	{
		internal static int Invocations;

		public Task HandleAsync(DependencyFreeAction message, CancellationToken cancellationToken)
		{
			_ = Interlocked.Increment(ref Invocations);
			return Task.CompletedTask;
		}
	}

	/// <summary>
	/// Does nothing but exist. Its only job is to be configured, which is enough to route an ordinary
	/// dispatch off the gated fast paths and onto the general path this lock is about.
	/// </summary>
	private sealed class NoOpMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(nextDelegate);
			return nextDelegate(message, context, cancellationToken);
		}
	}

	// ---- SAFETY: two root-provider dispatches on each path must not share a scoped instance. ----

	[Theory]
	[InlineData(ServiceLifetime.Transient)]
	[InlineData(ServiceLifetime.Scoped)]
	public async Task GiveAnActionHandlerAFreshScopedInstancePerDispatch(ServiceLifetime handlerLifetime)
	{
		// The general action path. With a middleware configured the eight gated fast-path sites are not
		// reachable, so this exercises the public entry the fast paths are the exception to.
		using var provider = Build(handlerLifetime);
		var seen = await DispatchTwiceAsync(provider, () => new ScopeProbeAction());

		AssertHandlerRan(seen, "action", expectedInvocations: 2);
		AssertDistinctAcrossDispatches(seen, "action", handlerLifetime);
	}

	[Theory]
	[InlineData(ServiceLifetime.Transient)]
	[InlineData(ServiceLifetime.Scoped)]
	public async Task GiveAnEventHandlerAFreshScopedInstancePerDispatch(ServiceLifetime handlerLifetime)
	{
		// Events never consulted the scope verdict at all: the publish path resolves handlers from the
		// context's request services, falling through to the root provider when a host supplies none.
		using var provider = Build(handlerLifetime);
		var seen = await DispatchTwiceAsync(provider, () => new ScopeProbeEvent());

		// Two handlers, two dispatches.
		AssertHandlerRan(seen, "event", expectedInvocations: 4);
		AssertDistinctAcrossDispatches(seen, "event", handlerLifetime);
	}

	[Theory]
	[InlineData(ServiceLifetime.Transient)]
	[InlineData(ServiceLifetime.Scoped)]
	public async Task GiveADocumentHandlerAFreshScopedInstancePerDispatch(ServiceLifetime handlerLifetime)
	{
		// The document path is the single-handler case of the same gap. Fixing only events would leave the
		// guarantee false and harder to find.
		using var provider = Build(handlerLifetime);
		var seen = await DispatchTwiceAsync(provider, () => new ScopeProbeDocument());

		AssertHandlerRan(seen, "document", expectedInvocations: 2);
		AssertDistinctAcrossDispatches(seen, "document", handlerLifetime);
	}

	// ---- LIVENESS: the scope must be opened for the handlers that need one, and NOT for those that ----
	// ---- do not. Every safety arm above is satisfied by an implementation that scopes everything.  ----

	[Fact]
	public async Task NotOpenAScopeForAHandlerThatDependsOnNothing()
	{
		// Two containers identical but for the handler's dependency graph, both with middleware, both
		// Transient so the handler's own lifetime forces nothing. The one whose handler reaches a Scoped
		// service must pay for a scope; the one whose handler reaches nothing must not.
		//
		// A blanket "always scope" implementation collapses the gap to nothing and fails here. So does an
		// implementation that opens no scope anywhere, which is the pre-fix state — so this arm is not a
		// free pass in either direction.
		var needsScope = await MeasureAllocationPerDispatchAsync(scopedDependency: true);
		var needsNone = await MeasureAllocationPerDispatchAsync(scopedDependency: false);

		var saved = needsScope - needsNone;

		saved.ShouldBeGreaterThanOrEqualTo(
			ScopeCostFloorBytes,
			$"a handler with a scoped dependency cost {needsScope:F0} B per dispatch and a handler with no "
			+ $"dependencies cost {needsNone:F0} B — a difference of {saved:F0} B, far less than a created "
			+ "scope. Either the handler that needs a scope is not getting one, or the handler that needs "
			+ "none is being charged for one anyway");
	}

	[Theory]
	[InlineData(ServiceLifetime.Transient)]
	[InlineData(ServiceLifetime.Scoped)]
	public async Task GiveEveryHandlerOfOneEventTheSameScopedInstance(ServiceLifetime handlerLifetime)
	{
		// The published semantic, stated as a contract rather than left as an implementation detail: one
		// scope per event, shared by all of its handlers, so the handling of a single event is one unit of
		// work. Opening a scope per handler would satisfy every safety arm above while silently handing two
		// handlers of the same event two different units of work.
		//
		// Both halves are asserted, because either alone is satisfiable by a wrong implementation: shared
		// WITHIN one publish, and fresh BETWEEN publishes.
		using var provider = Build(handlerLifetime);
		var seen = await DispatchTwiceAsync(provider, () => new ScopeProbeEvent());

		AssertHandlerRan(seen, "event", expectedInvocations: 4);

		var events = seen.Where(entry => entry.Path == "event").ToArray();
		var firstPublish = events.Take(2).ToArray();
		var secondPublish = events.Skip(2).Take(2).ToArray();

		firstPublish.Select(entry => entry.Tag).Distinct().Count().ShouldBe(
			2,
			"both registered handlers must run for one published event, or the sharing assertion below is "
			+ "comparing one handler against itself");

		firstPublish[1].Probe.ShouldBeSameAs(
			firstPublish[0].Probe,
			$"handlers {firstPublish[0].Tag} and {firstPublish[1].Tag} of a single published event observed "
			+ $"different scoped instances (#{firstPublish[0].Probe.Id} and #{firstPublish[1].Probe.Id}). All "
			+ "handlers for one event share one scope, so a consumer can treat handling that event as one "
			+ "unit of work");

		secondPublish[0].Probe.ShouldNotBeSameAs(
			firstPublish[0].Probe,
			$"the second published event reused scoped instance #{firstPublish[0].Probe.Id} from the first. "
			+ "Sharing a scope across the handlers of ONE event is the guarantee; sharing it across separate "
			+ "events is the captive dependency");
	}

	// ---- Helpers. ----

	private static void AssertHandlerRan(
		IReadOnlyList<(string Path, string Tag, IScopedProbe Probe)> seen,
		string path,
		int expectedInvocations)
	{
		// LIVENESS partner for every safety arm. A dispatcher that silently dropped the message would
		// capture nothing, and "no two instances were the same" would be vacuously true of an empty list.
		seen.Count(entry => entry.Path == path).ShouldBe(
			expectedInvocations,
			$"the {path} path must reach its handler(s) {expectedInvocations} times across two dispatches; a "
			+ "dispatch that never ran the handler proves nothing about the scope it would have run in");
	}

	private static void AssertDistinctAcrossDispatches(
		IReadOnlyList<(string Path, string Tag, IScopedProbe Probe)> seen,
		string path,
		ServiceLifetime handlerLifetime)
	{
		var probes = seen.Where(entry => entry.Path == path).Select(entry => entry.Probe).ToArray();
		var first = probes[0];
		var last = probes[^1];

		last.ShouldNotBeSameAs(
			first,
			$"two dispatches on the {path} path (handler registered {handlerLifetime}) handed the handler the "
			+ $"same scoped instance #{first.Id}. The dependency is registered Scoped, so this one was "
			+ "resolved from the root container and is now cached there for the life of the process — the "
			+ "captive dependency the published guarantee says cannot happen on any path");
	}

	/// <summary>
	/// Dispatches twice through <see cref="IDispatcher"/> from the <em>root</em> provider. No scope is
	/// opened by the caller and no ambient request scope exists, so any scope the handler runs in is one the
	/// dispatcher decided it owed — which is the whole question.
	/// </summary>
	private static async Task<IReadOnlyList<(string Path, string Tag, IScopedProbe Probe)>> DispatchTwiceAsync<TMessage>(
		ServiceProvider provider,
		Func<TMessage> message)
		where TMessage : IDispatchMessage
	{
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		_ = await dispatcher.DispatchAsync(message(), CancellationToken.None);
		_ = await dispatcher.DispatchAsync(message(), CancellationToken.None);

		return provider.GetRequiredService<ScopeRecorder>().Seen;
	}

	private static async Task<double> MeasureAllocationPerDispatchAsync(bool scopedDependency)
	{
		using var provider = Build(ServiceLifetime.Transient);
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		async Task DispatchAsync()
		{
			if (scopedDependency)
			{
				_ = await dispatcher.DispatchAsync(new ScopeProbeAction(), CancellationToken.None);
			}
			else
			{
				_ = await dispatcher.DispatchAsync(new DependencyFreeAction(), CancellationToken.None);
			}
		}

		for (var i = 0; i < 500; i++)
		{
			await DispatchAsync();
		}

		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();

		const int Iterations = 500;
		var threadBefore = Environment.CurrentManagedThreadId;
		var before = GC.GetAllocatedBytesForCurrentThread();

		for (var i = 0; i < Iterations; i++)
		{
			await DispatchAsync();
		}

		var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

		// GetAllocatedBytesForCurrentThread only counts this thread. If the dispatch went asynchronous and
		// resumed elsewhere the figure would silently undercount, so the measurement states its own
		// precondition rather than quietly reporting a number it cannot support.
		Environment.CurrentManagedThreadId.ShouldBe(
			threadBefore,
			"the measured dispatches must complete on the measuring thread, or the per-thread allocation "
			+ "counter has not observed all of the allocation it is being asked about");

		return allocated / (double)Iterations;
	}

	private static ServiceProvider Build(ServiceLifetime handlerLifetime)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddDispatch(dispatch =>
		{
			// THE POINT OF THIS LOCK. One middleware is enough to route an ordinary dispatch past every
			// gated fast-path site and onto the general path. Remove it and this whole class silently
			// re-tests the paths that were never broken.
			_ = dispatch.UseMiddleware<NoOpMiddleware>();

			// Explicit registration, at the lifetime under test, so each handler is registered exactly once
			// — zero-config discovery would add a second descriptor for the same interface and the event
			// fan-out would then invoke every handler twice.
			_ = dispatch.AddHandlersFromAssembly(typeof(ScopeProbeActionHandler).Assembly, handlerLifetime);
		});

		_ = services.AddScoped<IScopedProbe, ScopedProbe>();
		_ = services.AddSingleton<ScopeRecorder>();

		return services.BuildServiceProvider();
	}
}
