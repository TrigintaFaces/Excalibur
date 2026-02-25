// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security.Claims;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Hosting.AspNetCore;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Excalibur.Dispatch.Hosting.AspNetCore.Tests.Authorization;

/// <summary>
/// Tests for <see cref="AspNetCoreAuthorizationMiddleware"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class AspNetCoreAuthorizationMiddlewareShould : UnitTestBase
{
	private readonly IHttpContextAccessor _httpContextAccessor;
	private readonly IAuthorizationService _authorizationService;
	private readonly ILogger<AspNetCoreAuthorizationMiddleware> _logger;

	public AspNetCoreAuthorizationMiddlewareShould()
	{
		_httpContextAccessor = A.Fake<IHttpContextAccessor>();
		_authorizationService = A.Fake<IAuthorizationService>();
		_logger = NullLogger<AspNetCoreAuthorizationMiddleware>.Instance;
	}

	[Fact]
	public void ThrowWhenHttpContextAccessorIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new AspNetCoreAuthorizationMiddleware(
				null!,
				_authorizationService,
				_logger,
				Microsoft.Extensions.Options.Options.Create(new AspNetCoreAuthorizationOptions())));
	}

	[Fact]
	public void ThrowWhenAuthorizationServiceIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new AspNetCoreAuthorizationMiddleware(
				_httpContextAccessor,
				null!,
				_logger,
				Microsoft.Extensions.Options.Options.Create(new AspNetCoreAuthorizationOptions())));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new AspNetCoreAuthorizationMiddleware(
				_httpContextAccessor,
				_authorizationService,
				null!,
				Microsoft.Extensions.Options.Options.Create(new AspNetCoreAuthorizationOptions())));
	}

	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new AspNetCoreAuthorizationMiddleware(
				_httpContextAccessor,
				_authorizationService,
				_logger,
				null!));
	}

	[Fact]
	public void ConstructSuccessfully()
	{
		// Act
		var middleware = CreateMiddleware();

		// Assert
		middleware.ShouldNotBeNull();
	}

	[Fact]
	public void HaveAuthorizationStage()
	{
		// Arrange
		var middleware = CreateMiddleware();

		// Assert
		middleware.Stage.ShouldBe(DispatchMiddlewareStage.Authorization);
	}

	[Fact]
	public void HaveActionMessageKinds()
	{
		// Arrange
		var middleware = CreateMiddleware();

		// Assert
		middleware.ApplicableMessageKinds.ShouldBe(MessageKinds.Action);
	}

	[Fact]
	public async Task InvokeAsync_ThrowWhenMessageIsNull()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var context = A.Fake<IMessageContext>();
		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(A.Fake<IMessageResult>());

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			middleware.InvokeAsync(null!, context, next, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task InvokeAsync_ThrowWhenContextIsNull()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = A.Fake<IDispatchMessage>();
		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(A.Fake<IMessageResult>());

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			middleware.InvokeAsync(message, null!, next, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task InvokeAsync_ThrowWhenNextDelegateIsNull()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			middleware.InvokeAsync(message, context, null!, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task InvokeAsync_SkipWhenDisabled()
	{
		// Arrange
		var options = new AspNetCoreAuthorizationOptions { Enabled = false };
		var middleware = CreateMiddleware(options);
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();
		var nextCalled = false;

		DispatchRequestDelegate next = (_, _, _) =>
		{
			nextCalled = true;
			return ValueTask.FromResult(expectedResult);
		};

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		nextCalled.ShouldBeTrue();
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task InvokeAsync_PassThrough_WhenNoAuthorizeAttributes()
	{
		// Arrange — use a plain message type with no [Authorize] attribute
		var middleware = CreateMiddleware();
		var message = new PlainMessage();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.GetItem<Type>("HandlerType")).Returns(null);
		var expectedResult = A.Fake<IMessageResult>();
		var nextCalled = false;

		DispatchRequestDelegate next = (_, _, _) =>
		{
			nextCalled = true;
			return ValueTask.FromResult(expectedResult);
		};

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		nextCalled.ShouldBeTrue();
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task InvokeAsync_DenyWhenNoHttpContext_AndRequireAuthenticatedUser()
	{
		// Arrange
		var options = new AspNetCoreAuthorizationOptions { RequireAuthenticatedUser = true };
		var middleware = CreateMiddleware(options);
		var message = new AuthorizedMessage();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.GetItem<Type>("HandlerType")).Returns(null);
		A.CallTo(() => _httpContextAccessor.HttpContext).Returns(null);

		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(A.Fake<IMessageResult>());

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.Succeeded.ShouldBeFalse();
	}

	[Fact]
	public async Task InvokeAsync_PassThrough_WhenNoHttpContext_AndRequireAuthenticatedUserIsFalse()
	{
		// Arrange
		var options = new AspNetCoreAuthorizationOptions { RequireAuthenticatedUser = false };
		var middleware = CreateMiddleware(options);
		var message = new AuthorizedMessage();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.GetItem<Type>("HandlerType")).Returns(null);
		A.CallTo(() => _httpContextAccessor.HttpContext).Returns(null);
		var expectedResult = A.Fake<IMessageResult>();
		var nextCalled = false;

		DispatchRequestDelegate next = (_, _, _) =>
		{
			nextCalled = true;
			return ValueTask.FromResult(expectedResult);
		};

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		nextCalled.ShouldBeTrue();
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task InvokeAsync_DenyWhenUserNotAuthenticated()
	{
		// Arrange
		var httpContext = new DefaultHttpContext();
		httpContext.User = new ClaimsPrincipal(new ClaimsIdentity()); // Not authenticated
		A.CallTo(() => _httpContextAccessor.HttpContext).Returns(httpContext);

		var middleware = CreateMiddleware();
		var message = new AuthorizedMessage();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.GetItem<Type>("HandlerType")).Returns(null);

		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(A.Fake<IMessageResult>());

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.Succeeded.ShouldBeFalse();
	}

	[Fact]
	public async Task InvokeAsync_AllowAnonymousMessage_BypassesAuthorization()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new AnonymousMessage();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.GetItem<Type>("HandlerType")).Returns(null);
		var expectedResult = A.Fake<IMessageResult>();
		var nextCalled = false;

		DispatchRequestDelegate next = (_, _, _) =>
		{
			nextCalled = true;
			return ValueTask.FromResult(expectedResult);
		};

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		nextCalled.ShouldBeTrue();
		result.ShouldBe(expectedResult);
	}

	#region Helpers

	private AspNetCoreAuthorizationMiddleware CreateMiddleware(AspNetCoreAuthorizationOptions? options = null)
	{
		return new AspNetCoreAuthorizationMiddleware(
			_httpContextAccessor,
			_authorizationService,
			_logger,
			Microsoft.Extensions.Options.Options.Create(options ?? new AspNetCoreAuthorizationOptions()));
	}

	// Test message types
	private sealed class PlainMessage : IDispatchMessage;

	[Authorize]
	private sealed class AuthorizedMessage : IDispatchMessage;

	[AllowAnonymous]
	private sealed class AnonymousMessage : IDispatchMessage;

	#endregion
}
