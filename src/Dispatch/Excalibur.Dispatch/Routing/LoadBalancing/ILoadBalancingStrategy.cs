// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Routing;

namespace Excalibur.Dispatch.Routing.LoadBalancing;

/// <summary>
/// Defines a load balancing strategy for route selection.
/// </summary>
public interface ILoadBalancingStrategy
{
	/// <summary>
	/// Selects a route based on load balancing logic.
	/// </summary>
	/// <param name="routes"> The available routes. </param>
	/// <param name="context"> The routing context. </param>
	/// <returns> The selected route. </returns>
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
