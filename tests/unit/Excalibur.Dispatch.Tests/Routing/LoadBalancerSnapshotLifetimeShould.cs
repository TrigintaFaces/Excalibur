// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections;

using Excalibur.Dispatch.Routing;
using Excalibur.Dispatch.Routing.Strategies;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Routing;

/// <summary>
/// Locks the lifetime of the cached route snapshot held by the load-balancing strategies that build
/// one: <see cref="WeightedRoundRobinLoadBalancer"/> and <see cref="ConsistentHashLoadBalancer"/>.
/// </summary>
/// <remarks>
/// <para>
/// Two properties are pinned here, and they share a root cause -- a cached snapshot whose validity key
/// omitted the input that decides the answer, and which was re-read from a field after being validated
/// rather than carried through the call.
/// </para>
/// <list type="number">
/// <item>
/// <b>Freshness (sequential).</b> A host that swaps its routes for new objects carrying the same route
/// IDs and weights but different endpoints is making a real change. Keying validity on ID and weight
/// alone reported the swap as "unchanged", so selection kept handing back the retired endpoint objects.
/// </item>
/// <item>
/// <b>Invocation binding (concurrent).</b> The snapshot validated for a call must be the snapshot that
/// call selects from. Validating and then re-reading the field let a concurrent caller's replacement
/// land in between, so a selection could return a route that was not in its own input list at all.
/// </item>
/// </list>
/// <para>
/// Each property has a safety arm (the wrong route is never returned) and a liveness arm (weighting,
/// hash placement and rebuild-on-real-change still work, so the fix is not "rebuild always" in
/// disguise).
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class LoadBalancerSnapshotLifetimeShould
{
	private static readonly TimeSpan HandoffTimeout = TimeSpan.FromSeconds(30);

	#region Freshness -- safety

	[Fact]
	public void WeightedRoundRobin_ReturnReplacementEndpointsWhenRouteIdsAndWeightsAreUnchanged()
	{
		// Arrange
		var balancer = new WeightedRoundRobinLoadBalancer(NullLogger<WeightedRoundRobinLoadBalancer>.Instance);
		var retired = Routes(("route-1", "host-retired-1", 100), ("route-2", "host-retired-2", 100));
		_ = balancer.SelectRoute(retired, new RoutingContext());

		// Same IDs, same weights, new objects, new endpoints.
		var current = Routes(("route-1", "host-current-1", 100), ("route-2", "host-current-2", 100));

		// Act
		var selections = Select(balancer, current, 10);

		// Assert
		selections.Select(static r => r.Endpoint).Distinct(StringComparer.Ordinal)
			.ShouldBeSubsetOf(["host-current-1", "host-current-2"]);
		selections.ShouldAllBe(r => current.Contains(r));
	}

	[Fact]
	public void ConsistentHash_ReturnReplacementEndpointsWhenRouteIdsAndWeightsAreUnchanged()
	{
		// Arrange
		var balancer = new ConsistentHashLoadBalancer(NullLogger<ConsistentHashLoadBalancer>.Instance);
		var retired = Routes(("route-1", "host-retired-1", 100), ("route-2", "host-retired-2", 100));
		_ = balancer.SelectRoute(retired, Context("tenant-a"));

		var current = Routes(("route-1", "host-current-1", 100), ("route-2", "host-current-2", 100));

		// Act -- several distinct hash keys, so the assertion is not pinned to one ring position.
		var selections = Enumerable.Range(0, 10)
			.Select(i => balancer.SelectRoute(current, Context($"tenant-{i}")))
			.ToList();

		// Assert
		selections.Select(static r => r.Endpoint).Distinct(StringComparer.Ordinal)
			.ShouldBeSubsetOf(["host-current-1", "host-current-2"]);
		selections.ShouldAllBe(r => current.Contains(r));
	}

	#endregion

	#region Freshness -- liveness and controls

	[Fact]
	public void WeightedRoundRobin_RebuildForAWeightOnlyChange()
	{
		// Arrange
		var balancer = new WeightedRoundRobinLoadBalancer(NullLogger<WeightedRoundRobinLoadBalancer>.Instance);
		var even = Routes(("route-1", "host-1", 100), ("route-2", "host-2", 100));
		_ = Select(balancer, even, 8);

		var skewed = Routes(("route-1", "host-1", 300), ("route-2", "host-2", 100));

		// Act -- the expansion is [r1, r1, r1, r2], so 400 selections cover exactly 100 whole cycles
		// whatever the starting offset.
		var selections = Select(balancer, skewed, 400);

		// Assert
		selections.Count(static r => r.RouteId == "route-1").ShouldBe(300);
		selections.Count(static r => r.RouteId == "route-2").ShouldBe(100);
	}

	[Fact]
	public void WeightedRoundRobin_StopSelectingARemovedRoute()
	{
		// Arrange
		var balancer = new WeightedRoundRobinLoadBalancer(NullLogger<WeightedRoundRobinLoadBalancer>.Instance);
		var all = Routes(("route-1", "host-1", 100), ("route-2", "host-2", 100), ("route-3", "host-3", 100));
		_ = Select(balancer, all, 6);

		var remaining = new List<RouteDefinition> { all[0], all[1] };

		// Act
		var selections = Select(balancer, remaining, 50);

		// Assert
		selections.ShouldAllBe(r => r.RouteId != "route-3");
	}

	[Fact]
	public void WeightedRoundRobin_ReflectAnInPlaceEndpointMutationOfARetainedRoute()
	{
		// Arrange -- the same objects are passed back, so the snapshot legitimately stays valid. It holds
		// those same objects, so every field read off a selected route is the mutated one.
		var balancer = new WeightedRoundRobinLoadBalancer(NullLogger<WeightedRoundRobinLoadBalancer>.Instance);
		var routes = Routes(("route-1", "host-before-1", 100), ("route-2", "host-before-2", 100));
		_ = Select(balancer, routes, 4);

		routes[0].Endpoint = "host-after-1";
		routes[1].Endpoint = "host-after-2";

		// Act
		var selections = Select(balancer, routes, 10);

		// Assert
		selections.Select(static r => r.Endpoint).Distinct(StringComparer.Ordinal)
			.ShouldBeSubsetOf(["host-after-1", "host-after-2"]);
	}

	[Fact]
	public void ConsistentHash_KeepHashPlacementStableAcrossAnIdentityOnlyReplacement()
	{
		// Arrange -- ring placement is derived from route ID and weight, so replacing the objects while
		// leaving IDs and weights alone must not move any key to a different route.
		var balancer = new ConsistentHashLoadBalancer(NullLogger<ConsistentHashLoadBalancer>.Instance);
		var before = Routes(("route-1", "host-1", 100), ("route-2", "host-2", 100), ("route-3", "host-3", 100));
		var keys = Enumerable.Range(0, 50).Select(i => $"tenant-{i}").ToList();
		var placementBefore = keys.ToDictionary(
			key => key,
			key => balancer.SelectRoute(before, Context(key)).RouteId,
			StringComparer.Ordinal);

		var after = Routes(("route-1", "host-1", 100), ("route-2", "host-2", 100), ("route-3", "host-3", 100));

		// Act
		var placementAfter = keys.ToDictionary(
			key => key,
			key => balancer.SelectRoute(after, Context(key)).RouteId,
			StringComparer.Ordinal);

		// Assert
		placementAfter.ShouldBe(placementBefore);
	}

	[Fact]
	public void ConsistentHash_StopSelectingARemovedRoute()
	{
		// Arrange
		var balancer = new ConsistentHashLoadBalancer(NullLogger<ConsistentHashLoadBalancer>.Instance);
		var all = Routes(("route-1", "host-1", 100), ("route-2", "host-2", 100), ("route-3", "host-3", 100));
		var keys = Enumerable.Range(0, 50).Select(i => $"tenant-{i}").ToList();
		foreach (var key in keys)
		{
			_ = balancer.SelectRoute(all, Context(key));
		}

		var remaining = new List<RouteDefinition> { all[0], all[1] };

		// Act
		var selections = keys.Select(key => balancer.SelectRoute(remaining, Context(key))).ToList();

		// Assert
		selections.ShouldAllBe(r => r.RouteId != "route-3");
	}

	#endregion

	#region Invocation binding -- safety

	[Fact]
	public async Task WeightedRoundRobin_SelectFromItsOwnSnapshotWhenAnotherCallReplacesItMidValidation()
	{
		// Arrange
		var balancer = new WeightedRoundRobinLoadBalancer(NullLogger<WeightedRoundRobinLoadBalancer>.Instance);
		var routesA = Routes(("a1", "host-a1", 100), ("a2", "host-a2", 100));
		var routesB = Routes(("b1", "host-b1", 100), ("b2", "host-b2", 100));
		_ = balancer.SelectRoute(routesA, new RoutingContext());

		// Pauses A part-way through validating its own route list, which is the window in which B's
		// rebuild used to become visible to A.
		using var paused = new PausingRouteList(routesA, pauseAtAccess: 2);

		// Act
		var callA = Task.Run(() => balancer.SelectRoute(paused, new RoutingContext()));
		paused.WaitUntilPaused(HandoffTimeout).ShouldBeTrue("caller A never reached the pause point");

		var selectedByB = balancer.SelectRoute(routesB, new RoutingContext());
		paused.Resume();
		var selectedByA = await callA;

		// Assert
		routesA.ShouldContain(selectedByA);
		routesB.ShouldContain(selectedByB);
	}

	[Fact]
	public async Task ConsistentHash_SelectFromItsOwnSnapshotWhenAnotherCallReplacesItMidValidation()
	{
		// Arrange
		var balancer = new ConsistentHashLoadBalancer(NullLogger<ConsistentHashLoadBalancer>.Instance);
		var routesA = Routes(("a1", "host-a1", 100), ("a2", "host-a2", 100));
		var routesB = Routes(("b1", "host-b1", 100), ("b2", "host-b2", 100));
		_ = balancer.SelectRoute(routesA, Context("tenant-a"));

		using var paused = new PausingRouteList(routesA, pauseAtAccess: 2);

		// Act
		var callA = Task.Run(() => balancer.SelectRoute(paused, Context("tenant-a")));
		paused.WaitUntilPaused(HandoffTimeout).ShouldBeTrue("caller A never reached the pause point");

		var selectedByB = balancer.SelectRoute(routesB, Context("tenant-b"));
		paused.Resume();
		var selectedByA = await callA;

		// Assert
		routesA.ShouldContain(selectedByA);
		routesB.ShouldContain(selectedByB);
	}

	[Fact]
	public async Task WeightedRoundRobin_NeverReturnARouteOutsideTheCallersListUnderConcurrentAlternation()
	{
		// Arrange
		var balancer = new WeightedRoundRobinLoadBalancer(NullLogger<WeightedRoundRobinLoadBalancer>.Instance);
		var routesA = Routes(("a1", "host-a1", 100), ("a2", "host-a2", 100));
		var routesB = Routes(("b1", "host-b1", 100), ("b2", "host-b2", 100));

		// Act
		var violations = await RunAlternatingSelections(
			(routes, _) => balancer.SelectRoute(routes, new RoutingContext()),
			routesA,
			routesB);

		// Assert
		violations.ShouldBeEmpty();
	}

	[Fact]
	public async Task ConsistentHash_NeverReturnARouteOutsideTheCallersListUnderConcurrentAlternation()
	{
		// Arrange
		var balancer = new ConsistentHashLoadBalancer(NullLogger<ConsistentHashLoadBalancer>.Instance);
		var routesA = Routes(("a1", "host-a1", 100), ("a2", "host-a2", 100));
		var routesB = Routes(("b1", "host-b1", 100), ("b2", "host-b2", 100));

		// Act
		var violations = await RunAlternatingSelections(
			(routes, iteration) => balancer.SelectRoute(routes, Context($"tenant-{iteration}")),
			routesA,
			routesB);

		// Assert
		violations.ShouldBeEmpty();
	}

	#endregion

	#region Helpers

	private static async Task<IReadOnlyList<string>> RunAlternatingSelections(
		Func<IReadOnlyList<RouteDefinition>, int, RouteDefinition> select,
		IReadOnlyList<RouteDefinition> routesA,
		IReadOnlyList<RouteDefinition> routesB)
	{
		const int WorkersPerList = 4;
		const int IterationsPerWorker = 500;

		var violations = new System.Collections.Concurrent.ConcurrentBag<string>();
		using var start = new Barrier(WorkersPerList * 2);

		var workers = Enumerable.Range(0, WorkersPerList * 2).Select(worker => Task.Run(() =>
		{
			var routes = worker % 2 == 0 ? routesA : routesB;
			start.SignalAndWait();

			for (var iteration = 0; iteration < IterationsPerWorker; iteration++)
			{
				var selected = select(routes, iteration);
				if (!routes.Contains(selected))
				{
					violations.Add($"worker {worker} received {selected.RouteId} which is not in its own route list");
				}
			}
		})).ToArray();

		await Task.WhenAll(workers);
		return [.. violations];
	}

	private static List<RouteDefinition> Routes(params (string RouteId, string Endpoint, int Weight)[] routes) =>
		[.. routes.Select(static r => new RouteDefinition { RouteId = r.RouteId, Endpoint = r.Endpoint, Weight = r.Weight })];

	private static List<RouteDefinition> Select(
		WeightedRoundRobinLoadBalancer balancer,
		IReadOnlyList<RouteDefinition> routes,
		int count) =>
		[.. Enumerable.Range(0, count).Select(_ => balancer.SelectRoute(routes, new RoutingContext()))];

	private static RoutingContext Context(string correlationId) => new() { CorrelationId = correlationId };

	/// <summary>
	/// A route list that blocks on a chosen indexer access, so a second caller can be driven to
	/// completion while the first is part-way through validating its snapshot.
	/// </summary>
	private sealed class PausingRouteList(IReadOnlyList<RouteDefinition> inner, int pauseAtAccess)
		: IReadOnlyList<RouteDefinition>, IDisposable
	{
		private readonly ManualResetEventSlim _paused = new(false);
		private readonly ManualResetEventSlim _resume = new(false);
		private int _accessCount;

		public int Count => inner.Count;

		public RouteDefinition this[int index]
		{
			get
			{
				if (Interlocked.Increment(ref _accessCount) == pauseAtAccess)
				{
					_paused.Set();

					// The result is ASSERTED, not discarded. Discarding it meant a rendezvous that timed
					// out proceeded exactly as though it had completed, so the arm would report a pass for
					// an interleaving it never actually produced -- a green that was not earned.
					if (!_resume.Wait(HandoffTimeout))
					{
						throw new TimeoutException(
							"The paused caller was never resumed, so the interleaving this arm exists to "
							+ "create did not happen. The result proves nothing either way.");
					}
				}

				return inner[index];
			}
		}

		public bool WaitUntilPaused(TimeSpan timeout) => _paused.Wait(timeout);

		public void Resume() => _resume.Set();

		public IEnumerator<RouteDefinition> GetEnumerator() => inner.GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public void Dispose()
		{
			_resume.Set();
			_paused.Dispose();
			_resume.Dispose();
		}
	}

	#endregion
}
