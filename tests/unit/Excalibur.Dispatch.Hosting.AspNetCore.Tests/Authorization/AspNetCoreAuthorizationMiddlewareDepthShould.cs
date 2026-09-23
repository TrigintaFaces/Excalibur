// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Security.Claims;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Hosting.AspNetCore;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

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
	private readonly IAuthorizationPolicyProvider _policyProvider;
	private readonly ServiceProvider _authorizationContainer;
	private readonly ILogger<AspNetCoreAuthorizationMiddleware> _logger;

	public AspNetCoreAuthorizationMiddlewareDepthShould()
	{
		_httpContextAccessor = A.Fake<IHttpContextAccessor>();
		_logger = NullLogger<AspNetCoreAuthorizationMiddleware>.Instance;

		// The REAL authorization stack, configured the way a consuming host configures it. Policies and roles
		// are now composed by the host's provider and evaluated by the host's service -- the middleware no
		// longer evaluates either itself -- so a faked service would be the thing under test instead of the
		// composition, and every arm here would pass on whatever the fake was told to say.
		(_policyProvider, _authorizationService, _authorizationContainer) = TestAuthorizationHost.Build(
			static options => options.AddPolicy("AdminPolicy", static policy => policy.RequireRole("Admin")));
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_authorizationContainer.Dispose();
		}

		base.Dispose(disposing);
	}

	#region Policy Evaluation

	[Fact]
	public async Task InvokeAsync_EvaluatePolicy_WhenAuthorizeHasPolicy()
	{
		// Arrange
		var httpContext = CreateAuthenticatedHttpContext("Admin");
		A.CallTo(() => _httpContextAccessor.HttpContext).Returns(httpContext);

		var middleware = CreateMiddleware();
		var message = new PolicyProtectedMessage();
		var context = A.Fake<IMessageContext>();
		var items = new Dictionary<string, object>();
		A.CallTo(() => context.Items).Returns(items);
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

		var middleware = CreateMiddleware();
		var message = new PolicyProtectedMessage();
		var context = A.Fake<IMessageContext>();
		var items = new Dictionary<string, object>();
		A.CallTo(() => context.Items).Returns(items);

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
		var items = new Dictionary<string, object>();
		A.CallTo(() => context.Items).Returns(items);
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
		var items = new Dictionary<string, object>();
		A.CallTo(() => context.Items).Returns(items);

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

		// Set handler type that has [Authorize] via Items dictionary
		var items = new Dictionary<string, object> { ["HandlerType"] = typeof(AuthorizedHandler) };
		A.CallTo(() => context.Items).Returns(items);
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
		var items = new Dictionary<string, object> { ["HandlerType"] = typeof(AnonymousHandler) };
		A.CallTo(() => context.Items).Returns(items);
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

	/// <summary>
	/// SAFETY, and this is the arm the defect needed. A bare <c>[Authorize]</c> -- no policy, no roles, no
	/// schemes -- must resolve the HOST's default policy, the one the consumer configured through
	/// <c>AddAuthorization</c>. It used to resolve a default-policy name on our own options type, which is
	/// null unless set, so a bare [Authorize] collapsed to "any authenticated user" and a consumer who had
	/// HARDENED their default policy had that hardening silently ignored.
	/// </summary>
	/// <remarks>
	/// The whole authorization stack here is REAL -- a real provider over a real AuthorizationOptions, and
	/// the host's real IAuthorizationService. A fake provider has no consumer configuration to honour, so
	/// faking it would test the fake and pass whatever the middleware did.
	/// </remarks>
	[Fact]
	public async Task HonourTheHostsHardenedDefaultPolicyForABareAuthorize()
	{
		var (provider, service, container) = TestAuthorizationHost.Build(static options =>
			options.DefaultPolicy = new AuthorizationPolicyBuilder()
				.RequireAuthenticatedUser()
				.RequireClaim("scope", "orders.write")
				.Build());

		using (container)
		{
			// Authenticated, but WITHOUT the claim the consumer's hardened default policy demands.
			A.CallTo(() => _httpContextAccessor.HttpContext).Returns(CreateAuthenticatedHttpContext());

			var middleware = new AspNetCoreAuthorizationMiddleware(
				_httpContextAccessor,
				service,
				provider,
				TestHandlerRegistry.KnowingEveryMessageType,
				Microsoft.Extensions.Options.Options.Create(new AspNetCoreAuthorizationOptions()),
				_logger);

			var nextCalled = false;
			var result = await middleware.InvokeAsync(
				new AuthorizedMessagePlain(),
				FakeContext(),
				(_, _, _) =>
				{
					nextCalled = true;
					return ValueTask.FromResult(A.Fake<IMessageResult>());
				},
				TestContext.Current.CancellationToken);

			nextCalled.ShouldBeFalse("the consumer hardened their default policy and this caller does not satisfy it");
			result.Succeeded.ShouldBeFalse();
		}
	}

	/// <summary>
	/// LIVENESS. Without this the arm above is satisfied by a middleware that denies everything -- the
	/// cheapest way to look correct. Same hardened policy, a caller who DOES carry the required claim.
	/// </summary>
	[Fact]
	public async Task StillAdmitACallerWhoSatisfiesTheHostsHardenedDefaultPolicy()
	{
		var (provider, service, container) = TestAuthorizationHost.Build(static options =>
			options.DefaultPolicy = new AuthorizationPolicyBuilder()
				.RequireAuthenticatedUser()
				.RequireClaim("scope", "orders.write")
				.Build());

		using (container)
		{
			var httpContext = CreateAuthenticatedHttpContext();
			((ClaimsIdentity)httpContext.User.Identity!).AddClaim(new Claim("scope", "orders.write"));
			A.CallTo(() => _httpContextAccessor.HttpContext).Returns(httpContext);

			var middleware = new AspNetCoreAuthorizationMiddleware(
				_httpContextAccessor,
				service,
				provider,
				TestHandlerRegistry.KnowingEveryMessageType,
				Microsoft.Extensions.Options.Options.Create(new AspNetCoreAuthorizationOptions()),
				_logger);

			var nextCalled = false;
			_ = await middleware.InvokeAsync(
				new AuthorizedMessagePlain(),
				FakeContext(),
				(_, _, _) =>
				{
					nextCalled = true;
					return ValueTask.FromResult(A.Fake<IMessageResult>());
				},
				TestContext.Current.CancellationToken);

			nextCalled.ShouldBeTrue("a caller carrying the required claim must still be admitted");
		}
	}

	private static IMessageContext FakeContext()
	{
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());
		A.CallTo(() => context.CorrelationId).Returns("corr-1");

		return context;
	}

	#endregion

	#region Error Handling

	[Fact]
	public async Task InvokeAsync_Return500WithSanitizedDetail_WhenAuthorizationServiceThrows()
	{
		// Arrange
		var httpContext = CreateAuthenticatedHttpContext();
		A.CallTo(() => _httpContextAccessor.HttpContext).Returns(httpContext);

		// This arm is about a FAULT, not a denial, so the service is deliberately a double that throws.
		var faultingService = A.Fake<IAuthorizationService>();
		A.CallTo(() => faultingService.AuthorizeAsync(
				A<ClaimsPrincipal>._, A<object>._, A<IEnumerable<IAuthorizationRequirement>>._))
			.ThrowsAsync(new InvalidOperationException("Authorization service failure"));

		var middleware = new AspNetCoreAuthorizationMiddleware(
			_httpContextAccessor,
			faultingService,
			_policyProvider,
			TestHandlerRegistry.KnowingEveryMessageType,
			Microsoft.Extensions.Options.Options.Create(new AspNetCoreAuthorizationOptions()),
			_logger);

		var message = new PolicyProtectedMessage();
		var context = A.Fake<IMessageContext>();
		var items = new Dictionary<string, object>();
		A.CallTo(() => context.Items).Returns(items);

		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(A.Fake<IMessageResult>());

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert — S850 yq7m0s (S-F1): an authorization-EVALUATION exception fails closed as HTTP 500
		// with the generic sanitized detail, never the raw ex.Message ("Authorization service failure").
		// Pre-fix this path returned 403 + raw message; this lock RED-proves the 500-sanitize fix.
		result.ShouldNotBeNull();
		result.Succeeded.ShouldBeFalse();
		_ = result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails.ErrorCode.ShouldBe(500);
		result.ProblemDetails.Status.ShouldBe(500);
		result.ProblemDetails.Detail.ShouldBe(AspNetCoreAuthorizationMiddleware.ServerErrorDetail);
		result.ProblemDetails.Detail.ShouldNotContain("Authorization service failure");
	}

	#endregion

	#region Helpers

	private AspNetCoreAuthorizationMiddleware CreateMiddleware(AspNetCoreAuthorizationOptions? options = null)
	{
		return new AspNetCoreAuthorizationMiddleware(
			_httpContextAccessor,
			_authorizationService,
			_policyProvider,
			TestHandlerRegistry.KnowingEveryMessageType,
			Microsoft.Extensions.Options.Options.Create(options ?? new AspNetCoreAuthorizationOptions()),
			_logger);
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
