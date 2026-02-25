// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// CA2012: FakeItEasy's .Returns() stores ValueTask internally - this is expected for test setup
#pragma warning disable CA2012 // Use ValueTasks correctly

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Routing;

namespace Excalibur.Dispatch.Tests.Messaging.Routing;

/// <summary>
/// Unit tests for <see cref="RoutingMiddleware"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class RoutingMiddlewareShould
{
	#region Constructor and Stage tests

	[Fact]
	public void HaveRoutingStage()
	{
		// Arrange
		var router = A.Fake<IDispatchRouter>();
		var logger = A.Fake<ILogger<RoutingMiddleware>>();

		// Act
		var middleware = new RoutingMiddleware(router, logger);

		// Assert
		middleware.Stage.ShouldBe(DispatchMiddlewareStage.Routing);
	}

	[Fact]
	public void ThrowOnNullRouter()
	{
		// Arrange
		var logger = A.Fake<ILogger<RoutingMiddleware>>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new RoutingMiddleware(null!, logger));
	}

	[Fact]
	public void ThrowOnNullLogger()
	{
		// Arrange
		var router = A.Fake<IDispatchRouter>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new RoutingMiddleware(router, null!));
	}

	#endregion

	#region InvokeAsync validation tests

	[Fact]
	public async Task InvokeAsync_ThrowsOnNullMessage()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var context = CreateMockMessageContext();
		var nextDelegate = CreateSuccessfulNextDelegate();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			async () => await middleware.InvokeAsync(null!, context, nextDelegate, CancellationToken.None));
	}

	[Fact]
	public async Task InvokeAsync_ThrowsOnNullContext()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = CreateMockMessage();
		var nextDelegate = CreateSuccessfulNextDelegate();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			async () => await middleware.InvokeAsync(message, null!, nextDelegate, CancellationToken.None));
	}

	[Fact]
	public async Task InvokeAsync_ThrowsOnNullNextDelegate()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = CreateMockMessage();
		var context = CreateMockMessageContext();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			async () => await middleware.InvokeAsync(message, context, null!, CancellationToken.None));
	}

	#endregion

	#region Router service integration tests

	[Fact]
	public async Task InvokeAsync_CallsRouter()
	{
		// Arrange
		var router = A.Fake<IDispatchRouter>();
		var routingDecision = RoutingDecision.Success("local", ["endpoint-1"]);
		A.CallTo(() => router.RouteAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(ValueTask.FromResult(routingDecision));

		var middleware = CreateMiddleware(router);
		var message = CreateMockMessage();
		var context = CreateMockMessageContext();
		var nextDelegate = CreateSuccessfulNextDelegate();

		// Act
		await middleware.InvokeAsync(message, context, nextDelegate, CancellationToken.None);

		// Assert
		A.CallTo(() => router.RouteAsync(message, context, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task InvokeAsync_SkipsRouter_WhenContextAlreadyHasRoutingDecision()
	{
		// Arrange
		var router = A.Fake<IDispatchRouter>();
		var middleware = CreateMiddleware(router);
		var message = CreateMockMessage();
		var context = new MessageContext
		{
			RoutingDecision = RoutingDecision.Success("local", ["local"]),
		};
		var nextDelegate = CreateSuccessfulNextDelegate();

		// Act
		var result = await middleware.InvokeAsync(message, context, nextDelegate, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue();
		A.CallTo(() => router.RouteAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		context.RoutingDecision.ShouldNotBeNull();
		context.RoutingDecision.Transport.ShouldBe("local");
		context.Items.ShouldNotContainKey("routing:decision");
		context.Items.ShouldNotContainKey("routing:transport");
		context.Items.ShouldNotContainKey("routing:endpoints");
	}

	[Fact]
	public async Task InvokeAsync_PrecomputedFailure_DoesNotCallRouter_OrNext()
	{
		// Arrange
		var router = A.Fake<IDispatchRouter>();
		var middleware = CreateMiddleware(router);
		var message = CreateMockMessage();
		var context = new MessageContext
		{
			RoutingDecision = RoutingDecision.Failure("No route"),
		};
		var nextCalled = false;
		DispatchRequestDelegate nextDelegate = (_, _, _) =>
		{
			nextCalled = true;
			return ValueTask.FromResult(CreateSuccessMessageResult());
		};

		// Act
		var result = await middleware.InvokeAsync(message, context, nextDelegate, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeFalse();
		nextCalled.ShouldBeFalse();
		A.CallTo(() => router.RouteAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task InvokeAsync_SetsRoutingResultOnContext()
	{
		// Arrange
		var router = A.Fake<IDispatchRouter>();
		var routingDecision = RoutingDecision.Success("local", ["endpoint-1"]);
		A.CallTo(() => router.RouteAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(ValueTask.FromResult(routingDecision));

		var middleware = CreateMiddleware(router);
		var message = CreateMockMessage();

		// Use a real context with a settable property to verify assignment
		RoutingDecision? capturedRoutingDecision = null;
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());
		A.CallToSet(() => context.RoutingDecision)
			.Invokes((RoutingDecision? r) => capturedRoutingDecision = r);

		var nextDelegate = CreateSuccessfulNextDelegate();

		// Act
		await middleware.InvokeAsync(message, context, nextDelegate, CancellationToken.None);

		// Assert
		capturedRoutingDecision.ShouldNotBeNull();
		capturedRoutingDecision.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public async Task InvokeAsync_StoresDecisionOnContext()
	{
		// Arrange
		var router = A.Fake<IDispatchRouter>();
		var routingDecision = RoutingDecision.Success("rabbitmq", ["billing-service", "inventory-service"]);
		A.CallTo(() => router.RouteAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(ValueTask.FromResult(routingDecision));

		var middleware = CreateMiddleware(router);
		var message = CreateMockMessage();

		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());
		RoutingDecision? capturedRoutingDecision = null;
		A.CallToSet(() => context.RoutingDecision)
			.Invokes((RoutingDecision? decision) => capturedRoutingDecision = decision);

		var nextDelegate = CreateSuccessfulNextDelegate();

		// Act
		await middleware.InvokeAsync(message, context, nextDelegate, CancellationToken.None);

		// Assert
		capturedRoutingDecision.ShouldNotBeNull();
		capturedRoutingDecision.Transport.ShouldBe("rabbitmq");
		capturedRoutingDecision.Endpoints.Count.ShouldBe(2);
	}

	[Fact]
	public async Task InvokeAsync_CallsNextDelegateOnSuccess()
	{
		// Arrange
		var router = A.Fake<IDispatchRouter>();
		var routingDecision = RoutingDecision.Success("local", ["endpoint-1"]);
		A.CallTo(() => router.RouteAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(ValueTask.FromResult(routingDecision));

		var middleware = CreateMiddleware(router);
		var message = CreateMockMessage();
		var context = CreateMockMessageContext();

		var nextDelegateCalled = false;
		DispatchRequestDelegate nextDelegate = (_, _, _) =>
		{
			nextDelegateCalled = true;
			return ValueTask.FromResult(CreateSuccessMessageResult());
		};

		// Act
		await middleware.InvokeAsync(message, context, nextDelegate, CancellationToken.None);

		// Assert
		nextDelegateCalled.ShouldBeTrue();
	}

	[Fact]
	public async Task InvokeAsync_ReturnsFailureWhenRoutingFails()
	{
		// Arrange
		var router = A.Fake<IDispatchRouter>();
		var routingDecision = RoutingDecision.Failure("Route not found");
		A.CallTo(() => router.RouteAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(ValueTask.FromResult(routingDecision));

		var middleware = CreateMiddleware(router);
		var message = CreateMockMessage();
		var context = CreateMockMessageContext();
		var nextDelegate = CreateSuccessfulNextDelegate();

		// Act
		var result = await middleware.InvokeAsync(message, context, nextDelegate, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeFalse();
		result.ProblemDetails.ShouldNotBeNull();
		// The Status property is on the concrete MessageProblemDetails class
		var problemDetails = result.ProblemDetails as MessageProblemDetails;
		problemDetails.ShouldNotBeNull();
		problemDetails.Status.ShouldBe(404);
	}

	[Fact]
	public async Task InvokeAsync_DoesNotCallNextDelegateOnRoutingFailure()
	{
		// Arrange
		var router = A.Fake<IDispatchRouter>();
		var routingDecision = RoutingDecision.Failure("Route not found");
		A.CallTo(() => router.RouteAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(ValueTask.FromResult(routingDecision));

		var middleware = CreateMiddleware(router);
		var message = CreateMockMessage();
		var context = CreateMockMessageContext();

		var nextDelegateCalled = false;
		DispatchRequestDelegate nextDelegate = (_, _, _) =>
		{
			nextDelegateCalled = true;
			return ValueTask.FromResult(CreateSuccessMessageResult());
		};

		// Act
		await middleware.InvokeAsync(message, context, nextDelegate, CancellationToken.None);

		// Assert
		nextDelegateCalled.ShouldBeFalse();
	}

	#endregion

	#region Transport and endpoint routing tests

	[Fact]
	public async Task InvokeAsync_RoutesToLocalTransportByDefault()
	{
		// Arrange
		var router = A.Fake<IDispatchRouter>();
		var routingDecision = RoutingDecision.Success("local", ["handler-1"]);
		A.CallTo(() => router.RouteAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(ValueTask.FromResult(routingDecision));

		var middleware = CreateMiddleware(router);
		var message = CreateMockMessage();
		var context = CreateMockMessageContext();
		var nextDelegate = CreateSuccessfulNextDelegate();

		// Act
		await middleware.InvokeAsync(message, context, nextDelegate, CancellationToken.None);

		// Assert
		var decision = context.RoutingDecision;
		decision.ShouldNotBeNull();
		decision.Transport.ShouldBe("local");
	}

	[Fact]
	public async Task InvokeAsync_SupportsMultipleEndpoints()
	{
		// Arrange
		var router = A.Fake<IDispatchRouter>();
		var endpoints = new List<string> { "billing-service", "inventory-service", "analytics-service" };
		var routingDecision = RoutingDecision.Success("kafka", endpoints);
		A.CallTo(() => router.RouteAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(ValueTask.FromResult(routingDecision));

		var middleware = CreateMiddleware(router);
		var message = CreateMockMessage();
		var context = CreateMockMessageContext();
		var nextDelegate = CreateSuccessfulNextDelegate();

		// Act
		await middleware.InvokeAsync(message, context, nextDelegate, CancellationToken.None);

		// Assert
		var decision = context.RoutingDecision;
		decision.ShouldNotBeNull();
		decision.Endpoints.Count.ShouldBe(3);
		decision.Endpoints.ShouldContain("billing-service");
		decision.Endpoints.ShouldContain("inventory-service");
		decision.Endpoints.ShouldContain("analytics-service");
	}

	#endregion

	#region Helper methods

	private static RoutingMiddleware CreateMiddleware(IDispatchRouter? router = null)
	{
		router ??= CreateDefaultRouter();
		var logger = A.Fake<ILogger<RoutingMiddleware>>();
		return new RoutingMiddleware(router, logger);
	}

	private static IDispatchRouter CreateDefaultRouter()
	{
		var router = A.Fake<IDispatchRouter>();
		A.CallTo(() => router.RouteAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(ValueTask.FromResult(RoutingDecision.Success("local", ["default-handler"])));
		return router;
	}

	private static IDispatchMessage CreateMockMessage()
	{
		// IDispatchMessage is now a marker interface - just create a fake
		return A.Fake<IDispatchMessage>();
	}

	private static IMessageContext CreateMockMessageContext()
	{
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());
		return context;
	}

	private static DispatchRequestDelegate CreateSuccessfulNextDelegate()
	{
		return (_, _, _) => ValueTask.FromResult(CreateSuccessMessageResult());
	}

	private static IMessageResult CreateSuccessMessageResult()
	{
		var result = A.Fake<IMessageResult>();
		A.CallTo(() => result.Succeeded).Returns(true);
		return result;
	}

	#endregion
}
