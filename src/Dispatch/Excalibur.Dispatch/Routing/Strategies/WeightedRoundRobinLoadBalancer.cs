// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Routing.LoadBalancing;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Routing.Strategies;

/// <summary>
/// Implements weighted round-robin load balancing.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="WeightedRoundRobinLoadBalancer" /> class. </remarks>
/// <param name="logger"> The logger instance. </param>
public partial class WeightedRoundRobinLoadBalancer(ILogger<WeightedRoundRobinLoadBalancer> logger) : ILoadBalancingStrategy
{
	private readonly ILogger<WeightedRoundRobinLoadBalancer> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly ConcurrentDictionary<string, RouteState> _routeStates = new(StringComparer.Ordinal);
	private int _currentIndex;

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

		// Build weighted list
		var weightedRoutes = new List<RouteDefinition>();
		foreach (var route in routes)
		{
			var weight = Math.Max(1, route.Weight);
			for (var i = 0; i < weight; i++)
			{
				weightedRoutes.Add(route);
			}
		}

		// Select next route
		var index = Interlocked.Increment(ref _currentIndex) % weightedRoutes.Count;
		var selected = weightedRoutes[Math.Abs(index)];

		LogRouteSelectedWeightedRoundRobin(selected.RouteId);
		return selected;
	}

	/// <inheritdoc />
	public void UpdateMetrics(RouteDefinition route, bool success, TimeSpan latency)
	{
		ArgumentNullException.ThrowIfNull(route);

		var state = _routeStates.GetOrAdd(route.RouteId, static _ => new RouteState());

		state.TotalRequests++;
		if (success)
		{
			state.SuccessfulRequests++;
		}

		state.UpdateLatency(latency);
	}

	// Source-generated logging methods
	[LoggerMessage(MiddlewareEventId.RouteSelectedWeightedRoundRobin, LogLevel.Debug,
		"Selected route {RouteId} using weighted round-robin")]
	private partial void LogRouteSelectedWeightedRoundRobin(string routeId);

	private sealed class RouteState
	{
#if NET9_0_OR_GREATER
		private readonly Lock _lock = new();

#else

		private readonly object _lock = new();

#endif
		private double _totalLatency;

		public long TotalRequests { get; set; }

		public long SuccessfulRequests { get; set; }

		public double AverageLatency { get; private set; }

		public void UpdateLatency(TimeSpan latency)
		{
			lock (_lock)
			{
				_totalLatency += latency.TotalMilliseconds;
				AverageLatency = _totalLatency / TotalRequests;
			}
		}
	}
}
