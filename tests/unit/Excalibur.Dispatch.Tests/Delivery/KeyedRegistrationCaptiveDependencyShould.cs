// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.Delivery;

/// <summary>
/// A keyed registration must never change the scope verdict for a non-keyed dependency, because a keyed
/// descriptor is not reachable through the bare service type at all.
/// </summary>
/// <remarks>
/// <para>
/// <c>GetService(IProbeDependency)</c> never returns a keyed registration — only
/// <c>GetKeyedService(IProbeDependency, key)</c> does. So a container that records a keyed descriptor's
/// lifetime under the bare service type describes a resolution that cannot happen. When the keyed
/// descriptor happens to be registered last and claims a root-safe lifetime, it masks the real, non-keyed
/// Scoped registration underneath it: the handler is then judged root-safe and resolved from the root
/// container, and its Scoped dependency is resolved from the root scope — where it is cached for the life
/// of the process. Every dispatch receives the same instance. That is the captive dependency the scope
/// machinery exists to prevent, arrived at through the one path that reports itself as safe.
/// </para>
/// <para>
/// These arms assert the observable property only — <em>does dispatch N+1 see the same dependency instance
/// as dispatch N?</em> — never the lifetime a registry happens to record or the shape of the component that
/// decides it. A different but correct implementation must keep them green.
/// </para>
/// <para>
/// The arms are ordered pairs. Registering the keyed descriptor <em>last</em> is the failing direction;
/// registering it <em>first</em> is the same container in the other order and must stay correct. Together
/// they bind the defect to what it actually is — a last-wins write that is blind to keying — rather than to
/// the mere presence of a keyed registration.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class KeyedRegistrationCaptiveDependencyShould
{
	private const string DependencyKey = "cache";

	private sealed record CaptiveProbeCommand : IDispatchAction
	{
		public int Value { get; init; }
	}

	private sealed record DependencyFreeProbeCommand : IDispatchAction
	{
		public int Value { get; init; }
	}

	private interface IProbeDependency
	{
		int Id { get; }
	}

	private sealed class ProbeDependency : IProbeDependency
	{
		private static int s_next;

		public int Id { get; } = Interlocked.Increment(ref s_next);
	}

	/// <summary>
	/// Collects the dependency instance each dispatch actually received. Registered Singleton, so the
	/// dependency graph the scope verdict walks still turns entirely on <see cref="IProbeDependency"/>.
	/// </summary>
	private sealed class DependencyRecorder
	{
		private readonly List<IProbeDependency> _seen = [];

		public void Record(IProbeDependency dependency)
		{
			lock (_seen)
			{
				_seen.Add(dependency);
			}
		}

		public IReadOnlyList<IProbeDependency> Seen
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

	private sealed class CaptiveProbeHandler(IProbeDependency dependency, DependencyRecorder recorder)
		: IActionHandler<CaptiveProbeCommand>
	{
		public Task HandleAsync(CaptiveProbeCommand message, CancellationToken cancellationToken)
		{
			recorder.Record(dependency);
			return Task.CompletedTask;
		}
	}

	/// <summary>A handler with no constructor dependencies: nothing scoped is reachable from it.</summary>
	private sealed class DependencyFreeProbeHandler : IActionHandler<DependencyFreeProbeCommand>
	{
		public Task HandleAsync(DependencyFreeProbeCommand message, CancellationToken cancellationToken) =>
			Task.CompletedTask;
	}

	// ---- SAFETY. The measured failing arms: a keyed descriptor registered last must not mask the ----
	// ---- non-keyed Scoped registration it shares a service type with.                            ----

	[Fact]
	public async Task GiveAFreshDependencyPerDispatchWhenAKeyedSingletonIsRegisteredLast()
	{
		// AddScoped<IProbeDependency, ProbeDependency>() then AddKeyedSingleton(..., "cache").
		// The keyed descriptor is last and claims Singleton. If it is recorded under the bare service type,
		// the handler's only meaningful dependency reads as root-safe, the walk prunes the subtree, the
		// handler resolves from the root container, and both dispatches share one captive instance.
		using var provider = Build(services =>
		{
			_ = services.AddScoped<IProbeDependency, ProbeDependency>();
			_ = services.AddKeyedSingleton<IProbeDependency, ProbeDependency>(DependencyKey);
		});

		var seen = await DispatchTwiceAsync(provider);

		seen.Count.ShouldBe(2, "both dispatches must reach the handler for the comparison to mean anything");
		seen[1].ShouldNotBeSameAs(
			seen[0],
			$"the handler's Scoped dependency (instance #{seen[0].Id}) was reused across two dispatches, so it "
			+ "was resolved from the root container and is captive. A keyed descriptor is not reachable through "
			+ "the bare service type, so it must not be able to make a non-keyed Scoped registration look "
			+ "root-safe");
	}

	[Fact]
	public async Task GiveAFreshDependencyPerDispatchWhenAKeyedTransientIsRegisteredLast()
	{
		// The same masking, reached by a different route: recorded as Transient, the walk does not prune —
		// it recurses into ProbeDependency's parameterless constructor, finds nothing scoped, and concludes
		// root-safe. Two mechanisms, one property, so a fix that only special-cases the Singleton branch is
		// still red here.
		using var provider = Build(services =>
		{
			_ = services.AddScoped<IProbeDependency, ProbeDependency>();
			_ = services.AddKeyedTransient<IProbeDependency, ProbeDependency>(DependencyKey);
		});

		var seen = await DispatchTwiceAsync(provider);

		seen.Count.ShouldBe(2, "both dispatches must reach the handler for the comparison to mean anything");
		seen[1].ShouldNotBeSameAs(
			seen[0],
			$"the handler's Scoped dependency (instance #{seen[0].Id}) was reused across two dispatches. A keyed "
			+ "Transient masking the non-keyed Scoped registration is the same defect as a keyed Singleton doing "
			+ "so — it reaches the wrong verdict through the walk instead of through the prune");
	}

	// ---- LIVENESS. Without these, a resolver that stopped resolving anything, or one that could no ----
	// ---- longer see a Scoped registration at all, would satisfy every safety arm above.            ----

	[Fact]
	public async Task GiveAFreshDependencyPerDispatchWithNoKeyedRegistrationAtAll()
	{
		// The control. An ordinary Scoped dependency, no keyed registration anywhere: the scope machinery
		// must be working and must already be producing a fresh instance per dispatch. If this arm is red the
		// safety arms above prove nothing, because "distinct" would not be reachable in any configuration.
		using var provider = Build(services => services.AddScoped<IProbeDependency, ProbeDependency>());

		var seen = await DispatchTwiceAsync(provider);

		seen.Count.ShouldBe(2, "both dispatches must reach the handler; a dispatcher that silently dropped the "
			+ "message would leave nothing to compare and must not read as a pass");
		seen[1].ShouldNotBeSameAs(
			seen[0],
			"a handler taking a Scoped dependency must be resolved from a scope, so each dispatch gets its own "
			+ "instance. This is the behaviour the scope machinery exists to provide and it must keep working");
	}

	[Fact]
	public async Task GiveAFreshDependencyPerDispatchWhenTheKeyedRegistrationComesFirst()
	{
		// The same two registrations in the other order. This is correct today and must stay correct: it is
		// what makes the defect a last-wins ordering fault rather than "keyed registrations break dispatch",
		// and it stops a fix that simply refuses to run whenever a keyed descriptor is present.
		using var provider = Build(services =>
		{
			_ = services.AddKeyedSingleton<IProbeDependency, ProbeDependency>(DependencyKey);
			_ = services.AddScoped<IProbeDependency, ProbeDependency>();
		});

		var seen = await DispatchTwiceAsync(provider);

		seen.Count.ShouldBe(2, "both dispatches must reach the handler for the comparison to mean anything");
		seen[1].ShouldNotBeSameAs(
			seen[0],
			"registering the keyed descriptor first already produces the correct verdict; a fix for the "
			+ "keyed-last case must not regress the keyed-first case");
	}

	[Fact]
	public async Task NotCreateAScopePerDispatchForADependencyFreeHandlerWhenKeyedDescriptorsArePresent()
	{
		// LIVENESS against the cheapest wrong fix. Every arm above is satisfied by a resolver that creates a
		// scope for absolutely everything, which would be safe, correct, and would make all consumers pay for
		// a scope none of their handlers required. A handler with no constructor arguments has nothing to
		// capture, so the presence of keyed descriptors elsewhere in the container must not buy it a scope.
		//
		// Allocation is the observable that separates the two: a created IServiceScope and its scoped
		// provider cost several hundred bytes. Allocation is deterministic, unlike timing.
		using var provider = Build(services =>
		{
			_ = services.AddScoped<IProbeDependency, ProbeDependency>();
			_ = services.AddKeyedSingleton<IProbeDependency, ProbeDependency>(DependencyKey);
		});

		var dispatcher = provider.GetRequiredService<IDispatcher>();

		for (var i = 0; i < 200; i++)
		{
			_ = await dispatcher.DispatchAsync(new DependencyFreeProbeCommand { Value = i }, CancellationToken.None);
		}

		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();

		const int Iterations = 200;
		var before = GC.GetAllocatedBytesForCurrentThread();
		for (var i = 0; i < Iterations; i++)
		{
			_ = await dispatcher.DispatchAsync(new DependencyFreeProbeCommand { Value = i }, CancellationToken.None);
		}

		var perDispatch = (GC.GetAllocatedBytesForCurrentThread() - before) / (double)Iterations;

		perDispatch.ShouldBeLessThan(
			ScopeCostFloorBytes,
			$"dispatching a handler with no constructor dependencies allocated {perDispatch:F0} B, which is in "
			+ "the range of a created scope. Keeping keyed descriptors from masking a Scoped registration must "
			+ "not be done by scoping everything");
	}

	/// <summary>
	/// A conservative lower bound on what a created <c>IServiceScope</c> plus its scoped provider costs. Set
	/// well under the observed cost of a scope and well over the cost of a scope-free dispatch, so the bound
	/// separates the two cases without pinning a figure that ordinary churn would flap.
	/// </summary>
	private const int ScopeCostFloorBytes = 400;

	private static ServiceProvider Build(Action<IServiceCollection> registerDependency)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// The Direct profile with every optional feature off — the leanest configuration the framework
		// offers. A defect visible here is visible in every configuration above it.
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

		_ = services.AddSingleton<DependencyRecorder>();
		registerDependency(services);

		// Registered Transient, as the bug requires: a Scoped handler short-circuits the verdict before the
		// dependency walk runs, which is what masks this defect for assembly-discovered handlers today.
		_ = services.AddTransient<CaptiveProbeHandler>();
		_ = services.AddTransient<IActionHandler<CaptiveProbeCommand>, CaptiveProbeHandler>();
		_ = services.AddTransient<DependencyFreeProbeHandler>();
		_ = services.AddTransient<IActionHandler<DependencyFreeProbeCommand>, DependencyFreeProbeHandler>();

		return services.BuildServiceProvider();
	}

	/// <summary>
	/// Dispatches twice through <see cref="IDispatcher"/> from the <em>root</em> provider — no scope is
	/// opened by the caller, so whatever scope the handler gets is the one the dispatcher decided it needed.
	/// </summary>
	private static async Task<IReadOnlyList<IProbeDependency>> DispatchTwiceAsync(ServiceProvider provider)
	{
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		_ = await dispatcher.DispatchAsync(new CaptiveProbeCommand { Value = 1 }, CancellationToken.None);
		_ = await dispatcher.DispatchAsync(new CaptiveProbeCommand { Value = 2 }, CancellationToken.None);

		return provider.GetRequiredService<DependencyRecorder>().Seen;
	}
}
