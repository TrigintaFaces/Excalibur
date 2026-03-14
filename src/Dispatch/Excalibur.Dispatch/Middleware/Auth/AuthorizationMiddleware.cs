// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Abstractions.Features;
using Excalibur.Dispatch.Abstractions.Telemetry;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Options.Middleware;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using IMessageContext = Excalibur.Dispatch.Abstractions.IMessageContext;
using IMessageResult = Excalibur.Dispatch.Abstractions.IMessageResult;
using MessageKinds = Excalibur.Dispatch.Abstractions.MessageKinds;

namespace Excalibur.Dispatch.Middleware.Auth;

/// <summary>
/// Middleware responsible for authorizing message processing based on user roles, claims, grants, or other authorization policies.
/// </summary>
/// <remarks>
/// This middleware operates after authentication and tenant resolution to enforce authorization policies. It:
/// <list type="bullet">
/// <item> Extracts user/service identity from context </item>
/// <item> Evaluates authorization policies against message types </item>
/// <item> Supports role-based, claim-based, and resource-based authorization </item>
/// <item> Integrates with external authorization systems (OPA, etc.) </item>
/// <item> Provides extensibility for custom authorization logic </item>
/// </list>
/// </remarks>
[AppliesTo(MessageKinds.Action)]
[RequiresFeatures(DispatchFeatures.Authorization)]
public sealed partial class AuthorizationMiddleware : IDispatchMiddleware
{
	private static readonly ClaimLookup[] CommonClaimLookups =
	[
		new ClaimLookup("UserId", "claim:UserId"),
		new ClaimLookup("TenantId", "claim:TenantId"),
		new ClaimLookup("Role", "claim:Role"),
		new ClaimLookup("Email", "claim:Email"),
		new ClaimLookup("Name", "claim:Name"),
		new ClaimLookup("Sub", "claim:Sub"),
		new ClaimLookup("Aud", "claim:Aud"),
		new ClaimLookup("Iss", "claim:Iss"),
	];
	private static readonly ConcurrentDictionary<Type, bool> AllowAnonymousAttributeCache = new();
	private static readonly Func<ILogger, string, string, string, IDisposable?> AuthorizationLogScope =
		LoggerMessage.DefineScope<string, string, string>(
			"SubjectId:{SubjectId} TenantId:{TenantId} Roles:{Roles}");

	private readonly AuthorizationOptions _options;
	private readonly IAuthorizationService _authorizationService;
	private readonly ITelemetrySanitizer _sanitizer;
	private readonly ILogger<AuthorizationMiddleware> _logger;
	private readonly FrozenSet<string>? _bypassAuthorizationTypes;

	/// <summary>
	/// Initializes a new instance of the <see cref="AuthorizationMiddleware"/> class.
	/// Creates a new authorization middleware instance.
	/// </summary>
	/// <param name="options"> Configuration options for authorization. </param>
	/// <param name="authorizationService"> Service for evaluating authorization policies. </param>
	/// <param name="sanitizer"> The telemetry sanitizer for PII protection. </param>
	/// <param name="logger"> Logger for diagnostic information. </param>
	public AuthorizationMiddleware(
		IOptions<AuthorizationOptions> options,
		IAuthorizationService authorizationService,
		ITelemetrySanitizer sanitizer,
		ILogger<AuthorizationMiddleware> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(authorizationService);
		ArgumentNullException.ThrowIfNull(sanitizer);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_authorizationService = authorizationService;
		_sanitizer = sanitizer;
		_logger = logger;
		_bypassAuthorizationTypes = _options.BypassAuthorizationForTypes is { Length: > 0 } bypassTypes
			? bypassTypes.ToFrozenSet(StringComparer.Ordinal)
			: null;
	}

	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Authorization;

	/// <inheritdoc />
	/// <remarks>
	/// Authorization typically applies to Actions (commands/queries) rather than Events, as Events are usually internal notifications that
	/// don't require user authorization.
	/// </remarks>
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

		// Skip authorization if disabled
		if (!_options.Enabled)
		{
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		var messageRuntimeType = message.GetType();
		var messageType = messageRuntimeType.Name;

		// Fast path: when message type bypasses authorization, avoid identity extraction and scope setup.
		if (!RequiresAuthorization(messageRuntimeType, messageType))
		{
			LogMessageDoesNotRequireAuthorization(messageType);
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		// Extract authorization context
		var authIdentityContext = ExtractAuthorizationIdentityContext(context, messageRuntimeType);

		// Set up logging scope with authorization context
		using var logScope = CreateAuthorizationLoggingScope(authIdentityContext);

		// Set up OpenTelemetry activity tags
		SetAuthorizationActivityTags(authIdentityContext);

		LogEvaluatingAuthorizationForMessage(messageType, authIdentityContext.SubjectId ?? "anonymous");

		try
		{
			// Evaluate authorization policy
			var authResult = await EvaluateAuthorizationAsync(
					message,
					context,
					messageType,
					authIdentityContext,
					cancellationToken)
				.ConfigureAwait(false);

			if (!authResult.IsAuthorized)
			{
				var reason = string.IsNullOrWhiteSpace(authResult.Reason)
					? Resources.AuthorizationMiddleware_NoReasonProvided
					: authResult.Reason;
				var accessDeniedMessage = string.Format(
					CultureInfo.CurrentCulture,
					ErrorMessages.AccessDeniedForMessageType,
					messageType,
					reason);
				var exception = new UnauthorizedAccessException(accessDeniedMessage);

				LogAuthorizationFailedForMessage(messageType, authIdentityContext.SubjectId ?? "anonymous",
					reason, exception);

				throw exception;
			}

			LogAuthorizationSucceededForMessage(messageType, authIdentityContext.SubjectId ?? "anonymous");

			// Continue pipeline execution
			var result = await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);

			return result;
		}
		catch (Exception ex) when (ex is not UnauthorizedAccessException)
		{
			LogExceptionDuringAuthorizationEvaluation(messageType, ex);
			throw;
		}
	}

	/// <summary>
	/// Extracts authorization identity context from the message context.
	/// </summary>
	private static AuthorizationIdentityContext ExtractAuthorizationIdentityContext(
		IMessageContext context,
		Type messageType)
	{
		var contextUserId = context.GetUserId();

		// Extract subject/user identity
		var subjectId = (string.IsNullOrEmpty(contextUserId) ? GetPropertyValue(context, "UserId") : contextUserId) ??
						GetPropertyValue(context, "SubjectId") ??
						GetPropertyValue(context, "ServiceId");

		var contextTenantId = context.GetTenantId();

		// Extract tenant context if available
		var tenantId = string.IsNullOrEmpty(contextTenantId) ? GetPropertyValue(context, "TenantId") : contextTenantId;

		// Extract roles if available
		var roles = GetPropertyValue(context, "Roles")?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? [];

		return new AuthorizationIdentityContext(
			subjectId,
			tenantId,
			roles,
			messageType);
	}

	private static AuthorizationContext CreateAuthorizationContext(
		IMessageContext context,
		AuthorizationIdentityContext identityContext) =>
		new(
			identityContext.SubjectId,
			identityContext.TenantId,
			identityContext.Roles,
			ExtractClaims(context),
			identityContext.MessageType);

	/// <summary>
	/// Extracts claims from the message context.
	/// </summary>
	private static List<Claim> ExtractClaims(IMessageContext context)
	{
		var claims = new List<Claim>(capacity: 10);

		// Since we can't iterate over context items directly, we'll check for common claim types that might be stored
		foreach (var claimLookup in CommonClaimLookups)
		{
			var value = context.GetItem<object>(claimLookup.ContextKey);
			if (value != null)
			{
				claims.Add(new Claim(claimLookup.ClaimType, value.ToString() ?? string.Empty));
			}
		}

		// Also check for direct UserId and TenantId
		var contextUserId = context.GetUserId();
		var userId = string.IsNullOrEmpty(contextUserId) ? context.GetItem<object>("UserId") : contextUserId;
		if (userId != null)
		{
			claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.ToString() ?? string.Empty));
		}

		var contextTenantId = context.GetTenantId();
		var tenantId = string.IsNullOrEmpty(contextTenantId) ? context.GetItem<object>("TenantId") : contextTenantId;
		if (tenantId != null)
		{
			claims.Add(new Claim("TenantId", tenantId.ToString() ?? string.Empty));
		}

		// Check if there's a principal stored in context
		var principalObj = context.GetItem<object>("Principal");
		if (principalObj is ClaimsPrincipal principal)
		{
			claims.AddRange(principal.Claims);
		}

		return claims;
	}

	/// <summary>
	/// Gets a property value from the message context.
	/// </summary>
	private static string? GetPropertyValue(IMessageContext context, string propertyName)
	{
		// Use GetItem instead of Properties
		var value = context.GetItem<object>(propertyName);
		return value?.ToString();
	}

	/// <summary>
	/// Sets OpenTelemetry activity tags for authorization tracing.
	/// </summary>
	private void SetAuthorizationActivityTags(AuthorizationIdentityContext authContext)
	{
		var activity = Activity.Current;
		if (activity == null)
		{
			return;
		}

		if (!string.IsNullOrEmpty(authContext.SubjectId))
		{
			SetSanitizedTag(activity, "auth.subject_id", authContext.SubjectId);
		}

		if (!string.IsNullOrEmpty(authContext.TenantId))
		{
			SetSanitizedTag(activity, "auth.tenant_id", authContext.TenantId);
		}

		if (authContext.Roles.Length > 0)
		{
			_ = activity.SetTag("auth.roles", string.Join(',', authContext.Roles));
		}
	}

	private void SetSanitizedTag(Activity activity, string tagName, string? rawValue)
	{
		var sanitized = _sanitizer.SanitizeTag(tagName, rawValue);
		if (sanitized is not null)
		{
			_ = activity.SetTag(tagName, sanitized);
		}
	}

	// Source-generated logging methods (Sprint 360 - EventId Migration Phase 1)
	[LoggerMessage(MiddlewareEventId.AuthorizationMiddlewareExecuting, LogLevel.Debug,
		"Message type {MessageType} does not require authorization")]
	private partial void LogMessageDoesNotRequireAuthorization(string messageType);

	[LoggerMessage(MiddlewareEventId.AuthorizationGranted + 4, LogLevel.Debug,
		"Anonymous access allowed for message {MessageType}")]
	private partial void LogAnonymousAccessAllowed(string messageType);

	[LoggerMessage(MiddlewareEventId.PolicyEvaluationStarted, LogLevel.Debug,
		"Evaluating authorization for message {MessageType}, subject {SubjectId}")]
	private partial void LogEvaluatingAuthorizationForMessage(string messageType, string subjectId);

	[LoggerMessage(MiddlewareEventId.AuthorizationDenied, LogLevel.Warning,
		"Authorization failed for message {MessageType}, subject {SubjectId}: {Reason}")]
	private partial void LogAuthorizationFailedForMessage(string messageType, string subjectId, string reason, Exception? ex);

	[LoggerMessage(MiddlewareEventId.AuthorizationGranted, LogLevel.Debug,
		"Authorization succeeded for message {MessageType}, subject {SubjectId}")]
	private partial void LogAuthorizationSucceededForMessage(string messageType, string subjectId);

	[LoggerMessage(MiddlewareEventId.PolicyEvaluationCompleted, LogLevel.Error,
		"Exception during authorization evaluation for {MessageType}")]
	private partial void LogExceptionDuringAuthorizationEvaluation(string messageType, Exception ex);

	/// <summary>
	/// Evaluates authorization policy for the message.
	/// </summary>
	private async Task<AuthorizationResult> EvaluateAuthorizationAsync(
		IDispatchMessage message,
		IMessageContext context,
		string messageTypeName,
		AuthorizationIdentityContext authIdentityContext,
		CancellationToken cancellationToken)
	{
		// Allow anonymous access if configured and no subject is present
		if (string.IsNullOrEmpty(authIdentityContext.SubjectId))
		{
			if (_options.AllowAnonymousAccess)
			{
				LogAnonymousAccessAllowed(messageTypeName);
				return AuthorizationResult.Success();
			}

			return AuthorizationResult.Failure(ErrorMessages.NoAuthenticatedSubjectFound);
		}

		var authContext = CreateAuthorizationContext(context, authIdentityContext);

		// Delegate to authorization service for policy evaluation
		return await _authorizationService.AuthorizeAsync(message, context, authContext, cancellationToken)
			.ConfigureAwait(false);
	}

	/// <summary>
	/// Determines if a message type requires authorization.
	/// </summary>
	private bool RequiresAuthorization(Type messageType, string messageTypeName)
	{
		// Check for explicit bypass attributes
		var hasAllowAnonymousAttribute = AllowAnonymousAttributeCache.GetOrAdd(
			messageType,
			static type => type.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true).Length != 0);
		if (hasAllowAnonymousAttribute)
		{
			return false;
		}

		// Check if message type is in the bypass list
		if (_bypassAuthorizationTypes?.Contains(messageTypeName) == true)
		{
			return false;
		}

		// Default: require authorization for actions
		return true;
	}

	/// <summary>
	/// Creates a logging scope with authorization context.
	/// </summary>
	/// <exception cref="InvalidOperationException"></exception>
	private IDisposable CreateAuthorizationLoggingScope(AuthorizationIdentityContext authContext)
	{
		return AuthorizationLogScope(
				   _logger,
				   authContext.SubjectId ?? string.Empty,
				   authContext.TenantId ?? string.Empty,
				   authContext.Roles.Length > 0 ? string.Join(',', authContext.Roles) : string.Empty)
			   ?? throw new InvalidOperationException(ErrorMessages.LoggerIsNotInitialized);
	}

	/// <summary>
	/// Internal structure to hold authorization identity context during processing.
	/// </summary>
	private readonly record struct AuthorizationIdentityContext(
		string? SubjectId,
		string? TenantId,
		string[] Roles,
		Type MessageType);

	/// <summary>
	/// Internal structure to hold authorization context during processing.
	/// </summary>
	private readonly record struct AuthorizationContext(
		string? SubjectId,
		string? TenantId,
		string[] Roles,
		List<Claim> Claims,
		Type MessageType);

	private readonly record struct ClaimLookup(string ClaimType, string ContextKey);
}
