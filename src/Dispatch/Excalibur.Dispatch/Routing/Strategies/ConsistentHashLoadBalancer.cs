// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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
	private readonly ILogger<ConsistentHashLoadBalancer> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly Lock _rebuildLock = new();
	private volatile HashRingSnapshot _hashRingSnapshot = HashRingSnapshot.Empty;

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

		// Rebuild the ring if the caller route objects are not the ones it was built from, and select
		// from the ring returned for *this* call. Re-reading the field after validating would let a
		// concurrent caller replacement be selected from, returning a route absent from this caller
		// input list.
		var hashRingSnapshot = GetOrBuildHashRing(routes);

		// Get hash key from context
		var hashKey = GetHashKey(context);
		var hash = ComputeHash(hashKey);

		// Find the route in the ring
		var route = GetRouteFromRing(hashRingSnapshot, hash);
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

	/// <summary>
	/// Returns the hash ring for <paramref name="routes"/>, rebuilding it if the caller route objects
	/// are not the ones it was built from. The ring is <b>returned</b> rather than left in a field for
	/// the caller to re-read, which is what binds a selection to the routes it was validated against.
	/// </summary>
	private HashRingSnapshot GetOrBuildHashRing(IReadOnlyList<RouteDefinition> routes)
	{
		var snapshot = _hashRingSnapshot;
		if (snapshot.Matches(routes))
		{
			return snapshot;
		}

		lock (_rebuildLock)
		{
			snapshot = _hashRingSnapshot;
			if (snapshot.Matches(routes))
			{
				return snapshot;
			}

			snapshot = BuildHashRing(routes);
			_hashRingSnapshot = snapshot;
			return snapshot;
		}
	}

	private HashRingSnapshot BuildHashRing(IReadOnlyList<RouteDefinition> routes)
	{
		var source = new RouteDefinition[routes.Count];

		var estimatedNodes = 0;
		for (var i = 0; i < routes.Count; i++)
		{
			source[i] = routes[i];
			var weight = Math.Max(1, source[i].Weight);
			estimatedNodes += virtualNodesPerRoute * weight / 100;
		}

		var nodes = new List<HashRingNode>(Math.Max(estimatedNodes, 0));

		for (var routeIndex = 0; routeIndex < source.Length; routeIndex++)
		{
			var route = source[routeIndex];
			var weight = Math.Max(1, route.Weight);

			// A route present in the caller's list ALWAYS gets at least one node. The expression below is a
			// percentage scaling, so it floors to zero whenever virtualNodesPerRoute * weight < 100 --
			// reachable with the ordinary combination of a configured 50 virtual nodes and the default
			// Weight of 1. Every route then produced zero nodes, the ring came out empty, and SelectRoute
			// threw "Hash ring is empty" on every call, permanently, for a perfectly valid non-empty route
			// list. The partial case was worse because it was silent: with mixed weights a route that
			// rounded to zero was excluded from the ring and never selected again, with no error and no log.
			// Math.Max composes the floor into the computation, so "a listed route is absent from the ring"
			// is no longer expressible rather than merely unlikely.
			var nodeCount = Math.Max(1, virtualNodesPerRoute * weight / 100);
			for (var nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
			{
				var virtualKey = $"{route.RouteId}:{nodeIndex}";
				var hash = ComputeHash(virtualKey);
				nodes.Add(new HashRingNode(hash, route));
			}
		}

		if (nodes.Count == 0)
		{
			return new HashRingSnapshot(source, [], []);
		}

		nodes.Sort(static (left, right) => left.Hash.CompareTo(right.Hash));

		var hashes = new uint[nodes.Count];
		var mappedRoutes = new RouteDefinition[nodes.Count];
		for (var i = 0; i < nodes.Count; i++)
		{
			hashes[i] = nodes[i].Hash;
			mappedRoutes[i] = nodes[i].Route;
		}

		return new HashRingSnapshot(source, hashes, mappedRoutes);
	}

	private static RouteDefinition GetRouteFromRing(HashRingSnapshot hashRingSnapshot, uint hash)
	{
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

	/// <summary>
	/// A sorted hash ring together with the exact route objects it was built from.
	/// </summary>
	/// <remarks>
	/// Validity is decided by the identity of the caller <see cref="RouteDefinition"/> objects, not by
	/// their route IDs and weights. A host that replaces its routes with new objects carrying the same
	/// IDs and weights but different endpoints is making a real change, and a ring keyed on ID and
	/// weight reports it as unchanged and keeps handing out the retired endpoints. Because ring
	/// placement is derived from route ID and weight, an identity-keyed rebuild reproduces the previous
	/// placement whenever the IDs and weights are unchanged, so hash stability is preserved.
	/// </remarks>
	private sealed class HashRingSnapshot(RouteDefinition[] source, uint[] hashes, RouteDefinition[] routes)
	{
		public static readonly HashRingSnapshot Empty = new([], [], []);

		// Captured explicitly rather than left as the primary-constructor parameter, which lowers to a
		// NON-readonly field. Nothing assigns it today, so the invariant held only by inspection; this
		// makes the compiler keep it.
		private readonly RouteDefinition[] _source = source;

		public uint[] Hashes { get; } = hashes;

		public RouteDefinition[] Routes { get; } = routes;

		public int Count => Hashes.Length;

		public bool Matches(IReadOnlyList<RouteDefinition> candidate)
		{
			if (candidate.Count != _source.Length)
			{
				return false;
			}

			for (var i = 0; i < _source.Length; i++)
			{
				if (!ReferenceEquals(candidate[i], _source[i]))
				{
					return false;
				}
			}

			return true;
		}
	}

	// Source-generated logging methods
	[LoggerMessage(MiddlewareEventId.RouteSelectedConsistentHash, LogLevel.Debug,
		"Selected route {RouteId} using consistent hash for key {HashKey}")]
	private partial void LogRouteSelectedUsingConsistentHash(string routeId, string hashKey);
}
