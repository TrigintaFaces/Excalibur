// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security.Claims;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Hosting.AspNetCore;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

using MsAuthorizationResult = Microsoft.AspNetCore.Authorization.AuthorizationResult;

namespace Excalibur.Dispatch.Hosting.AspNetCore.Tests.Authorization;

/// <summary>
/// Depth tests for <see cref="AspNetCoreAuthorizationMiddleware"/> covering
/// policy evaluation, role checking, handler-level attributes, and error scenarios.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class AspNetCoreAuthorizationMiddlewareDepthShould : UnitTestBase
{
	private readonly IHttpContextAccessor _httpContextAccessor;
	private readonly IAuthorizationService _authorizationService;
	private readonly ILogger<AspNetCoreAuthorizationMiddleware> _logger;

	public AspNetCoreAuthorizationMiddlewareDepthShould()
	{
		_httpContextAccessor = A.Fake<IHttpContextAccessor>();
		_authorizationService = A.Fake<IAuthorizationService>();
		_logger = NullLogger<AspNetCoreAuthorizationMiddleware>.Instance;
	}

	#region Policy Evaluation

	[Fact]
	public async Task InvokeAsync_EvaluatePolicy_WhenAuthorizeHasPolicy()
	{
		// Arrange
		var httpContext = CreateAuthenticatedHttpContext("Admin");
		A.CallTo(() => _httpContextAccessor.HttpContext).Returns(httpContext);

		A.CallTo(() => _authorizationService.AuthorizeAsync(
				A<ClaimsPrincipal>._, A<object>._, "AdminPolicy"))
			.Returns(MsAuthorizationResult.Success());

		var middleware = CreateMiddleware();
		var message = new PolicyProtectedMessage();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.GetItem<Type>("HandlerType")).Returns(null);
		var nextCalled = false;
		var expectedResult = A.Fake<IMessageResult>();

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
	public async Task InvokeAsync_DenyWhenPolicyFails()
	{
		// Arrange
		var httpContext = CreateAuthenticatedHttpContext();
		A.CallTo(() => _httpContextAccessor.HttpContext).Returns(httpContext);

		A.CallTo(() => _authorizationService.AuthorizeAsync(
				A<ClaimsPrincipal>._, A<object>._, "AdminPolicy"))
			.Returns(MsAuthorizationResult.Failed());

		var middleware = CreateMiddleware();
		var message = new PolicyProtectedMessage();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.GetItem<Type>("HandlerType")).Returns(null);

		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(A.Fake<IMessageResult>());

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.Succeeded.ShouldBeFalse();
	}

	#endregion

	#region Role Evaluation

	[Fact]
	public async Task InvokeAsync_AllowWhenUserHasRole()
	{
		// Arrange
		var httpContext = CreateAuthenticatedHttpContext("Admin");
		A.CallTo(() => _httpContextAccessor.HttpContext).Returns(httpContext);

		var middleware = CreateMiddleware();
		var message = new RoleProtectedMessage();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.GetItem<Type>("HandlerType")).Returns(null);
		var nextCalled = false;
		var expectedResult = A.Fake<IMessageResult>();

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
	public async Task InvokeAsync_DenyWhenUserLacksRole()
	{
		// Arrange
		var httpContext = CreateAuthenticatedHttpContext("User"); // Has User role but not Admin
		A.CallTo(() => _httpContextAccessor.HttpContext).Returns(httpContext);

		var middleware = CreateMiddleware();
		var message = new RoleProtectedMessage(); // Requires "Admin" role
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.GetItem<Type>("HandlerType")).Returns(null);

		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(A.Fake<IMessageResult>());

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.Succeeded.ShouldBeFalse();
	}

	#endregion

	#region Handler-Type Authorization

	[Fact]
	public async Task InvokeAsync_CheckHandlerTypeAuthorizeAttributes()
	{
		// Arrange
		var httpContext = CreateAuthenticatedHttpContext();
		A.CallTo(() => _httpContextAccessor.HttpContext).Returns(httpContext);

		var middleware = CreateMiddleware();
		var message = new PlainMessage(); // No [Authorize] on message
		var context = A.Fake<IMessageContext>();

		// Set handler type that has [Authorize] — user is authenticated, no policy/role, so should pass
		A.CallTo(() => context.GetItem<Type>("HandlerType")).Returns(typeof(AuthorizedHandler));
		var nextCalled = false;
		var expectedResult = A.Fake<IMessageResult>();

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
	public async Task InvokeAsync_AllowAnonymousOnHandler_BypassesAuthorization()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new AuthorizedMessagePlain();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.GetItem<Type>("HandlerType")).Returns(typeof(AnonymousHandler));
		var nextCalled = false;
		var expectedResult = A.Fake<IMessageResult>();

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

	#endregion

	#region Default Policy

	[Fact]
	public async Task InvokeAsync_UseDefaultPolicy_WhenAuthorizeHasNoPolicy()
	{
		// Arrange
		var httpContext = CreateAuthenticatedHttpContext();
		A.CallTo(() => _httpContextAccessor.HttpContext).Returns(httpContext);

		A.CallTo(() => _authorizationService.AuthorizeAsync(
				A<ClaimsPrincipal>._, A<object>._, "DefaultPolicy"))
			.Returns(MsAuthorizationResult.Success());

		var options = new AspNetCoreAuthorizationOptions { DefaultPolicy = "DefaultPolicy" };
		var middleware = CreateMiddleware(options);
		var message = new AuthorizedMessagePlain(); // [Authorize] with no specific policy
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.GetItem<Type>("HandlerType")).Returns(null);
		var nextCalled = false;
		var expectedResult = A.Fake<IMessageResult>();

		DispatchRequestDelegate next = (_, _, _) =>
		{
			nextCalled = true;
			return ValueTask.FromResult(expectedResult);
		};

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		nextCalled.ShouldBeTrue();
		A.CallTo(() => _authorizationService.AuthorizeAsync(
			A<ClaimsPrincipal>._, A<object>._, "DefaultPolicy"))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region Error Handling

	[Fact]
	public async Task InvokeAsync_ReturnForbidden_WhenAuthorizationServiceThrows()
	{
		// Arrange
		var httpContext = CreateAuthenticatedHttpContext();
		A.CallTo(() => _httpContextAccessor.HttpContext).Returns(httpContext);

		A.CallTo(() => _authorizationService.AuthorizeAsync(
				A<ClaimsPrincipal>._, A<object>._, A<string>._))
			.ThrowsAsync(new InvalidOperationException("Authorization service failure"));

		var middleware = CreateMiddleware();
		var message = new PolicyProtectedMessage();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.GetItem<Type>("HandlerType")).Returns(null);

		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(A.Fake<IMessageResult>());

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.Succeeded.ShouldBeFalse();
	}

	#endregion

	#region Helpers

	private AspNetCoreAuthorizationMiddleware CreateMiddleware(AspNetCoreAuthorizationOptions? options = null)
	{
		return new AspNetCoreAuthorizationMiddleware(
			_httpContextAccessor,
			_authorizationService,
			_logger,
			Microsoft.Extensions.Options.Options.Create(options ?? new AspNetCoreAuthorizationOptions()));
	}

	private static DefaultHttpContext CreateAuthenticatedHttpContext(params string[] roles)
	{
		var claims = new List<Claim>
		{
			new(ClaimTypes.NameIdentifier, "user-1"),
			new(ClaimTypes.Name, "Test User"),
		};

		foreach (var role in roles)
		{
			claims.Add(new Claim(ClaimTypes.Role, role));
		}

		var identity = new ClaimsIdentity(claims, "TestAuth");
		return new DefaultHttpContext
		{
			User = new ClaimsPrincipal(identity)
		};
	}

	// Test message types
	private sealed class PlainMessage : IDispatchMessage;

	[Authorize]
	private sealed class AuthorizedMessagePlain : IDispatchMessage;

	[Authorize(Policy = "AdminPolicy")]
	private sealed class PolicyProtectedMessage : IDispatchMessage;

	[Authorize(Roles = "Admin")]
	private sealed class RoleProtectedMessage : IDispatchMessage;

	// Test handler types
	[Authorize]
	private sealed class AuthorizedHandler;

	[AllowAnonymous]
	private sealed class AnonymousHandler;

	#endregion
}
