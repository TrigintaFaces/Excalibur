// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// CA2012: FakeItEasy's .Returns() stores ValueTask internally - this is expected for test setup
#pragma warning disable CA2012 // Use ValueTasks correctly

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Routing.Builder;

namespace Excalibur.Dispatch.Tests.Messaging.Routing.Builder;

/// <summary>
/// Unit tests for <see cref="DefaultDispatchRouter"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DefaultDispatchRouterShould
{
	private static readonly string[] LocalTransport = ["local"];
	private static readonly string[] LocalAndRabbitMq = ["local", "rabbitmq"];
	private static readonly string[] RabbitMqOnly = ["rabbitmq"];

	#region Constructor tests

	[Fact]
	public void ThrowOnNullTransportSelector()
	{
		// Arrange
		var endpointRouter = A.Fake<IEndpointRouter>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => new DefaultDispatchRouter(null!, endpointRouter));
	}

	[Fact]
	public void ThrowOnNullEndpointRouter()
	{
		// Arrange
		var transportSelector = A.Fake<ITransportSelector>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => new DefaultDispatchRouter(transportSelector, null!));
	}

	#endregion

	#region RouteAsync tests

	[Fact]
	public async Task ReturnSuccessDecisionWithTransportAndEndpoints()
	{
		// Arrange
		var transportSelector = A.Fake<ITransportSelector>();
		A.CallTo(() => transportSelector.SelectTransportAsync(
				A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(new ValueTask<string>("rabbitmq"));

		var endpointRouter = A.Fake<IEndpointRouter>();
		A.CallTo(() => endpointRouter.RouteToEndpointsAsync(
				A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<string>>(
				new List<string> { "billing-service", "inventory-service" }.AsReadOnly()));

		var router = new DefaultDispatchRouter(transportSelector, endpointRouter);
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();

		// Act
		var decision = await router.RouteAsync(message, context, CancellationToken.None);

		// Assert
		decision.IsSuccess.ShouldBeTrue();
		decision.Transport.ShouldBe("rabbitmq");
		decision.Endpoints.Count.ShouldBe(2);
		decision.Endpoints.ShouldContain("billing-service");
		decision.Endpoints.ShouldContain("inventory-service");
	}

	[Fact]
	public async Task ReturnFailureDecisionWhenTransportIsEmpty()
	{
		// Arrange
		var transportSelector = A.Fake<ITransportSelector>();
		A.CallTo(() => transportSelector.SelectTransportAsync(
				A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(new ValueTask<string>(string.Empty));

		var endpointRouter = A.Fake<IEndpointRouter>();
		A.CallTo(() => endpointRouter.RouteToEndpointsAsync(
				A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<string>>(Array.Empty<string>()));

		var router = new DefaultDispatchRouter(transportSelector, endpointRouter);
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();

		// Act
		var decision = await router.RouteAsync(message, context, CancellationToken.None);

		// Assert
		decision.IsSuccess.ShouldBeFalse();
		decision.FailureReason.ShouldContain("No transport");
	}

	[Fact]
	public async Task IncludeMatchedRulesInDecision()
	{
		// Arrange
		var transportSelector = A.Fake<ITransportSelector>();
		A.CallTo(() => transportSelector.SelectTransportAsync(
				A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(new ValueTask<string>("kafka"));

		var endpointRouter = A.Fake<IEndpointRouter>();
		A.CallTo(() => endpointRouter.RouteToEndpointsAsync(
				A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<string>>(
				new List<string> { "analytics" }.AsReadOnly()));

		var router = new DefaultDispatchRouter(transportSelector, endpointRouter);
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();

		// Act
		var decision = await router.RouteAsync(message, context, CancellationToken.None);

		// Assert
		decision.MatchedRules.ShouldContain("transport:kafka");
		decision.MatchedRules.ShouldContain("endpoint:analytics");
	}

	[Fact]
	public async Task CallTransportSelectorWithCorrectArguments()
	{
		// Arrange
		var transportSelector = A.Fake<ITransportSelector>();
		A.CallTo(() => transportSelector.SelectTransportAsync(
				A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(new ValueTask<string>("local"));

		var endpointRouter = A.Fake<IEndpointRouter>();
		A.CallTo(() => endpointRouter.RouteToEndpointsAsync(
				A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<string>>(Array.Empty<string>()));

		var router = new DefaultDispatchRouter(transportSelector, endpointRouter);
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var cts = new CancellationTokenSource();

		// Act
		await router.RouteAsync(message, context, cts.Token);

		// Assert
		A.CallTo(() => transportSelector.SelectTransportAsync(message, context, cts.Token))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task CallEndpointRouterWithCorrectArguments()
	{
		// Arrange
		var transportSelector = A.Fake<ITransportSelector>();
		A.CallTo(() => transportSelector.SelectTransportAsync(
				A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(new ValueTask<string>("local"));

		var endpointRouter = A.Fake<IEndpointRouter>();
		A.CallTo(() => endpointRouter.RouteToEndpointsAsync(
				A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<string>>(Array.Empty<string>()));

		var router = new DefaultDispatchRouter(transportSelector, endpointRouter);
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var cts = new CancellationTokenSource();

		// Act
		await router.RouteAsync(message, context, cts.Token);

		// Assert
		A.CallTo(() => endpointRouter.RouteToEndpointsAsync(message, context, cts.Token))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ThrowOnNullMessageForRouteAsync()
	{
		// Arrange
		var router = CreateDefaultRouter();
		var context = A.Fake<IMessageContext>();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			async () => await router.RouteAsync(null!, context, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowOnNullContextForRouteAsync()
	{
		// Arrange
		var router = CreateDefaultRouter();
		var message = A.Fake<IDispatchMessage>();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			async () => await router.RouteAsync(message, null!, CancellationToken.None));
	}

	[Fact]
	public async Task HandleEmptyEndpointList()
	{
		// Arrange
		var transportSelector = A.Fake<ITransportSelector>();
		A.CallTo(() => transportSelector.SelectTransportAsync(
				A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(new ValueTask<string>("local"));

		var endpointRouter = A.Fake<IEndpointRouter>();
		A.CallTo(() => endpointRouter.RouteToEndpointsAsync(
				A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<string>>(Array.Empty<string>()));

		var router = new DefaultDispatchRouter(transportSelector, endpointRouter);
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();

		// Act
		var decision = await router.RouteAsync(message, context, CancellationToken.None);

		// Assert
		decision.IsSuccess.ShouldBeTrue();
		decision.Transport.ShouldBe("local");
		decision.Endpoints.ShouldBeEmpty();
	}

	#endregion

	#region CanRouteTo tests

	[Fact]
	public void ReturnTrueForConfiguredTransport()
	{
		// Arrange
		var transportSelector = A.Fake<ITransportSelector>();
		A.CallTo(() => transportSelector.GetAvailableTransports(A<Type>._))
			.Returns(LocalAndRabbitMq);

		var endpointRouter = A.Fake<IEndpointRouter>();
		var router = new DefaultDispatchRouter(transportSelector, endpointRouter);
		var message = A.Fake<IDispatchMessage>();

		// Act
		var canRoute = router.CanRouteTo(message, "rabbitmq");

		// Assert
		canRoute.ShouldBeTrue();
	}

	[Fact]
	public void ReturnTrueForConfiguredEndpoint()
	{
		// Arrange
		var transportSelector = A.Fake<ITransportSelector>();
		A.CallTo(() => transportSelector.GetAvailableTransports(A<Type>._))
			.Returns(LocalTransport);

		var endpointRouter = A.Fake<IEndpointRouter>();
		A.CallTo(() => endpointRouter.CanRouteToEndpoint(A<IDispatchMessage>._, "billing-service"))
			.Returns(true);

		var router = new DefaultDispatchRouter(transportSelector, endpointRouter);
		var message = A.Fake<IDispatchMessage>();

		// Act
		var canRoute = router.CanRouteTo(message, "billing-service");

		// Assert
		canRoute.ShouldBeTrue();
	}

	[Fact]
	public void ReturnFalseForUnconfiguredDestination()
	{
		// Arrange
		var transportSelector = A.Fake<ITransportSelector>();
		A.CallTo(() => transportSelector.GetAvailableTransports(A<Type>._))
			.Returns(LocalTransport);

		var endpointRouter = A.Fake<IEndpointRouter>();
		A.CallTo(() => endpointRouter.CanRouteToEndpoint(A<IDispatchMessage>._, A<string>._))
			.Returns(false);

		var router = new DefaultDispatchRouter(transportSelector, endpointRouter);
		var message = A.Fake<IDispatchMessage>();

		// Act
		var canRoute = router.CanRouteTo(message, "unknown");

		// Assert
		canRoute.ShouldBeFalse();
	}

	[Fact]
	public void ThrowOnNullMessageForCanRouteTo()
	{
		// Arrange
		var router = CreateDefaultRouter();

		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => router.CanRouteTo(null!, "destination"));
	}

	[Fact]
	public void ThrowOnNullDestinationForCanRouteTo()
	{
		// Arrange
		var router = CreateDefaultRouter();
		var message = A.Fake<IDispatchMessage>();

		// Act & Assert
		Should.Throw<ArgumentException>(
			() => router.CanRouteTo(message, null!));
	}

	[Fact]
	public void ThrowOnEmptyDestinationForCanRouteTo()
	{
		// Arrange
		var router = CreateDefaultRouter();
		var message = A.Fake<IDispatchMessage>();

		// Act & Assert
		Should.Throw<ArgumentException>(
			() => router.CanRouteTo(message, ""));
	}

	#endregion

	#region GetAvailableRoutes tests

	[Fact]
	public void ReturnTransportRoutes()
	{
		// Arrange
		var transportSelector = A.Fake<ITransportSelector>();
		A.CallTo(() => transportSelector.GetAvailableTransports(A<Type>._))
			.Returns(LocalAndRabbitMq);

		var endpointRouter = A.Fake<IEndpointRouter>();
		A.CallTo(() => endpointRouter.GetEndpointRoutes(A<IDispatchMessage>._, A<IMessageContext>._))
			.Returns(Enumerable.Empty<RouteInfo>());

		var router = new DefaultDispatchRouter(transportSelector, endpointRouter);
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();

		// Act
		var routes = router.GetAvailableRoutes(message, context).ToList();

		// Assert
		routes.Count.ShouldBe(2);
		routes.ShouldContain(r => r.Endpoint == "local");
		routes.ShouldContain(r => r.Endpoint == "rabbitmq");
		routes.All(r => (string)r.Metadata["route_type"]! == "transport").ShouldBeTrue();
	}

	[Fact]
	public void ReturnEndpointRoutes()
	{
		// Arrange
		var transportSelector = A.Fake<ITransportSelector>();
		A.CallTo(() => transportSelector.GetAvailableTransports(A<Type>._))
			.Returns(Enumerable.Empty<string>());

		var endpointRoute = new RouteInfo("ep-rule", "billing-service", 0);
		var endpointRouter = A.Fake<IEndpointRouter>();
		A.CallTo(() => endpointRouter.GetEndpointRoutes(A<IDispatchMessage>._, A<IMessageContext>._))
			.Returns([endpointRoute]);

		var router = new DefaultDispatchRouter(transportSelector, endpointRouter);
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();

		// Act
		var routes = router.GetAvailableRoutes(message, context).ToList();

		// Assert
		routes.ShouldContain(r => r.Endpoint == "billing-service");
	}

	[Fact]
	public void CombineTransportAndEndpointRoutes()
	{
		// Arrange
		var transportSelector = A.Fake<ITransportSelector>();
		A.CallTo(() => transportSelector.GetAvailableTransports(A<Type>._))
			.Returns(LocalAndRabbitMq);

		var endpointRoute = new RouteInfo("ep-rule", "billing-service", 0);
		var endpointRouter = A.Fake<IEndpointRouter>();
		A.CallTo(() => endpointRouter.GetEndpointRoutes(A<IDispatchMessage>._, A<IMessageContext>._))
			.Returns([endpointRoute]);

		var router = new DefaultDispatchRouter(transportSelector, endpointRouter);
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();

		// Act
		var routes = router.GetAvailableRoutes(message, context).ToList();

		// Assert
		routes.Count.ShouldBe(3); // 2 transports + 1 endpoint
	}

	[Fact]
	public void SetBusNameOnTransportRoutes()
	{
		// Arrange
		var transportSelector = A.Fake<ITransportSelector>();
		A.CallTo(() => transportSelector.GetAvailableTransports(A<Type>._))
			.Returns(RabbitMqOnly);

		var endpointRouter = A.Fake<IEndpointRouter>();
		A.CallTo(() => endpointRouter.GetEndpointRoutes(A<IDispatchMessage>._, A<IMessageContext>._))
			.Returns(Enumerable.Empty<RouteInfo>());

		var router = new DefaultDispatchRouter(transportSelector, endpointRouter);
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();

		// Act
		var routes = router.GetAvailableRoutes(message, context).ToList();

		// Assert
		var transportRoute = routes.First(r => r.Endpoint == "rabbitmq");
		transportRoute.BusName.ShouldBe("rabbitmq");
	}

	[Fact]
	public void ThrowOnNullMessageForGetAvailableRoutes()
	{
		// Arrange
		var router = CreateDefaultRouter();
		var context = A.Fake<IMessageContext>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => router.GetAvailableRoutes(null!, context).ToList());
	}

	[Fact]
	public void ThrowOnNullContextForGetAvailableRoutes()
	{
		// Arrange
		var router = CreateDefaultRouter();
		var message = A.Fake<IDispatchMessage>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => router.GetAvailableRoutes(message, null!).ToList());
	}

	#endregion

	#region Helpers

	private static DefaultDispatchRouter CreateDefaultRouter()
	{
		var transportSelector = A.Fake<ITransportSelector>();
		A.CallTo(() => transportSelector.SelectTransportAsync(
				A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(new ValueTask<string>("local"));
		A.CallTo(() => transportSelector.GetAvailableTransports(A<Type>._))
			.Returns(LocalTransport);

		var endpointRouter = A.Fake<IEndpointRouter>();
		A.CallTo(() => endpointRouter.RouteToEndpointsAsync(
				A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<string>>(Array.Empty<string>()));
		A.CallTo(() => endpointRouter.GetEndpointRoutes(A<IDispatchMessage>._, A<IMessageContext>._))
			.Returns(Enumerable.Empty<RouteInfo>());

		return new DefaultDispatchRouter(transportSelector, endpointRouter);
	}

	#endregion
}
