// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Telemetry;
using Excalibur.Dispatch.Security.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

using MessageProblemDetails = Excalibur.Dispatch.Abstractions.MessageProblemDetails;

namespace Excalibur.Dispatch.Security;

/// <summary>
/// Middleware that handles JWT-based authentication for message processing.
/// </summary>
/// <remarks>
/// This middleware provides:
/// <list type="bullet">
/// <item> JWT token validation with configurable validation parameters </item>
/// <item> Claims extraction and principal creation </item>
/// <item> Support for multiple token sources (header, message property) </item>
/// <item> Token refresh and expiration handling </item>
/// <item> Integration with external identity providers </item>
/// </list>
/// </remarks>
public sealed partial class JwtAuthenticationMiddleware : IDispatchMiddleware
{
	private readonly JwtAuthenticationOptions _options;
	private readonly ITelemetrySanitizer _sanitizer;
	private readonly ILogger<JwtAuthenticationMiddleware> _logger;
	private readonly ICredentialStore? _credentialStore;
	private readonly JwtSecurityTokenHandler _tokenHandler;
	private readonly TokenValidationParameters _validationParameters;

	/// <summary>
	/// Initializes a new instance of the <see cref="JwtAuthenticationMiddleware" /> class.
	/// </summary>
	/// <param name="options"> The JWT authentication configuration. </param>
	/// <param name="sanitizer"> The telemetry sanitizer for PII protection. </param>
	/// <param name="logger"> The logger used for diagnostics. </param>
	/// <param name="credentialStore"> Optional credential store for runtime signing key resolution. </param>
	public JwtAuthenticationMiddleware(
		IOptions<JwtAuthenticationOptions> options,
		ITelemetrySanitizer sanitizer,
		ILogger<JwtAuthenticationMiddleware> logger,
		ICredentialStore? credentialStore = null)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(sanitizer);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_sanitizer = sanitizer;
		_logger = logger;
		_credentialStore = credentialStore;
		_tokenHandler = new JwtSecurityTokenHandler();
		_validationParameters = BuildValidationParameters();
	}

	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Authentication;

	/// <inheritdoc />
	public MessageKinds ApplicableMessageKinds => MessageKinds.Action | MessageKinds.Event;

	/// <inheritdoc />
	[UnconditionalSuppressMessage(
			"AOT",
			"IL3050:Using RequiresDynamicCode member in AOT",
			Justification = "JWT authentication inspects claims and uses runtime metadata.")]
	[UnconditionalSuppressMessage(
			"Trimming",
			"IL2026:Members annotated with RequiresUnreferencedCode may break with trimming",
			Justification = "JWT authentication uses dynamic claim extraction.")]
	public async ValueTask<IMessageResult> InvokeAsync(
	IDispatchMessage message,
	IMessageContext context,
	DispatchRequestDelegate nextDelegate,
	CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(nextDelegate);

		// Skip authentication if disabled or anonymous
		if (ShouldSkipAuthentication(message))
		{
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		// Extract JWT token
		var token = ExtractToken(message, context);
		if (string.IsNullOrEmpty(token))
		{
			return await HandleMissingTokenAsync(message, context, nextDelegate, cancellationToken).ConfigureAwait(false);
		}

		// Authenticate with token
		return await AuthenticateWithTokenAsync(message, context, nextDelegate, token, cancellationToken).ConfigureAwait(false);
	}

	private static void SetAuthenticationContext(IMessageContext context, ClaimsPrincipal principal)
	{
		// Set the principal
		context.SetProperty("Principal", principal);

		// Extract and set common claims
		var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
					 principal.FindFirst("sub")?.Value;
		if (!string.IsNullOrEmpty(userId))
		{
			context.SetProperty("UserId", userId);
		}

		var userName = principal.FindFirst(ClaimTypes.Name)?.Value ??
					   principal.FindFirst("name")?.Value;
		if (!string.IsNullOrEmpty(userName))
		{
			context.SetProperty("UserName", userName);
		}

		var email = principal.FindFirst(ClaimTypes.Email)?.Value ??
					principal.FindFirst("email")?.Value;
		if (!string.IsNullOrEmpty(email))
		{
			context.SetProperty("Email", email);
		}

		// Extract tenant ID if present
		var tenantId = principal.FindFirst("tenant_id")?.Value ??
					   principal.FindFirst("tid")?.Value;
		if (!string.IsNullOrEmpty(tenantId))
		{
			context.SetProperty("TenantId", tenantId);
		}

		// Extract roles (filter out empty/whitespace claim values)
		var roles = principal.FindAll(ClaimTypes.Role)
			.Union(principal.FindAll("role"))
			.Union(principal.FindAll("roles"))
			.Select(static c => c.Value)
			.Where(static v => !string.IsNullOrWhiteSpace(v))
			.Distinct(StringComparer.Ordinal)
			.ToList();
		if (roles.Count > 0)
		{
			context.SetProperty("Roles", roles);
		}

		// Set authentication time
		context.SetProperty("AuthenticatedAt", DateTimeOffset.UtcNow);

		// Set authentication method (fall back to "jwt" if claim value is empty)
		var amrClaim = principal.FindFirst("amr")?.Value;
		var authMethod = !string.IsNullOrWhiteSpace(amrClaim) ? amrClaim : "jwt";
		context.SetProperty("AuthenticationMethod", authMethod);
	}

	private bool ShouldSkipAuthentication(IDispatchMessage message)
	{
		if (!_options.Enabled)
		{
			return true;
		}

		if (_options.AllowAnonymousMessageTypes.Contains(message.GetType().Name))
		{
			LogSkippingAnonymous(message.GetType().Name);
			return true;
		}

		return false;
	}

	private async Task<IMessageResult> HandleMissingTokenAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		if (_options.RequireAuthentication)
		{
			LogMissingToken(message.GetType().Name);
			return new AuthenticationFailedResult
			{
				Succeeded = false,
				ProblemDetails = MessageProblemDetails.AuthorizationError("Authentication required but no token provided"),
				Reason = AuthenticationFailureReason.MissingToken,
			};
		}

		// Continue without authentication if not required
		return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
	}

	private async Task<IMessageResult> AuthenticateWithTokenAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		string token,
		CancellationToken cancellationToken)
	{
		using var activity = Activity.Current?.Source.StartActivity("Authentication.ValidateToken");

		try
		{
			var principal = await ValidateTokenAsync(token, cancellationToken).ConfigureAwait(false);
			if (principal == null)
			{
				return CreateInvalidTokenResult(message);
			}

			SetAuthenticationContext(context, principal);
			LogAuthenticationSuccess(principal, message, activity);

			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}
		catch (SecurityTokenExpiredException)
		{
			return CreateExpiredTokenResult(message);
		}
		catch (SecurityTokenException ex)
		{
			return CreateValidationErrorResult(message, ex);
		}
		catch (Exception ex)
		{
			return CreateUnexpectedErrorResult(message, ex);
		}
	}

	private AuthenticationFailedResult CreateInvalidTokenResult(IDispatchMessage message)
	{
		LogValidationFailed(message.GetType().Name);
		return new AuthenticationFailedResult
		{
			Succeeded = false,
			ProblemDetails = MessageProblemDetails.AuthorizationError("Invalid authentication token"),
			Reason = AuthenticationFailureReason.InvalidToken,
		};
	}

	private AuthenticationFailedResult CreateExpiredTokenResult(IDispatchMessage message)
	{
		LogTokenExpired(message.GetType().Name);
		return new AuthenticationFailedResult
		{
			Succeeded = false,
			ProblemDetails = MessageProblemDetails.AuthorizationError("Authentication token has expired"),
			Reason = AuthenticationFailureReason.TokenExpired,
		};
	}

	private AuthenticationFailedResult CreateValidationErrorResult(IDispatchMessage message, SecurityTokenException ex)
	{
		LogTokenValidationError(message.GetType().Name, ex);
		return new AuthenticationFailedResult
		{
			Succeeded = false,
			ProblemDetails = MessageProblemDetails.ValidationError("Token validation failed"),
			Reason = AuthenticationFailureReason.ValidationError,
		};
	}

	private AuthenticationFailedResult CreateUnexpectedErrorResult(IDispatchMessage message, Exception ex)
	{
		LogUnexpectedAuthError(message.GetType().Name, ex);
		return new AuthenticationFailedResult
		{
			Succeeded = false,
			ProblemDetails = MessageProblemDetails.InternalError("Authentication failed"),
			Reason = AuthenticationFailureReason.UnknownError,
		};
	}

	private void LogAuthenticationSuccess(ClaimsPrincipal principal, IDispatchMessage message, Activity? activity)
	{
		var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
		LogAuthSuccess(userId, message.GetType().Name);
		if (activity is not null)
		{
			var sanitized = _sanitizer.SanitizeTag("auth.user_id", userId);
			if (sanitized is not null)
			{
				_ = activity.SetTag("auth.user_id", sanitized);
			}

			_ = activity.SetTag("auth.success", value: true);
		}
	}

	[RequiresDynamicCode("Uses reflection to extract token from message properties")]
	[UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Property extraction is optional and only used when explicitly enabled. GetType() usage is unavoidable for dynamic property access.")]
	private string? ExtractToken(IDispatchMessage message, IMessageContext context)
	{
		// Try to get token from context
		if (context.TryGetValue<string>(_options.TokenContextKey, out var contextToken) &&
			contextToken != null &&
			!string.IsNullOrEmpty(contextToken))
		{
			return contextToken;
		}

		// Try to get token from message headers/metadata
		if (message is IMessageWithHeaders msgWithHeaders &&
			msgWithHeaders.Headers.TryGetValue(_options.TokenHeaderName, out var headerToken) &&
			!string.IsNullOrEmpty(headerToken))
		{
			// Remove "Bearer " prefix if present
			if (headerToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
			{
				return headerToken[7..];
			}

			return headerToken;
		}

		// Try to get token from message property via reflection (if enabled)
		if (_options.EnablePropertyExtraction && message is not null)
		{
			var tokenProperty = message.GetType().GetProperty(_options.TokenPropertyName);
			if (tokenProperty?.GetValue(message) is string propToken && !string.IsNullOrEmpty(propToken))
			{
				return propToken;
			}
		}

		return null;
	}

	private async Task<ClaimsPrincipal?> ValidateTokenAsync(string token, CancellationToken cancellationToken)
	{
		try
		{
			// For async key retrieval scenarios (like fetching from Key Vault)
			if (_options.Credentials.UseAsyncKeyRetrieval)
			{
				// Pre-fetch the signing key asynchronously before validation This avoids blocking in the synchronous
				// IssuerSigningKeyResolver delegate
				var jwtToken = _tokenHandler.ReadJwtToken(token);
				var keyId = jwtToken?.Header?.Kid;
				var signingKey = await GetSigningKeyAsync(keyId, cancellationToken).ConfigureAwait(false);

				var validationParams = _validationParameters.Clone();
				if (signingKey != null)
				{
					// Use the pre-fetched key directly
					validationParams.IssuerSigningKey = signingKey;
					validationParams.IssuerSigningKeyResolver = null;
				}
				else
				{
					// If no key found, validation will fail
					LogNoSigningKey(keyId);
					return null;
				}

				var result = await _tokenHandler.ValidateTokenAsync(token, validationParams).ConfigureAwait(false);
				return result.IsValid ? new ClaimsPrincipal(result.ClaimsIdentity) : null;
			}

			// Synchronous validation
			return _tokenHandler.ValidateToken(token, _validationParameters, out _);
		}
		catch (Exception ex)
		{
			LogValidationFailedDebug(ex);
			throw;
		}
	}

	private async Task<SecurityKey?> GetSigningKeyAsync(string? keyId, CancellationToken cancellationToken)
	{
		_ = keyId;

		// When a credential name is configured and a credential store is available, resolve the key at runtime
		if (!string.IsNullOrEmpty(_options.Credentials.SigningKeyCredentialName) && _credentialStore is not null)
		{
			var secureKey = await _credentialStore.GetCredentialAsync(
				_options.Credentials.SigningKeyCredentialName, cancellationToken).ConfigureAwait(false);
			if (secureKey is not null)
			{
				// Convert SecureString to string and create symmetric key
				var ptr = System.Runtime.InteropServices.Marshal.SecureStringToGlobalAllocUnicode(secureKey);
				try
				{
					var keyValue = System.Runtime.InteropServices.Marshal.PtrToStringUni(ptr) ?? string.Empty;
					return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyValue));
				}
				finally
				{
					System.Runtime.InteropServices.Marshal.ZeroFreeGlobalAllocUnicode(ptr);
				}
			}
		}

		// Fallback to static signing key from options
		if (!string.IsNullOrEmpty(_options.Credentials.SigningKey))
		{
			return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Credentials.SigningKey));
		}

		return null;
	}

	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
		Justification = "RSA instance is owned by RsaSecurityKey and will be disposed when RsaSecurityKey is disposed")]
	private TokenValidationParameters BuildValidationParameters()
	{
		var parameters = new TokenValidationParameters
		{
			ValidateIssuer = _options.Validation.ValidateIssuer,
			ValidateAudience = _options.Validation.ValidateAudience,
			ValidateLifetime = _options.Validation.ValidateLifetime,
			ValidateIssuerSigningKey = _options.Validation.ValidateSigningKey,
			ClockSkew = TimeSpan.FromSeconds(_options.ClockSkewSeconds),
			RequireExpirationTime = _options.Validation.RequireExpirationTime,
			RequireSignedTokens = _options.Validation.RequireSignedTokens,
		};

		// Set issuer(s)
		if (!string.IsNullOrEmpty(_options.Credentials.ValidIssuer))
		{
			parameters.ValidIssuer = _options.Credentials.ValidIssuer;
		}

		if (_options.Credentials.ValidIssuers?.Length > 0)
		{
			parameters.ValidIssuers = _options.Credentials.ValidIssuers;
		}

		// Set audience(s)
		if (!string.IsNullOrEmpty(_options.Credentials.ValidAudience))
		{
			parameters.ValidAudience = _options.Credentials.ValidAudience;
		}

		if (_options.Credentials.ValidAudiences?.Length > 0)
		{
			parameters.ValidAudiences = _options.Credentials.ValidAudiences;
		}

		// Set signing key
		if (!string.IsNullOrEmpty(_options.Credentials.SigningKey))
		{
			parameters.IssuerSigningKey = new SymmetricSecurityKey(
				Encoding.UTF8.GetBytes(_options.Credentials.SigningKey));
		}

		// Set RSA public key if configured
		if (!string.IsNullOrEmpty(_options.Credentials.RsaPublicKey))
		{
			// RSA instance will be owned by RsaSecurityKey which will dispose it
			var rsa = RSA.Create();
			rsa.ImportFromPem(_options.Credentials.RsaPublicKey);
			parameters.IssuerSigningKey =
				new RsaSecurityKey(rsa) { CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false } };
		}

		return parameters;
	}

	// Source-generated logging methods
	[LoggerMessage(SecurityEventId.AuthenticationSkippedAnonymous, LogLevel.Debug,
		"Skipping authentication for anonymous message type {MessageType}")]
	private partial void LogSkippingAnonymous(string messageType);

	[LoggerMessage(SecurityEventId.AuthenticationTokenMissing, LogLevel.Warning,
		"No authentication token found for message {MessageType}")]
	private partial void LogMissingToken(string messageType);

	[LoggerMessage(SecurityEventId.JwtTokenValidationFailed, LogLevel.Warning,
		"Token validation failed for message {MessageType}")]
	private partial void LogValidationFailed(string messageType);

	[LoggerMessage(SecurityEventId.AuthenticationSucceeded, LogLevel.Debug,
		"Successfully authenticated user {UserId} for message {MessageType}")]
	private partial void LogAuthSuccess(string userId, string messageType);

	[LoggerMessage(SecurityEventId.JwtTokenExpired, LogLevel.Warning,
		"Token expired for message {MessageType}")]
	private partial void LogTokenExpired(string messageType);

	[LoggerMessage(SecurityEventId.JwtTokenValidationError, LogLevel.Error,
		"Token validation error for message {MessageType}")]
	private partial void LogTokenValidationError(string messageType, Exception ex);

	[LoggerMessage(SecurityEventId.AuthenticationFailed, LogLevel.Error,
		"Unexpected authentication error for message {MessageType}")]
	private partial void LogUnexpectedAuthError(string messageType, Exception ex);

	[LoggerMessage(SecurityEventId.AuthenticationNoSigningKey, LogLevel.Warning,
		"No signing key found for key ID: {KeyId}")]
	private partial void LogNoSigningKey(string? keyId);

	[LoggerMessage(SecurityEventId.AuthenticationValidationDebug, LogLevel.Debug,
		"Token validation failed")]
	private partial void LogValidationFailedDebug(Exception ex);
}
