// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Routing;

namespace Excalibur.Dispatch.Tests.Messaging.Routing;

[Trait("Category", TestCategories.Unit)]
[Trait("Component", TestComponents.Messaging)]
public sealed class SpanBasedMessageRouterShould
{
	[Fact]
	public void SelectHighestPriorityRouteAndReuseCache()
	{
		using var sut = new SpanBasedMessageRouter<TestRoutableMessage>(maxRoutes: 4, cacheSize: 64);
		var highPriorityEvaluations = 0;
		var lowPriorityEvaluations = 0;

		sut.TryAddRoute(
			routeId: 100,
			predicate: message =>
			{
				_ = Interlocked.Increment(ref lowPriorityEvaluations);
				return message.Kind >= 1;
			},
			priority: 10).ShouldBeTrue();

		sut.TryAddRoute(
			routeId: 200,
			predicate: message =>
			{
				_ = Interlocked.Increment(ref highPriorityEvaluations);
				return message.Kind == 2;
			},
			priority: 50).ShouldBeTrue();

		var message = new TestRoutableMessage(RouteKey: 12, Kind: 2);

		var first = sut.RouteMessage(in message);
		first.ShouldBe(200);

		var lowAfterFirst = Volatile.Read(ref lowPriorityEvaluations);
		var highAfterFirst = Volatile.Read(ref highPriorityEvaluations);

		var second = sut.RouteMessage(in message);
		second.ShouldBe(200);

		Volatile.Read(ref lowPriorityEvaluations).ShouldBe(lowAfterFirst);
		Volatile.Read(ref highPriorityEvaluations).ShouldBe(highAfterFirst);
	}

	[Fact]
	public void ReturnMinusOneWhenNoRoutesMatchAndRouteKeyIsZero()
	{
		using var sut = new SpanBasedMessageRouter<TestRoutableMessage>(maxRoutes: 2, cacheSize: 8);
		var message = new TestRoutableMessage(RouteKey: 0, Kind: 9);

		var route = sut.RouteMessage(in message);

		route.ShouldBe(-1);
	}

	[Fact]
	public void ReturnFalseWhenRouteCapacityIsReached()
	{
		using var sut = new SpanBasedMessageRouter<TestRoutableMessage>(maxRoutes: 2);

		sut.TryAddRoute(1, static _ => true, priority: 0).ShouldBeTrue();
		sut.TryAddRoute(2, static _ => true, priority: 0).ShouldBeTrue();
		sut.TryAddRoute(3, static _ => true, priority: 0).ShouldBeFalse();
	}

	[Fact]
	public void RouteBatchUsingConfiguredPredicates()
	{
		using var sut = new SpanBasedMessageRouter<TestRoutableMessage>(maxRoutes: 4, cacheSize: 32);
		sut.TryAddRoute(10, static message => message.Kind == 1, priority: 10).ShouldBeTrue();
		sut.TryAddRoute(20, static message => message.Kind == 2, priority: 10).ShouldBeTrue();

		var messages = new[]
		{
			new TestRoutableMessage(RouteKey: 1, Kind: 1),
			new TestRoutableMessage(RouteKey: 2, Kind: 2),
			new TestRoutableMessage(RouteKey: 3, Kind: 3),
		};
		var routeIds = new int[messages.Length];

		sut.RouteBatch(messages, routeIds);

		routeIds[0].ShouldBe(10);
		routeIds[1].ShouldBe(20);
		routeIds[2].ShouldBe(-1);
	}

	[Fact]
	public void FindAllRoutesRespectingOutputBufferLength()
	{
		using var sut = new SpanBasedMessageRouter<TestRoutableMessage>(maxRoutes: 4);
		sut.TryAddRoute(1, static message => message.Kind > 0).ShouldBeTrue();
		sut.TryAddRoute(2, static message => message.Kind > 0).ShouldBeTrue();
		sut.TryAddRoute(3, static message => message.Kind > 0).ShouldBeTrue();

		var message = new TestRoutableMessage(RouteKey: 99, Kind: 5);
		Span<int> matches = stackalloc int[2];

		var count = sut.FindAllRoutes(in message, matches);

		count.ShouldBe(2);
		matches[0].ShouldBe(1);
		matches[1].ShouldBe(2);
	}

	[Fact]
	public void ReportActiveRoutesInStatistics()
	{
		using var sut = new SpanBasedMessageRouter<TestRoutableMessage>(maxRoutes: 5);
		sut.TryAddRoute(1, static _ => true).ShouldBeTrue();
		sut.TryAddRoute(2, static _ => true).ShouldBeTrue();
		sut.TryAddRoute(3, static _ => true).ShouldBeTrue();

		var statistics = sut.GetStatistics();

		statistics.ActiveRoutes.ShouldBe(3);
	}

	private readonly record struct TestRoutableMessage(int RouteKey, int Kind) : IRoutableMessage
	{
		public int GetRouteKey() => RouteKey;
	}
}
