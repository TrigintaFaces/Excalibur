// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// Note: Will use Excalibur.Dispatch.Metrics once high-performance metrics are available
namespace Excalibur.Dispatch.Routing;

/// <summary>
/// High-performance message router that uses <see cref="Span{T}"/> and avoids allocations. Optimized for scenarios where routing decisions must be
/// made in microseconds.
/// </summary>
public sealed class SpanBasedMessageRouter<TMessage>(int maxRoutes, int cacheSize = 1024) : IDisposable
	where TMessage : unmanaged, IRoutableMessage
{
	private readonly Route[] _routes = new Route[maxRoutes];

	private readonly RouteCache _cache = new(cacheSize);

	/// <summary>
	/// Pre-allocated arrays to avoid allocations.
	/// </summary>
	private readonly ThreadLocal<int[]> _threadLocalMatchBuffer = new(() => new int[maxRoutes]);

	/*, MetricRegistry? metrics = null*/

	// Metrics initialization will be enabled once high-performance metrics are available: var registry = metrics ?? MetricRegistry.Global;
	// _routingLatency = registry.Histogram("router_latency_us", HistogramConfiguration.Exponential(0.1, 2, 20)); _routingDecisions =
	// registry.Counter("router_decisions"); _cacheHits = registry.Counter("router_cache_hits"); _cacheMisses = registry.Counter("router_cache_misses");

	/// <summary>
	/// Add a route to the router.
	/// </summary>
	public bool TryAddRoute(int routeId, Predicate<TMessage> predicate, int priority = 0)
	{
		for (var i = 0; i < _routes.Length; i++)
		{
			if (!_routes[i].IsActive)
			{
				_routes[i] = new Route(routeId, predicate, priority, isActive: true);
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Route a single message with minimal overhead.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public int RouteMessage(in TMessage message)
	{
		var startTimestamp = Stopwatch.GetTimestamp();

		// Check cache first
		var routeKey = message.GetRouteKey();
		if (_cache.TryGetRoute(routeKey, out var cachedRoute))
		{
			// _cacheHits.Increment();
			RecordLatency(startTimestamp);
			return cachedRoute;
		}

		// _cacheMisses.Increment();

		// Evaluate routes
		var bestRoute = -1;
		var bestPriority = int.MinValue;

		for (var i = 0; i < _routes.Length; i++)
		{
			ref readonly var route = ref _routes[i];
			if (!route.IsActive)
			{
				continue;
			}

			if (route.Predicate(message) && route.Priority > bestPriority)
			{
				bestRoute = route.RouteId;
				bestPriority = route.Priority;
			}
		}

		// Cache the result
		if (bestRoute >= 0)
		{
			_cache.AddRoute(routeKey, bestRoute);
		}

		// _routingDecisions.Increment();
		RecordLatency(startTimestamp);

		return bestRoute;
	}

	/// <summary>
	/// Route multiple messages in batch with optimized memory access.
	/// </summary>
	public void RouteBatch(ReadOnlySpan<TMessage> messages, Span<int> routeIds)
	{
		var startTimestamp = Stopwatch.GetTimestamp();

		// Process messages in chunks for better cache locality
		const int ChunkSize = 64; // Typical cache line size

		for (var start = 0; start < messages.Length; start += ChunkSize)
		{
			var end = Math.Min(start + ChunkSize, messages.Length);
			var chunk = messages.Slice(start, end - start);
			var resultChunk = routeIds.Slice(start, end - start);

			// Process chunk
			for (var i = 0; i < chunk.Length; i++)
			{
				resultChunk[i] = RouteMessageCore(in chunk[i]);
			}
		}

		// _routingDecisions.Increment(messages.Length);
		RecordLatency(startTimestamp);
	}

	/// <summary>
	/// Find all matching routes for a message (for multicast scenarios).
	/// </summary>
	public int FindAllRoutes(in TMessage message, Span<int> matchingRoutes)
	{
		var matches = 0;

		for (var i = 0; i < _routes.Length && matches < matchingRoutes.Length; i++)
		{
			ref readonly var route = ref _routes[i];
			if (!route.IsActive)
			{
				continue;
			}

			if (route.Predicate(message))
			{
				matchingRoutes[matches++] = route.RouteId;
			}
		}

		return matches;
	}

	/// <summary>
	/// Clears the internal routing cache to force fresh evaluation of routing decisions.
	/// </summary>
	public void ClearCache() => _cache.Clear();

	/// <summary>
	/// Gets comprehensive statistics about the routing performance and state.
	/// </summary>
	/// <returns> Statistics including active routes, cache performance, and routing metrics. </returns>
	public RouteStatistics GetStatistics()
	{
		var activeRoutes = 0;
		for (var i = 0; i < _routes.Length; i++)
		{
			if (_routes[i].IsActive)
			{
				activeRoutes++;
			}
		}

		return new RouteStatistics(
			activeRoutes: activeRoutes,
			totalDecisions: 0, // _routingDecisions.Value,
			cacheHitRate: 0, // _cacheHits.Value / (double)(_cacheHits.Value + _cacheMisses.Value),
			averageLatencyUs: 0); // _routingLatency.GetSnapshot().Mean
	}

	/// <summary>
	/// Disposes the message router, releasing thread-local buffers and other resources.
	/// </summary>
	public void Dispose() => _threadLocalMatchBuffer?.Dispose();

	// Metrics disposal will be enabled once high-performance metrics are available: _routingLatency?.Dispose();
	// _routingDecisions?.Dispose(); _cacheHits?.Dispose(); _cacheMisses?.Dispose();

	/// <summary>
	/// Core routing logic extracted for reuse.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private int RouteMessageCore(in TMessage message)
	{
		var routeKey = message.GetRouteKey();

		// Check cache
		if (_cache.TryGetRoute(routeKey, out var cachedRoute))
		{
			return cachedRoute;
		}

		// Evaluate routes
		var bestRoute = -1;
		var bestPriority = int.MinValue;

		for (var i = 0; i < _routes.Length; i++)
		{
			ref readonly var route = ref _routes[i];
			if (!route.IsActive)
			{
				continue;
			}

			if (route.Predicate(message) && route.Priority > bestPriority)
			{
				bestRoute = route.RouteId;
				bestPriority = route.Priority;
			}
		}

		// Cache result
		if (bestRoute >= 0)
		{
			_cache.AddRoute(routeKey, bestRoute);
		}

		return bestRoute;
	}

	/// <summary>
	/// MA0038: Cannot make method static - will require access to _routingLatency instance field once high-performance metrics are enabled.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter",
		Justification = "Timestamp parameter reserved for future high-performance latency metrics implementation")]
	private void RecordLatency(long startTimestamp)
	{
		// Latency recording will be enabled once high-performance metrics are available: var elapsedTicks = Stopwatch.GetTimestamp() -
		// startTimestamp; var latencyUs = elapsedTicks * 1_000_000.0 / Stopwatch.Frequency; _routingLatency.Record(latencyUs);
	}

#pragma warning restore MA0038

	/// <summary>
	/// Route definition.
	/// </summary>
	[StructLayout(LayoutKind.Auto)]
	private readonly struct Route(int routeId, Predicate<TMessage> predicate, int priority, bool isActive)
	{
		public readonly int RouteId = routeId;
		public readonly Predicate<TMessage> Predicate = predicate;
		public readonly int Priority = priority;
		public readonly bool IsActive = isActive;
	}
}
