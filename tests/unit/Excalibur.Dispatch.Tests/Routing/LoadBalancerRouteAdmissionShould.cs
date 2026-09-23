// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Routing;
using Excalibur.Dispatch.Routing.Strategies;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Routing;

/// <summary>
/// Locks two properties that the snapshot-lifetime arms structurally cannot reach, because neither
/// depends on the cached snapshot at all: every route the caller supplies is ADMITTED as a candidate,
/// and selection stays correct when <see cref="ILoadBalancingStrategy.SelectRoute"/> is entered
/// concurrently.
/// </summary>
/// <remarks>
/// <para>
/// Both were found by an adversarial review rather than by a failing test, and both are the same shape:
/// a route is present in the caller's list and is nonetheless never returned, with no exception and no
/// log. A balancer that silently narrows its own candidate set looks healthy from every angle a
/// freshness or binding arm inspects.
/// </para>
/// <para>
/// The consistent-hash case is LATENT rather than live, and the distinction is recorded here so nobody
/// reads these arms as evidence of a shipped defect. <see cref="ConsistentHashLoadBalancer"/> is
/// internal, its virtual-node count defaults to 150, and no call site in the tree passes anything else,
/// so with the default the floor is never reached. The arms below pass the smaller value the
/// constructor openly invites, which is the state that was one argument away.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class LoadBalancerRouteAdmissionShould
{
	#region Consistent hash -- every supplied route reaches the ring

	/// <summary>
	/// SAFETY. A virtual-node count below the percentage scale still admits every route.
	/// </summary>
	/// <remarks>
	/// Ring placement was <c>virtualNodesPerRoute * weight / 100</c>, an integer division that floors to
	/// ZERO whenever the product is under 100 -- reachable with a configured 50 virtual nodes and the
	/// default weight of 1. Every route then contributed no nodes, the ring came out empty, and selection
	/// threw "Hash ring is empty" on every call from then on, for a perfectly well-formed non-empty route
	/// list. This arm fails with that exception against the unfloored expression.
	/// </remarks>
	[Fact]
	public void AdmitEveryRouteWhenTheVirtualNodeCountIsBelowTheScale()
	{
		var balancer = new ConsistentHashLoadBalancer(
			NullLogger<ConsistentHashLoadBalancer>.Instance,
			virtualNodesPerRoute: 50);

		var routes = Routes(("route-1", "host-1", 1), ("route-2", "host-2", 1), ("route-3", "host-3", 1));

		var selections = Enumerable.Range(0, 200)
			.Select(i => balancer.SelectRoute(routes, Context($"key-{i}")))
			.ToList();

		selections.ShouldAllBe(r => routes.Contains(r));

		// LIVENESS, and the half that catches the silent variant: it is not enough that selection stopped
		// throwing. Every route must actually be reachable, because a route that rounds to zero nodes is
		// excluded from the ring permanently while the others keep working.
		selections.Select(static r => r.RouteId).Distinct(StringComparer.Ordinal).Count()
			.ShouldBe(routes.Count, "a route in the caller's list was never selected, so it is absent from the ring");
	}

	/// <summary>
	/// SAFETY. A route whose weight rounds to zero is not silently dropped while its siblings survive.
	/// </summary>
	/// <remarks>
	/// The partial case is worse than the empty ring: nothing throws, most traffic routes correctly, and
	/// one route simply never receives any. That is invisible until someone asks why a host is idle.
	/// </remarks>
	[Fact]
	public void AdmitALowWeightedRouteThatRoundsToZeroNodes()
	{
		var balancer = new ConsistentHashLoadBalancer(
			NullLogger<ConsistentHashLoadBalancer>.Instance,
			virtualNodesPerRoute: 50);

		// The numbers are chosen so the routes land on OPPOSITE sides of the floor, which is what makes
		// this the partial case rather than a second copy of the empty-ring one: 50 * 100 / 100 == 50
		// nodes for each heavy route, and 50 * 1 / 100 == 0 for the light one. The ring is therefore
		// non-empty, nothing throws, most traffic routes correctly -- and one route is gone.
		var routes = Routes(("heavy-1", "host-h1", 100), ("heavy-2", "host-h2", 100), ("light", "host-light", 1));

		var selections = Enumerable.Range(0, 400)
			.Select(i => balancer.SelectRoute(routes, Context($"key-{i}")))
			.ToList();

		selections.ShouldAllBe(r => routes.Contains(r));
		selections.ShouldContain(r => r.RouteId == "light", "the low-weighted route never reached the ring");
	}

	/// <summary>
	/// LIVENESS CONTROL. The default configuration is unchanged by the floor.
	/// </summary>
	/// <remarks>
	/// The floor must not become "every route gets one node regardless": weighting still has to work, or
	/// the fix would have traded a silent exclusion for a silent flattening.
	/// </remarks>
	[Fact]
	public void StillFavourTheHeavierRouteAtTheDefaultNodeCount()
	{
		var balancer = new ConsistentHashLoadBalancer(NullLogger<ConsistentHashLoadBalancer>.Instance);
		var routes = Routes(("heavy", "host-heavy", 1000), ("light", "host-light", 100));

		var selections = Enumerable.Range(0, 600)
			.Select(i => balancer.SelectRoute(routes, Context($"key-{i}")))
			.ToList();

		var heavy = selections.Count(static r => r.RouteId == "heavy");
		var light = selections.Count(static r => r.RouteId == "light");

		light.ShouldBeGreaterThan(0, "the lighter route must still be reachable");
		heavy.ShouldBeGreaterThan(light, "the heavier route must still attract more traffic");
	}

	#endregion

	#region Random -- concurrent selection

	/// <summary>
	/// SAFETY and LIVENESS. Concurrent selection returns only supplied routes, and reaches all of them.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <see cref="RandomLoadBalancer"/> held a per-instance <see cref="Random"/> and called it from
	/// <c>SelectRoute</c>, which is entered concurrently by design. <see cref="Random"/>'s instance
	/// methods are documented as not thread-safe: concurrent use corrupts the internal state and the
	/// generator degenerates, which destroys the weight proportionality the type exists to provide. There
	/// is no exception and no log -- the distribution simply stops being one.
	/// </para>
	/// <para>
	/// Stated honestly: this arm is a REGRESSION lock, not a proof of thread-safety. Thread-safety is not
	/// provable by sampling, and a test that claimed otherwise would be the kind of false green this suite
	/// exists to prevent. What it does bind is that the type is exercised concurrently at all, and that
	/// concurrent use neither throws nor collapses onto a single route. The actual guarantee comes from
	/// <see cref="Random.Shared"/>, which is thread-safe by contract.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task SelectOnlySuppliedRoutesWhenEnteredConcurrently()
	{
		const int Workers = 8;
		const int IterationsPerWorker = 2_000;

		var balancer = new RandomLoadBalancer(NullLogger<RandomLoadBalancer>.Instance);
		var routes = Routes(("route-1", "host-1", 100), ("route-2", "host-2", 100), ("route-3", "host-3", 100));

		var violations = new ConcurrentBag<string>();
		var seen = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
		using var start = new Barrier(Workers);

		var work = Enumerable.Range(0, Workers).Select(worker => Task.Run(() =>
		{
			start.SignalAndWait();

			for (var i = 0; i < IterationsPerWorker; i++)
			{
				RouteDefinition selected;
				try
				{
					selected = balancer.SelectRoute(routes, new RoutingContext());
				}
				catch (Exception ex)
				{
					violations.Add($"worker {worker} threw {ex.GetType().Name}: {ex.Message}");
					return;
				}

				if (!routes.Contains(selected))
				{
					violations.Add($"worker {worker} received {selected.RouteId}, which is not in the supplied list");
					return;
				}

				_ = seen.TryAdd(selected.RouteId, 0);
			}
		})).ToArray();

		await Task.WhenAll(work);

		violations.ShouldBeEmpty();
		seen.Count.ShouldBe(routes.Count, "concurrent selection collapsed onto a subset of the routes");
	}

	#endregion

	private static List<RouteDefinition> Routes(params (string RouteId, string Endpoint, int Weight)[] routes) =>
		[.. routes.Select(static r => new RouteDefinition { RouteId = r.RouteId, Endpoint = r.Endpoint, Weight = r.Weight })];

	private static RoutingContext Context(string correlationId) => new() { CorrelationId = correlationId };
}
