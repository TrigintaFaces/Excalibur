// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Routing.LoadBalancing;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Routing.Strategies;

/// <summary>
/// Implements least connections load balancing.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="LeastConnectionsLoadBalancer" /> class. </remarks>
/// <param name="logger"> The logger instance. </param>
public partial class LeastConnectionsLoadBalancer(ILogger<LeastConnectionsLoadBalancer> logger) : ILoadBalancingStrategy
{
	private readonly ILogger<LeastConnectionsLoadBalancer> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly ConcurrentDictionary<string, long> _activeConnections = new(StringComparer.Ordinal);

	/// <inheritdoc />
	public RouteDefinition SelectRoute(IReadOnlyList<RouteDefinition> routes, RoutingContext context)
	{
		ArgumentNullException.ThrowIfNull(routes);
		if (!routes.Any())
		{
			throw new ArgumentException(
				Resources.LoadBalancing_NoRoutesAvailable,
				nameof(routes));
		}

		// Select route with least connections
		RouteDefinition? selectedRoute = null;
		var minConnections = long.MaxValue;

		foreach (var route in routes)
		{
			var connections = _activeConnections.GetOrAdd(route.RouteId, 0);
			if (connections < minConnections)
			{
				minConnections = connections;
				selectedRoute = route;
			}
		}

		if (selectedRoute != null)
		{
			_ = _activeConnections.AddOrUpdate(selectedRoute.RouteId, 1, static (_, count) => count + 1);
			LogRouteSelectedWithLeastConnections(selectedRoute.RouteId, (int)minConnections);
		}

		return selectedRoute ?? routes[0];
	}

	/// <inheritdoc />
	public void UpdateMetrics(RouteDefinition route, bool success, TimeSpan latency)
	{
		ArgumentNullException.ThrowIfNull(route);

		// Decrement connection count when request completes
		_ = _activeConnections.AddOrUpdate(route.RouteId, 0, static (_, count) => Math.Max(0, count - 1));
	}

	// Source-generated logging methods
	[LoggerMessage(MiddlewareEventId.RouteSelectedLeastConnections, LogLevel.Debug,
		"Selected route {RouteId} with {Connections} active connections")]
	private partial void LogRouteSelectedWithLeastConnections(string routeId, int connections);
}
