// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Security.Claims;

using Excalibur.Dispatch.Abstractions;

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
	private readonly IHttpContextAccessor _httpContextAccessor;
	private readonly IAuthorizationService _authorizationService;
	private readonly ILogger<AspNetCoreAuthorizationMiddleware> _logger;
	private readonly AspNetCoreAuthorizationOptions _options;

	private static readonly ConcurrentDictionary<Type, AuthorizeAttribute[]> AuthorizeAttributeCache = new();
	private static readonly ConcurrentDictionary<Type, bool> AllowAnonymousCache = new();

	/// <summary>
	/// Initializes a new instance of the <see cref="AspNetCoreAuthorizationMiddleware"/> class.
	/// </summary>
	/// <param name="httpContextAccessor">Provides access to the current <see cref="HttpContext"/>.</param>
	/// <param name="authorizationService">The ASP.NET Core authorization service for policy evaluation.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="options">Configuration options for this middleware.</param>
	public AspNetCoreAuthorizationMiddleware(
		IHttpContextAccessor httpContextAccessor,
		IAuthorizationService authorizationService,
		ILogger<AspNetCoreAuthorizationMiddleware> logger,
		IOptions<AspNetCoreAuthorizationOptions> options)
	{
		ArgumentNullException.ThrowIfNull(httpContextAccessor);
		ArgumentNullException.ThrowIfNull(authorizationService);
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(options);

		_httpContextAccessor = httpContextAccessor;
		_authorizationService = authorizationService;
		_logger = logger;
		_options = options.Value;
	}

	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Authorization;

	/// <inheritdoc />
	public MessageKinds ApplicableMessageKinds => MessageKinds.Action;

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

		// Check [AllowAnonymous] on message type or handler type
		var handlerType = context.GetItem<Type>("HandlerType");

		if (HasAllowAnonymous(messageType) || (handlerType is not null && HasAllowAnonymous(handlerType)))
		{
			LogAllowAnonymousApplied(messageType.Name);
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		// Collect [Authorize] attributes from message type and handler type
		var messageAttributes = GetAuthorizeAttributes(messageType);
		var handlerAttributes = handlerType is not null ? GetAuthorizeAttributes(handlerType) : [];

		if (messageAttributes.Length == 0 && handlerAttributes.Length == 0)
		{
			LogAuthorizationSkipped("no [Authorize] attributes found");
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		// Source ClaimsPrincipal from HttpContext
		var httpContext = _httpContextAccessor.HttpContext;
		if (httpContext is null)
		{
			if (_options.RequireAuthenticatedUser)
			{
				LogAuthorizationDenied(messageType.Name, "No HttpContext available");
				return CreateForbiddenResult("No HttpContext available. Authorization cannot be evaluated outside of an HTTP request.");
			}

			LogAuthorizationSkipped("no HttpContext and RequireAuthenticatedUser is false");
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		var principal = httpContext.User;
		if (_options.RequireAuthenticatedUser && (principal.Identity is null || !principal.Identity.IsAuthenticated))
		{
			LogAuthorizationDenied(messageType.Name, "User is not authenticated");
			return CreateForbiddenResult("User is not authenticated.");
		}

		// Evaluate all [Authorize] attributes (AND logic)
		try
		{
			var allAttributes = CombineAttributes(messageAttributes, handlerAttributes);

			foreach (var attr in allAttributes)
			{
				var result = await EvaluateAttributeAsync(principal, message, attr, cancellationToken)
					.ConfigureAwait(false);

				if (result is not null)
				{
					LogAuthorizationDenied(messageType.Name, result);
					return CreateForbiddenResult(result);
				}
			}
		}
		catch (Exception ex)
		{
			LogAuthorizationError(messageType.Name, ex);
			return CreateForbiddenResult($"An error occurred during authorization evaluation: {ex.Message}");
		}

		LogAuthorizationGranted(messageType.Name);
		return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Evaluates a single <see cref="AuthorizeAttribute"/>. Returns <see langword="null"/> if authorized,
	/// or an error detail string if denied.
	/// </summary>
	private async Task<string?> EvaluateAttributeAsync(
		ClaimsPrincipal principal,
		IDispatchMessage message,
		AuthorizeAttribute attr,
		CancellationToken cancellationToken)
	{
		_ = cancellationToken; // IAuthorizationService.AuthorizeAsync does not accept CancellationToken

		// Check policy
		var policyName = attr.Policy ?? _options.DefaultPolicy;
		if (!string.IsNullOrEmpty(policyName))
		{
			var policyResult = await _authorizationService
				.AuthorizeAsync(principal, message, policyName)
				.ConfigureAwait(false);

			if (!policyResult.Succeeded)
			{
				return $"Policy '{policyName}' evaluation failed.";
			}
		}

		// Check roles (OR logic within a single attribute)
		if (!string.IsNullOrEmpty(attr.Roles))
		{
			var roles = attr.Roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			var hasAnyRole = false;

			foreach (var role in roles)
			{
				if (principal.IsInRole(role))
				{
					hasAnyRole = true;
					break;
				}
			}

			if (!hasAnyRole)
			{
				return $"None of the required roles [{attr.Roles}] are present.";
			}
		}

		// Check authentication schemes — these are informational only in the Dispatch pipeline
		// (scheme enforcement is an HTTP-level concern, not a message pipeline concern).

		return null;
	}

	private static AuthorizeAttribute[] GetAuthorizeAttributes(Type type)
	{
		return AuthorizeAttributeCache.GetOrAdd(type, static t =>
		{
			var attrs = t.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true);
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
		});
	}

	private static bool HasAllowAnonymous(Type type)
	{
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

	private static IMessageResult CreateForbiddenResult(string detail)
	{
		var problemDetails = new MessageProblemDetails
		{
			Type = "about:blank",
			Title = "Authorization Failed",
			ErrorCode = 403,
			Status = 403,
			Detail = detail,
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
		"ASP.NET Core authorization denied for message type {MessageType}: {Reason}")]
	private partial void LogAuthorizationDenied(string messageType, string reason);

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
