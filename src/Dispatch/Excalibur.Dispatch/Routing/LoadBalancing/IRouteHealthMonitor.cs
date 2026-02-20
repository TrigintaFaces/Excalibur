// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Routing;

namespace Excalibur.Dispatch.Routing.LoadBalancing;

/// <summary>
/// Provides route health monitoring capabilities.
/// </summary>
public interface IRouteHealthMonitor
{
	/// <summary>
	/// Checks the health of a route.
	/// </summary>
	/// <param name="route"> The route to check. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The health status of the route. </returns>
	Task<RouteHealthStatus> CheckHealthAsync(
		RouteDefinition route,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the current health status of all monitored routes.
	/// </summary>
	/// <returns> The health status of all routes. </returns>
	IReadOnlyDictionary<string, RouteHealthStatus> GetHealthStatuses();

	/// <summary>
	/// Registers a route for health monitoring.
	/// </summary>
	/// <param name="route"> The route to monitor. </param>
	void RegisterRoute(RouteDefinition route);

	/// <summary>
	/// Unregisters a route from health monitoring.
	/// </summary>
	/// <param name="routeId"> The ID of the route to unregister. </param>
	void UnregisterRoute(string routeId);
}
