// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.Delivery;

/// <summary>
/// The ultra-local path must cost the same whether or not the consumer also registered the concrete handler
/// type alongside the interface mapping.
/// </summary>
/// <remarks>
/// <para>
/// <c>AddTransient&lt;IActionHandler&lt;TCommand&gt;, THandler&gt;()</c> is how a handler is registered.
/// Additionally registering the concrete type is unusual, and no documentation asks for it. If the leaner
/// path is only reached by the second, undocumented registration, then the published figure describes a
/// configuration a consumer will not naturally write — and nothing tells them. That is why this is a
/// correctness lock and not a performance preference: the number we publish has to be the number they get.
/// </para>
/// <para>
/// These arms assert a <em>relationship between containers</em>, not a constant. Each pair differs in
/// exactly one respect — how the handler is registered — so any allocation one pays beyond the other is
/// attributable to the registration shape alone. Nothing here hardcodes the published figure, so the lock
/// stays true if the baseline legitimately moves — it only fails if the shapes diverge, or if a
/// dependency-free handler starts paying for a scope.
/// </para>
/// <para>
/// Three shapes, because there are two independent registration <em>paths</em> and a lock only proves the
/// path it builds. Manual registration alongside <c>AddDispatch()</c> and explicit scanning
/// (<c>AddDispatch(d =&gt; d.AddHandlersFromAssembly(asm))</c>) are separate code paths with separate
/// lifetime defaults, and the documentation teaches the second. A container built only the first way cannot
/// observe the two disagreeing.
/// </para>
/// <para>
/// Allocation, not latency, is the measurement. It is deterministic on a given build, which makes an
/// equality assertion sound here in a way a timing assertion never would be.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InterfaceOnlyRegistrationAllocationShould
{
	/// <summary>Warmup dispatches, to retire JIT, plan resolution and the scope-verdict cache.</summary>
	private const int WarmupDispatches = 2000;

	/// <summary>Measured dispatches. Allocation is deterministic, so this averages nothing away.</summary>
	private const int MeasuredDispatches = 1000;

	/// <summary>
	/// Slack on the equality assertion. Allocation is deterministic per dispatch, so the two shapes should
	/// agree exactly; a few bytes absorb a one-off cost that survived warmup without admitting a per-dispatch
	/// scope (hundreds of bytes) or the measured 472 B regression.
	/// </summary>
	private const double EqualityToleranceBytes = 8;

	private sealed record ShapeProbeCommand : IDispatchAction
	{
		public int Value { get; init; }
	}

	/// <summary>
	/// A handler with no constructor dependencies. There is nothing for it to capture, so no registration
	/// shape can honestly require a scope for it — which is what makes any difference between the two
	/// containers attributable to the registration shape rather than to the handler.
	/// </summary>
	private sealed class ShapeProbeCommandHandler : IActionHandler<ShapeProbeCommand>
	{
		internal static int Invocations;

		internal static int Sum;

		public Task HandleAsync(ShapeProbeCommand message, CancellationToken cancellationToken)
		{
			// Real work, however small: a dispatcher that silently no-oped would allocate nothing at all and
			// would otherwise sail through every allocation assertion below.
			_ = Interlocked.Add(ref Sum, message.Value);
			_ = Interlocked.Increment(ref Invocations);
			return Task.CompletedTask;
		}
	}

	private enum RegistrationShape
	{
		/// <summary>The interface mapping only, over zero-config discovery — the idiomatic registration.</summary>
		InterfaceOnly,

		/// <summary>The interface mapping plus the concrete type — the shape the figure was measured on.</summary>
		InterfaceAndConcrete,

		/// <summary>
		/// <c>AddDispatch(d =&gt; d.AddHandlersFromAssembly(asm))</c> — the registration our own getting-started
		/// documentation teaches, and a path no other arm in this class constructs.
		/// </summary>
		ExplicitAssemblyScan,
	}

	// ---- SAFETY. The registration shape must not change what a dispatch costs. ----

	[Fact]
	public async Task CostTheSamePerDispatchWithOrWithoutTheConcreteHandlerRegistration()
	{
		// Both containers are byte-for-byte identical apart from one registration line, and the handler in
		// both has no dependencies at all. Any gap between them is the registration shape and nothing else.
		var withConcrete = await MeasureAllocationPerDispatchAsync(RegistrationShape.InterfaceAndConcrete);
		var interfaceOnly = await MeasureAllocationPerDispatchAsync(RegistrationShape.InterfaceOnly);

		var delta = interfaceOnly - withConcrete;

		delta.ShouldBeLessThanOrEqualTo(
			EqualityToleranceBytes,
			$"registering only the handler interface cost {interfaceOnly:F0} B per dispatch against "
			+ $"{withConcrete:F0} B when the concrete type was also registered — a {delta:F0} B penalty for "
			+ "writing the registration the documentation actually teaches. Either both shapes reach the same "
			+ "path, or the published ultra-local figure has to name the registration it requires");
	}

	[Fact]
	public async Task StayBelowTheCostOfAPerDispatchScopeWhenOnlyTheInterfaceIsRegistered()
	{
		// The absolute arm. The equality assertion above is satisfied if both shapes become equally expensive;
		// this one refuses that escape. A handler with no constructor arguments cannot capture anything, so
		// this dispatch must not be paying for a scope under any registration shape.
		var interfaceOnly = await MeasureAllocationPerDispatchAsync(RegistrationShape.InterfaceOnly);

		_ = interfaceOnly;

		// The property, observed rather than inferred from a byte total: no scope is taken at all. A
		// ceiling on total allocation cannot express this -- the path legitimately allocates for reasons
		// that have nothing to do with scoping, so the bound fails for costs it was never about while a
		// real scope could hide beneath a generous one.
		(await ScopePathEntriesAsync(RegistrationShape.InterfaceOnly)).ShouldBe(
			0,
			"a handler with no constructor dependencies, registered the ordinary way, must not drive the "
			+ "dispatch into the scope-taking branch -- there is nothing for a scope to hold");
	}

	[Fact]
	public async Task CostTheSamePerDispatchWhenHandlersAreRegisteredByExplicitAssemblyScan()
	{
		// SAFETY, and a path the two arms above cannot see. They both build their container with
		// AddDispatch() plus a manual registration — the hand-registered path. A consumer following
		// our getting-started documentation writes AddDispatch(d => d.AddHandlersFromAssembly(asm)) instead,
		// which is a DIFFERENT registration path with its own lifetime default. A lock proves the path it
		// constructs; it says nothing about a path it never builds, so a discrepancy BETWEEN the two paths
		// is invisible to every other assertion in this class.
		//
		// Both containers are otherwise identical and the handler has no dependencies in either, so the
		// documented registration must not cost more than the one nothing documents.
		var handRegistered = await MeasureAllocationPerDispatchAsync(RegistrationShape.InterfaceOnly);
		var explicitScan = await MeasureAllocationPerDispatchAsync(RegistrationShape.ExplicitAssemblyScan);

		var delta = explicitScan - handRegistered;

		delta.ShouldBeLessThanOrEqualTo(
			EqualityToleranceBytes,
			$"registering handlers the way the documentation teaches — AddHandlersFromAssembly(asm) — cost "
			+ $"{explicitScan:F0} B per dispatch against {handRegistered:F0} B for hand registration of the "
			+ $"same handler, a {delta:F0} B penalty. Two registration paths for one framework must not "
			+ "disagree about what a dependency-free handler costs, and the documented one is the wrong one "
			+ "to be slower");

		// Matching the other path is not sufficient if both take a scope, so observe the property itself.
		(await ScopePathEntriesAsync(RegistrationShape.ExplicitAssemblyScan)).ShouldBe(
			0,
			"the documented registration path must not drive a dependency-free handler into the "
			+ "scope-taking branch");
	}

	/// <summary>
	/// LIVENESS for the scope-path assertions above. They are satisfied by a probe wired to nothing and
	/// by a resolver that never scopes for anyone; this arm proves the same probe, under the same
	/// registration path, moves when a handler genuinely needs a scope.
	/// </summary>
	[Fact]
	public async Task DriveTheScopeTakingBranchWhenTheHandlerReachesAScopedService()
	{
		var probe = new ScopePathProbe();
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch();
		_ = services.AddSingleton<IDispatchAmbientScopeAccessor>(probe);
		_ = services.AddScoped<IScopedShapeDependency, ScopedShapeDependency>();
		_ = services.AddTransient<IActionHandler<ShapeProbeCommand>, ScopedShapeProbeCommandHandler>();
		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		for (var i = 0; i < 10; i++)
		{
			_ = await dispatcher.DispatchAsync(new ShapeProbeCommand { Value = i }, CancellationToken.None);
		}

		probe.ScopePathEntries.ShouldBeGreaterThan(
			0,
			"a handler whose constructor reaches a scoped service MUST drive the dispatch into the "
			+ "scope-taking branch; if this is zero the probe observes nothing and every zero asserted "
			+ "above proves nothing");
	}

	private interface IScopedShapeDependency
	{
		int Value { get; }
	}

	private sealed class ScopedShapeDependency : IScopedShapeDependency
	{
		public int Value => 1;
	}

	private sealed class ScopedShapeProbeCommandHandler(IScopedShapeDependency dependency)
		: IActionHandler<ShapeProbeCommand>
	{
		public Task HandleAsync(ShapeProbeCommand message, CancellationToken cancellationToken)
		{
			_ = dependency.Value;
			return Task.CompletedTask;
		}
	}

	// ---- LIVENESS. Without this, a dispatcher that silently dropped the message would allocate ----
	// ---- nothing and pass every arm above.                                                     ----

	[Fact]
	public async Task ActuallyInvokeTheHandlerUnderEveryRegistrationShape()
	{
		foreach (var shape in Enum.GetValues<RegistrationShape>())
		{
			using var provider = Build(shape);
			var dispatcher = provider.GetRequiredService<IDispatcher>();

			var invocationsBefore = Volatile.Read(ref ShapeProbeCommandHandler.Invocations);
			var sumBefore = Volatile.Read(ref ShapeProbeCommandHandler.Sum);

			for (var i = 1; i <= 10; i++)
			{
				_ = await dispatcher.DispatchAsync(new ShapeProbeCommand { Value = i }, CancellationToken.None);
			}

			Volatile.Read(ref ShapeProbeCommandHandler.Invocations).ShouldBe(
				invocationsBefore + 10,
				$"ten dispatches under {shape} must reach the handler ten times; an allocation figure from a "
				+ "dispatcher that was not running the handler measures nothing");
			Volatile.Read(ref ShapeProbeCommandHandler.Sum).ShouldBe(
				sumBefore + 55,
				$"the handler must observe each message it was given under {shape}, not merely be entered");
		}
	}

	private static async Task<double> MeasureAllocationPerDispatchAsync(RegistrationShape shape)
	{
		using var provider = Build(shape);
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		for (var i = 0; i < WarmupDispatches; i++)
		{
			_ = await dispatcher.DispatchAsync(new ShapeProbeCommand { Value = i }, CancellationToken.None);
		}

		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();

		var invocationsBefore = Volatile.Read(ref ShapeProbeCommandHandler.Invocations);
		var before = GC.GetAllocatedBytesForCurrentThread();

		for (var i = 0; i < MeasuredDispatches; i++)
		{
			_ = await dispatcher.DispatchAsync(new ShapeProbeCommand { Value = i }, CancellationToken.None);
		}

		var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

		// Guards the measurement itself: a run that did not dispatch what it thought it dispatched would
		// report a small, clean, meaningless number.
		Volatile.Read(ref ShapeProbeCommandHandler.Invocations).ShouldBe(
			invocationsBefore + MeasuredDispatches,
			$"the measured window under {shape} must contain exactly the dispatches it counted");

		return allocated / (double)MeasuredDispatches;
	}

	/// <summary>Dispatches under <paramref name="shape"/> and reports how often a scope was taken.</summary>
	private static async Task<int> ScopePathEntriesAsync(RegistrationShape shape)
	{
		var probe = new ScopePathProbe();
		using var provider = Build(shape, probe);
		var dispatcher = provider.GetRequiredService<IDispatcher>();
		for (var i = 0; i < 100; i++)
		{
			_ = await dispatcher.DispatchAsync(new ShapeProbeCommand { Value = i }, CancellationToken.None);
		}

		return probe.ScopePathEntries;
	}

	private static ServiceProvider Build(RegistrationShape shape) => Build(shape, probe: null);

	private static ServiceProvider Build(RegistrationShape shape, ScopePathProbe? probe)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		if (probe is not null)
		{
			_ = services.AddSingleton<IDispatchAmbientScopeAccessor>(probe);
		}

		// Identical in every arm: the Direct profile with every optional feature off, matching the container
		// the published ultra-local figure was measured on. THE ONLY DIFFERENCE IS HOW THE HANDLER IS
		// REGISTERED.
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

			if (shape == RegistrationShape.ExplicitAssemblyScan)
			{
				// Verbatim the form the documentation teaches (getting-started and the intro both show
				// exactly this single-argument call). A consumer on this path writes no other registration,
				// so neither does this arm.
				_ = dispatch.AddHandlersFromAssembly(typeof(ShapeProbeCommandHandler).Assembly);
			}
		});

		if (shape != RegistrationShape.ExplicitAssemblyScan)
		{
			if (shape == RegistrationShape.InterfaceAndConcrete)
			{
				_ = services.AddTransient<ShapeProbeCommandHandler>();
			}

			_ = services.AddTransient<IActionHandler<ShapeProbeCommand>, ShapeProbeCommandHandler>();
		}

		return services.BuildServiceProvider();
	}
}
