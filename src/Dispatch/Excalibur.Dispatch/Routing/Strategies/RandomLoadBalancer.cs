// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Routing.LoadBalancing;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Routing.Strategies;

/// <summary>
/// Implements random load balancing with optional weighting.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="RandomLoadBalancer" /> class. </remarks>
/// <param name="logger"> The logger instance. </param>
public partial class RandomLoadBalancer(ILogger<RandomLoadBalancer> logger) : ILoadBalancingStrategy
{
	private readonly ILogger<RandomLoadBalancer> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly Random _random = new();

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

		if (routes.Count == 1)
		{
			return routes[0];
		}

		// Calculate total weight
		var totalWeight = routes.Sum(static r => Math.Max(1, r.Weight));
		// CA5394: Random used for weighted load balancing, not cryptographic purposes (ADR-039)
#pragma warning disable CA5394
		var randomValue = _random.Next(totalWeight);
#pragma warning restore CA5394

		// Select based on weight
		var cumulativeWeight = 0;
		foreach (var route in routes)
		{
			cumulativeWeight += Math.Max(1, route.Weight);
			if (randomValue < cumulativeWeight)
			{
				LogRouteSelectedRandomly(route.RouteId);
				return route;
			}
		}

		return routes[^1];
	}

	/// <inheritdoc />
	public void UpdateMetrics(RouteDefinition route, bool success, TimeSpan latency)
	{
		// Random doesn't track metrics
	}

	// Source-generated logging methods
	[LoggerMessage(MiddlewareEventId.RouteSelectedRandom, LogLevel.Debug,
		"Selected route {RouteId} using random selection")]
	private partial void LogRouteSelectedRandomly(string routeId);
}
