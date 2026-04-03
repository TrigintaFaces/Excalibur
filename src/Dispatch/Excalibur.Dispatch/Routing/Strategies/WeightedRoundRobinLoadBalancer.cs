// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
	private static readonly Dictionary<string, int> EmptyRouteWeightSnapshot = new(0, StringComparer.Ordinal);

	private readonly ILogger<WeightedRoundRobinLoadBalancer> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly ConcurrentDictionary<string, RouteState> _routeStates = new(StringComparer.Ordinal);
#if NET9_0_OR_GREATER
	private readonly System.Threading.Lock _snapshotLock = new();
#else
	private readonly object _snapshotLock = new();
#endif
	private RouteDefinition[] _weightedRoutesSnapshot = [];
	private Dictionary<string, int> _routeWeightSnapshot = EmptyRouteWeightSnapshot;
	private int _currentIndex;

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

		EnsureWeightedRoutesSnapshot(routes);
		var weightedRoutes = _weightedRoutesSnapshot;
		if (weightedRoutes.Length == 0)
		{
			return routes[0];
		}

		// Select next route
		var index = Interlocked.Increment(ref _currentIndex);
		var selectedIndex = (index & int.MaxValue) % weightedRoutes.Length;
		var selected = weightedRoutes[selectedIndex];

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

	private void EnsureWeightedRoutesSnapshot(IReadOnlyList<RouteDefinition> routes)
	{
		if (RoutesUnchanged(routes, _routeWeightSnapshot))
		{
			return;
		}

		lock (_snapshotLock)
		{
			if (RoutesUnchanged(routes, _routeWeightSnapshot))
			{
				return;
			}

			_weightedRoutesSnapshot = BuildWeightedRoutesSnapshot(routes, out var routeWeightSnapshot);
			_routeWeightSnapshot = routeWeightSnapshot;
		}
	}

	private static bool RoutesUnchanged(
		IReadOnlyList<RouteDefinition> routes,
		IReadOnlyDictionary<string, int> routeWeightSnapshot)
	{
		if (routes.Count != routeWeightSnapshot.Count)
		{
			return false;
		}

		for (var i = 0; i < routes.Count; i++)
		{
			var route = routes[i];
			var weight = Math.Max(1, route.Weight);
			if (!routeWeightSnapshot.TryGetValue(route.RouteId, out var previousWeight) ||
				previousWeight != weight)
			{
				return false;
			}
		}

		return true;
	}

	private static RouteDefinition[] BuildWeightedRoutesSnapshot(
		IReadOnlyList<RouteDefinition> routes,
		out Dictionary<string, int> routeWeightSnapshot)
	{
		routeWeightSnapshot = new Dictionary<string, int>(routes.Count, StringComparer.Ordinal);

		var totalWeight = 0;
		for (var i = 0; i < routes.Count; i++)
		{
			var weight = Math.Max(1, routes[i].Weight);
			totalWeight += weight;
		}

		var weightedRoutes = new List<RouteDefinition>(Math.Max(totalWeight, 0));

		for (var i = 0; i < routes.Count; i++)
		{
			var route = routes[i];
			var weight = Math.Max(1, route.Weight);
			routeWeightSnapshot[route.RouteId] = weight;

			for (var repeat = 0; repeat < weight; repeat++)
			{
				weightedRoutes.Add(route);
			}
		}

		return [.. weightedRoutes];
	}

	private sealed class RouteState
	{
#if NET9_0_OR_GREATER
		private readonly System.Threading.Lock _lock = new();
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
