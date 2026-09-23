// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using System.Security.Claims;

using Excalibur.Dispatch.Delivery.Handlers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Hosting.AspNetCore;

/// <summary>
/// Middleware that bridges ASP.NET Core authorization into the Dispatch messaging pipeline.
/// </summary>
/// <remarks>
/// <para>
/// This middleware reads <see cref="AuthorizeAttribute"/> and <see cref="AllowAnonymousAttribute"/>
/// from message types and handler types, then evaluates the policies via ASP.NET Core's
/// <see cref="IAuthorizationService"/>. The <see cref="ClaimsPrincipal"/> is sourced from
/// <see cref="IHttpContextAccessor"/>.
/// </para>
/// <para>
/// Multiple <c>[Authorize]</c> attributes are combined with AND logic — all policies must pass.
/// Roles within a single <c>[Authorize(Roles = "A,B")]</c> are combined with OR logic — any matching role suffices.
/// </para>
/// <para>
/// This middleware co-exists with the Excalibur A3 authorization middleware (which processes
/// <c>[RequirePermission]</c> attributes) and the Dispatch core authorization middleware.
/// </para>
/// </remarks>
public sealed partial class AspNetCoreAuthorizationMiddleware : IDispatchMiddleware
{
	/// <summary>
	/// Maximum number of entries allowed in each attribute cache.
	/// When the cap is reached, new lookups compute attributes without caching to prevent unbounded memory growth.
	/// </summary>
	private const int MaxCacheEntries = 1024;

	/// <summary>
	/// Generic, non-leaking detail returned to the caller when authorization evaluation faults.
	/// The full exception is logged server-side (see <see cref="LogAuthorizationError"/>); this constant
	/// is intentionally free of any exception message, stack, or PII so nothing internal crosses the
	/// trust boundary in the consumer-facing response body.
	/// </summary>
	internal const string ServerErrorDetail = "An internal error occurred while evaluating authorization.";

	/// <summary>
	/// The ONLY detail a denial returns to the caller. Constant by design: policy names, role names and
	/// evaluation failure messages are the consumer's own authorization vocabulary, and disclosing them to an
	/// unauthenticated or under-privileged caller is reconnaissance value (CWE-200) with no benefit to a
	/// legitimate one, who cannot act on the name either way. The specific reason is logged server-side with the
	/// correlation id (see <see cref="LogAuthorizationDenied"/>), matching ASP.NET Core's own bare 403.
	/// </summary>
	internal const string DenialDetail = "You do not have permission to access this resource";

	private readonly IHttpContextAccessor _httpContextAccessor;
	private readonly IAuthorizationService _authorizationService;
	private readonly IAuthorizationPolicyProvider _policyProvider;
	private readonly IHandlerRegistry _handlerRegistry;
	private readonly ILogger<AspNetCoreAuthorizationMiddleware> _logger;
	private readonly AspNetCoreAuthorizationOptions _options;

	private static readonly ConcurrentDictionary<Type, AuthorizeAttribute[]> AuthorizeAttributeCache = new();
	private static readonly ConcurrentDictionary<Type, bool> AllowAnonymousCache = new();

	/// <summary>
	/// Initializes a new instance of the <see cref="AspNetCoreAuthorizationMiddleware"/> class.
	/// </summary>
	/// <param name="httpContextAccessor">Provides access to the current <see cref="HttpContext"/>.</param>
	/// <param name="authorizationService">The ASP.NET Core authorization service for policy evaluation.</param>
	/// <param name="policyProvider">
	/// The host's authorization policy provider. This is the same provider ASP.NET Core's own authorization
	/// middleware uses, which is what makes a consumer's configured and hardened policies apply here too.
	/// </param>
	/// <param name="handlerRegistry">
	/// The registry that maps a message type to the handler types that will process it. This is how the
	/// middleware learns what a handler requires; without it, a requirement declared on a handler is invisible
	/// here and would go unenforced.
	/// </param>
	/// <param name="options">Configuration options for this middleware.</param>
	/// <param name="logger">The logger instance.</param>
	public AspNetCoreAuthorizationMiddleware(
		IHttpContextAccessor httpContextAccessor,
		IAuthorizationService authorizationService,
		IAuthorizationPolicyProvider policyProvider,
		IHandlerRegistry handlerRegistry,
		IOptions<AspNetCoreAuthorizationOptions> options,
		ILogger<AspNetCoreAuthorizationMiddleware> logger)
	{
		ArgumentNullException.ThrowIfNull(httpContextAccessor);
		ArgumentNullException.ThrowIfNull(authorizationService);
		ArgumentNullException.ThrowIfNull(policyProvider);
		ArgumentNullException.ThrowIfNull(handlerRegistry);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_httpContextAccessor = httpContextAccessor;
		_authorizationService = authorizationService;
		_policyProvider = policyProvider;
		_handlerRegistry = handlerRegistry;
		_options = options.Value;
		_logger = logger;
	}

	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Authorization;

	/// <inheritdoc />
	/// <remarks>
	/// Authorization applies to both Actions (commands/queries) and Events. Events are not necessarily internal: an event arriving from a
	/// transport adapter is inbound and untrusted, so it is authorized on the same terms as an Action. Documents are excluded.
	/// <para>
	/// This type carries no <c>AppliesTo</c> attribute, so this property is the sole source of its applicability decision.
	/// </para>
	/// </remarks>
	public MessageKinds ApplicableMessageKinds => MessageKinds.Action | MessageKinds.Event;

	/// <inheritdoc />
	public async ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(nextDelegate);

		if (!_options.Enabled)
		{
			LogAuthorizationSkipped("middleware disabled");
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		var messageType = message.GetType();
		LogAuthorizationExecuting(messageType.Name);

		// WHICH HANDLERS WILL RUN. This used to read a context item alone, and nothing in the framework sets
		// that item on the ordinary dispatch path -- handler selection happens downstream of the middleware
		// pipeline -- so a handler's own [Authorize] was NEVER consulted and a message carrying no attributes
		// of its own executed a protected handler for any caller. The registry is asked instead; the context
		// item is still honoured first, because a host that already knows the handler has better information
		// than a lookup.
		var handlerTypes = ResolveHandlerTypes(context, messageType, out var handlersAreDeterminable);

		if (HasAllowAnonymous(messageType) || handlerTypes.Any(HasAllowAnonymous))
		{
			LogAllowAnonymousApplied(messageType.Name);
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		var messageAttributes = GetAuthorizeAttributes(messageType);
		var handlerAttributes = handlerTypes.SelectMany(GetAuthorizeAttributes).ToArray();

		if (messageAttributes.Length == 0 && handlerAttributes.Length == 0)
		{
			// THREE STATE, and the third one is the whole point: PASS, FAIL, REFUSE -- and REFUSE IS NOT PASS.
			// Finding no attributes means "nothing is required" ONLY if we know what would have declared them.
			// When the handlers cannot be determined, we do not know whether authorization applies, and an
			// absence of metadata must not be read as an absence of a requirement. A capability, or a
			// requirement, is declared -- never inferred from an absence.
			if (!handlersAreDeterminable)
			{
				// WHY THERE IS NO CARVE-OUT FOR EVENTS. Recorded because the next reader will propose one,
				// and because the first refutation of it was itself wrong -- a reader who finds only the
				// thread will inherit the wrong mechanism.
				//
				// The proposal: let an EVENT through when the registry does not know it, reasoning that an
				// unknown event has no subscriber to protect. It was withdrawn on the grounds that a
				// factory-registered IEventHandler is invisible to the registry yet still fans out, so
				// letting it through would authorize nothing while a handler ran. THAT GROUND IS FALSE and
				// is recorded here only so nobody re-derives it: LocalMessageBus.GetEventHandlers resolves
				// from the registry through all four of its paths and has no container fallback, so a
				// handler the registry cannot see does not run at all.
				//
				// THE ACTUAL REASON, which does not depend on any measurement of the current bus: this
				// middleware cannot know which IMessageBus is installed. AddMessageBus(..., Func<
				// IServiceProvider, IMessageBus>) is shipped public API, and a consumer bus that resolves
				// IEnumerable<IEventHandler<T>> from the container -- the obvious way to write one -- runs
				// handlers the registry never saw. A carve-out justified by the in-process bus's behaviour
				// is an authorization decision resting on an undeclared coupling to a swappable component.
				// Failing closed is correct under every bus, which is the only property available here.
				//
				// The cost is that publishing an event nobody subscribes to is also refused. That is a real
				// defect and it is tracked. Closing it needs a source that can distinguish "no subscriber
				// exists" from "a subscriber exists that this middleware cannot see" -- which is a question
				// about the installed bus, not about the registry.
				LogAuthorizationDenied(
					messageType.Name,
					context.CorrelationId,
					"The handlers for this message could not be determined, so it is unknown whether "
					+ "authorization applies. Register the handler through the dispatch handler registry, or "
					+ "declare the requirement on the message type, so the decision can be made rather than assumed.");
				return CreateForbiddenResult();
			}

			LogAuthorizationSkipped("no [Authorize] attributes found");
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		// Source ClaimsPrincipal from HttpContext
		var httpContext = _httpContextAccessor.HttpContext;
		if (httpContext is null)
		{
			if (_options.RequireAuthenticatedUser)
			{
				LogAuthorizationDenied(
					messageType.Name,
					context.CorrelationId,
					"No HttpContext available. Authorization cannot be evaluated outside of an HTTP request.");
				return CreateForbiddenResult();
			}

			LogAuthorizationSkipped("no HttpContext and RequireAuthenticatedUser is false");
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		var principal = httpContext.User;
		if (_options.RequireAuthenticatedUser && (principal.Identity is null || !principal.Identity.IsAuthenticated))
		{
			LogAuthorizationDenied(messageType.Name, context.CorrelationId, "User is not authenticated.");
			return CreateForbiddenResult();
		}

		// Build ONE policy from every attribute and evaluate it, exactly as ASP.NET Core's own authorization
		// middleware does. AuthorizationPolicy.CombineAsync is the host's combiner: it resolves each named
		// policy through the consumer's IAuthorizationPolicyProvider, folds Roles into RequireRole,
		// AND-combines multiple attributes, and -- the part that was missing -- applies the host's
		// GetDefaultPolicyAsync() for a bare [Authorize] that names no policy, no roles and no schemes.
		//
		// We do NOT evaluate policy ourselves any more. A hand-rolled evaluator cannot see consumer
		// configuration it does not own, so every knob it grows duplicates one the host already has and has
		// already been set. Consumers routinely HARDEN AuthorizationOptions.DefaultPolicy; the previous code
		// consulted a DefaultPolicy option of our own (null by default) and silently collapsed a bare
		// [Authorize] to "any authenticated user", which is weaker than what the host was told to require.
		try
		{
			var allAttributes = CombineAttributes(messageAttributes, handlerAttributes);

			var policy = await AuthorizationPolicy
				.CombineAsync(_policyProvider, allAttributes)
				.ConfigureAwait(false);

			if (policy is null)
			{
				// CombineAsync returns null only when it was handed no authorize data. We already returned
				// above in that case, so reaching here means the attribute set changed underneath us.
				// Undeterminable is not permitted to mean unrestricted.
				LogAuthorizationDenied(
					messageType.Name,
					context.CorrelationId,
					"No authorization policy could be composed from the declared attributes.");
				return CreateForbiddenResult();
			}

			var policyResult = await _authorizationService
				.AuthorizeAsync(principal, message, policy)
				.ConfigureAwait(false);

			if (!policyResult.Succeeded)
			{
				// SERVER-SIDE ONLY. The caller's response body is the constant DenialDetail and never varies;
				// this names what was required so a developer can diagnose a 403 without reproducing it.
				LogAuthorizationDenied(messageType.Name, context.CorrelationId, DescribeRequirements(allAttributes));
				return CreateForbiddenResult();
			}
		}
		catch (Exception ex)
		{
			// An evaluation FAULT is not an authorization DENIAL: log the full exception server-side and
			// return a generic 500 with NO exception text. Leaking ex.Message across the trust boundary
			// discloses internal detail; mapping a fault to 403 masks a 500-class error as a denial.
			LogAuthorizationError(messageType.Name, ex);
			return CreateServerErrorResult();
		}

		LogAuthorizationGranted(messageType.Name);
		return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Describes, for the SERVER-SIDE LOG ONLY, what the declared attributes required.
	/// </summary>
	/// <remarks>
	/// This string must never reach a response body. Policy and role names are the consumer's own
	/// authorization vocabulary, and disclosing them to an unauthenticated or under-privileged caller is
	/// reconnaissance value with no benefit to a legitimate one. The body is <see cref="DenialDetail"/>,
	/// which is constant; this exists so that a developer reading the log can still tell WHY a 403 happened.
	/// </remarks>
	private static string DescribeRequirements(AuthorizeAttribute[] attributes)
	{
		var policies = attributes
			.Select(static a => a.Policy)
			.Where(static p => !string.IsNullOrEmpty(p))
			.ToArray();

		var roles = attributes
			.Select(static a => a.Roles)
			.Where(static r => !string.IsNullOrEmpty(r))
			.ToArray();

		if (policies.Length == 0 && roles.Length == 0)
		{
			// A bare [Authorize] resolves the host's default policy, which carries no name to report.
			return "Authorization failed against the host's default policy.";
		}

		var required = new List<string>(2);
		if (policies.Length > 0)
		{
			required.Add("policy " + string.Join(", ", policies));
		}

		if (roles.Length > 0)
		{
			required.Add("roles " + string.Join(", ", roles));
		}

		return "Authorization failed. Required " + string.Join("; ", required) + ".";
	}

	/// <summary>
	/// Determines which handler types will process this message, and — separately — whether that question could
	/// be answered at all.
	/// </summary>
	/// <param name="context">The message context, which a host may have seeded with a known handler type.</param>
	/// <param name="messageType">The message being dispatched.</param>
	/// <param name="determinable">
	/// <see langword="true"/> when the handlers are known; <see langword="false"/> when nothing could answer.
	/// An empty list with <paramref name="determinable"/> <see langword="false"/> is UNKNOWN, not "none" — and
	/// the two must not be collapsed, because one of them is safe to pass and the other is not.
	/// </param>
	/// <returns>The handler types to read authorization metadata from; empty when none are known.</returns>
	private IReadOnlyList<Type> ResolveHandlerTypes(IMessageContext context, Type messageType, out bool determinable)
	{
		if (context.GetItem<Type>("HandlerType") is { } seeded)
		{
			determinable = true;
			return [seeded];
		}

		// The RETURN VALUE answers "can the registry speak for this message type", and the list answers "what
		// does it say". They are different questions and the first version of this code collapsed them:
		// it inferred determinability from a non-empty list, so a message type the registry KNOWS and has
		// NO handlers for -- an event with no subscribers, which is ordinary in pub/sub -- was reported as
		// undetermined and refused. Branch on the bool.
		if (!_handlerRegistry.TryGetHandlers(messageType, out var registered))
		{
			// Not "it has no handler": a composition may dispatch through a path the registry never saw.
			determinable = false;
			return [];
		}

		determinable = true;

		var types = new Type[registered.Count];
		for (var i = 0; i < registered.Count; i++)
		{
			types[i] = registered[i].HandlerType;
		}

		return types;
	}

	private static AuthorizeAttribute[] GetAuthorizeAttributes(Type type)
	{
		if (AuthorizeAttributeCache.TryGetValue(type, out var cached))
		{
			return cached;
		}

		if (AuthorizeAttributeCache.Count >= MaxCacheEntries)
		{
			// Cache full -- compute without caching to prevent unbounded growth
			return ComputeAuthorizeAttributes(type);
		}

		return AuthorizeAttributeCache.GetOrAdd(type, static t => ComputeAuthorizeAttributes(t));
	}

	private static AuthorizeAttribute[] ComputeAuthorizeAttributes(Type type)
	{
		var attrs = type.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true);
		if (attrs.Length == 0)
		{
			return [];
		}

		var result = new AuthorizeAttribute[attrs.Length];
		for (var i = 0; i < attrs.Length; i++)
		{
			result[i] = (AuthorizeAttribute)attrs[i];
		}

		return result;
	}

	private static bool HasAllowAnonymous(Type type)
	{
		if (AllowAnonymousCache.TryGetValue(type, out var cached))
		{
			return cached;
		}

		if (AllowAnonymousCache.Count >= MaxCacheEntries)
		{
			// Cache full -- compute without caching to prevent unbounded growth
			return type.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true).Length > 0;
		}

		return AllowAnonymousCache.GetOrAdd(type, static t =>
			t.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true).Length > 0);
	}

	private static AuthorizeAttribute[] CombineAttributes(AuthorizeAttribute[] first, AuthorizeAttribute[] second)
	{
		if (first.Length == 0)
		{
			return second;
		}

		if (second.Length == 0)
		{
			return first;
		}

		var combined = new AuthorizeAttribute[first.Length + second.Length];
		first.CopyTo(combined, 0);
		second.CopyTo(combined, first.Length);
		return combined;
	}

	/// <summary>
	/// Creates the 403 denial result. It takes no detail parameter on purpose: with nothing to pass, a future
	/// caller cannot reopen the disclosure by handing it a policy or role name. The specific reason travels to
	/// the log, never to the body.
	/// </summary>
	private static IMessageResult CreateForbiddenResult()
	{
		var problemDetails = new MessageProblemDetails
		{
			Type = "about:blank",
			Title = "Authorization Failed",
			ErrorCode = 403,
			Status = 403,
			Detail = DenialDetail,
			Instance = string.Empty,
		};

		return MessageResult.Failed(problemDetails);
	}

	/// <summary>
	/// Creates a fail-closed 500-class result for an authorization-evaluation fault. The detail is the
	/// generic <see cref="ServerErrorDetail"/> constant — it never carries the exception message, stack,
	/// or any PII, so no internal detail is disclosed to the caller.
	/// </summary>
	private static IMessageResult CreateServerErrorResult()
	{
		var problemDetails = new MessageProblemDetails
		{
			Type = "about:blank",
			Title = "Authorization Error",
			ErrorCode = 500,
			Status = 500,
			Detail = ServerErrorDetail,
			Instance = string.Empty,
		};

		return MessageResult.Failed(problemDetails);
	}

	// Source-generated logging methods

	[LoggerMessage(AspNetCoreAuthorizationEventId.AuthorizationExecuting, LogLevel.Debug,
		"ASP.NET Core authorization executing for message type {MessageType}")]
	private partial void LogAuthorizationExecuting(string messageType);

	[LoggerMessage(AspNetCoreAuthorizationEventId.AuthorizationGranted, LogLevel.Debug,
		"ASP.NET Core authorization granted for message type {MessageType}")]
	private partial void LogAuthorizationGranted(string messageType);

	[LoggerMessage(AspNetCoreAuthorizationEventId.AuthorizationDenied, LogLevel.Warning,
		"ASP.NET Core authorization denied for message type {MessageType} (correlation {CorrelationId}): {Reason}")]
	private partial void LogAuthorizationDenied(string messageType, string? correlationId, string reason);

	[LoggerMessage(AspNetCoreAuthorizationEventId.AuthorizationSkipped, LogLevel.Debug,
		"ASP.NET Core authorization skipped: {Reason}")]
	private partial void LogAuthorizationSkipped(string reason);

	[LoggerMessage(AspNetCoreAuthorizationEventId.AllowAnonymousApplied, LogLevel.Debug,
		"[AllowAnonymous] applied for message type {MessageType}; authorization bypassed")]
	private partial void LogAllowAnonymousApplied(string messageType);

	[LoggerMessage(AspNetCoreAuthorizationEventId.AuthorizationError, LogLevel.Error,
		"Error during ASP.NET Core authorization evaluation for message type {MessageType}")]
	private partial void LogAuthorizationError(string messageType, Exception ex);
}
