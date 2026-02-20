// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Hosting.AspNetCore;

using Microsoft.AspNetCore.Http;

namespace Excalibur.Dispatch.Hosting.AspNetCore.Tests;

/// <summary>
/// Tests for <see cref="RouteMessageHandlerFactory"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class RouteMessageHandlerFactoryShould : UnitTestBase
{
	#region CreateMessageHandler<TRequest, TAction> (non-generic result)

	[Fact]
	public void CreateMessageHandler_WithRequestFactory_ReturnDelegate()
	{
		// Act
		var handler = RouteMessageHandlerFactory.CreateMessageHandler<TestRequest, TestAction>(
			(request, _) => new TestAction(),
			(_, result) => Results.Accepted());

		// Assert
		handler.ShouldNotBeNull();
		handler.Method.ShouldNotBeNull();
	}

	[Fact]
	public void CreateMessageHandler_WithRequestFactory_AcceptCustomizeContext()
	{
		// Act
		var handler = RouteMessageHandlerFactory.CreateMessageHandler<TestRequest, TestAction>(
			(request, _) => new TestAction(),
			(_, result) => Results.Accepted(),
			customizeContext: ctx => ctx.CorrelationId = "custom");

		// Assert
		handler.ShouldNotBeNull();
	}

	#endregion

	#region CreateMessageHandler<TAction> (simplified, non-generic)

	[Fact]
	public void CreateMessageHandler_Simplified_ReturnDelegate()
	{
		// Act
		var handler = RouteMessageHandlerFactory.CreateMessageHandler<TestAction>(
			(_, result) => Results.Accepted());

		// Assert
		handler.ShouldNotBeNull();
		handler.Method.ShouldNotBeNull();
	}

	[Fact]
	public void CreateMessageHandler_Simplified_AcceptCustomizeContext()
	{
		// Act
		var handler = RouteMessageHandlerFactory.CreateMessageHandler<TestAction>(
			(_, result) => Results.Accepted(),
			customizeContext: ctx => ctx.CorrelationId = "custom");

		// Assert
		handler.ShouldNotBeNull();
	}

	#endregion

	#region CreateMessageHandler<TRequest, TAction, TResponse> (generic result)

	[Fact]
	public void CreateMessageHandler_WithRequestAndResponse_ReturnDelegate()
	{
		// Act
		var handler = RouteMessageHandlerFactory.CreateMessageHandler<TestRequest, TestActionWithResponse, TestResponse>(
			(request, _) => new TestActionWithResponse(),
			(_, result) => Results.Ok(result.ReturnValue));

		// Assert
		handler.ShouldNotBeNull();
		handler.Method.ShouldNotBeNull();
	}

	#endregion

	#region CreateMessageHandler<TAction, TResponse> (simplified, generic)

	[Fact]
	public void CreateMessageHandler_SimplifiedWithResponse_ReturnDelegate()
	{
		// Act
		var handler = RouteMessageHandlerFactory.CreateMessageHandler<TestActionWithResponse, TestResponse>(
			(_, result) => Results.Ok(result.ReturnValue));

		// Assert
		handler.ShouldNotBeNull();
		handler.Method.ShouldNotBeNull();
	}

	#endregion

	#region CreateEventHandler<TRequest, TEvent>

	[Fact]
	public void CreateEventHandler_WithRequestFactory_ReturnDelegate()
	{
		// Act
		var handler = RouteMessageHandlerFactory.CreateEventHandler<TestRequest, TestEvent>(
			(request, _) => new TestEvent(),
			(_, result) => Results.Accepted());

		// Assert
		handler.ShouldNotBeNull();
		handler.Method.ShouldNotBeNull();
	}

	[Fact]
	public void CreateEventHandler_WithRequestFactory_AcceptCustomizeContext()
	{
		// Act
		var handler = RouteMessageHandlerFactory.CreateEventHandler<TestRequest, TestEvent>(
			(request, _) => new TestEvent(),
			(_, result) => Results.Accepted(),
			customizeContext: ctx => ctx.CorrelationId = "custom");

		// Assert
		handler.ShouldNotBeNull();
	}

	#endregion

	#region CreateEventHandler<TEvent> (simplified)

	[Fact]
	public void CreateEventHandler_Simplified_ReturnDelegate()
	{
		// Act
		var handler = RouteMessageHandlerFactory.CreateEventHandler<TestEvent>(
			(_, result) => Results.Accepted());

		// Assert
		handler.ShouldNotBeNull();
		handler.Method.ShouldNotBeNull();
	}

	[Fact]
	public void CreateEventHandler_Simplified_AcceptCustomizeContext()
	{
		// Act
		var handler = RouteMessageHandlerFactory.CreateEventHandler<TestEvent>(
			(_, result) => Results.Accepted(),
			customizeContext: ctx => ctx.CorrelationId = "custom");

		// Assert
		handler.ShouldNotBeNull();
	}

	#endregion

	#region Test Types

	private sealed class TestRequest;

	private sealed class TestAction : IDispatchAction;

	private sealed class TestActionWithResponse : IDispatchAction<TestResponse>;

	private sealed class TestResponse;

	private sealed class TestEvent : IDispatchEvent;

	#endregion
}

