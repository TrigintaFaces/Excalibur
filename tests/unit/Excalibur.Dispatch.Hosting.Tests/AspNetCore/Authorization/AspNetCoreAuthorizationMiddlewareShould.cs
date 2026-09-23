// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Security.Claims;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Hosting.AspNetCore;

using FakeItEasy;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Hosting.Tests.AspNetCore.Authorization;

/// <summary>
/// Unit tests for <see cref="AspNetCoreAuthorizationMiddleware"/>.
/// </summary>
/// <remarks>
/// <para>
/// Covers attribute reading, policy composition, role-based authorization, AND logic across message and
/// handler attributes, edge cases, and co-existence with the rest of the pipeline.
/// </para>
/// <para>
/// The middleware does not evaluate policy itself: it composes one policy from every declared attribute
/// through the host's <see cref="IAuthorizationPolicyProvider"/> and hands it to the host's
/// <see cref="IAuthorizationService"/>. Arms that exercise policy or role evaluation therefore build a
/// REAL authorization stack (see <see cref="AuthorizationHost"/>) rather than faking the service — a fake
/// has no consumer configuration to honour, and honouring the consumer's configuration is the property
/// under test. A fake is kept only where an arm deliberately makes evaluation FAULT.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Authorization")]
public sealed class AspNetCoreAuthorizationMiddlewareShould
{
	/// <summary>
	/// The only detail a denial returns to the caller. Hardcoded because this project is not in the
	/// middleware's <c>InternalsVisibleTo</c> set — keep in sync with
	/// <c>AspNetCoreAuthorizationMiddleware.DenialDetail</c>.
	/// </summary>
	private const string ExpectedDenialDetail = "You do not have permission to access this resource";

	/// <summary>
	/// The only detail an evaluation fault returns to the caller. Hardcoded for the same reason — keep in
	/// sync with <c>AspNetCoreAuthorizationMiddleware.ServerErrorDetail</c>.
	/// </summary>
	private const string ExpectedServerErrorDetail = "An internal error occurred while evaluating authorization.";

	private readonly IHttpContextAccessor _httpContextAccessor;
	private readonly IAuthorizationService _authorizationService;

	/// <summary>
	/// The production registry, not a stand-in. It OVERRIDES the interface's <c>GetAll</c>-filtering default
	/// for <c>GetHandlers</c> with an index lookup, and that override is the one the middleware calls — so a
	/// hand-written registry here would exercise a lookup that never runs in production. The distinction it
	/// answers is load-bearing: an empty result is UNDETERMINED and the middleware refuses on it, so
	/// "registered" versus "unknown" has to be decided by the real implementation.
	/// </summary>
	private readonly HandlerRegistry _handlerRegistry;
	private readonly ILogger<AspNetCoreAuthorizationMiddleware> _logger;
	private readonly IMessageContext _context;
	private readonly DispatchRequestDelegate _successDelegate;
	private readonly Dictionary<string, object> _items;

	/// <summary>Counts how many times the pipeline continued past the middleware.</summary>
	private int _nextInvocations;

	public AspNetCoreAuthorizationMiddlewareShould()
	{
		_httpContextAccessor = A.Fake<IHttpContextAccessor>();
		_authorizationService = A.Fake<IAuthorizationService>();
		_handlerRegistry = new HandlerRegistry();
		_logger = A.Fake<ILogger<AspNetCoreAuthorizationMiddleware>>();
		_context = A.Fake<IMessageContext>();
		_items = new Dictionary<string, object>();

		_ = A.CallTo(() => _context.MessageId).Returns("test-msg-id");
		_ = A.CallTo(() => _context.Items).Returns(_items);

		_successDelegate = (msg, ctx, ct) =>
		{
			_nextInvocations++;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		};
	}

	/// <summary>
	/// The host's own policy provider, over a default <see cref="AuthorizationOptions"/> -- the shape a host
	/// has after <c>AddAuthorization()</c> with no further configuration. Not a fake: the middleware composes
	/// policies through this, and a fake provider has no consumer configuration to honour, which is the exact
	/// property these arms exist to check.
	/// </summary>
	private static readonly IAuthorizationPolicyProvider HostPolicyProvider =
		new DefaultAuthorizationPolicyProvider(MsOptions.Create(new AuthorizationOptions()));

	private AspNetCoreAuthorizationMiddleware CreateMiddleware(
		Action<AspNetCoreAuthorizationOptions>? configure = null)
	{
		var options = new AspNetCoreAuthorizationOptions();
		configure?.Invoke(options);
		return new AspNetCoreAuthorizationMiddleware(
			_httpContextAccessor,
			_authorizationService,
			HostPolicyProvider,
			_handlerRegistry,
			MsOptions.Create(options),
			_logger);
	}

	/// <summary>
	/// Builds the middleware over a REAL authorization stack, so an arm asks what the CONSUMER's
	/// configuration does rather than what a mock was told to say.
	/// </summary>
	private AspNetCoreAuthorizationMiddleware CreateMiddlewareOver(
		AuthorizationHost host,
		Action<AspNetCoreAuthorizationOptions>? configure = null)
	{
		var options = new AspNetCoreAuthorizationOptions();
		configure?.Invoke(options);
		return new AspNetCoreAuthorizationMiddleware(
			_httpContextAccessor,
			host.Service,
			host.Provider,
			_handlerRegistry,
			MsOptions.Create(options),
			_logger);
	}

	/// <summary>The named policies the policy arms below resolve through the host.</summary>
	private static AuthorizationHost CreateNamedPolicyHost() => AuthorizationHost.Create(static options =>
	{
		options.AddPolicy("AdminOnly", static p => p.RequireClaim("scope", "admin"));
		options.AddPolicy("CanCreateOrders", static p => p.RequireClaim("scope", "orders.write"));
		options.AddPolicy("IsActive", static p => p.RequireClaim("status", "active"));
		options.AddPolicy("HandlerPolicy", static p => p.RequireClaim("scope", "handler"));
	});

	private void SetupAuthenticatedUser(string userId = "user-123", params string[] roles)
	{
		var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId) };
		foreach (var role in roles)
		{
			claims.Add(new Claim(ClaimTypes.Role, role));
		}

		SetupPrincipal(claims);
	}

	/// <summary>Authenticates a principal carrying the supplied claims, for arms whose policies require them.</summary>
	private void SetupAuthenticatedUserWithClaims(params Claim[] claims)
	{
		var all = new List<Claim> { new(ClaimTypes.NameIdentifier, "user-123") };
		all.AddRange(claims);
		SetupPrincipal(all);
	}

	private void SetupPrincipal(IEnumerable<Claim> claims)
	{
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

	#region Constructor Tests

	[Fact]
	public void ThrowArgumentNullException_WhenHttpContextAccessorIsNull()
	{
		_ = Should.Throw<ArgumentNullException>(() =>
			new AspNetCoreAuthorizationMiddleware(
				null!, _authorizationService, HostPolicyProvider, _handlerRegistry,
				MsOptions.Create(new AspNetCoreAuthorizationOptions()), _logger));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenAuthorizationServiceIsNull()
	{
		_ = Should.Throw<ArgumentNullException>(() =>
			new AspNetCoreAuthorizationMiddleware(
				_httpContextAccessor, null!, HostPolicyProvider, _handlerRegistry,
				MsOptions.Create(new AspNetCoreAuthorizationOptions()), _logger));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenPolicyProviderIsNull()
	{
		_ = Should.Throw<ArgumentNullException>(() =>
			new AspNetCoreAuthorizationMiddleware(
				_httpContextAccessor, _authorizationService, null!, _handlerRegistry,
				MsOptions.Create(new AspNetCoreAuthorizationOptions()), _logger));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenHandlerRegistryIsNull()
	{
		_ = Should.Throw<ArgumentNullException>(() =>
			new AspNetCoreAuthorizationMiddleware(
				_httpContextAccessor, _authorizationService, HostPolicyProvider, null!,
				MsOptions.Create(new AspNetCoreAuthorizationOptions()), _logger));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		_ = Should.Throw<ArgumentNullException>(() =>
			new AspNetCoreAuthorizationMiddleware(
				_httpContextAccessor, _authorizationService, HostPolicyProvider, _handlerRegistry,
				MsOptions.Create(new AspNetCoreAuthorizationOptions()), null!));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		_ = Should.Throw<ArgumentNullException>(() =>
			new AspNetCoreAuthorizationMiddleware(
				_httpContextAccessor, _authorizationService, HostPolicyProvider, _handlerRegistry,
				null!, _logger));
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
	public void ApplyToActionsAndEvents()
	{
		var middleware = CreateMiddleware();
		middleware.ApplicableMessageKinds.ShouldBe(MessageKinds.Action | MessageKinds.Event);
	}

	#endregion

	#region Disabled Middleware Tests

	[Fact]
	public async Task PassThrough_WhenDisabled()
	{
		var middleware = CreateMiddleware(o => o.Enabled = false);
		var message = new AuthorizedNoPolicy();

		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		result.Succeeded.ShouldBeTrue();
		_nextInvocations.ShouldBe(1);
	}

	[Fact]
	public async Task NotCallAuthorizationService_WhenDisabled()
	{
		var middleware = CreateMiddleware(o => o.Enabled = false);
		var message = new AuthorizedNoPolicy();

		_ = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		// The composed-policy call reaches the service through its requirements overload, so that is the
		// overload a "must not have happened" assertion has to name to be non-vacuous.
		A.CallTo(() => _authorizationService.AuthorizeAsync(
			A<ClaimsPrincipal>._, A<object?>._, A<IEnumerable<IAuthorizationRequirement>>._))
			.MustNotHaveHappened();
	}

	#endregion

	#region No Attributes Tests

	[Fact]
	public async Task PassThrough_WhenHandlerIsKnown_AndNeitherDeclaresAuthorization()
	{
		// LIVENESS partner to the refusal below: when the handlers ARE known and none of them (nor the
		// message) declares a requirement, "no attributes" genuinely means "nothing is required".
		SetupAuthenticatedUser();
		_handlerRegistry.Register(typeof(PlainMessage), typeof(PlainHandler), expectsResponse: false);
		var middleware = CreateMiddleware();
		var message = new PlainMessage();

		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		result.Succeeded.ShouldBeTrue();
		_nextInvocations.ShouldBe(1);
	}

	[Fact]
	public async Task Refuse_WhenHandlersAreIndeterminable_AndNoAuthorizationIsDeclared()
	{
		// SAFETY. An empty registry answer is UNKNOWN, not "nothing applies". Reading an absence of metadata
		// as an absence of a requirement is how an unattributed message reaches a protected handler.
		SetupAuthenticatedUser();
		var middleware = CreateMiddleware();
		var message = new PlainMessage();

		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		result.Succeeded.ShouldBeFalse();
		_ = result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails.ErrorCode.ShouldBe(403);
		_nextInvocations.ShouldBe(0);
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

		result.Succeeded.ShouldBeTrue();
		_nextInvocations.ShouldBe(1);
	}

	[Fact]
	public async Task BypassAuthorization_WhenHandlerHasAllowAnonymous()
	{
		SetupAuthenticatedUser();
		_items["HandlerType"] = typeof(AllowAnonymousHandler);
		var middleware = CreateMiddleware();
		var message = new AuthorizedNoPolicy();

		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		result.Succeeded.ShouldBeTrue();
		_nextInvocations.ShouldBe(1);
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

		result.Succeeded.ShouldBeFalse();
		_ = result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails.ErrorCode.ShouldBe(403);
		_nextInvocations.ShouldBe(0);
	}

	[Fact]
	public async Task PassThrough_WhenNoHttpContext_AndNotRequireAuthenticatedUser()
	{
		_ = A.CallTo(() => _httpContextAccessor.HttpContext).Returns(null);
		var middleware = CreateMiddleware(o => o.RequireAuthenticatedUser = false);
		var message = new AuthorizedNoPolicy();

		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		result.Succeeded.ShouldBeTrue();
		_nextInvocations.ShouldBe(1);
	}

	[Fact]
	public async Task Return403_WhenUserNotAuthenticated_AndRequireAuthenticatedUser()
	{
		SetupUnauthenticatedUser();
		var middleware = CreateMiddleware(o => o.RequireAuthenticatedUser = true);
		var message = new AuthorizedNoPolicy();

		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		result.Succeeded.ShouldBeFalse();
		_ = result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails.ErrorCode.ShouldBe(403);
		_nextInvocations.ShouldBe(0);
	}

	#endregion

	#region Policy Evaluation Tests

	[Fact]
	public async Task Succeed_WhenPolicyPasses()
	{
		using var host = CreateNamedPolicyHost();
		SetupAuthenticatedUserWithClaims(new Claim("scope", "admin"));
		var middleware = CreateMiddlewareOver(host);
		var message = new AdminOnlyMessage();

		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		result.Succeeded.ShouldBeTrue();
		_nextInvocations.ShouldBe(1);
	}

	[Fact]
	public async Task Return403_WhenPolicyFails()
	{
		using var host = CreateNamedPolicyHost();
		SetupAuthenticatedUserWithClaims(new Claim("scope", "reader"));
		var middleware = CreateMiddlewareOver(host);
		var message = new AdminOnlyMessage();

		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		result.Succeeded.ShouldBeFalse();
		_ = result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails.ErrorCode.ShouldBe(403);
		// The denial body is constant and never names the policy that refused the caller — the policy set
		// protecting a resource is not reconnaissance an under-privileged caller is entitled to.
		result.ProblemDetails.Detail.ShouldBe(ExpectedDenialDetail);
		result.ProblemDetails.Detail.ShouldNotContain("AdminOnly");
		_nextInvocations.ShouldBe(0);
	}

	[Fact]
	public async Task PassMessageAsResource_ToPolicyEvaluation()
	{
		var requirement = new ResourceCapturingRequirement();
		using var host = AuthorizationHost.Create(
			options => options.AddPolicy("CapturesResource", p => p.AddRequirements(requirement)),
			static services => services.AddSingleton<IAuthorizationHandler, ResourceCapturingHandler>());

		SetupAuthenticatedUser();
		var middleware = CreateMiddlewareOver(host);
		var message = new ResourceProbeMessage();

		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		result.Succeeded.ShouldBeTrue();
		// Asserted through the real handler's own context rather than a mock's recorded argument: the
		// message must arrive at the requirement as the authorization RESOURCE, so a consumer's
		// resource-based requirement can inspect it.
		requirement.CapturedResource.ShouldBeSameAs(message);
	}

	[Fact]
	public async Task Deny_WhenBareAuthorize_AndTheHostDefaultPolicyIsNotSatisfied()
	{
		// SAFETY. A bare [Authorize] resolves the HOST's default policy. A consumer who has hardened that
		// policy must have the hardening enforced here — the middleware has no default-policy setting of
		// its own to collapse to, and collapsing to "any authenticated user" is the weakening this arm bars.
		using var host = AuthorizationHost.Create(static options =>
			options.DefaultPolicy = new AuthorizationPolicyBuilder()
				.RequireAuthenticatedUser()
				.RequireClaim("scope", "orders.write")
				.Build());

		SetupAuthenticatedUserWithClaims(new Claim("scope", "orders.read"));
		var middleware = CreateMiddlewareOver(host);
		var message = new AuthorizedNoPolicy();

		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		result.Succeeded.ShouldBeFalse();
		_ = result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails.ErrorCode.ShouldBe(403);
		_nextInvocations.ShouldBe(0);
	}

	[Fact]
	public async Task Allow_WhenBareAuthorize_AndTheHostDefaultPolicyIsSatisfied()
	{
		// LIVENESS partner to the arm above. Without it a middleware that denies everything would satisfy
		// the safety assertion, and inaction would read as correctness.
		using var host = AuthorizationHost.Create(static options =>
			options.DefaultPolicy = new AuthorizationPolicyBuilder()
				.RequireAuthenticatedUser()
				.RequireClaim("scope", "orders.write")
				.Build());

		SetupAuthenticatedUserWithClaims(new Claim("scope", "orders.write"));
		var middleware = CreateMiddlewareOver(host);
		var message = new AuthorizedNoPolicy();

		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		result.Succeeded.ShouldBeTrue();
		_nextInvocations.ShouldBe(1);
	}

	#endregion

	#region Multiple Policy (AND Logic) Tests

	[Fact]
	public async Task EvaluateAllPolicies_WithAndLogic()
	{
		using var host = CreateNamedPolicyHost();
		SetupAuthenticatedUserWithClaims(
			new Claim("scope", "orders.write"),
			new Claim("status", "active"));
		var middleware = CreateMiddlewareOver(host);
		var message = new MultiPolicyMessage();

		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		result.Succeeded.ShouldBeTrue();
		_nextInvocations.ShouldBe(1);
	}

	[Fact]
	public async Task Return403_WhenAnyPolicyFails_WithAndLogic()
	{
		using var host = CreateNamedPolicyHost();
		// Satisfies CanCreateOrders but NOT IsActive — the AND across two [Authorize] attributes is the
		// property, asserted through the composed policy rather than by counting per-policy service calls.
		SetupAuthenticatedUserWithClaims(new Claim("scope", "orders.write"));
		var middleware = CreateMiddlewareOver(host);
		var message = new MultiPolicyMessage();

		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		result.Succeeded.ShouldBeFalse();
		_ = result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails.Detail.ShouldBe(ExpectedDenialDetail);
		result.ProblemDetails.Detail.ShouldNotContain("IsActive");
		_nextInvocations.ShouldBe(0);
	}

	#endregion

	#region Role-Based Authorization Tests

	[Fact]
	public async Task Succeed_WhenUserHasRequiredRole()
	{
		using var host = AuthorizationHost.Create(static _ => { });
		SetupAuthenticatedUser("user-1", "Admin");
		var middleware = CreateMiddlewareOver(host);
		var message = new AdminRoleMessage();

		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		result.Succeeded.ShouldBeTrue();
		_nextInvocations.ShouldBe(1);
	}

	[Fact]
	public async Task Return403_WhenUserLacksRequiredRole()
	{
		using var host = AuthorizationHost.Create(static _ => { });
		SetupAuthenticatedUser("user-1", "User");
		var middleware = CreateMiddlewareOver(host);
		var message = new AdminRoleMessage();

		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		result.Succeeded.ShouldBeFalse();
		_ = result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails.Detail.ShouldBe(ExpectedDenialDetail);
		result.ProblemDetails.Detail.ShouldNotContain("Admin");
		_nextInvocations.ShouldBe(0);
	}

	[Fact]
	public async Task Succeed_WhenUserHasAnyOfMultipleRoles_OrLogic()
	{
		using var host = AuthorizationHost.Create(static _ => { });
		SetupAuthenticatedUser("user-1", "Manager");
		var middleware = CreateMiddlewareOver(host);
		var message = new MultiRoleMessage();

		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		result.Succeeded.ShouldBeTrue();
		_nextInvocations.ShouldBe(1);
	}

	[Fact]
	public async Task Return403_WhenUserHasNoneOfMultipleRoles()
	{
		using var host = AuthorizationHost.Create(static _ => { });
		SetupAuthenticatedUser("user-1", "Guest");
		var middleware = CreateMiddlewareOver(host);
		var message = new MultiRoleMessage();

		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		result.Succeeded.ShouldBeFalse();
		_nextInvocations.ShouldBe(0);
	}

	#endregion

	#region Handler Type Authorization Tests

	[Fact]
	public async Task ReadAuthorizeAttributes_FromSeededHandlerType()
	{
		using var host = CreateNamedPolicyHost();
		// The MESSAGE carries no attributes, so a middleware that ignored the handler type would have
		// nothing to enforce. Denial is therefore the direction that proves the handler was read at all.
		SetupAuthenticatedUserWithClaims(new Claim("scope", "reader"));
		_items["HandlerType"] = typeof(AuthorizedHandler);
		var middleware = CreateMiddlewareOver(host);
		var message = new PlainMessage();

		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		result.Succeeded.ShouldBeFalse();
		_ = result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails.ErrorCode.ShouldBe(403);
		_nextInvocations.ShouldBe(0);
	}

	[Fact]
	public async Task Succeed_WhenSeededHandlerPolicyIsSatisfied()
	{
		// LIVENESS partner: the handler's policy is enforced, not merely always-denied.
		using var host = CreateNamedPolicyHost();
		SetupAuthenticatedUserWithClaims(new Claim("scope", "handler"));
		_items["HandlerType"] = typeof(AuthorizedHandler);
		var middleware = CreateMiddlewareOver(host);
		var message = new PlainMessage();

		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		result.Succeeded.ShouldBeTrue();
		_nextInvocations.ShouldBe(1);
	}

	[Fact]
	public async Task ReadAuthorizeAttributes_FromTheRegisteredHandler_WhenNoHandlerTypeIsSeeded()
	{
		// Nothing on the ordinary dispatch path seeds the context item, so the registry is the only route by
		// which a handler's own [Authorize] can be consulted. Without this the message below — which
		// declares nothing itself — would execute a protected handler for any caller.
		using var host = CreateNamedPolicyHost();
		SetupAuthenticatedUserWithClaims(new Claim("scope", "reader"));
		_handlerRegistry.Register(typeof(PlainMessage), typeof(AuthorizedHandler), expectsResponse: false);
		var middleware = CreateMiddlewareOver(host);
		var message = new PlainMessage();

		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		result.Succeeded.ShouldBeFalse();
		_ = result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails.ErrorCode.ShouldBe(403);
		_nextInvocations.ShouldBe(0);
	}

	[Fact]
	public async Task Succeed_WhenTheRegisteredHandlersPolicyIsSatisfied()
	{
		// LIVENESS partner to the arm above.
		using var host = CreateNamedPolicyHost();
		SetupAuthenticatedUserWithClaims(new Claim("scope", "handler"));
		_handlerRegistry.Register(typeof(PlainMessage), typeof(AuthorizedHandler), expectsResponse: false);
		var middleware = CreateMiddlewareOver(host);
		var message = new PlainMessage();

		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		result.Succeeded.ShouldBeTrue();
		_nextInvocations.ShouldBe(1);
	}

	[Fact]
	public async Task CombineAttributes_FromMessageAndHandler()
	{
		using var host = CreateNamedPolicyHost();
		SetupAuthenticatedUserWithClaims(
			new Claim("scope", "admin"),
			new Claim("scope", "handler"));
		_items["HandlerType"] = typeof(AuthorizedHandler);
		var middleware = CreateMiddlewareOver(host);
		var message = new AdminOnlyMessage();

		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		result.Succeeded.ShouldBeTrue();
		_nextInvocations.ShouldBe(1);
	}

	[Fact]
	public async Task Return403_WhenHandlerPolicyFails_EvenThoughMessagePolicyPasses()
	{
		// The AND across message + handler attributes: satisfying the message's policy alone is not enough.
		using var host = CreateNamedPolicyHost();
		SetupAuthenticatedUserWithClaims(new Claim("scope", "admin"));
		_items["HandlerType"] = typeof(AuthorizedHandler);
		var middleware = CreateMiddlewareOver(host);
		var message = new AdminOnlyMessage();

		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		result.Succeeded.ShouldBeFalse();
		_ = result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails.ErrorCode.ShouldBe(403);
		_nextInvocations.ShouldBe(0);
	}

	#endregion

	#region Error Handling Tests

	[Fact]
	public async Task Return500_WhenAuthorizationServiceThrows()
	{
		SetupAuthenticatedUser();

		// A fake is correct HERE and only here: the arm needs evaluation to FAULT, which a real service will
		// not do. The message is a bare [Authorize], so composition against the real default policy provider
		// succeeds and the only thing left that can throw is the service call itself.
		_ = A.CallTo(() => _authorizationService.AuthorizeAsync(
			A<ClaimsPrincipal>._, A<object?>._, A<IEnumerable<IAuthorizationRequirement>>._))
			.Throws(new InvalidOperationException("Service error"));

		var middleware = CreateMiddleware();
		var message = new AuthorizedNoPolicy();

		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		// An authorization-EVALUATION exception must fail closed as HTTP 500 with a generic sanitized detail
		// — NOT 403 (which is a policy denial) and NOT leaking ex.Message. (Literal is hardcoded: this
		// project is not in the middleware's InternalsVisibleTo set.)
		result.Succeeded.ShouldBeFalse();
		_ = result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails.ErrorCode.ShouldBe(500);
		result.ProblemDetails.Status.ShouldBe(500);
		result.ProblemDetails.Detail.ShouldBe(ExpectedServerErrorDetail);
		result.ProblemDetails.Detail.ShouldNotContain("Service error");
		_nextInvocations.ShouldBe(0);

		// Pins WHICH fault produced the 500: the service was actually reached, so this is not a composition
		// failure wearing the same status code.
		A.CallTo(() => _authorizationService.AuthorizeAsync(
			A<ClaimsPrincipal>._, A<object?>._, A<IEnumerable<IAuthorizationRequirement>>._))
			.MustHaveHappenedOnceExactly();
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
		using var host = CreateNamedPolicyHost();
		SetupAuthenticatedUserWithClaims(new Claim("scope", "reader"));
		var middleware = CreateMiddlewareOver(host);
		var message = new AdminOnlyMessage();

		var result = await middleware.InvokeAsync(message, _context, _successDelegate, CancellationToken.None);

		result.Succeeded.ShouldBeFalse();
		var pd = result.ProblemDetails;
		_ = pd.ShouldNotBeNull();
		pd.Title.ShouldBe("Authorization Failed");
		pd.ErrorCode.ShouldBe(403);
		pd.Status.ShouldBe(403);
		pd.Type.ShouldBe("about:blank");
	}

	#endregion

	#region Test Fixtures

	/// <summary>
	/// Builds the authorization services a real ASP.NET Core host would have, so an arm can ask what a
	/// CONSUMER's configuration does rather than what a mock was told to say.
	/// </summary>
	private sealed class AuthorizationHost : IDisposable
	{
		private readonly ServiceProvider _container;

		private AuthorizationHost(ServiceProvider container) => _container = container;

		public IAuthorizationPolicyProvider Provider => _container.GetRequiredService<IAuthorizationPolicyProvider>();

		public IAuthorizationService Service => _container.GetRequiredService<IAuthorizationService>();

		/// <param name="configureAuthorization">The consumer's own <c>AddAuthorization</c> configuration.</param>
		/// <param name="configureServices">Additional registrations, e.g. a custom authorization handler.</param>
		public static AuthorizationHost Create(
			Action<AuthorizationOptions> configureAuthorization,
			Action<IServiceCollection>? configureServices = null)
		{
			var services = new ServiceCollection();
			_ = services.AddLogging();
			_ = services.AddAuthorization(configureAuthorization);
			configureServices?.Invoke(services);

			return new AuthorizationHost(services.BuildServiceProvider());
		}

		public void Dispose() => _container.Dispose();
	}

	/// <summary>A requirement that records the resource it was evaluated against.</summary>
	private sealed class ResourceCapturingRequirement : IAuthorizationRequirement
	{
		public object? CapturedResource { get; set; }
	}

	private sealed class ResourceCapturingHandler : AuthorizationHandler<ResourceCapturingRequirement>
	{
		protected override Task HandleRequirementAsync(
			AuthorizationHandlerContext context,
			ResourceCapturingRequirement requirement)
		{
			requirement.CapturedResource = context.Resource;
			context.Succeed(requirement);
			return Task.CompletedTask;
		}
	}

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

	/// <summary>[Authorize] with no policy name — resolves the host's default policy.</summary>
	[Authorize]
	private sealed class AuthorizedNoPolicy : IDispatchMessage;

	/// <summary>Carries a policy whose requirement records the authorization resource.</summary>
	[Authorize("CapturesResource")]
	private sealed class ResourceProbeMessage : IDispatchMessage;

	/// <summary>[AllowAnonymous] bypasses authorization.</summary>
	[AllowAnonymous]
	private sealed class AnonymousMessage : IDispatchMessage;

	/// <summary>Handler declaring no authorization of its own.</summary>
	private sealed class PlainHandler;

	/// <summary>Handler with [Authorize] policy.</summary>
	[Authorize("HandlerPolicy")]
	private sealed class AuthorizedHandler;

	/// <summary>Handler with [AllowAnonymous].</summary>
	[AllowAnonymous]
	private sealed class AllowAnonymousHandler;

	#endregion
}
