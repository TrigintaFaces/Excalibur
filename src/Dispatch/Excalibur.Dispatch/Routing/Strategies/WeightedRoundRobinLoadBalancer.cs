// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;

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
	private readonly Lock _snapshotLock = new();
	private volatile WeightedSnapshot _snapshot = WeightedSnapshot.Empty;
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

		// Take the snapshot that was validated for *this* call and select from it. Re-reading the field
		// after validating would let a concurrent caller's replacement be selected from, returning a
		// route that is not in this caller's input list at all.
		var weightedRoutes = GetOrBuildSnapshot(routes).WeightedRoutes;
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

		state.IncrementTotalRequests();
		if (success)
		{
			state.IncrementSuccessfulRequests();
		}

		state.UpdateLatency(latency);
	}

	// Source-generated logging methods
	[LoggerMessage(MiddlewareEventId.RouteSelectedWeightedRoundRobin, LogLevel.Debug,
		"Selected route {RouteId} using weighted round-robin")]
	private partial void LogRouteSelectedWeightedRoundRobin(string routeId);

	/// <summary>
	/// Returns the weighted snapshot for <paramref name="routes"/>, rebuilding it if the caller's route
	/// objects are not the ones it was built from. The snapshot is <b>returned</b> rather than left in a
	/// field for the caller to re-read, which is what binds a selection to the routes it was validated
	/// against.
	/// </summary>
	private WeightedSnapshot GetOrBuildSnapshot(IReadOnlyList<RouteDefinition> routes)
	{
		var snapshot = _snapshot;
		if (snapshot.Matches(routes))
		{
			return snapshot;
		}

		lock (_snapshotLock)
		{
			snapshot = _snapshot;
			if (snapshot.Matches(routes))
			{
				return snapshot;
			}

			snapshot = WeightedSnapshot.Build(routes);
			_snapshot = snapshot;
			return snapshot;
		}
	}

	/// <summary>
	/// An expanded weighted route array together with the exact route objects it was expanded from.
	/// </summary>
	/// <remarks>
	/// Validity is decided by the identity of the caller's <see cref="RouteDefinition"/> objects, not by
	/// their route IDs and weights. A host that replaces its routes with new objects carrying the same
	/// IDs and weights but different endpoints is making a real change, and a snapshot keyed on ID and
	/// weight reports it as unchanged and keeps handing out the retired endpoints. In-place mutation of
	/// a route object that is still referenced is deliberately not a change: the snapshot holds that
	/// same object, so every field a caller reads off the selected route is the mutated one.
	/// </remarks>
	private sealed class WeightedSnapshot
	{
		public static readonly WeightedSnapshot Empty = new([], []);

		private readonly RouteDefinition[] _source;

		private WeightedSnapshot(RouteDefinition[] source, RouteDefinition[] weightedRoutes)
		{
			_source = source;
			WeightedRoutes = weightedRoutes;
		}

		public RouteDefinition[] WeightedRoutes { get; }

		public static WeightedSnapshot Build(IReadOnlyList<RouteDefinition> routes)
		{
			var source = new RouteDefinition[routes.Count];

			var totalWeight = 0;
			for (var i = 0; i < routes.Count; i++)
			{
				source[i] = routes[i];
				totalWeight += Math.Max(1, source[i].Weight);
			}

			var weightedRoutes = new List<RouteDefinition>(Math.Max(totalWeight, 0));

			for (var i = 0; i < source.Length; i++)
			{
				var route = source[i];
				var weight = Math.Max(1, route.Weight);

				for (var repeat = 0; repeat < weight; repeat++)
				{
					weightedRoutes.Add(route);
				}
			}

			return new WeightedSnapshot(source, [.. weightedRoutes]);
		}

		public bool Matches(IReadOnlyList<RouteDefinition> routes)
		{
			if (routes.Count != _source.Length)
			{
				return false;
			}

			for (var i = 0; i < _source.Length; i++)
			{
				if (!ReferenceEquals(routes[i], _source[i]))
				{
					return false;
				}
			}

			return true;
		}
	}

	private sealed class RouteState
	{
		private readonly Lock _lock = new();
		private long _totalRequests;
		private long _successfulRequests;
		private double _totalLatency;

		public long TotalRequests => Interlocked.Read(ref _totalRequests);

		public long SuccessfulRequests => Interlocked.Read(ref _successfulRequests);

		public double AverageLatency { get; private set; }

		public void IncrementTotalRequests() => Interlocked.Increment(ref _totalRequests);

		public void IncrementSuccessfulRequests() => Interlocked.Increment(ref _successfulRequests);

		public void UpdateLatency(TimeSpan latency)
		{
			lock (_lock)
			{
				_totalLatency += latency.TotalMilliseconds;
				var total = Interlocked.Read(ref _totalRequests);
				AverageLatency = total > 0 ? _totalLatency / total : 0;
			}
		}
	}
}
