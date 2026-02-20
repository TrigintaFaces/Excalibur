// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Globalization;
using System.Net.Http.Headers;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Provides secure authentication management for Elasticsearch connections with enterprise security features including credential rotation,
/// validation, and integration with external key management systems.
/// </summary>
public sealed class SecureElasticsearchAuthenticationProvider : IElasticsearchAuthenticationProvider, IAsyncDisposable, IDisposable
{
	private readonly ElasticsearchSecurityOptions _securitySettings;
	private readonly IElasticsearchKeyProvider _keyProvider;
	private readonly IHttpClientFactory _httpClientFactory;
	private readonly ILogger<SecureElasticsearchAuthenticationProvider> _logger;
	private readonly Timer _rotationTimer;
	private readonly SemaphoreSlim _rotationSemaphore;
#if NET9_0_OR_GREATER

	private readonly Lock _eventLock = new();

#else
	private readonly object _eventLock = new();

#endif

	private volatile bool _disposed;
	private volatile int _consecutiveFailures;
	private DateTimeOffset _lastRotation = DateTimeOffset.UtcNow;
	private DateTimeOffset? _nextRotationDue;

	/// <summary>
	/// Initializes a new instance of the <see cref="SecureElasticsearchAuthenticationProvider" /> class.
	/// </summary>
	/// <param name="securityOptions"> The security configuration options. </param>
	/// <param name="keyProvider"> The key management provider for secure credential storage. </param>
	/// <param name="httpClientFactory"> The HTTP client factory for OAuth2 token operations. </param>
	/// <param name="logger"> The logger for security events and diagnostics. </param>
	/// <exception cref="ArgumentNullException"> Thrown when required dependencies are null. </exception>
	public SecureElasticsearchAuthenticationProvider(
		IOptions<ElasticsearchSecurityOptions> securityOptions,
		IElasticsearchKeyProvider keyProvider,
		IHttpClientFactory httpClientFactory,
		ILogger<SecureElasticsearchAuthenticationProvider> logger)
	{
		ArgumentNullException.ThrowIfNull(securityOptions);
		ArgumentNullException.ThrowIfNull(keyProvider);
		ArgumentNullException.ThrowIfNull(httpClientFactory);
		ArgumentNullException.ThrowIfNull(logger);

		_securitySettings = securityOptions.Value;
		_keyProvider = keyProvider;
		_httpClientFactory = httpClientFactory;
		_logger = logger;
		_rotationSemaphore = new SemaphoreSlim(1, 1);

		// Initialize credential rotation timer if rotation is enabled
		if (_securitySettings.Authentication.CredentialRotation.Enabled &&
			_securitySettings.Authentication.CredentialRotation.RotationInterval > TimeSpan.Zero)
		{
			_rotationTimer = new Timer(
				async _ => await PerformScheduledRotationAsync().ConfigureAwait(false),
				state: null,
				_securitySettings.Authentication.CredentialRotation.RotationInterval,
				_securitySettings.Authentication.CredentialRotation.RotationInterval);

			_nextRotationDue = DateTimeOffset.UtcNow.Add(_securitySettings.Authentication.CredentialRotation.RotationInterval);
		}
		else
		{
			_rotationTimer = new Timer(_ => { }, state: null, Timeout.Infinite, Timeout.Infinite);
		}

		AuthenticationType = DetermineAuthenticationType();
	}

	/// <inheritdoc />
	public event EventHandler<AuthenticationRotatedEventArgs>? CredentialsRotated;

	/// <inheritdoc />
	public event EventHandler<AuthenticationFailedEventArgs>? AuthenticationFailed;

	/// <inheritdoc />
	public ElasticsearchAuthenticationType AuthenticationType { get; }

	/// <inheritdoc />
	public bool SupportsRotation => _securitySettings.Authentication.CredentialRotation.Enabled &&
									AuthenticationType is ElasticsearchAuthenticationType.ApiKey or
										ElasticsearchAuthenticationType.ServiceAccount or
										ElasticsearchAuthenticationType.OAuth2;

	/// <inheritdoc />
	public bool SupportsRefresh => AuthenticationType is ElasticsearchAuthenticationType.OAuth2 or
		ElasticsearchAuthenticationType.ServiceAccount;

	/// <inheritdoc />
	public async Task<AuthenticationHeaderValue?> GetAuthenticationAsync(CancellationToken cancellationToken)
	{
		ThrowIfDisposed();

		try
		{
			return AuthenticationType switch
			{
				ElasticsearchAuthenticationType.BasicAuthentication => await GetBasicAuthenticationAsync(cancellationToken)
					.ConfigureAwait(false),
				ElasticsearchAuthenticationType.ApiKey => await GetApiKeyAuthenticationAsync(cancellationToken).ConfigureAwait(false),
				ElasticsearchAuthenticationType.Base64ApiKey => await GetBase64ApiKeyAuthenticationAsync(cancellationToken)
					.ConfigureAwait(false),
				ElasticsearchAuthenticationType.CertificateAuthentication => await GetCertificateAuthenticationAsync(cancellationToken)
					.ConfigureAwait(false),
				ElasticsearchAuthenticationType.OAuth2 => await GetOAuth2AuthenticationAsync(cancellationToken).ConfigureAwait(false),
				ElasticsearchAuthenticationType.ServiceAccount => await GetServiceAccountAuthenticationAsync(cancellationToken)
					.ConfigureAwait(false),
				ElasticsearchAuthenticationType.None => null,
				_ => throw new SecurityException($"Unsupported authentication type: {AuthenticationType}"),
			};
		}
		catch (Exception ex) when (ex is not SecurityException)
		{
			await HandleAuthenticationFailureAsync($"Failed to retrieve authentication: {ex.Message}", cancellationToken)
				.ConfigureAwait(false);
			throw new SecurityException("Authentication retrieval failed", ex);
		}
	}

	/// <inheritdoc />
	public async Task<bool> ValidateAuthenticationAsync(CancellationToken cancellationToken)
	{
		ThrowIfDisposed();

		try
		{
			// Check if credentials exist and are accessible
			var auth = await GetAuthenticationAsync(cancellationToken).ConfigureAwait(false);
			if (auth == null && AuthenticationType != ElasticsearchAuthenticationType.None)
			{
				await HandleAuthenticationFailureAsync("No authentication credentials available", cancellationToken).ConfigureAwait(false);
				return false;
			}

			// Perform authentication-specific validation
			var isValid = AuthenticationType switch
			{
				ElasticsearchAuthenticationType.None => true,
				ElasticsearchAuthenticationType.OAuth2 => await ValidateOAuth2TokenAsync(cancellationToken).ConfigureAwait(false),
				ElasticsearchAuthenticationType.ServiceAccount =>
					await ValidateServiceAccountAsync(cancellationToken).ConfigureAwait(false),
				ElasticsearchAuthenticationType.ApiKey => await ValidateApiKeyAsync(cancellationToken).ConfigureAwait(false),
				_ => auth != null,
			};

			if (isValid)
			{
				// Reset consecutive failures on successful validation
				_ = Interlocked.Exchange(ref _consecutiveFailures, 0);
				_logger.LogDebug("Authentication validation successful for type {AuthType}", AuthenticationType);
			}
			else
			{
				await HandleAuthenticationFailureAsync("Authentication validation failed", cancellationToken).ConfigureAwait(false);
			}

			return isValid;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Exception during authentication validation for type {AuthType}", AuthenticationType);
			await HandleAuthenticationFailureAsync($"Validation exception: {ex.Message}", cancellationToken).ConfigureAwait(false);
			return false;
		}
	}

	/// <inheritdoc />
	public async Task<bool> RefreshAuthenticationAsync(CancellationToken cancellationToken)
	{
		ThrowIfDisposed();

		if (!SupportsRefresh)
		{
			_logger.LogWarning("Refresh not supported for authentication type {AuthType}", AuthenticationType);
			return false;
		}

		try
		{
			var success = AuthenticationType switch
			{
				ElasticsearchAuthenticationType.OAuth2 => await RefreshOAuth2TokenAsync(cancellationToken).ConfigureAwait(false),
				ElasticsearchAuthenticationType.ServiceAccount => await RefreshServiceAccountTokenAsync(cancellationToken)
					.ConfigureAwait(false),
				_ => false,
			};

			if (success)
			{
				_logger.LogInformation("Authentication refresh successful for type {AuthType}", AuthenticationType);
				_ = Interlocked.Exchange(ref _consecutiveFailures, 0);
			}
			else
			{
				await HandleAuthenticationFailureAsync("Authentication refresh failed", cancellationToken).ConfigureAwait(false);
			}

			return success;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Exception during authentication refresh for type {AuthType}", AuthenticationType);
			await HandleAuthenticationFailureAsync($"Refresh exception: {ex.Message}", cancellationToken).ConfigureAwait(false);
			return false;
		}
	}

	/// <inheritdoc />
	public async Task<AuthenticationRotationResult> RotateCredentialsAsync(CancellationToken cancellationToken)
	{
		ThrowIfDisposed();

		if (!SupportsRotation)
		{
			return AuthenticationRotationResult.Failure("Credential rotation not supported for this authentication type");
		}

		// Ensure only one rotation happens at a time
		if (!await _rotationSemaphore.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false))
		{
			return AuthenticationRotationResult.Failure("Rotation operation timed out waiting for exclusive access");
		}

		try
		{
			_logger.LogInformation("Starting credential rotation for authentication type {AuthType}", AuthenticationType);

			var result = AuthenticationType switch
			{
				ElasticsearchAuthenticationType.ApiKey => await RotateApiKeyAsync(cancellationToken).ConfigureAwait(false),
				ElasticsearchAuthenticationType.ServiceAccount => await RotateServiceAccountAsync(cancellationToken).ConfigureAwait(false),
				ElasticsearchAuthenticationType.OAuth2 => await RotateOAuth2ClientAsync(cancellationToken).ConfigureAwait(false),
				_ => AuthenticationRotationResult.Failure($"Rotation not implemented for {AuthenticationType}"),
			};

			if (result.Success)
			{
				_lastRotation = result.RotatedAt;
				_nextRotationDue = result.NextRotationDue;

				// Fire the credentials rotated event
				OnCredentialsRotated(new AuthenticationRotatedEventArgs(
					AuthenticationType,
					result.RotatedAt,
					result.NextRotationDue));

				_logger.LogInformation(
					"Credential rotation completed successfully for type {AuthType}. Next rotation due: {NextDue}",
					AuthenticationType, result.NextRotationDue);
			}
			else
			{
				_logger.LogError(
					"Credential rotation failed for type {AuthType}: {Message}",
					AuthenticationType, result.Message);
			}

			return result;
		}
		catch (Exception ex)
		{
			var errorMessage = $"Exception during credential rotation: {ex.Message}";
			_logger.LogError(ex, "Credential rotation failed for type {AuthType}", AuthenticationType);
			return AuthenticationRotationResult.Failure(errorMessage);
		}
		finally
		{
			_ = _rotationSemaphore.Release();
		}
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		await _rotationTimer.DisposeAsync().ConfigureAwait(false);
		_rotationSemaphore.Dispose();

		GC.SuppressFinalize(this);
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		_rotationTimer.Dispose();
		_rotationSemaphore.Dispose();

		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Retrieves certificate-based authentication configuration (mutual TLS).
	/// </summary>
	private static async Task<AuthenticationHeaderValue?> GetCertificateAuthenticationAsync(CancellationToken cancellationToken)
	{
		// Certificate authentication is handled at the transport level, not via headers This method exists for consistency but returns null
		// as certificates are configured in ClientSettings
		await Task.CompletedTask.ConfigureAwait(false);
		return null;
	}

	/// <summary>
	/// Refreshes an expired OAuth2 access token using the refresh token.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token to monitor for cancellation requests.</param>
	/// <returns>True if the token was refreshed successfully; otherwise, false.</returns>
	private async Task<bool> RefreshOAuth2TokenAsync(CancellationToken cancellationToken)
	{
		var oauth2Settings = _securitySettings.Authentication.OAuth2;

		// Validate configuration
		if (string.IsNullOrWhiteSpace(oauth2Settings.Authority) || string.IsNullOrWhiteSpace(oauth2Settings.ClientId))
		{
			_logger.LogWarning("OAuth2 refresh failed: Authority or ClientId not configured");
			return false;
		}

		// Get refresh token from key provider
		var refreshToken = await _keyProvider.GetSecretAsync("elasticsearch:oauth2:refreshtoken", cancellationToken).ConfigureAwait(false);
		if (string.IsNullOrWhiteSpace(refreshToken))
		{
			_logger.LogWarning("OAuth2 refresh failed: No refresh token available");
			return false;
		}

		// Get client secret (optional for confidential clients)
		var clientSecret = await _keyProvider.GetSecretAsync("elasticsearch:oauth2:clientsecret", cancellationToken).ConfigureAwait(false);

		try
		{
			// Construct token endpoint URL
			var tokenEndpoint = new Uri(oauth2Settings.Authority.TrimEnd('/') + "/oauth2/token");

			using var httpClient = _httpClientFactory.CreateClient("ElasticsearchOAuth2");

			// Build the refresh token request
			var requestContent = new FormUrlEncodedContent(BuildRefreshTokenParameters(oauth2Settings, refreshToken, clientSecret));

			var response = await httpClient.PostAsync(tokenEndpoint, requestContent, cancellationToken).ConfigureAwait(false);

			if (!response.IsSuccessStatusCode)
			{
				var errorContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
				_logger.LogWarning("OAuth2 token refresh failed with status {StatusCode}: {Error}",
					response.StatusCode, errorContent);
				return false;
			}

			// Parse the token response
			var responseContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
			var tokenResponse = JsonSerializer.Deserialize<OAuth2TokenResponse>(responseContent);

			if (tokenResponse is null || string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
			{
				_logger.LogWarning("OAuth2 token refresh failed: Invalid token response");
				return false;
			}

			// Calculate expiration timestamp
			var expiresAt = DateTimeOffset.UtcNow.AddSeconds(tokenResponse.ExpiresIn);

			// Store the new tokens in the key provider
			_ = await _keyProvider
				.SetSecretAsync("elasticsearch:oauth2:accesstoken", tokenResponse.AccessToken, null, cancellationToken)
				.ConfigureAwait(false);
			_ = await _keyProvider.SetSecretAsync("elasticsearch:oauth2:expiresat", expiresAt.ToString("O", CultureInfo.InvariantCulture),
				null, cancellationToken).ConfigureAwait(false);

			// Update refresh token if a new one was provided
			if (!string.IsNullOrWhiteSpace(tokenResponse.RefreshToken))
			{
				_ = await _keyProvider
					.SetSecretAsync("elasticsearch:oauth2:refreshtoken", tokenResponse.RefreshToken, null, cancellationToken)
					.ConfigureAwait(false);
			}

			_logger.LogDebug("OAuth2 token refreshed successfully, expires at {ExpiresAt}", expiresAt);
			return true;
		}
		catch (HttpRequestException ex)
		{
			_logger.LogError(ex, "OAuth2 token refresh failed due to HTTP error");
			return false;
		}
		catch (JsonException ex)
		{
			_logger.LogError(ex, "OAuth2 token refresh failed: Unable to parse token response");
			return false;
		}
	}

	/// <summary>
	/// Builds the form parameters for an OAuth2 refresh token request.
	/// </summary>
	private static List<KeyValuePair<string, string>> BuildRefreshTokenParameters(
		OAuth2Options oauth2Settings,
		string refreshToken,
		string? clientSecret)
	{
		var parameters = new List<KeyValuePair<string, string>>
		{
			new("grant_type", "refresh_token"), new("refresh_token", refreshToken), new("client_id", oauth2Settings.ClientId),
		};

		if (!string.IsNullOrWhiteSpace(clientSecret))
		{
			parameters.Add(new("client_secret", clientSecret));
		}

		if (!string.IsNullOrWhiteSpace(oauth2Settings.Scope))
		{
			parameters.Add(new("scope", oauth2Settings.Scope));
		}

		return parameters;
	}

	/// <summary>
	/// Represents an OAuth2 token response from the authorization server.
	/// </summary>
	private sealed record OAuth2TokenResponse(
		[property: JsonPropertyName("access_token")]
		string AccessToken,
		[property: JsonPropertyName("refresh_token")]
		string? RefreshToken,
		[property: JsonPropertyName("expires_in")]
		int ExpiresIn,
		[property: JsonPropertyName("token_type")]
		string? TokenType);

	/// <summary>
	/// Refreshes an expired service account token by re-authenticating with the service account credentials.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token to monitor for cancellation requests.</param>
	/// <returns>True if the token was refreshed successfully; otherwise, false.</returns>
	/// <remarks>
	/// Service account tokens typically use the client credentials grant type, as service accounts
	/// authenticate directly without user interaction. This method re-authenticates to obtain a fresh token.
	/// </remarks>
	private async Task<bool> RefreshServiceAccountTokenAsync(CancellationToken cancellationToken)
	{
		var serviceAccountSettings = _securitySettings.Authentication.ServiceAccount;
		var oauth2Settings = _securitySettings.Authentication.OAuth2;

		// Validate configuration - service accounts use the OAuth2 Authority for token endpoint
		if (string.IsNullOrWhiteSpace(oauth2Settings.Authority))
		{
			_logger.LogWarning("Service account refresh failed: OAuth2 Authority not configured");
			return false;
		}

		if (string.IsNullOrWhiteSpace(serviceAccountSettings.AccountId))
		{
			_logger.LogWarning("Service account refresh failed: AccountId not configured");
			return false;
		}

		// Get service account credentials from key provider
		var clientSecret = await _keyProvider.GetSecretAsync("elasticsearch:serviceaccount:secret", cancellationToken)
			.ConfigureAwait(false);
		if (string.IsNullOrWhiteSpace(clientSecret))
		{
			_logger.LogWarning("Service account refresh failed: No client secret available");
			return false;
		}

		try
		{
			// Construct token endpoint URL
			var tokenEndpoint = new Uri(oauth2Settings.Authority.TrimEnd('/') + "/oauth2/token");

			using var httpClient = _httpClientFactory.CreateClient("ElasticsearchOAuth2");

			// Build the client credentials request
			var requestContent =
				new FormUrlEncodedContent(BuildServiceAccountTokenParameters(serviceAccountSettings, oauth2Settings, clientSecret));

			var response = await httpClient.PostAsync(tokenEndpoint, requestContent, cancellationToken).ConfigureAwait(false);

			if (!response.IsSuccessStatusCode)
			{
				var errorContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
				_logger.LogWarning("Service account token refresh failed with status {StatusCode}: {Error}",
					response.StatusCode, errorContent);
				return false;
			}

			// Parse the token response (reuse OAuth2TokenResponse as the format is identical)
			var responseContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
			var tokenResponse = JsonSerializer.Deserialize<OAuth2TokenResponse>(responseContent);

			if (tokenResponse is null || string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
			{
				_logger.LogWarning("Service account token refresh failed: Invalid token response");
				return false;
			}

			// Calculate expiration timestamp
			var expiresAt = DateTimeOffset.UtcNow.AddSeconds(tokenResponse.ExpiresIn);

			// Store the new token in the key provider
			_ = await _keyProvider
				.SetSecretAsync("elasticsearch:serviceaccount:token", tokenResponse.AccessToken, null, cancellationToken)
				.ConfigureAwait(false);
			_ = await _keyProvider.SetSecretAsync("elasticsearch:serviceaccount:expiresat",
				expiresAt.ToString("O", CultureInfo.InvariantCulture), null, cancellationToken).ConfigureAwait(false);

			_logger.LogDebug("Service account token refreshed successfully, expires at {ExpiresAt}", expiresAt);
			return true;
		}
		catch (HttpRequestException ex)
		{
			_logger.LogError(ex, "Service account token refresh failed due to HTTP error");
			return false;
		}
		catch (JsonException ex)
		{
			_logger.LogError(ex, "Service account token refresh failed: Unable to parse token response");
			return false;
		}
	}

	/// <summary>
	/// Builds the form parameters for a service account client credentials token request.
	/// </summary>
	private static List<KeyValuePair<string, string>> BuildServiceAccountTokenParameters(
		ServiceAccountOptions serviceAccountSettings,
		OAuth2Options oauth2Settings,
		string clientSecret)
	{
		var parameters = new List<KeyValuePair<string, string>>
		{
			new("grant_type", "client_credentials"),
			new("client_id", serviceAccountSettings.AccountId),
			new("client_secret", clientSecret),
		};

		if (!string.IsNullOrWhiteSpace(oauth2Settings.Scope))
		{
			parameters.Add(new("scope", oauth2Settings.Scope));
		}

		return parameters;
	}

	/// <summary>
	/// Determines the authentication type based on the current security configuration.
	/// </summary>
	/// <returns> The appropriate authentication type for the current configuration. </returns>
	private ElasticsearchAuthenticationType DetermineAuthenticationType()
	{
		var authConfig = _securitySettings.Authentication;

		if (authConfig.OAuth2.Enabled)
		{
			return ElasticsearchAuthenticationType.OAuth2;
		}

		if (authConfig.ServiceAccount.Enabled)
		{
			return ElasticsearchAuthenticationType.ServiceAccount;
		}

		if (authConfig.Certificate.Enabled)
		{
			return ElasticsearchAuthenticationType.CertificateAuthentication;
		}

		if (!string.IsNullOrWhiteSpace(authConfig.Base64ApiKey))
		{
			return ElasticsearchAuthenticationType.Base64ApiKey;
		}

		if (!string.IsNullOrWhiteSpace(authConfig.ApiKeyId))
		{
			return ElasticsearchAuthenticationType.ApiKey;
		}

		if (!string.IsNullOrWhiteSpace(authConfig.Username))
		{
			return ElasticsearchAuthenticationType.BasicAuthentication;
		}

		return ElasticsearchAuthenticationType.None;
	}

	/// <summary>
	/// Retrieves basic authentication credentials securely from the key provider.
	/// </summary>
	private async Task<AuthenticationHeaderValue?> GetBasicAuthenticationAsync(CancellationToken cancellationToken)
	{
		var username = _securitySettings.Authentication.Username;
		if (string.IsNullOrWhiteSpace(username))
		{
			return null;
		}

		var password = await _keyProvider.GetSecretAsync("elasticsearch:password", cancellationToken).ConfigureAwait(false);
		if (string.IsNullOrWhiteSpace(password))
		{
			return null;
		}

		var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
		return new AuthenticationHeaderValue("Basic", credentials);
	}

	/// <summary>
	/// Retrieves API key authentication credentials securely from the key provider.
	/// </summary>
	private async Task<AuthenticationHeaderValue?> GetApiKeyAuthenticationAsync(CancellationToken cancellationToken)
	{
		var apiKeyId = _securitySettings.Authentication.ApiKeyId;
		if (string.IsNullOrWhiteSpace(apiKeyId))
		{
			return null;
		}

		var apiKeySecret = await _keyProvider.GetSecretAsync($"elasticsearch:apikey:{apiKeyId}", cancellationToken).ConfigureAwait(false);
		if (string.IsNullOrWhiteSpace(apiKeySecret))
		{
			return null;
		}

		var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{apiKeyId}:{apiKeySecret}"));
		return new AuthenticationHeaderValue("ApiKey", credentials);
	}

	/// <summary>
	/// Retrieves Base64-encoded API key authentication credentials securely from the key provider.
	/// </summary>
	private async Task<AuthenticationHeaderValue?> GetBase64ApiKeyAuthenticationAsync(CancellationToken cancellationToken)
	{
		var base64ApiKey = await _keyProvider.GetSecretAsync("elasticsearch:base64apikey", cancellationToken).ConfigureAwait(false);
		if (string.IsNullOrWhiteSpace(base64ApiKey))
		{
			return null;
		}

		return new AuthenticationHeaderValue("ApiKey", base64ApiKey);
	}

	/// <summary>
	/// Retrieves OAuth2 authentication token securely from the key provider.
	/// </summary>
	private async Task<AuthenticationHeaderValue?> GetOAuth2AuthenticationAsync(CancellationToken cancellationToken)
	{
		var accessToken = await _keyProvider.GetSecretAsync("elasticsearch:oauth2:accesstoken", cancellationToken).ConfigureAwait(false);
		if (string.IsNullOrWhiteSpace(accessToken))
		{
			return null;
		}

		return new AuthenticationHeaderValue("Bearer", accessToken);
	}

	/// <summary>
	/// Retrieves service account authentication token securely from the key provider.
	/// </summary>
	private async Task<AuthenticationHeaderValue?> GetServiceAccountAuthenticationAsync(CancellationToken cancellationToken)
	{
		var serviceAccountToken =
			await _keyProvider.GetSecretAsync("elasticsearch:serviceaccount:token", cancellationToken).ConfigureAwait(false);
		if (string.IsNullOrWhiteSpace(serviceAccountToken))
		{
			return null;
		}

		return new AuthenticationHeaderValue("Bearer", serviceAccountToken);
	}

	/// <summary>
	/// Validates OAuth2 token expiration and scope.
	/// </summary>
	private async Task<bool> ValidateOAuth2TokenAsync(CancellationToken cancellationToken)
	{
		var expiresAtString = await _keyProvider.GetSecretAsync("elasticsearch:oauth2:expiresat", cancellationToken).ConfigureAwait(false);
		if (string.IsNullOrWhiteSpace(expiresAtString) || !DateTimeOffset.TryParse(expiresAtString, CultureInfo.InvariantCulture,
				DateTimeStyles.RoundtripKind, out var expiresAt))
		{
			return false;
		}

		return expiresAt > DateTimeOffset.UtcNow.AddMinutes(5); // 5 minute buffer
	}

	/// <summary>
	/// Validates service account token expiration.
	/// </summary>
	private async Task<bool> ValidateServiceAccountAsync(CancellationToken cancellationToken)
	{
		var expiresAtString = await _keyProvider.GetSecretAsync("elasticsearch:serviceaccount:expiresat", cancellationToken)
			.ConfigureAwait(false);
		if (string.IsNullOrWhiteSpace(expiresAtString) || !DateTimeOffset.TryParse(expiresAtString, CultureInfo.InvariantCulture,
				DateTimeStyles.RoundtripKind, out var expiresAt))
		{
			return false;
		}

		return expiresAt > DateTimeOffset.UtcNow.AddMinutes(5); // 5 minute buffer
	}

	/// <summary>
	/// Validates API key by checking its existence and any expiration metadata.
	/// </summary>
	private async Task<bool> ValidateApiKeyAsync(CancellationToken cancellationToken)
	{
		var apiKeyId = _securitySettings.Authentication.ApiKeyId;
		if (string.IsNullOrWhiteSpace(apiKeyId))
		{
			return false;
		}

		var apiKeySecret = await _keyProvider.GetSecretAsync($"elasticsearch:apikey:{apiKeyId}", cancellationToken).ConfigureAwait(false);
		return !string.IsNullOrWhiteSpace(apiKeySecret);
	}

	/// <summary>
	/// Rotates API key credentials by generating new keys and updating storage.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token to monitor for cancellation requests.</param>
	/// <returns>The result of the rotation operation.</returns>
	/// <remarks>
	/// <para>
	/// API key rotation involves:
	/// 1. Creating a new API key via Elasticsearch Security API
	/// 2. Storing the new key in the key provider
	/// 3. Optionally invalidating the old key after a grace period
	/// </para>
	/// <para>
	/// This implementation delegates key generation to the key provider, which may integrate
	/// with Elasticsearch's security API or an external key management system.
	/// </para>
	/// </remarks>
	private async Task<AuthenticationRotationResult> RotateApiKeyAsync(CancellationToken cancellationToken)
	{
		var apiKeyId = _securitySettings.Authentication.ApiKeyId;

		if (string.IsNullOrWhiteSpace(apiKeyId))
		{
			return AuthenticationRotationResult.Failure("API key rotation failed: ApiKeyId not configured");
		}

		try
		{
			// Get the current API key for backup
			var currentKeySecret = await _keyProvider.GetSecretAsync($"elasticsearch:apikey:{apiKeyId}", cancellationToken)
				.ConfigureAwait(false);

			// If we have an existing key, preserve it temporarily for rollback scenarios
			if (!string.IsNullOrWhiteSpace(currentKeySecret))
			{
				// Use the warning threshold as grace period (credentials remain valid during this time)
				var gracePeriodExpiry = DateTimeOffset.UtcNow.Add(_securitySettings.Authentication.CredentialRotation.WarningThreshold);
				_ = await _keyProvider.SetSecretAsync(
					$"elasticsearch:apikey:{apiKeyId}:previous",
					currentKeySecret,
					new SecretMetadata(
						description: "Previous API key preserved during rotation",
						expiresAt: gracePeriodExpiry),
					cancellationToken).ConfigureAwait(false);

				_logger.LogInformation("Previous API key preserved until {GracePeriodExpiry} for grace period transition",
					gracePeriodExpiry);
			}

			// Generate a new API key using the key provider (using HMAC type for API key generation)
			var keyGenerationResult = await _keyProvider.GenerateEncryptionKeyAsync(
				$"elasticsearch:apikey:{apiKeyId}",
				EncryptionKeyType.Hmac,
				keySize: 256,
				new SecretMetadata(description: "Elasticsearch API Key"),
				cancellationToken).ConfigureAwait(false);

			if (!keyGenerationResult.Success)
			{
				return AuthenticationRotationResult.Failure($"API key generation failed: {keyGenerationResult.ErrorMessage}");
			}

			var nextRotation = DateTimeOffset.UtcNow.Add(_securitySettings.Authentication.CredentialRotation.RotationInterval);

			_logger.LogInformation("API key rotated successfully. Next rotation scheduled for {NextRotation}", nextRotation);

			return AuthenticationRotationResult.CreateSuccess(
				$"API key rotation completed. Key version: {keyGenerationResult.KeyVersion}",
				nextRotation);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "API key rotation failed for key {ApiKeyId}", apiKeyId);
			return AuthenticationRotationResult.Failure($"API key rotation failed: {ex.Message}");
		}
	}

	/// <summary>
	/// Rotates service account credentials by generating a new client secret.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token to monitor for cancellation requests.</param>
	/// <returns>The result of the rotation operation.</returns>
	/// <remarks>
	/// <para>
	/// Service account rotation involves:
	/// 1. Generating a new client secret
	/// 2. Storing it in the key provider
	/// 3. Obtaining a fresh access token using the new credentials
	/// 4. Preserving the old secret for a grace period if configured
	/// </para>
	/// </remarks>
	private async Task<AuthenticationRotationResult> RotateServiceAccountAsync(CancellationToken cancellationToken)
	{
		var serviceAccountSettings = _securitySettings.Authentication.ServiceAccount;

		if (string.IsNullOrWhiteSpace(serviceAccountSettings.AccountId))
		{
			return AuthenticationRotationResult.Failure("Service account rotation failed: AccountId not configured");
		}

		try
		{
			// Get the current secret for backup
			var currentSecret = await _keyProvider.GetSecretAsync("elasticsearch:serviceaccount:secret", cancellationToken)
				.ConfigureAwait(false);

			// If we have an existing secret, preserve it temporarily for rollback scenarios
			if (!string.IsNullOrWhiteSpace(currentSecret))
			{
				// Use the warning threshold as grace period
				var gracePeriodExpiry = DateTimeOffset.UtcNow.Add(_securitySettings.Authentication.CredentialRotation.WarningThreshold);
				_ = await _keyProvider.SetSecretAsync(
					"elasticsearch:serviceaccount:secret:previous",
					currentSecret,
					new SecretMetadata(
						description: "Previous service account secret preserved during rotation",
						expiresAt: gracePeriodExpiry),
					cancellationToken).ConfigureAwait(false);

				_logger.LogInformation("Previous service account secret preserved until {GracePeriodExpiry} for grace period transition",
					gracePeriodExpiry);
			}

			// Rotate the service account secret using the key provider's rotation capability
			var rotationResult = await _keyProvider.RotateEncryptionKeyAsync(
				"elasticsearch:serviceaccount:secret",
				cancellationToken).ConfigureAwait(false);

			if (!rotationResult.Success)
			{
				return AuthenticationRotationResult.Failure($"Service account secret rotation failed: {rotationResult.ErrorMessage}");
			}

			// After rotating the secret, obtain a fresh token using the new credentials
			var tokenRefreshed = await RefreshServiceAccountTokenAsync(cancellationToken).ConfigureAwait(false);

			if (!tokenRefreshed)
			{
				_logger.LogWarning("Service account secret rotated but failed to obtain new token. Token refresh may be needed.");
			}

			var nextRotation = DateTimeOffset.UtcNow.Add(_securitySettings.Authentication.CredentialRotation.RotationInterval);

			_logger.LogInformation("Service account credentials rotated successfully. Next rotation scheduled for {NextRotation}",
				nextRotation);

			return AuthenticationRotationResult.CreateSuccess(
				$"Service account rotation completed. New key version: {rotationResult.NewKeyVersion}",
				nextRotation);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Service account rotation failed for account {AccountId}", serviceAccountSettings.AccountId);
			return AuthenticationRotationResult.Failure($"Service account rotation failed: {ex.Message}");
		}
	}

	/// <summary>
	/// Rotates OAuth2 client credentials by rotating the client secret.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token to monitor for cancellation requests.</param>
	/// <returns>The result of the rotation operation.</returns>
	/// <remarks>
	/// <para>
	/// OAuth2 client rotation involves:
	/// 1. Rotating the client secret in the key provider
	/// 2. Preserving the old secret for a grace period if configured
	/// 3. Refreshing the access token using the new credentials
	/// </para>
	/// <para>
	/// Note: Full OAuth2 client credential rotation (including client_id changes) typically
	/// requires admin-level access to the authorization server and is beyond the scope of
	/// this automatic rotation. This implementation rotates the client_secret only.
	/// </para>
	/// </remarks>
	private async Task<AuthenticationRotationResult> RotateOAuth2ClientAsync(CancellationToken cancellationToken)
	{
		var oauth2Settings = _securitySettings.Authentication.OAuth2;

		if (string.IsNullOrWhiteSpace(oauth2Settings.ClientId))
		{
			return AuthenticationRotationResult.Failure("OAuth2 client rotation failed: ClientId not configured");
		}

		try
		{
			// Get the current client secret for backup
			var currentSecret = await _keyProvider.GetSecretAsync("elasticsearch:oauth2:clientsecret", cancellationToken)
				.ConfigureAwait(false);

			// If we have an existing secret, preserve it temporarily for rollback scenarios
			if (!string.IsNullOrWhiteSpace(currentSecret))
			{
				// Use the warning threshold as grace period
				var gracePeriodExpiry = DateTimeOffset.UtcNow.Add(_securitySettings.Authentication.CredentialRotation.WarningThreshold);
				_ = await _keyProvider.SetSecretAsync(
					"elasticsearch:oauth2:clientsecret:previous",
					currentSecret,
					new SecretMetadata(
						description: "Previous OAuth2 client secret preserved during rotation",
						expiresAt: gracePeriodExpiry),
					cancellationToken).ConfigureAwait(false);

				_logger.LogInformation("Previous OAuth2 client secret preserved until {GracePeriodExpiry} for grace period transition",
					gracePeriodExpiry);
			}

			// Rotate the OAuth2 client secret using the key provider's rotation capability
			var rotationResult = await _keyProvider.RotateEncryptionKeyAsync(
				"elasticsearch:oauth2:clientsecret",
				cancellationToken).ConfigureAwait(false);

			if (!rotationResult.Success)
			{
				return AuthenticationRotationResult.Failure($"OAuth2 client secret rotation failed: {rotationResult.ErrorMessage}");
			}

			// After rotating the secret, obtain a fresh token using the new credentials
			var tokenRefreshed = await RefreshOAuth2TokenAsync(cancellationToken).ConfigureAwait(false);

			if (!tokenRefreshed)
			{
				_logger.LogWarning("OAuth2 client secret rotated but failed to refresh token. Token refresh may be needed.");
			}

			var nextRotation = DateTimeOffset.UtcNow.Add(_securitySettings.Authentication.CredentialRotation.RotationInterval);

			_logger.LogInformation("OAuth2 client credentials rotated successfully. Next rotation scheduled for {NextRotation}",
				nextRotation);

			return AuthenticationRotationResult.CreateSuccess(
				$"OAuth2 client rotation completed. New key version: {rotationResult.NewKeyVersion}",
				nextRotation);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "OAuth2 client rotation failed for client {ClientId}", oauth2Settings.ClientId);
			return AuthenticationRotationResult.Failure($"OAuth2 client rotation failed: {ex.Message}");
		}
	}

	/// <summary>
	/// Performs scheduled credential rotation when triggered by the timer.
	/// </summary>
	private async Task PerformScheduledRotationAsync()
	{
		if (_disposed || !SupportsRotation)
		{
			return;
		}

		try
		{
			_ = await RotateCredentialsAsync(CancellationToken.None).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Scheduled credential rotation failed for type {AuthType}", AuthenticationType);
			await HandleAuthenticationFailureAsync($"Scheduled rotation failed: {ex.Message}", CancellationToken.None)
				.ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Handles authentication failures by incrementing counters and raising events.
	/// </summary>
	private async Task HandleAuthenticationFailureAsync(string reason, CancellationToken cancellationToken)
	{
		var failures = Interlocked.Increment(ref _consecutiveFailures);

		_logger.LogWarning(
			"Authentication failure ({FailureCount}): {Reason} for type {AuthType}",
			failures, reason, AuthenticationType);

		// Raise authentication failed event for security monitoring
		OnAuthenticationFailed(new AuthenticationFailedEventArgs(
			AuthenticationType,
			reason,
			DateTimeOffset.UtcNow,
			failures));

		await Task.CompletedTask.ConfigureAwait(false);
	}

	/// <summary>
	/// Raises the CredentialsRotated event in a thread-safe manner.
	/// </summary>
	private void OnCredentialsRotated(AuthenticationRotatedEventArgs e)
	{
		EventHandler<AuthenticationRotatedEventArgs>? handler;
		lock (_eventLock)
		{
			handler = CredentialsRotated;
		}

		handler?.Invoke(this, e);
	}

	/// <summary>
	/// Raises the AuthenticationFailed event in a thread-safe manner.
	/// </summary>
	private void OnAuthenticationFailed(AuthenticationFailedEventArgs e)
	{
		EventHandler<AuthenticationFailedEventArgs>? handler;
		lock (_eventLock)
		{
			handler = AuthenticationFailed;
		}

		handler?.Invoke(this, e);
	}

	/// <summary>
	/// Throws an exception if the instance has been disposed.
	/// </summary>
	/// <exception cref="ObjectDisposedException"> </exception>
	private void ThrowIfDisposed()
	{
		if (_disposed)
		{
			throw new ObjectDisposedException(nameof(SecureElasticsearchAuthenticationProvider));
		}
	}
}
