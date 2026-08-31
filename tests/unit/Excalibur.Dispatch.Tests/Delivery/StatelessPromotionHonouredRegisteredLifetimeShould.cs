// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.Delivery;

/// <summary>
/// Substituting one shared handler instance for per-dispatch activation changes the lifetime the consumer
/// registered, so it is permitted only where the consumer expressed no preference — a
/// <see cref="ServiceLifetime.Transient"/> registration, which is the discovery default. A consumer who
/// writes <c>AddScoped</c> or <c>AddSingleton</c> has departed from that default deliberately and must get
/// exactly what Microsoft dependency injection defines for it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scoped is the serious half.</b> Scoped is chosen precisely when per-request isolation matters. Handing
/// every dispatch one shared instance takes that isolation away silently: nothing throws, nothing logs, and
/// the handler simply stops being per-request. A consumer would discover it as cross-request state bleed in
/// production, not as a failure here.
/// </para>
/// <para>
/// <b>Every arm asserts instance identity through <see cref="IDispatcher"/> and nothing else.</b> No arm
/// reads a lifetime, consults a cache, names a promotion method, or asserts that any particular gate exists.
/// The property under test is what two dispatches observe, so an implementation that reaches the same
/// guarantee by a different mechanism stays green — and one that reaches a different guarantee by the
/// expected mechanism goes red.
/// </para>
/// <para>
/// <b>The handlers are registered after <c>AddDispatch</c> on purpose, and that placement is load-bearing.</b>
/// There are two promotion paths. The dependency-injection rewrite runs inside <c>AddDispatch</c>'s build
/// step and has always skipped any descriptor that is not Transient, so it was never the defect. The runtime
/// path, which substitutes a cached instance during dispatch, never consulted the registered lifetime at all.
/// Registering the handlers afterwards puts them out of the rewrite's reach and leaves the runtime path as
/// the only thing that can promote them — so these arms bind the path that carries the defect rather than
/// the sibling that was already correct.
/// </para>
/// <para>
/// <b>Both directions are asserted.</b> The safety arms alone are satisfiable by deleting the optimisation
/// outright, which would trade a correctness defect for a performance regression; the liveness arm at
/// <see cref="ShareOneInstanceForATransientHandlerWhenPromotionIsEnabled"/> forbids that. Every arm also
/// counts invocations, because a dispatcher that silently dropped the message would produce a stable,
/// entirely fictional identity and pass several arms above it.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class StatelessPromotionHonouredRegisteredLifetimeShould
{
	private sealed record LifetimeProbeAction : IDispatchAction;

	/// <summary>
	/// The shape promotion is gated on: a parameterless constructor and no instance fields, so the framework
	/// can prove sharing one instance is safe. It reports its own identity to a static sink because holding
	/// a collaborator in a field would make it stateful and take it outside the gate — the handler cannot
	/// record what it is without being the kind of handler this test is about.
	/// </summary>
	private sealed class LifetimeProbeHandler : IActionHandler<LifetimeProbeAction>
	{
		public Task HandleAsync(LifetimeProbeAction message, CancellationToken cancellationToken)
		{
			Probe.Record(this);
			return Task.CompletedTask;
		}
	}

	// ---- SAFETY. A lifetime the consumer chose deliberately must be delivered as Microsoft DI defines it. ----

	[Fact]
	public async Task GiveAScopedHandlerAFreshInstancePerDispatch()
	{
		// The defect that matters. Scoped means per-scope, and two dispatches with no ambient scope are two
		// scopes. A consumer registers Scoped to get isolation between units of work; sharing one instance
		// across every dispatch removes that isolation without any diagnostic, so the first sign of it is
		// state from one request being visible in the next.
		var seen = await DispatchTwiceAsync(autoPromote: true, ServiceLifetime.Scoped);

		seen[1].ShouldNotBeSameAs(
			seen[0],
			"a handler registered Scoped was handed to two separate dispatches as ONE instance. Scoped is a "
			+ "deliberate departure from the Transient default and is chosen precisely for per-request "
			+ "isolation, so substituting a shared instance silently gives the consumer a lifetime they did "
			+ "not ask for and cannot see they are not getting");
	}

	[Fact]
	public async Task GiveASingletonHandlerTheSameInstanceOnEveryDispatch()
	{
		// The other half of honouring the registration, and the one a careless fix breaks. Restricting
		// promotion to Transient must not be implemented as "activate per dispatch unless promoted", which
		// would start activating a Singleton per dispatch and violate the registration in the other
		// direction.
		var seen = await DispatchTwiceAsync(autoPromote: true, ServiceLifetime.Singleton);

		seen[1].ShouldBeSameAs(
			seen[0],
			"a handler registered Singleton was activated twice across two dispatches. Singleton is also a "
			+ "deliberate departure from the default and means one instance for the life of the container; "
			+ "honouring Scoped must not be achieved by activating everything that was not promoted");
	}

	[Fact]
	public async Task GiveATransientHandlerAFreshInstanceWhenPromotionIsDisabled()
	{
		// The option must control the runtime path, not merely the dependency-injection rewrite. A consumer
		// who finds the switch, reads what it claims, and turns it off is entitled to have turned it off; a
		// documented control that changes nothing is worse than no control, because it ends the consumer's
		// search for a way out.
		var seen = await DispatchTwiceAsync(autoPromote: false, ServiceLifetime.Transient);

		seen[1].ShouldNotBeSameAs(
			seen[0],
			"promotion was disabled and a Transient handler was still shared between two dispatches. The "
			+ "option is the supported way to opt out of the optimisation, so a consumer who sets it false "
			+ "and still gets one shared instance has no remaining way to get the lifetime they registered");
	}

	// ---- LIVENESS. Every safety arm above is satisfied by removing the optimisation entirely. ----

	[Fact]
	public async Task ShareOneInstanceForATransientHandlerWhenPromotionIsEnabled()
	{
		// This is the arm that makes the lock non-trivial. Deleting promotion outright would turn all three
		// safety arms green while throwing away the performance the feature exists for — a regression
		// wearing a correctness fix's clothes. Transient is the discovery default, so a consumer who writes
		// AddTransient has asked for nothing different from what they would have received anyway; that is
		// the one registration promotion is entitled to change, and it must still change it.
		var seen = await DispatchTwiceAsync(autoPromote: true, ServiceLifetime.Transient);

		seen[1].ShouldBeSameAs(
			seen[0],
			"a stateless Transient handler was activated twice with promotion enabled, so the optimisation is "
			+ "no longer firing anywhere. Restricting promotion to Transient must narrow which registrations "
			+ "it may change, not switch it off");
	}

	// ---- The option and the lifetime rule must COMPOSE: neither may mask the other. ----

	[Theory]
	[InlineData(ServiceLifetime.Scoped)]
	[InlineData(ServiceLifetime.Singleton)]
	public async Task HonourANonTransientRegistrationWhenPromotionIsDisabledToo(ServiceLifetime handlerLifetime)
	{
		// Two independent reasons not to promote must not be implemented as one. An option check that
		// short-circuits before the lifetime check, or a lifetime check that only runs when the option is on,
		// would both pass the arms above while leaving a case where one gate silently stands in for the
		// other. Turning the optimisation off cannot change what a Scoped or Singleton registration means.
		var seen = await DispatchTwiceAsync(autoPromote: false, handlerLifetime);

		if (handlerLifetime == ServiceLifetime.Scoped)
		{
			seen[1].ShouldNotBeSameAs(
				seen[0],
				"a Scoped handler was shared between two dispatches with promotion already disabled, so the "
				+ "registration is being overridden by something other than the promotion the option names");
		}
		else
		{
			seen[1].ShouldBeSameAs(
				seen[0],
				"a Singleton handler was activated twice with promotion disabled. Disabling an optimisation "
				+ "must not change what a registered lifetime means");
		}
	}

	// ---- Helpers. ----

	/// <summary>
	/// Dispatches twice through <see cref="IDispatcher"/> from the root provider and returns the handler
	/// instances that actually ran, having first established that the handler ran at all.
	/// </summary>
	/// <remarks>
	/// The invocation count is asserted here rather than in each arm because it is the liveness partner every
	/// identity assertion needs: a dispatcher that dropped the message would record nothing, and a dispatcher
	/// that ran the handler once would let a single instance be compared against itself. Both would satisfy
	/// "the two instances were the same" without the handler ever having been dispatched twice.
	/// </remarks>
	private static async Task<IReadOnlyList<object>> DispatchTwiceAsync(bool autoPromote, ServiceLifetime handlerLifetime)
	{
		Probe.Reset();

		using var provider = Build(autoPromote, handlerLifetime);
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		_ = await dispatcher.DispatchAsync(new LifetimeProbeAction(), CancellationToken.None);
		_ = await dispatcher.DispatchAsync(new LifetimeProbeAction(), CancellationToken.None);

		var seen = Probe.Captured;

		seen.Count.ShouldBe(
			2,
			$"the handler must run exactly once per dispatch and ran {seen.Count} times across two dispatches "
			+ $"(promotion {(autoPromote ? "enabled" : "disabled")}, registered {handlerLifetime}). Comparing "
			+ "instance identity says nothing about a lifetime unless both dispatches actually reached the "
			+ "handler");

		return seen;
	}

	private static ServiceProvider Build(bool autoPromote, ServiceLifetime handlerLifetime)
	{
		IServiceCollection services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddDispatch(dispatch =>
			dispatch.WithOptions(options =>
				options.CrossCutting.Performance.AutoPromoteStatelessHandlersToSingleton = autoPromote));

		// Deliberately after AddDispatch: see the class remarks. This keeps the descriptor-rewriting
		// promotion path — which already honours the rule — away from these handlers, so the runtime path
		// is the only thing that could share an instance here. Both descriptors carry the lifetime under
		// test, because the concrete registration is what makes the handler self-registered and the
		// interface registration is what the dispatch resolves.
		services.Add(new ServiceDescriptor(typeof(LifetimeProbeHandler), typeof(LifetimeProbeHandler), handlerLifetime));
		services.Add(new ServiceDescriptor(
			typeof(IActionHandler<LifetimeProbeAction>),
			typeof(LifetimeProbeHandler),
			handlerLifetime));

		return services.BuildServiceProvider();
	}

	/// <summary>
	/// Collects the handler instances that ran. Static because the handler under test must have no instance
	/// fields to be eligible for promotion at all, so it cannot hold a recorder of its own. Safe as static
	/// state because this assembly runs tests sequentially, and each measurement resets it first.
	/// </summary>
	private static class Probe
	{
		private static readonly List<object> Seen = [];

		public static void Reset()
		{
			lock (Seen)
			{
				Seen.Clear();
			}
		}

		public static void Record(object handler)
		{
			lock (Seen)
			{
				Seen.Add(handler);
			}
		}

		public static IReadOnlyList<object> Captured
		{
			get
			{
				lock (Seen)
				{
					return [.. Seen];
				}
			}
		}
	}
}
