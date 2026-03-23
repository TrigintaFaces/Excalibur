// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Routing.LoadBalancing;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Routing.Strategies;

/// <summary>
/// Implements consistent hash load balancing.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="ConsistentHashLoadBalancer" /> class. </remarks>
/// <param name="logger"> The logger instance. </param>
/// <param name="virtualNodesPerRoute"> Number of virtual nodes per route. </param>
internal partial class ConsistentHashLoadBalancer(
	ILogger<ConsistentHashLoadBalancer> logger,
	int virtualNodesPerRoute = 150) : ILoadBalancingStrategy
{
	private static readonly Dictionary<string, int> EmptyRouteWeightSnapshot = new(0, StringComparer.Ordinal);

	private readonly ILogger<ConsistentHashLoadBalancer> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
#if NET9_0_OR_GREATER
	private readonly System.Threading.Lock _rebuildLock = new();
#else
	private readonly object _rebuildLock = new();
#endif
	private HashRingSnapshot _hashRingSnapshot = HashRingSnapshot.Empty;
	private Dictionary<string, int> _routeWeightSnapshot = EmptyRouteWeightSnapshot;

	/// <inheritdoc />
	public RouteDefinition SelectRoute(IReadOnlyList<RouteDefinition> routes, RoutingContext context)
	{
		ArgumentNullException.ThrowIfNull(routes);
		ArgumentNullException.ThrowIfNull(context);
		if (routes.Count == 0)
		{
			throw new ArgumentException(
				Resources.LoadBalancing_NoRoutesAvailable,
				nameof(routes));
		}

		// Rebuild hash ring if routes changed
		RebuildHashRing(routes);

		// Get hash key from context
		var hashKey = GetHashKey(context);
		var hash = ComputeHash(hashKey);

		// Find the route in the ring
		var route = GetRouteFromRing(hash);
		LogRouteSelectedUsingConsistentHash(route.RouteId, hashKey);

		return route;
	}

	/// <inheritdoc />
	public void UpdateMetrics(RouteDefinition route, bool success, TimeSpan latency)
	{
		// Consistent hash doesn't track metrics
	}

	private static string GetHashKey(RoutingContext context)
	{
		// Use correlation ID if available
		if (!string.IsNullOrEmpty(context.CorrelationId))
		{
			return context.CorrelationId;
		}

		// Use source + message type
		var source = context.Source ?? "unknown";
		var messageType = context.MessageType ?? "unknown";
		return $"{source}:{messageType}";
	}

	private static uint ComputeHash(string key)
	{
		// Simple FNV-1a hash
		const uint fnvPrime = 16777619;
		const uint fnvOffsetBasis = 2166136261;

		var hash = fnvOffsetBasis;
		foreach (var c in key)
		{
			hash ^= c;
			hash *= fnvPrime;
		}

		return hash;
	}

	private void RebuildHashRing(IReadOnlyList<RouteDefinition> routes)
	{
		if (RoutesUnchanged(routes, _routeWeightSnapshot))
		{
			return;
		}

		lock (_rebuildLock)
		{
			if (RoutesUnchanged(routes, _routeWeightSnapshot))
			{
				return;
			}

			var hashRingSnapshot = BuildHashRing(routes, out var routeWeightSnapshot);
			_hashRingSnapshot = hashRingSnapshot;
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

	private HashRingSnapshot BuildHashRing(
		IReadOnlyList<RouteDefinition> routes,
		out Dictionary<string, int> routeWeightSnapshot)
	{
		routeWeightSnapshot = new Dictionary<string, int>(routes.Count, StringComparer.Ordinal);

		var estimatedNodes = 0;
		for (var i = 0; i < routes.Count; i++)
		{
			var weight = Math.Max(1, routes[i].Weight);
			estimatedNodes += virtualNodesPerRoute * weight / 100;
		}

		var nodes = new List<HashRingNode>(Math.Max(estimatedNodes, 0));

		for (var routeIndex = 0; routeIndex < routes.Count; routeIndex++)
		{
			var route = routes[routeIndex];
			var weight = Math.Max(1, route.Weight);
			routeWeightSnapshot[route.RouteId] = weight;

			var nodeCount = virtualNodesPerRoute * weight / 100;
			for (var nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
			{
				var virtualKey = $"{route.RouteId}:{nodeIndex}";
				var hash = ComputeHash(virtualKey);
				nodes.Add(new HashRingNode(hash, route));
			}
		}

		if (nodes.Count == 0)
		{
			return HashRingSnapshot.Empty;
		}

		nodes.Sort(static (left, right) => left.Hash.CompareTo(right.Hash));

		var hashes = new uint[nodes.Count];
		var mappedRoutes = new RouteDefinition[nodes.Count];
		for (var i = 0; i < nodes.Count; i++)
		{
			hashes[i] = nodes[i].Hash;
			mappedRoutes[i] = nodes[i].Route;
		}

		return new HashRingSnapshot(hashes, mappedRoutes);
	}

	private RouteDefinition GetRouteFromRing(uint hash)
	{
		var hashRingSnapshot = _hashRingSnapshot;
		if (hashRingSnapshot.Count == 0)
		{
			throw new InvalidOperationException("Hash ring is empty.");
		}

		var index = Array.BinarySearch(hashRingSnapshot.Hashes, hash);
		if (index < 0)
		{
			index = ~index;
		}

		if (index >= hashRingSnapshot.Count)
		{
			index = 0;
		}

		return hashRingSnapshot.Routes[index];
	}

	private readonly record struct HashRingNode(uint Hash, RouteDefinition Route);

	private sealed class HashRingSnapshot(uint[] hashes, RouteDefinition[] routes)
	{
		public static readonly HashRingSnapshot Empty = new([], []);

		public uint[] Hashes { get; } = hashes;

		public RouteDefinition[] Routes { get; } = routes;

		public int Count => Hashes.Length;
	}

	// Source-generated logging methods
	[LoggerMessage(MiddlewareEventId.RouteSelectedConsistentHash, LogLevel.Debug,
		"Selected route {RouteId} using consistent hash for key {HashKey}")]
	private partial void LogRouteSelectedUsingConsistentHash(string routeId, string hashKey);
}
