// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security.Claims;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Hosting.AspNetCore;
using Excalibur.Dispatch.Messaging;

using MsgResult = Excalibur.Dispatch.Abstractions.MessageResult;

using FakeItEasy;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Hosting.Tests.AspNetCore;

/// <summary>
/// Unit tests for <see cref="ControllerBaseExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Hosting")]
public sealed class ControllerBaseExtensionsShould : UnitTestBase
{
	private readonly IDispatcher _dispatcher;

	public ControllerBaseExtensionsShould()
	{
		_dispatcher = A.Fake<IDispatcher>();
		_ = Services.AddSingleton(_dispatcher);
		BuildServiceProvider();
	}

	#region DispatchMessageAsync Tests

	[Fact]
	public void DispatchMessageAsync_WithNullController_ThrowsArgumentNullException()
	{
		// Arrange
		TestController? controller = null;
		var message = new TestDispatchAction();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(
			() => controller.DispatchMessageAsync(message, CancellationToken.None));
	}

	[Fact]
	public void DispatchMessageAsync_WithNullMessage_ThrowsArgumentNullException()
	{
		// Arrange
		var controller = CreateAuthenticatedController();
		TestDispatchAction? message = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(
			() => controller.DispatchMessageAsync(message!, CancellationToken.None));
	}

	[Fact]
	public async Task DispatchMessageAsync_DispatchesMessageToDispatcher()
	{
		// Arrange
		var controller = CreateAuthenticatedController();
		var message = new TestDispatchAction();
		var expectedResult = MsgResult.Success();

		_ = A.CallTo(() => _dispatcher.DispatchAsync(
			message,
			A<IMessageContext>._,
			A<CancellationToken>._))
			.Returns(Task.FromResult(expectedResult));

		// Act
		var result = await controller.DispatchMessageAsync(message, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
		A.CallTo(() => _dispatcher.DispatchAsync(
			message,
			A<IMessageContext>._,
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DispatchMessageAsync_WithCustomizeContext_InvokesCustomization()
	{
		// Arrange
		var controller = CreateAuthenticatedController();
		var message = new TestDispatchAction();
		var customizeCalled = false;
		MessageContext? capturedContext = null;

		_ = A.CallTo(() => _dispatcher.DispatchAsync(
			A<TestDispatchAction>._,
			A<IMessageContext>._,
			A<CancellationToken>._))
			.Returns(Task.FromResult(MsgResult.Success()));

		// Act
		_ = await controller.DispatchMessageAsync(message, CancellationToken.None, ctx =>
		{
			customizeCalled = true;
			capturedContext = ctx;
			ctx.Items["CustomKey"] = "CustomValue";
		});

		// Assert
		customizeCalled.ShouldBeTrue();
		_ = capturedContext.ShouldNotBeNull();
		capturedContext.Items["CustomKey"].ShouldBe("CustomValue");
	}

	[Fact]
	public async Task DispatchMessageAsync_PassesCancellationToken()
	{
		// Arrange
		var controller = CreateAuthenticatedController();
		var message = new TestDispatchAction();
		using var cts = new CancellationTokenSource();
		var token = cts.Token;

		_ = A.CallTo(() => _dispatcher.DispatchAsync(
			A<TestDispatchAction>._,
			A<IMessageContext>._,
			token))
			.Returns(Task.FromResult(MsgResult.Success()));

		// Act
		_ = await controller.DispatchMessageAsync(message, cancellationToken: token);

		// Assert
		A.CallTo(() => _dispatcher.DispatchAsync(
			A<TestDispatchAction>._,
			A<IMessageContext>._,
			token))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region DispatchMessageAsync<TMessage, TResponse> Tests

	[Fact]
	public void DispatchMessageAsyncGeneric_WithNullController_ThrowsArgumentNullException()
	{
		// Arrange
		TestController? controller = null;
		var message = new TestDispatchActionWithResponse();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(
			() => controller.DispatchMessageAsync<TestDispatchActionWithResponse, string>(message, CancellationToken.None));
	}

	[Fact]
	public void DispatchMessageAsyncGeneric_WithNullMessage_ThrowsArgumentNullException()
	{
		// Arrange
		var controller = CreateAuthenticatedController();
		TestDispatchActionWithResponse? message = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(
			() => controller.DispatchMessageAsync<TestDispatchActionWithResponse, string>(message, CancellationToken.None));
	}

	[Fact]
	public async Task DispatchMessageAsyncGeneric_DispatchesMessageToDispatcher()
	{
		// Arrange
		var controller = CreateAuthenticatedController();
		var message = new TestDispatchActionWithResponse();
		var expectedResponse = "test-response";
		var expectedResult = A.Fake<IMessageResult<string>>();
		_ = A.CallTo(() => expectedResult.ReturnValue).Returns(expectedResponse);

		_ = A.CallTo(() => _dispatcher.DispatchAsync<TestDispatchActionWithResponse, string>(
			message,
			A<IMessageContext>._,
			A<CancellationToken>._))
			.Returns(Task.FromResult(expectedResult));

		// Act
		var result = await controller.DispatchMessageAsync<TestDispatchActionWithResponse, string>(message, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
		result.ReturnValue.ShouldBe(expectedResponse);
	}

	#endregion

	#region DispatchEventAsync Tests

	[Fact]
	public void DispatchEventAsync_WithNullController_ThrowsArgumentNullException()
	{
		// Arrange
		TestController? controller = null;
		var @event = new TestDispatchEvent();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(
			() => controller.DispatchEventAsync(@event, CancellationToken.None));
	}

	[Fact]
	public void DispatchEventAsync_WithNullEvent_ThrowsArgumentNullException()
	{
		// Arrange
		var controller = CreateAuthenticatedController();
		TestDispatchEvent? @event = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(
			() => controller.DispatchEventAsync(@event!, CancellationToken.None));
	}

	[Fact]
	public async Task DispatchEventAsync_DispatchesEventToDispatcher()
	{
		// Arrange
		var controller = CreateAuthenticatedController();
		var @event = new TestDispatchEvent();
		var expectedResult = MsgResult.Success();

		_ = A.CallTo(() => _dispatcher.DispatchAsync(
			@event,
			A<IMessageContext>._,
			A<CancellationToken>._))
			.Returns(Task.FromResult(expectedResult));

		// Act
		var result = await controller.DispatchEventAsync(@event, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task DispatchEventAsync_WithCustomizeContext_InvokesCustomization()
	{
		// Arrange
		var controller = CreateAuthenticatedController();
		var @event = new TestDispatchEvent();
		var customizeCalled = false;

		_ = A.CallTo(() => _dispatcher.DispatchAsync(
			A<TestDispatchEvent>._,
			A<IMessageContext>._,
			A<CancellationToken>._))
			.Returns(Task.FromResult(MsgResult.Success()));

		// Act
		_ = await controller.DispatchEventAsync(@event, CancellationToken.None, ctx =>
		{
			customizeCalled = true;
		});

		// Assert
		customizeCalled.ShouldBeTrue();
	}

	#endregion

	#region DispatchMessageAsync with Factory Tests

	[Fact]
	public async Task DispatchMessageAsyncWithFactory_WithNullController_ThrowsArgumentNullException()
	{
		// Arrange
		TestController? controller = null;
		Func<HttpContext, TestDispatchAction> factory = _ => new TestDispatchAction();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			controller.DispatchMessageAsync(factory, CancellationToken.None));
	}

	[Fact]
	public async Task DispatchMessageAsyncWithFactory_WithNullFactory_ThrowsArgumentNullException()
	{
		// Arrange
		var controller = CreateAuthenticatedController();
		Func<HttpContext, TestDispatchAction>? factory = null;

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			controller.DispatchMessageAsync(factory, CancellationToken.None));
	}

	[Fact]
	public async Task DispatchMessageAsyncWithFactory_UsesFactoryToCreateMessage()
	{
		// Arrange
		var controller = CreateAuthenticatedController();
		var expectedMessage = new TestDispatchAction { Data = "factory-created" };
		HttpContext? capturedContext = null;

		_ = A.CallTo(() => _dispatcher.DispatchAsync(
			A<TestDispatchAction>._,
			A<IMessageContext>._,
			A<CancellationToken>._))
			.Returns(Task.FromResult(MsgResult.Success()));

		// Act
		_ = await controller.DispatchMessageAsync(ctx =>
		{
			capturedContext = ctx;
			return expectedMessage;
		}, CancellationToken.None);

		// Assert
		_ = capturedContext.ShouldNotBeNull();
		A.CallTo(() => _dispatcher.DispatchAsync(
			expectedMessage,
			A<IMessageContext>._,
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task DispatchMessageAsyncWithFactory_UsesDefaultResultFactory()
	{
		// Arrange
		var controller = CreateAuthenticatedController();

		_ = A.CallTo(() => _dispatcher.DispatchAsync(
			A<TestDispatchAction>._,
			A<IMessageContext>._,
			A<CancellationToken>._))
			.Returns(Task.FromResult(MsgResult.Success()));

		// Act
		var result = await controller.DispatchMessageAsync(_ => new TestDispatchAction(), CancellationToken.None);

		// Assert - should return AcceptedResult by default for success
		result.ShouldBeOfType<AcceptedResult>();
	}

	[Fact]
	public async Task DispatchMessageAsyncWithFactory_UsesCustomResultFactory()
	{
		// Arrange
		var controller = CreateAuthenticatedController();
		var customResult = new OkResult();

		_ = A.CallTo(() => _dispatcher.DispatchAsync(
			A<TestDispatchAction>._,
			A<IMessageContext>._,
			A<CancellationToken>._))
			.Returns(Task.FromResult(MsgResult.Success()));

		// Act
		var result = await controller.DispatchMessageAsync(
			_ => new TestDispatchAction(),
			CancellationToken.None,
			null,
			(_, _) => customResult);

		// Assert
		result.ShouldBe(customResult);
	}

	#endregion

	#region DispatchEventAsync with Factory Tests

	[Fact]
	public async Task DispatchEventAsyncWithFactory_WithNullController_ThrowsArgumentNullException()
	{
		// Arrange
		TestController? controller = null;
		Func<HttpContext, TestDispatchEvent> factory = _ => new TestDispatchEvent();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			controller.DispatchEventAsync(factory, CancellationToken.None));
	}

	[Fact]
	public async Task DispatchEventAsyncWithFactory_WithNullFactory_ThrowsArgumentNullException()
	{
		// Arrange
		var controller = CreateAuthenticatedController();
		Func<HttpContext, TestDispatchEvent>? factory = null;

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			controller.DispatchEventAsync(factory, CancellationToken.None));
	}

	[Fact]
	public async Task DispatchEventAsyncWithFactory_UsesFactoryToCreateEvent()
	{
		// Arrange
		var controller = CreateAuthenticatedController();
		var expectedEvent = new TestDispatchEvent();

		_ = A.CallTo(() => _dispatcher.DispatchAsync(
			A<TestDispatchEvent>._,
			A<IMessageContext>._,
			A<CancellationToken>._))
			.Returns(Task.FromResult(MsgResult.Success()));

		// Act
		_ = await controller.DispatchEventAsync(_ => expectedEvent, CancellationToken.None);

		// Assert
		A.CallTo(() => _dispatcher.DispatchAsync(
			expectedEvent,
			A<IMessageContext>._,
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region Helper Methods

	private TestController CreateAuthenticatedController()
	{
		var claims = new List<Claim>
		{
			new(ClaimTypes.NameIdentifier, "user-123")
		};
		var identity = new ClaimsIdentity(claims, "TestAuth");
		var principal = new ClaimsPrincipal(identity);

		var httpContext = new DefaultHttpContext
		{
			User = principal,
			RequestServices = ServiceProvider
		};
		httpContext.Request.Headers[WellKnownHeaderNames.CorrelationId] = Guid.NewGuid().ToString();

		return new TestController
		{
			ControllerContext = new ControllerContext
			{
				HttpContext = httpContext
			}
		};
	}

	#endregion

	#region Test Types

	private sealed class TestController : ControllerBase;

	private sealed class TestDispatchAction : IDispatchAction
	{
		public string? Data { get; set; }
	}

	private sealed class TestDispatchActionWithResponse : IDispatchAction<string>;

	private sealed class TestDispatchEvent : IDispatchEvent;

	#endregion
}
