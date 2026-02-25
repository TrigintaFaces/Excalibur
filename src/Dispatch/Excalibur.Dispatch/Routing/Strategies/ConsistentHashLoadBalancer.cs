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
public partial class ConsistentHashLoadBalancer(
	ILogger<ConsistentHashLoadBalancer> logger,
	int virtualNodesPerRoute = 150) : ILoadBalancingStrategy
{
	private readonly ILogger<ConsistentHashLoadBalancer> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly SortedDictionary<uint, RouteDefinition> _hashRing = [];

	/// <inheritdoc />
	public RouteDefinition SelectRoute(IReadOnlyList<RouteDefinition> routes, RoutingContext context)
	{
		ArgumentNullException.ThrowIfNull(routes);
		ArgumentNullException.ThrowIfNull(context);
		if (!routes.Any())
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
		lock (_hashRing)
		{
			// Check if rebuild needed
			var currentRoutes = _hashRing.Values.Select(static r => r.RouteId).Distinct(StringComparer.Ordinal)
				.Order(StringComparer.Ordinal);
			var newRoutes = routes.Select(static r => r.RouteId).Order(StringComparer.Ordinal);

			if (currentRoutes.SequenceEqual(newRoutes, StringComparer.Ordinal))
			{
				return;
			}

			_hashRing.Clear();

			foreach (var route in routes)
			{
				var weight = Math.Max(1, route.Weight);
				var nodes = virtualNodesPerRoute * weight / 100;

				for (var i = 0; i < nodes; i++)
				{
					var virtualKey = $"{route.RouteId}:{i}";
					var hash = ComputeHash(virtualKey);
					_hashRing[hash] = route;
				}
			}
		}
	}

	private RouteDefinition GetRouteFromRing(uint hash)
	{
		lock (_hashRing)
		{
			// Find the first node with hash >= our hash
			foreach (var kvp in _hashRing)
			{
				if (kvp.Key >= hash)
				{
					return kvp.Value;
				}
			}

			// Wrap around to the first node
			return _hashRing.First().Value;
		}
	}

	// Source-generated logging methods
	[LoggerMessage(MiddlewareEventId.RouteSelectedConsistentHash, LogLevel.Debug,
		"Selected route {RouteId} using consistent hash for key {HashKey}")]
	private partial void LogRouteSelectedUsingConsistentHash(string routeId, string hashKey);
}
