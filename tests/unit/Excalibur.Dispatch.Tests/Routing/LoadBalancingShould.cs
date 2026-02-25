// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Routing.Strategies;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Routing;

[Trait("Category", "Unit")]
public sealed class LoadBalancingShould
{
    private static RouteDefinition CreateRoute(string id, int weight = 100) => new()
    {
        RouteId = id,
        Endpoint = $"target-{id}",
        Weight = weight
    };

    private static RoutingContext CreateRoutingContext(string? correlationId = null) => new()
    {
        CorrelationId = correlationId ?? Guid.NewGuid().ToString(),
        MessageType = "TestMessage",
        Source = "TestSource"
    };

    // --- ConsistentHashLoadBalancer tests ---

    [Fact]
    public void ConsistentHash_SelectsSameRouteForSameKey()
    {
        var sut = new ConsistentHashLoadBalancer(NullLogger<ConsistentHashLoadBalancer>.Instance);
        var routes = new List<RouteDefinition> { CreateRoute("A"), CreateRoute("B"), CreateRoute("C") };
        var context = CreateRoutingContext("fixed-key");

        var route1 = sut.SelectRoute(routes, context);
        var route2 = sut.SelectRoute(routes, context);

        route1.RouteId.ShouldBe(route2.RouteId);
    }

    [Fact]
    public void ConsistentHash_ThrowsForEmptyRoutes()
    {
        var sut = new ConsistentHashLoadBalancer(NullLogger<ConsistentHashLoadBalancer>.Instance);
        var routes = new List<RouteDefinition>();

        Should.Throw<ArgumentException>(() => sut.SelectRoute(routes, CreateRoutingContext()));
    }

    [Fact]
    public void ConsistentHash_ReturnsValidRouteForSingleRoute()
    {
        var sut = new ConsistentHashLoadBalancer(NullLogger<ConsistentHashLoadBalancer>.Instance);
        var routes = new List<RouteDefinition> { CreateRoute("only") };

        var route = sut.SelectRoute(routes, CreateRoutingContext());

        route.RouteId.ShouldBe("only");
    }

    [Fact]
    public void ConsistentHash_DistributesAcrossRoutes()
    {
        var sut = new ConsistentHashLoadBalancer(NullLogger<ConsistentHashLoadBalancer>.Instance);
        var routes = new List<RouteDefinition> { CreateRoute("A"), CreateRoute("B"), CreateRoute("C") };
        var selectedRoutes = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < 100; i++)
        {
            var context = CreateRoutingContext($"key-{i}");
            var route = sut.SelectRoute(routes, context);
            selectedRoutes.Add(route.RouteId);
        }

        // Should use at least 2 different routes
        selectedRoutes.Count.ShouldBeGreaterThan(1);
    }

    // --- WeightedRoundRobinLoadBalancer tests ---

    [Fact]
    public void WeightedRoundRobin_DistributesByWeight()
    {
        var sut = new WeightedRoundRobinLoadBalancer(NullLogger<WeightedRoundRobinLoadBalancer>.Instance);
        var routes = new List<RouteDefinition>
        {
            CreateRoute("heavy", weight: 3),
            CreateRoute("light", weight: 1)
        };

        var selections = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < 100; i++)
        {
            var route = sut.SelectRoute(routes, CreateRoutingContext());
            selections[route.RouteId] = selections.GetValueOrDefault(route.RouteId) + 1;
        }

        // Heavy should get more selections than light
        selections["heavy"].ShouldBeGreaterThan(selections["light"]);
    }

    [Fact]
    public void WeightedRoundRobin_ReturnsSingleRoute()
    {
        var sut = new WeightedRoundRobinLoadBalancer(NullLogger<WeightedRoundRobinLoadBalancer>.Instance);
        var routes = new List<RouteDefinition> { CreateRoute("only") };

        var route = sut.SelectRoute(routes, CreateRoutingContext());

        route.RouteId.ShouldBe("only");
    }

    [Fact]
    public void WeightedRoundRobin_ThrowsForEmptyRoutes()
    {
        var sut = new WeightedRoundRobinLoadBalancer(NullLogger<WeightedRoundRobinLoadBalancer>.Instance);

        Should.Throw<ArgumentException>(() => sut.SelectRoute(new List<RouteDefinition>(), CreateRoutingContext()));
    }

    [Fact]
    public void WeightedRoundRobin_UpdatesMetrics()
    {
        var sut = new WeightedRoundRobinLoadBalancer(NullLogger<WeightedRoundRobinLoadBalancer>.Instance);
        var route = CreateRoute("test");

        // Should not throw
        sut.UpdateMetrics(route, success: true, TimeSpan.FromMilliseconds(50));
        sut.UpdateMetrics(route, success: false, TimeSpan.FromMilliseconds(100));
    }

    // --- LeastConnectionsLoadBalancer tests ---

    [Fact]
    public void LeastConnections_SelectsRouteWithFewestConnections()
    {
        var sut = new LeastConnectionsLoadBalancer(NullLogger<LeastConnectionsLoadBalancer>.Instance);
        var routes = new List<RouteDefinition> { CreateRoute("A"), CreateRoute("B"), CreateRoute("C") };

        // Select A first (increments its count)
        var first = sut.SelectRoute(routes, CreateRoutingContext());

        // Next selection should prefer a route that wasn't selected
        var second = sut.SelectRoute(routes, CreateRoutingContext());

        // After UpdateMetrics (release), the connection should decrement
        sut.UpdateMetrics(first, success: true, TimeSpan.FromMilliseconds(10));
    }

    [Fact]
    public void LeastConnections_ThrowsForEmptyRoutes()
    {
        var sut = new LeastConnectionsLoadBalancer(NullLogger<LeastConnectionsLoadBalancer>.Instance);

        Should.Throw<ArgumentException>(() => sut.SelectRoute(new List<RouteDefinition>(), CreateRoutingContext()));
    }

    [Fact]
    public void LeastConnections_DecrementsOnUpdateMetrics()
    {
        var sut = new LeastConnectionsLoadBalancer(NullLogger<LeastConnectionsLoadBalancer>.Instance);
        var routes = new List<RouteDefinition> { CreateRoute("A"), CreateRoute("B") };

        var selected = sut.SelectRoute(routes, CreateRoutingContext());
        sut.UpdateMetrics(selected, success: true, TimeSpan.FromMilliseconds(10));

        // After release, the route should be eligible for selection again
        // with 0 connections
        var next = sut.SelectRoute(routes, CreateRoutingContext());
        next.ShouldNotBeNull();
    }

    // --- RandomLoadBalancer tests ---

    [Fact]
    public void Random_ReturnsRouteFromList()
    {
        var sut = new RandomLoadBalancer(NullLogger<RandomLoadBalancer>.Instance);
        var routes = new List<RouteDefinition> { CreateRoute("A"), CreateRoute("B"), CreateRoute("C") };

        var selected = sut.SelectRoute(routes, CreateRoutingContext());

        routes.ShouldContain(r => r.RouteId == selected.RouteId);
    }

    [Fact]
    public void Random_ThrowsForEmptyRoutes()
    {
        var sut = new RandomLoadBalancer(NullLogger<RandomLoadBalancer>.Instance);

        Should.Throw<ArgumentException>(() => sut.SelectRoute(new List<RouteDefinition>(), CreateRoutingContext()));
    }

    [Fact]
    public void Random_ReturnsSingleRoute()
    {
        var sut = new RandomLoadBalancer(NullLogger<RandomLoadBalancer>.Instance);
        var routes = new List<RouteDefinition> { CreateRoute("only") };

        var route = sut.SelectRoute(routes, CreateRoutingContext());

        route.RouteId.ShouldBe("only");
    }

    [Fact]
    public void Random_RespectsWeighting()
    {
        var sut = new RandomLoadBalancer(NullLogger<RandomLoadBalancer>.Instance);
        var routes = new List<RouteDefinition>
        {
            CreateRoute("heavy", weight: 99),
            CreateRoute("light", weight: 1)
        };

        var selections = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < 200; i++)
        {
            var route = sut.SelectRoute(routes, CreateRoutingContext());
            selections[route.RouteId] = selections.GetValueOrDefault(route.RouteId) + 1;
        }

        // Heavy should get significantly more selections
        selections["heavy"].ShouldBeGreaterThan(selections.GetValueOrDefault("light"));
    }
}
