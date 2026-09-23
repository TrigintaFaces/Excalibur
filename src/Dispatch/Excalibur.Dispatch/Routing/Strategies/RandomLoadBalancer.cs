// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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
	// Random.Shared, never a per-instance Random. SelectRoute is entered concurrently by design, and
	// System.Random's INSTANCE methods are documented as not thread-safe -- concurrent calls corrupt its
	// internal state and it degenerates into returning the same value, which silently destroys the weight
	// proportionality this balancer exists to provide. There is no exception and no log; the distribution
	// just stops being a distribution. Random.Shared is thread-safe by contract and is the BCL's own
	// answer to exactly this, so there is nothing to hand-roll and no lock to take.

	/// <inheritdoc />
	public RouteDefinition SelectRoute(IReadOnlyList<RouteDefinition> routes, RoutingContext context)
	{
		ArgumentNullException.ThrowIfNull(routes);
		if (routes.Count == 0)
		{
			throw new ArgumentException(
				Resources.LoadBalancing_NoRoutesAvailable,
				nameof(routes));
		}

		if (routes.Count == 1)
		{
			return routes[0];
		}

		// Calculate total weight.
		var totalWeight = 0;
		foreach (var route in routes)
		{
			totalWeight += Math.Max(1, route.Weight);
		}
		// CA5394: Random used for weighted load balancing, not cryptographic purposes
#pragma warning disable CA5394
		var randomValue = Random.Shared.Next(totalWeight);
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
