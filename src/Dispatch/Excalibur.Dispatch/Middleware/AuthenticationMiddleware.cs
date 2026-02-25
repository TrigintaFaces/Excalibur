// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Abstractions.Telemetry;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Options.Middleware;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using IMessageContext = Excalibur.Dispatch.Abstractions.IMessageContext;
using IMessageResult = Excalibur.Dispatch.Abstractions.IMessageResult;
using MessageKinds = Excalibur.Dispatch.Abstractions.MessageKinds;

namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Middleware responsible for authenticating message processing based on tokens, certificates, or other authentication mechanisms.
/// </summary>
/// <remarks>
/// This middleware operates early in the pipeline to establish identity before authorization and business logic. It:
/// <list type="bullet">
/// <item> Extracts authentication credentials from context (headers, certificates, etc.) </item>
/// <item> Validates tokens, certificates, or API keys </item>
/// <item> Establishes ClaimsPrincipal for downstream middleware </item>
/// <item> Supports multiple authentication schemes (Bearer, Certificate, ApiKey) </item>
/// <item> Provides optional caching for performance </item>
/// </list>
/// </remarks>
[AppliesTo(MessageKinds.Action)]
[RequiresFeatures(DispatchFeatures.Authorization)]
public sealed partial class AuthenticationMiddleware : IDispatchMiddleware
{
	private readonly AuthenticationOptions _options;
	private readonly IAuthenticationService _authenticationService;
	private readonly ITelemetrySanitizer _sanitizer;
	private readonly ILogger<AuthenticationMiddleware> _logger;

	/// <summary>
	/// Simple in-memory cache for demonstration - production would use IMemoryCache.
	/// </summary>
	private readonly Dictionary<string, (ClaimsPrincipal Principal, DateTimeOffset Expiry)> _cache = [];

	/// <summary>
	/// Initializes a new instance of the <see cref="AuthenticationMiddleware"/> class.
	/// Creates a new authentication middleware instance.
	/// </summary>
	/// <param name="options"> Configuration options for authentication. </param>
	/// <param name="authenticationService"> Service for performing authentication. </param>
	/// <param name="sanitizer"> The telemetry sanitizer for PII protection. </param>
	/// <param name="logger"> Logger for diagnostic information. </param>
	public AuthenticationMiddleware(
		IOptions<AuthenticationOptions> options,
		IAuthenticationService authenticationService,
		ITelemetrySanitizer sanitizer,
		ILogger<AuthenticationMiddleware> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(authenticationService);
		ArgumentNullException.ThrowIfNull(sanitizer);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_authenticationService = authenticationService;
		_sanitizer = sanitizer;
		_logger = logger;
	}

	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Authentication;

	/// <inheritdoc />
	/// <remarks>
	/// Authentication typically applies to Actions (commands/queries) rather than Events, as Events are usually internal notifications that
	/// don't require authentication.
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

		var messageType = message.GetType().Name;

		// Skip authentication if disabled
		if (!_options.Enabled)
		{
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		// Check if message type allows anonymous access
		if (AllowsAnonymousAccess(message))
		{
			LogMessageTypeAllowsAnonymousAccess(messageType);

			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		// Extract authentication token from context
		var token = ExtractAuthenticationToken(context);

		// Allow anonymous access if configured and no token is present
		if (string.IsNullOrEmpty(token))
		{
			if (!_options.RequireAuthentication)
			{
				LogNoAuthenticationTokenFoundAllowingAnonymousAccess(messageType);

				return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
			}

			// Authentication required but no token found
			LogAuthenticationRequiredButNoTokenFound(messageType);

			throw new UnauthorizedAccessException(
				string.Format(
					CultureInfo.CurrentCulture,
					ErrorMessages.AuthenticationRequiredButNoTokenFound,
					messageType));
		}

		try
		{
			// Authenticate the token
			var principal = await AuthenticateAsync(token, context, cancellationToken).ConfigureAwait(false);

			if (principal == null || principal.Identity?.IsAuthenticated != true)
			{
				LogAuthenticationFailedForMessageType(messageType);

				throw new UnauthorizedAccessException(
					string.Format(
						CultureInfo.CurrentCulture,
						ErrorMessages.AuthenticationFailedForMessageType,
						messageType));
			}

			// Set authenticated principal in context
			context.SetItem("Principal", principal);
			context.SetItem("UserId", principal.Identity.Name);

			// Set up OpenTelemetry activity tags
			SetAuthenticationActivityTags(principal);

			LogSuccessfullyAuthenticatedPrincipalForMessage(principal.Identity?.Name, messageType);

			// Continue pipeline execution
			var result = await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);

			return result;
		}
		catch (Exception ex) when (ex is not UnauthorizedAccessException)
		{
			LogUnexpectedErrorDuringAuthentication(messageType, ex);

			throw;
		}
	}

	/// <summary>
	/// Extracts the authentication token from the message context.
	/// </summary>
	private static string? ExtractAuthenticationToken(IMessageContext context)
	{
		// Try to get from Authorization header
		var authHeader = context.GetItem<object>("Authorization")?.ToString();
		if (!string.IsNullOrEmpty(authHeader))
		{
			// Extract Bearer token if present
			if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
			{
				return authHeader["Bearer ".Length..];
			}

			// Return the header value directly for other schemes
			return authHeader;
		}

		// Try to get from ApiKey header
		var apiKey = context.GetItem<object>("ApiKey")?.ToString();
		if (!string.IsNullOrEmpty(apiKey))
		{
			return apiKey;
		}

		// Try to get from context properties directly
		var token = context.GetItem<object>("Token")?.ToString();
		if (!string.IsNullOrEmpty(token))
		{
			return token;
		}

		return null;
	}

	/// <summary>
	/// Determines the authentication scheme from the token or context.
	/// </summary>
	private static AuthenticationScheme DetermineAuthenticationScheme(string token, IMessageContext context)
	{
		// Check for certificate in context
		if (context.GetItem<object>("ClientCertificate") != null)
		{
			return AuthenticationScheme.Certificate;
		}

		// Check for API key format (simple check - customize as needed)
		if (token.StartsWith("ak_", StringComparison.OrdinalIgnoreCase) || token.Length == 32)
		{
			return AuthenticationScheme.ApiKey;
		}

		// Default to Bearer token (JWT)
		return AuthenticationScheme.Bearer;
	}

	/// <summary>
	/// Sets OpenTelemetry activity tags for authentication tracing.
	/// </summary>
	private void SetAuthenticationActivityTags(ClaimsPrincipal principal)
	{
		var activity = Activity.Current;
		if (activity == null)
		{
			return;
		}

		if (principal.Identity != null)
		{
			SetSanitizedTag(activity, "auth.identity_name", principal.Identity.Name);
			_ = activity.SetTag("auth.is_authenticated", principal.Identity.IsAuthenticated);
			_ = activity.SetTag("auth.authentication_type", principal.Identity.AuthenticationType);
		}

		// Add relevant claims as tags — sanitize PII values
		var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier);
		if (userIdClaim != null)
		{
			SetSanitizedTag(activity, "auth.user_id", userIdClaim.Value);
		}

		var emailClaim = principal.FindFirst(ClaimTypes.Email);
		if (emailClaim != null)
		{
			SetSanitizedTag(activity, "auth.email", emailClaim.Value);
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
	[LoggerMessage(MiddlewareEventId.AnonymousAccessAllowed, LogLevel.Debug,
		"Message type {MessageType} allows anonymous access")]
	private partial void LogMessageTypeAllowsAnonymousAccess(string messageType);

	[LoggerMessage(MiddlewareEventId.AnonymousAccessAllowed + 10, LogLevel.Debug,
		"No authentication token found, allowing anonymous access for {MessageType}")]
	private partial void LogNoAuthenticationTokenFoundAllowingAnonymousAccess(string messageType);

	[LoggerMessage(MiddlewareEventId.AuthenticationFailed, LogLevel.Warning,
		"Authentication required but no token found for {MessageType}")]
	private partial void LogAuthenticationRequiredButNoTokenFound(string messageType);

	[LoggerMessage(MiddlewareEventId.AuthenticationFailed + 10, LogLevel.Warning,
		"Authentication failed for message type {MessageType}")]
	private partial void LogAuthenticationFailedForMessageType(string messageType);

	[LoggerMessage(MiddlewareEventId.AuthenticationSucceeded, LogLevel.Debug,
		"Successfully authenticated principal {PrincipalName} for message {MessageType}")]
	private partial void LogSuccessfullyAuthenticatedPrincipalForMessage(string? principalName, string messageType);

	[LoggerMessage(MiddlewareEventId.AuthenticationErrorDetails, LogLevel.Error,
		"Unexpected error during authentication for {MessageType}")]
	private partial void LogUnexpectedErrorDuringAuthentication(string messageType, Exception ex);

	[LoggerMessage(MiddlewareEventId.TokenValidated, LogLevel.Debug,
		"Using cached authentication for token")]
	private partial void LogUsingCachedAuthenticationForToken();

	[LoggerMessage(MiddlewareEventId.TokenInvalid, LogLevel.Warning,
		"Invalid API key format")]
	private partial void LogInvalidApiKeyFormat();

	[LoggerMessage(MiddlewareEventId.TokenExpired, LogLevel.Warning,
		"No client certificate found in context")]
	private partial void LogNoClientCertificateFoundInContext();

	/// <summary>
	/// Determines if a message type allows anonymous access.
	/// </summary>
	private bool AllowsAnonymousAccess(IDispatchMessage message)
	{
		var messageType = message.GetType();

		// Check for explicit AllowAnonymous attributes
		if (messageType.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true).Length != 0)
		{
			return true;
		}

		// Check if message type is in the allow list
		if (_options.AllowAnonymousForTypes?.Contains(messageType.Name) == true)
		{
			return true;
		}

		// Default: require authentication for actions
		return false;
	}

	/// <summary>
	/// Authenticates the token and returns the principal.
	/// </summary>
	private async Task<ClaimsPrincipal?> AuthenticateAsync(string token, IMessageContext context,
		CancellationToken cancellationToken)
	{
		// Check cache first
		if (_options.EnableCaching && _cache.TryGetValue(token, out var cached))
		{
			if (cached.Expiry > DateTimeOffset.UtcNow)
			{
				LogUsingCachedAuthenticationForToken();

				return cached.Principal;
			}

			// Remove expired entry
			_ = _cache.Remove(token);
		}

		// Determine authentication scheme
		var scheme = DetermineAuthenticationScheme(token, context);

		// Authenticate based on scheme
		var principal = scheme switch
		{
			AuthenticationScheme.Bearer => await AuthenticateBearerTokenAsync(token, cancellationToken)
				.ConfigureAwait(false),
			AuthenticationScheme.ApiKey => await AuthenticateApiKeyAsync(token, cancellationToken).ConfigureAwait(false),
			AuthenticationScheme.Certificate => await AuthenticateCertificateAsync(context, cancellationToken)
				.ConfigureAwait(false),
			_ => null,
		};

		// Cache successful authentication
		if (principal != null && _options.EnableCaching)
		{
			_cache[token] = (principal, DateTimeOffset.UtcNow.Add(_options.CacheDuration));
		}

		return principal;
	}

	/// <summary>
	/// Authenticates a bearer token (JWT).
	/// </summary>
	private async Task<ClaimsPrincipal?> AuthenticateBearerTokenAsync(
		string token,
		CancellationToken cancellationToken) =>

		// Delegate to authentication service
		await _authenticationService.AuthenticateBearerTokenAsync(token, cancellationToken).ConfigureAwait(false);

	/// <summary>
	/// Authenticates an API key.
	/// </summary>
	private async Task<ClaimsPrincipal?> AuthenticateApiKeyAsync(string apiKey, CancellationToken cancellationToken)
	{
		// Validate API key format
		if (string.IsNullOrWhiteSpace(apiKey))
		{
			LogInvalidApiKeyFormat();

			return null;
		}

		// Delegate to authentication service
		return await _authenticationService.AuthenticateApiKeyAsync(apiKey, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Authenticates a client certificate.
	/// </summary>
	private async Task<ClaimsPrincipal?> AuthenticateCertificateAsync(
		IMessageContext context,
		CancellationToken cancellationToken)
	{
		// Extract certificate from context
		var certificateObj = context.GetItem<object>("ClientCertificate");
		if (certificateObj == null)
		{
			LogNoClientCertificateFoundInContext();

			return null;
		}

		// Delegate to authentication service
		return await _authenticationService.AuthenticateCertificateAsync(certificateObj, cancellationToken)
			.ConfigureAwait(false);
	}
}
