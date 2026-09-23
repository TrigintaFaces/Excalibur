// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Routing.LoadBalancing;

/// <summary>
/// Defines a load balancing strategy for route selection.
/// </summary>
public interface ILoadBalancingStrategy
{
	/// <summary>
	/// Selects a route based on load balancing logic.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The returned route is always reference-identical to an element of the <paramref name="routes"/>
	/// passed to <b>this</b> call. An implementation may cache a structure derived from the routes, but a
	/// cached structure is valid for an argument only while it holds the very objects that argument holds:
	/// validity is decided by identity, never by content. Two route objects carrying the same identifier
	/// and weight but different endpoints are different routes, and a strategy must not treat the second
	/// as the first.
	/// </para>
	/// <para>
	/// The caller must not mutate <paramref name="routes"/>, or the route objects it contains, for the
	/// duration of the call. <see cref="IReadOnlyList{T}"/> is a read-only <i>view</i>, not a guarantee of
	/// immutability, so a caller that holds the underlying list can still modify it mid-call; a strategy
	/// validating a cached structure against the list cannot then be relied on to return a member of it.
	/// </para>
	/// </remarks>
	/// <param name="routes"> The available routes. Must not be empty, and must not be mutated during the call. </param>
	/// <param name="context"> The routing context. </param>
	/// <returns> The selected route, reference-identical to an element of <paramref name="routes"/>. </returns>
	RouteDefinition SelectRoute(
		IReadOnlyList<RouteDefinition> routes,
		RoutingContext context);

	/// <summary>
	/// Updates route metrics after a routing decision.
	/// </summary>
	/// <param name="route"> The route that was used. </param>
	/// <param name="success"> Whether the routing was successful. </param>
	/// <param name="latency"> The observed latency. </param>
	void UpdateMetrics(RouteDefinition route, bool success, TimeSpan latency);
}
