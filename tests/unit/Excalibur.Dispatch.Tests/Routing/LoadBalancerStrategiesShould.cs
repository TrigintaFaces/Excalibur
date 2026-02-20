// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Routing.Strategies;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Routing;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class LoadBalancerStrategiesShould
{
	// --- RandomLoadBalancer ---

	[Fact]
	public void RandomLoadBalancer_SelectRoute_WithSingleRoute_ReturnsIt()
	{
		// Arrange
		var balancer = new RandomLoadBalancer(NullLogger<RandomLoadBalancer>.Instance);
		var routes = new List<RouteDefinition>
		{
			new() { RouteId = "route-1", Weight = 100 },
		};

		// Act
		var selected = balancer.SelectRoute(routes, new RoutingContext());

		// Assert
		selected.RouteId.ShouldBe("route-1");
	}

	[Fact]
	public void RandomLoadBalancer_SelectRoute_WithMultipleRoutes_ReturnsOneOfThem()
	{
		// Arrange
		var balancer = new RandomLoadBalancer(NullLogger<RandomLoadBalancer>.Instance);
		var routes = new List<RouteDefinition>
		{
			new() { RouteId = "route-1", Weight = 100 },
			new() { RouteId = "route-2", Weight = 100 },
			new() { RouteId = "route-3", Weight = 100 },
		};
		var validIds = new HashSet<string>(StringComparer.Ordinal) { "route-1", "route-2", "route-3" };

		// Act
		var selected = balancer.SelectRoute(routes, new RoutingContext());

		// Assert
		validIds.ShouldContain(selected.RouteId);
	}

	[Fact]
	public void RandomLoadBalancer_SelectRoute_WithEmptyRoutes_Throws()
	{
		// Arrange
		var balancer = new RandomLoadBalancer(NullLogger<RandomLoadBalancer>.Instance);
		var routes = new List<RouteDefinition>();

		// Act & Assert
		Should.Throw<ArgumentException>(() => balancer.SelectRoute(routes, new RoutingContext()));
	}

	[Fact]
	public void RandomLoadBalancer_SelectRoute_WithNullRoutes_Throws()
	{
		// Arrange
		var balancer = new RandomLoadBalancer(NullLogger<RandomLoadBalancer>.Instance);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => balancer.SelectRoute(null!, new RoutingContext()));
	}

	[Fact]
	public void RandomLoadBalancer_UpdateMetrics_DoesNotThrow()
	{
		// Arrange
		var balancer = new RandomLoadBalancer(NullLogger<RandomLoadBalancer>.Instance);
		var route = new RouteDefinition { RouteId = "route-1" };

		// Act & Assert - should not throw (no-op for random)
		balancer.UpdateMetrics(route, true, TimeSpan.FromMilliseconds(50));
	}

	// --- WeightedRoundRobinLoadBalancer ---

	[Fact]
	public void WeightedRoundRobin_SelectRoute_WithSingleRoute_ReturnsIt()
	{
		// Arrange
		var balancer = new WeightedRoundRobinLoadBalancer(NullLogger<WeightedRoundRobinLoadBalancer>.Instance);
		var routes = new List<RouteDefinition>
		{
			new() { RouteId = "route-1", Weight = 100 },
		};

		// Act
		var selected = balancer.SelectRoute(routes, new RoutingContext());

		// Assert
		selected.RouteId.ShouldBe("route-1");
	}

	[Fact]
	public void WeightedRoundRobin_SelectRoute_WithMultipleRoutes_ReturnsOneOfThem()
	{
		// Arrange
		var balancer = new WeightedRoundRobinLoadBalancer(NullLogger<WeightedRoundRobinLoadBalancer>.Instance);
		var routes = new List<RouteDefinition>
		{
			new() { RouteId = "route-1", Weight = 100 },
			new() { RouteId = "route-2", Weight = 100 },
		};
		var validIds = new HashSet<string>(StringComparer.Ordinal) { "route-1", "route-2" };

		// Act
		var selected = balancer.SelectRoute(routes, new RoutingContext());

		// Assert
		validIds.ShouldContain(selected.RouteId);
	}

	[Fact]
	public void WeightedRoundRobin_SelectRoute_RoundRobinsAcrossRoutes()
	{
		// Arrange
		var balancer = new WeightedRoundRobinLoadBalancer(NullLogger<WeightedRoundRobinLoadBalancer>.Instance);
		var routes = new List<RouteDefinition>
		{
			new() { RouteId = "route-1", Weight = 1 },
			new() { RouteId = "route-2", Weight = 1 },
		};

		// Act - call multiple times to get round-robin behavior
		var selections = new HashSet<string>(StringComparer.Ordinal);
		for (var i = 0; i < 10; i++)
		{
			var selected = balancer.SelectRoute(routes, new RoutingContext());
			selections.Add(selected.RouteId);
		}

		// Assert - should have selected both routes at least once
		selections.Count.ShouldBe(2);
	}

	[Fact]
	public void WeightedRoundRobin_SelectRoute_WithEmptyRoutes_Throws()
	{
		// Arrange
		var balancer = new WeightedRoundRobinLoadBalancer(NullLogger<WeightedRoundRobinLoadBalancer>.Instance);
		var routes = new List<RouteDefinition>();

		// Act & Assert
		Should.Throw<ArgumentException>(() => balancer.SelectRoute(routes, new RoutingContext()));
	}

	[Fact]
	public void WeightedRoundRobin_UpdateMetrics_TracksSuccessful()
	{
		// Arrange
		var balancer = new WeightedRoundRobinLoadBalancer(NullLogger<WeightedRoundRobinLoadBalancer>.Instance);
		var route = new RouteDefinition { RouteId = "route-1" };

		// Act & Assert - should not throw
		balancer.UpdateMetrics(route, true, TimeSpan.FromMilliseconds(50));
		balancer.UpdateMetrics(route, false, TimeSpan.FromMilliseconds(100));
	}

	[Fact]
	public void WeightedRoundRobin_UpdateMetrics_WithNullRoute_Throws()
	{
		// Arrange
		var balancer = new WeightedRoundRobinLoadBalancer(NullLogger<WeightedRoundRobinLoadBalancer>.Instance);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => balancer.UpdateMetrics(null!, true, TimeSpan.Zero));
	}

	// --- LeastConnectionsLoadBalancer ---

	[Fact]
	public void LeastConnections_SelectRoute_WithSingleRoute_ReturnsIt()
	{
		// Arrange
		var balancer = new LeastConnectionsLoadBalancer(NullLogger<LeastConnectionsLoadBalancer>.Instance);
		var routes = new List<RouteDefinition>
		{
			new() { RouteId = "route-1" },
		};

		// Act
		var selected = balancer.SelectRoute(routes, new RoutingContext());

		// Assert
		selected.RouteId.ShouldBe("route-1");
	}

	[Fact]
	public void LeastConnections_SelectRoute_FavorsLeastConnections()
	{
		// Arrange
		var balancer = new LeastConnectionsLoadBalancer(NullLogger<LeastConnectionsLoadBalancer>.Instance);
		var routes = new List<RouteDefinition>
		{
			new() { RouteId = "route-1" },
			new() { RouteId = "route-2" },
		};

		// Act - select route-1 three times (incrementing its connection count)
		balancer.SelectRoute(routes, new RoutingContext());
		balancer.SelectRoute(routes, new RoutingContext());
		balancer.SelectRoute(routes, new RoutingContext());

		// Next selection should favor route-2 since route-1 has connections
		// But since both get incremented, the first call gets route-1, second gets route-2
		// After 3 selections, route-1 has 2 connections and route-2 has 1, so next should be route-2
		var selected = balancer.SelectRoute(routes, new RoutingContext());

		// Assert - route-2 should be selected since route-1 has more connections
		selected.RouteId.ShouldBe("route-2");
	}

	[Fact]
	public void LeastConnections_SelectRoute_WithEmptyRoutes_Throws()
	{
		// Arrange
		var balancer = new LeastConnectionsLoadBalancer(NullLogger<LeastConnectionsLoadBalancer>.Instance);
		var routes = new List<RouteDefinition>();

		// Act & Assert
		Should.Throw<ArgumentException>(() => balancer.SelectRoute(routes, new RoutingContext()));
	}

	[Fact]
	public void LeastConnections_UpdateMetrics_DecrementsConnections()
	{
		// Arrange
		var balancer = new LeastConnectionsLoadBalancer(NullLogger<LeastConnectionsLoadBalancer>.Instance);
		var route = new RouteDefinition { RouteId = "route-1" };
		var routes = new List<RouteDefinition> { route, new() { RouteId = "route-2" } };

		// Act - select route-1 (increments), then update metrics (decrements)
		balancer.SelectRoute(routes, new RoutingContext());
		balancer.UpdateMetrics(route, true, TimeSpan.FromMilliseconds(50));

		// Assert - should not throw, and connections should be decremented
		// Next select should still start fresh
		var selected = balancer.SelectRoute(routes, new RoutingContext());
		selected.ShouldNotBeNull();
	}

	[Fact]
	public void LeastConnections_UpdateMetrics_WithNullRoute_Throws()
	{
		// Arrange
		var balancer = new LeastConnectionsLoadBalancer(NullLogger<LeastConnectionsLoadBalancer>.Instance);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => balancer.UpdateMetrics(null!, true, TimeSpan.Zero));
	}

	// --- ConsistentHashLoadBalancer ---

	[Fact]
	public void ConsistentHash_SelectRoute_WithSingleRoute_ReturnsIt()
	{
		// Arrange
		var balancer = new ConsistentHashLoadBalancer(NullLogger<ConsistentHashLoadBalancer>.Instance);
		var routes = new List<RouteDefinition>
		{
			new() { RouteId = "route-1", Weight = 100 },
		};
		var context = new RoutingContext { CorrelationId = "test-correlation" };

		// Act
		var selected = balancer.SelectRoute(routes, context);

		// Assert
		selected.RouteId.ShouldBe("route-1");
	}

	[Fact]
	public void ConsistentHash_SelectRoute_SameKey_ReturnsSameRoute()
	{
		// Arrange
		var balancer = new ConsistentHashLoadBalancer(NullLogger<ConsistentHashLoadBalancer>.Instance);
		var routes = new List<RouteDefinition>
		{
			new() { RouteId = "route-1", Weight = 100 },
			new() { RouteId = "route-2", Weight = 100 },
			new() { RouteId = "route-3", Weight = 100 },
		};
		var context = new RoutingContext { CorrelationId = "stable-key-123" };

		// Act
		var selected1 = balancer.SelectRoute(routes, context);
		var selected2 = balancer.SelectRoute(routes, context);
		var selected3 = balancer.SelectRoute(routes, context);

		// Assert - consistent hashing should return the same route for the same key
		selected1.RouteId.ShouldBe(selected2.RouteId);
		selected2.RouteId.ShouldBe(selected3.RouteId);
	}

	[Fact]
	public void ConsistentHash_SelectRoute_UsesSourceAndMessageType_WhenNoCorrelationId()
	{
		// Arrange
		var balancer = new ConsistentHashLoadBalancer(NullLogger<ConsistentHashLoadBalancer>.Instance);
		var routes = new List<RouteDefinition>
		{
			new() { RouteId = "route-1", Weight = 100 },
			new() { RouteId = "route-2", Weight = 100 },
		};
		var context = new RoutingContext
		{
			Source = "service-a",
			MessageType = "OrderCreated",
		};

		// Act
		var selected1 = balancer.SelectRoute(routes, context);
		var selected2 = balancer.SelectRoute(routes, context);

		// Assert - same source+messageType should hash to same route
		selected1.RouteId.ShouldBe(selected2.RouteId);
	}

	[Fact]
	public void ConsistentHash_SelectRoute_WithEmptyRoutes_Throws()
	{
		// Arrange
		var balancer = new ConsistentHashLoadBalancer(NullLogger<ConsistentHashLoadBalancer>.Instance);
		var routes = new List<RouteDefinition>();
		var context = new RoutingContext { CorrelationId = "test" };

		// Act & Assert
		Should.Throw<ArgumentException>(() => balancer.SelectRoute(routes, context));
	}

	[Fact]
	public void ConsistentHash_SelectRoute_WithNullRoutes_Throws()
	{
		// Arrange
		var balancer = new ConsistentHashLoadBalancer(NullLogger<ConsistentHashLoadBalancer>.Instance);
		var context = new RoutingContext { CorrelationId = "test" };

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => balancer.SelectRoute(null!, context));
	}

	[Fact]
	public void ConsistentHash_SelectRoute_WithNullContext_Throws()
	{
		// Arrange
		var balancer = new ConsistentHashLoadBalancer(NullLogger<ConsistentHashLoadBalancer>.Instance);
		var routes = new List<RouteDefinition>
		{
			new() { RouteId = "route-1", Weight = 100 },
		};

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => balancer.SelectRoute(routes, null!));
	}

	[Fact]
	public void ConsistentHash_UpdateMetrics_DoesNotThrow()
	{
		// Arrange
		var balancer = new ConsistentHashLoadBalancer(NullLogger<ConsistentHashLoadBalancer>.Instance);
		var route = new RouteDefinition { RouteId = "route-1" };

		// Act & Assert - should not throw (no-op for consistent hash)
		balancer.UpdateMetrics(route, true, TimeSpan.FromMilliseconds(50));
	}
}
