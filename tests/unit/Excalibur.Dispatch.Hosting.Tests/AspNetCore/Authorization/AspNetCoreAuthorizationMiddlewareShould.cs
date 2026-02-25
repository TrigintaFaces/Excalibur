// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security.Claims;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Hosting.AspNetCore;

using FakeItEasy;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MsAuthorizationResult = Microsoft.AspNetCore.Authorization.AuthorizationResult;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Hosting.Tests.AspNetCore.Authorization;

/// <summary>
/// Unit tests for <see cref="AspNetCoreAuthorizationMiddleware"/>.
/// </summary>
/// <remarks>
/// Sprint 503 - Task S503.6 (bd-i38rs): Comprehensive tests for the ASP.NET Core authorization bridge.
/// Covers attribute reading, policy evaluation, role-based auth, AND logic, edge cases, and co-existence.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Authorization")]
public sealed class AspNetCoreAuthorizationMiddlewareShould
{
	private readonly IHttpContextAccessor _httpContextAccessor;
	private readonly IAuthorizationService _authorizationService;
	private readonly ILogger<AspNetCoreAuthorizationMiddleware> _logger;
	private readonly IMessageContext _context;
	private readonly DispatchRequestDelegate _successDelegate;

	public AspNetCoreAuthorizationMiddlewareShould()
	{
		_httpContextAccessor = A.Fake<IHttpContextAccessor>();
		_authorizationService = A.Fake<IAuthorizationService>();
		_logger = A.Fake<ILogger<AspNetCoreAuthorizationMiddleware>>();
		_context = A.Fake<IMessageContext>();

		_ = A.CallTo(() => _context.MessageId).Returns("test-msg-id");
		_ = A.CallTo(() => _context.GetItem<Type>("HandlerType")).Returns(null);

		_successDelegate = (msg, ctx, ct) => new ValueTask<IMessageResult>(MessageResult.Success());
	}

	private AspNetCoreAuthorizationMiddleware CreateMiddleware(
		Action<AspNetCoreAuthorizationOptions>? configure = null)
	{
		var options = new AspNetCoreAuthorizationOptions();
		configure?.Invoke(options);
		return new AspNetCoreAuthorizationMiddleware(
			_httpContextAccessor,
			_authorizationService,
			_logger,
			MsOptions.Create(options));
	}

	private void SetupAuthenticatedUser(string userId = "user-123", params string[] roles)
	{
		var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId) };
		foreach (var role in roles)
		{
			claims.Add(new Claim(ClaimTypes.Role, role));
		}

		var identity = new ClaimsIdentity(claims, "TestAuth");
		var principal = new ClaimsPrincipal(identity);
		var httpContext = new DefaultHttpContext { User = principal };
		_ = A.CallTo(() => _httpContextAccessor.HttpContext).Returns(httpContext);
	}

	private void SetupUnauthenticatedUser()
	{
		var httpContext = new DefaultHttpContext();
		_ = A.CallTo(() => _httpContextAccessor.HttpContext).Returns(httpContext);
	}

	private void SetupPolicySuccess(string policyName)
	{
		_ = A.CallTo(() => _authorizationService.AuthorizeAsync(
			A<ClaimsPrincipal>._,
			A<object?>._,
			policyName))
			.Returns(MsAuthorizationResult.Success());
	}

	private void SetupPolicyFailure(string policyName)
	{
		_ = A.CallTo(() => _authorizationService.AuthorizeAsync(
			A<ClaimsPrincipal>._,
			A<object?>._,
			policyName))
			.Returns(MsAuthorizationResult.Failed());
	}

	#region Constructor Tests

	[Fact]
	public void ThrowArgumentNullException_WhenHttpContextAccessorIsNull()
	{
		_ = Should.Throw<ArgumentNullException>(() =>
			new AspNetCoreAuthorizationMiddleware(
				null!, _authorizationService, _logger,
				MsOptions.Create(new AspNetCoreAuthorizationOptions())));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenAuthorizationServiceIsNull()
	{
		_ = Should.Throw<ArgumentNullException>(() =>
			new AspNetCoreAuthorizationMiddleware(
				_httpContextAccessor, null!, _logger,
				MsOptions.Create(new AspNetCoreAuthorizationOptions())));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		_ = Should.Throw<ArgumentNullException>(() =>
			new AspNetCoreAuthorizationMiddleware(
				_httpContextAccessor, _authorizationService, null!,
				MsOptions.Create(new AspNetCoreAuthorizationOptions())));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		_ = Should.Throw<ArgumentNullException>(() =>
			new AspNetCoreAuthorizationMiddleware(
				_httpContextAccessor, _authorizationService, _logger, null!));
	}

	#endregion

	#region Stage and Applicability Tests

	[Fact]
	public void HaveAuthorizationStage()
	{
		var middleware = CreateMiddleware();
		middleware.Stage.ShouldBe(DispatchMiddlewareStage.Authorization);
	}

	[Fact]
	public void ApplyToActionsOnly()
	{
		var middleware = CreateMiddleware();
		middleware.ApplicableMessageKinds.ShouldBe(MessageKinds.Action);
	}

	#endregion

	#region Disabled Middleware Tests

	[Fact]
	public async Task PassThrough_WhenDisabled()
	{
		var middleware = CreateMiddleware(o => o.Enabled = false);
		var message = new AuthorizedNoPolicy();

		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public async Task NotCallAuthorizationService_WhenDisabled()
	{
		var middleware = CreateMiddleware(o => o.Enabled = false);
		var message = new AuthorizedNoPolicy();

		_ = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		A.CallTo(() => _authorizationService.AuthorizeAsync(
			A<ClaimsPrincipal>._, A<object?>._, A<string>._))
			.MustNotHaveHappened();
	}

	#endregion

	#region No Attributes Tests

	[Fact]
	public async Task PassThrough_WhenNoAuthorizeAttributes()
	{
		SetupAuthenticatedUser();
		var middleware = CreateMiddleware();
		var message = new PlainMessage();

		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		result.IsSuccess.ShouldBeTrue();
	}

	#endregion

	#region AllowAnonymous Tests

	[Fact]
	public async Task BypassAuthorization_WhenMessageHasAllowAnonymous()
	{
		var middleware = CreateMiddleware();
		var message = new AnonymousMessage();
		// No HttpContext at all — should still pass
		_ = A.CallTo(() => _httpContextAccessor.HttpContext).Returns(null);

		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public async Task BypassAuthorization_WhenHandlerHasAllowAnonymous()
	{
		SetupAuthenticatedUser();
		_ = A.CallTo(() => _context.GetItem<Type>("HandlerType")).Returns(typeof(AllowAnonymousHandler));
		var middleware = CreateMiddleware();
		var message = new AuthorizedNoPolicy();

		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		result.IsSuccess.ShouldBeTrue();
	}

	#endregion

	#region HttpContext and Authentication Tests

	[Fact]
	public async Task Return403_WhenNoHttpContext_AndRequireAuthenticatedUser()
	{
		_ = A.CallTo(() => _httpContextAccessor.HttpContext).Returns(null);
		var middleware = CreateMiddleware(o => o.RequireAuthenticatedUser = true);
		var message = new AuthorizedNoPolicy();

		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		result.IsSuccess.ShouldBeFalse();
		_ = result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails.ErrorCode.ShouldBe(403);
	}

	[Fact]
	public async Task PassThrough_WhenNoHttpContext_AndNotRequireAuthenticatedUser()
	{
		_ = A.CallTo(() => _httpContextAccessor.HttpContext).Returns(null);
		var middleware = CreateMiddleware(o => o.RequireAuthenticatedUser = false);
		var message = new AuthorizedNoPolicy();

		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public async Task Return403_WhenUserNotAuthenticated_AndRequireAuthenticatedUser()
	{
		SetupUnauthenticatedUser();
		var middleware = CreateMiddleware(o => o.RequireAuthenticatedUser = true);
		var message = new AuthorizedNoPolicy();

		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		result.IsSuccess.ShouldBeFalse();
		_ = result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails.ErrorCode.ShouldBe(403);
	}

	#endregion

	#region Policy Evaluation Tests

	[Fact]
	public async Task Succeed_WhenPolicyPasses()
	{
		SetupAuthenticatedUser();
		SetupPolicySuccess("AdminOnly");
		var middleware = CreateMiddleware();
		var message = new AdminOnlyMessage();

		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public async Task Return403_WhenPolicyFails()
	{
		SetupAuthenticatedUser();
		SetupPolicyFailure("AdminOnly");
		var middleware = CreateMiddleware();
		var message = new AdminOnlyMessage();

		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		result.IsSuccess.ShouldBeFalse();
		_ = result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails.ErrorCode.ShouldBe(403);
		result.ProblemDetails.Detail.ShouldContain("AdminOnly");
	}

	[Fact]
	public async Task PassMessageAsResource_ToPolicyEvaluation()
	{
		SetupAuthenticatedUser();
		SetupPolicySuccess("AdminOnly");
		var middleware = CreateMiddleware();
		var message = new AdminOnlyMessage();

		_ = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		_ = A.CallTo(() => _authorizationService.AuthorizeAsync(
			A<ClaimsPrincipal>._,
			message,
			"AdminOnly"))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task UseDefaultPolicy_WhenAuthorizeHasNoPolicy()
	{
		SetupAuthenticatedUser();
		SetupPolicySuccess("DefaultPolicy");
		var middleware = CreateMiddleware(o => o.DefaultPolicy = "DefaultPolicy");
		var message = new AuthorizedNoPolicy();

		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		result.IsSuccess.ShouldBeTrue();
		_ = A.CallTo(() => _authorizationService.AuthorizeAsync(
			A<ClaimsPrincipal>._, A<object?>._, "DefaultPolicy"))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region Multiple Policy (AND Logic) Tests

	[Fact]
	public async Task EvaluateAllPolicies_WithAndLogic()
	{
		SetupAuthenticatedUser();
		SetupPolicySuccess("CanCreateOrders");
		SetupPolicySuccess("IsActive");
		var middleware = CreateMiddleware();
		var message = new MultiPolicyMessage();

		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		result.IsSuccess.ShouldBeTrue();
		_ = A.CallTo(() => _authorizationService.AuthorizeAsync(
			A<ClaimsPrincipal>._, A<object?>._, "CanCreateOrders"))
			.MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => _authorizationService.AuthorizeAsync(
			A<ClaimsPrincipal>._, A<object?>._, "IsActive"))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Return403_WhenAnyPolicyFails_WithAndLogic()
	{
		SetupAuthenticatedUser();
		SetupPolicySuccess("CanCreateOrders");
		SetupPolicyFailure("IsActive");
		var middleware = CreateMiddleware();
		var message = new MultiPolicyMessage();

		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		result.IsSuccess.ShouldBeFalse();
		_ = result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails.Detail.ShouldContain("IsActive");
	}

	#endregion

	#region Role-Based Authorization Tests

	[Fact]
	public async Task Succeed_WhenUserHasRequiredRole()
	{
		SetupAuthenticatedUser("user-1", "Admin");
		var middleware = CreateMiddleware();
		var message = new AdminRoleMessage();

		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public async Task Return403_WhenUserLacksRequiredRole()
	{
		SetupAuthenticatedUser("user-1", "User");
		var middleware = CreateMiddleware();
		var message = new AdminRoleMessage();

		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		result.IsSuccess.ShouldBeFalse();
		_ = result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails.Detail.ShouldContain("Admin");
	}

	[Fact]
	public async Task Succeed_WhenUserHasAnyOfMultipleRoles_OrLogic()
	{
		SetupAuthenticatedUser("user-1", "Manager");
		var middleware = CreateMiddleware();
		var message = new MultiRoleMessage();

		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public async Task Return403_WhenUserHasNoneOfMultipleRoles()
	{
		SetupAuthenticatedUser("user-1", "Guest");
		var middleware = CreateMiddleware();
		var message = new MultiRoleMessage();

		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		result.IsSuccess.ShouldBeFalse();
	}

	#endregion

	#region Handler Type Authorization Tests

	[Fact]
	public async Task ReadAuthorizeAttributes_FromHandlerType()
	{
		SetupAuthenticatedUser();
		SetupPolicySuccess("HandlerPolicy");
		_ = A.CallTo(() => _context.GetItem<Type>("HandlerType")).Returns(typeof(AuthorizedHandler));
		var middleware = CreateMiddleware();
		var message = new PlainMessage();

		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		result.IsSuccess.ShouldBeTrue();
		_ = A.CallTo(() => _authorizationService.AuthorizeAsync(
			A<ClaimsPrincipal>._, A<object?>._, "HandlerPolicy"))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task CombineAttributes_FromMessageAndHandler()
	{
		SetupAuthenticatedUser();
		SetupPolicySuccess("AdminOnly");
		SetupPolicySuccess("HandlerPolicy");
		_ = A.CallTo(() => _context.GetItem<Type>("HandlerType")).Returns(typeof(AuthorizedHandler));
		var middleware = CreateMiddleware();
		var message = new AdminOnlyMessage();

		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		result.IsSuccess.ShouldBeTrue();
		// Both policies should be evaluated (AND logic across message + handler)
		_ = A.CallTo(() => _authorizationService.AuthorizeAsync(
			A<ClaimsPrincipal>._, A<object?>._, "AdminOnly"))
			.MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => _authorizationService.AuthorizeAsync(
			A<ClaimsPrincipal>._, A<object?>._, "HandlerPolicy"))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region Error Handling Tests

	[Fact]
	public async Task Return403_WhenAuthorizationServiceThrows()
	{
		SetupAuthenticatedUser();
		_ = A.CallTo(() => _authorizationService.AuthorizeAsync(
			A<ClaimsPrincipal>._, A<object?>._, A<string>._))
			.Throws(new InvalidOperationException("Service error"));
		var middleware = CreateMiddleware();
		var message = new AdminOnlyMessage();

		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		result.IsSuccess.ShouldBeFalse();
		_ = result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails.ErrorCode.ShouldBe(403);
		result.ProblemDetails.Detail.ShouldContain("error");
	}

	#endregion

	#region Parameter Validation Tests

	[Fact]
	public async Task ThrowArgumentNullException_WhenMessageIsNull()
	{
		var middleware = CreateMiddleware();
		_ = await Should.ThrowAsync<ArgumentNullException>(
			middleware.InvokeAsync(null!, _context, _successDelegate, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenContextIsNull()
	{
		var middleware = CreateMiddleware();
		_ = await Should.ThrowAsync<ArgumentNullException>(
			middleware.InvokeAsync(new PlainMessage(), null!, _successDelegate, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenNextDelegateIsNull()
	{
		var middleware = CreateMiddleware();
		_ = await Should.ThrowAsync<ArgumentNullException>(
			middleware.InvokeAsync(new PlainMessage(), _context, null!, CancellationToken.None).AsTask());
	}

	#endregion

	#region 403 Result Structure Tests

	[Fact]
	public async Task Return403_WithCorrectProblemDetailsStructure()
	{
		SetupAuthenticatedUser();
		SetupPolicyFailure("AdminOnly");
		var middleware = CreateMiddleware();
		var message = new AdminOnlyMessage();

		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		result.IsSuccess.ShouldBeFalse();
		var pd = result.ProblemDetails;
		_ = pd.ShouldNotBeNull();
		pd.Title.ShouldBe("Authorization Failed");
		pd.ErrorCode.ShouldBe(403);
		pd.ErrorCode.ShouldBe(403);
		pd.Type.ShouldBe("about:blank");
	}

	#endregion

	#region Test Fixtures

	/// <summary>No authorization attributes — should pass through.</summary>
	private sealed class PlainMessage : IDispatchMessage;

	/// <summary>Single named policy.</summary>
	[Authorize("AdminOnly")]
	private sealed class AdminOnlyMessage : IDispatchMessage;

	/// <summary>Multiple policies — AND logic.</summary>
	[Authorize("CanCreateOrders")]
	[Authorize("IsActive")]
	private sealed class MultiPolicyMessage : IDispatchMessage;

	/// <summary>Role-based authorization.</summary>
	[Authorize(Roles = "Admin")]
	private sealed class AdminRoleMessage : IDispatchMessage;

	/// <summary>Multiple roles — OR logic within single attribute.</summary>
	[Authorize(Roles = "Admin,Manager")]
	private sealed class MultiRoleMessage : IDispatchMessage;

	/// <summary>[Authorize] with no policy name — uses default policy if configured.</summary>
	[Authorize]
	private sealed class AuthorizedNoPolicy : IDispatchMessage;

	/// <summary>[AllowAnonymous] bypasses authorization.</summary>
	[AllowAnonymous]
	private sealed class AnonymousMessage : IDispatchMessage;

	/// <summary>Handler with [Authorize] policy.</summary>
	[Authorize("HandlerPolicy")]
	private sealed class AuthorizedHandler;

	/// <summary>Handler with [AllowAnonymous].</summary>
	[AllowAnonymous]
	private sealed class AllowAnonymousHandler;

	#endregion
}
