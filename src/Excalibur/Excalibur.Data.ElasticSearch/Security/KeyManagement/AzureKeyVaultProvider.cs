// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Security;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Secrets;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Azure Key Vault implementation of the Elasticsearch key provider with enterprise-grade security features including HSM support,
/// automatic rotation, and comprehensive auditing.
/// </summary>
public sealed partial class AzureKeyVaultProvider : IElasticsearchKeyProvider, IDisposable, IAsyncDisposable
{
	private readonly SecretClient _secretClient;
	private readonly KeyClient _keyClient;
	private readonly ILogger<AzureKeyVaultProvider> _logger;
	private readonly AzureKeyVaultOptions _options;
	private readonly SemaphoreSlim _operationSemaphore;
	private readonly Timer _healthCheckTimer;
	private readonly ConcurrentBag<Task> _trackedTasks = [];
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="AzureKeyVaultProvider" /> class.
	/// </summary>
	/// <param name="options"> The Azure Key Vault configuration options. </param>
	/// <param name="logger"> The logger for operational and security events. </param>
	/// <exception cref="ArgumentNullException"> Thrown when required dependencies are null. </exception>
	/// <exception cref="SecurityException"> Thrown when Azure Key Vault connection cannot be established. </exception>
	public AzureKeyVaultProvider(
		IOptions<AzureKeyVaultOptions> options,
		ILogger<AzureKeyVaultProvider> logger)
	{
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		if (string.IsNullOrEmpty(_options.VaultUri))
		{
			throw new SecurityException("Azure Key Vault URI is required");
		}

		try
		{
			var credential = CreateCredential();
			var vaultUri = new Uri(_options.VaultUri);

			_secretClient = new SecretClient(vaultUri, credential);
			_keyClient = new KeyClient(vaultUri, credential);

			_operationSemaphore = new SemaphoreSlim(_options.MaxConcurrentOperations, _options.MaxConcurrentOperations);

			// Initialize health check timer
			_healthCheckTimer = new Timer(PerformHealthCheck, state: null,
				TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15));

			_logger.LogInformation("Azure Key Vault provider initialized for vault {VaultUri}", _options.VaultUri);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to initialize Azure Key Vault provider for vault {VaultUri}", _options.VaultUri);
			throw new SecurityException("Failed to initialize Azure Key Vault provider", ex);
		}
	}

	/// <inheritdoc />
	public event EventHandler<SecretAccessedEventArgs>? SecretAccessed;

	/// <inheritdoc />
	public event EventHandler<KeyRotatedEventArgs>? KeyRotated;

	/// <inheritdoc />
	public KeyManagementProviderType ProviderType => KeyManagementProviderType.AzureKeyVault;

	/// <inheritdoc />
	public bool SupportsHsm => _options.UseHsm;

	/// <inheritdoc />
	public bool SupportsKeyRotation => true;

	/// <inheritdoc />
	public async Task<string?> GetSecretAsync(string keyName, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(keyName))
		{
			throw new ArgumentException("Key name cannot be null or empty", nameof(keyName));
		}

		await _operationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var secretName = SanitizeSecretName(keyName);
			var response = await _secretClient.GetSecretAsync(secretName, cancellationToken: cancellationToken).ConfigureAwait(false);

			// Raise secret accessed event for auditing
			SecretAccessed?.Invoke(this, new SecretAccessedEventArgs(
				keyName, SecretOperation.Read, DateTimeOffset.UtcNow, GetCurrentUserId()));

			_logger.LogDebug("Secret {SecretName} retrieved successfully from Azure Key Vault", secretName);
			return response.Value.Value;
		}
		catch (RequestFailedException ex) when (ex.Status == 404)
		{
			_logger.LogDebug("Secret {KeyName} not found in Azure Key Vault", keyName);
			return null;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to retrieve secret {KeyName} from Azure Key Vault", keyName);
			throw new SecurityException($"Failed to retrieve secret {keyName}", ex);
		}
		finally
		{
			_ = _operationSemaphore.Release();
		}
	}

	/// <inheritdoc />
	public async Task<bool> SetSecretAsync(
		string keyName,
		string secretValue,
		SecretMetadata? metadata,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(keyName))
		{
			throw new ArgumentException("Key name cannot be null or empty", nameof(keyName));
		}

		if (string.IsNullOrEmpty(secretValue))
		{
			throw new ArgumentException("Secret value cannot be null or empty", nameof(secretValue));
		}

		await _operationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var secretName = SanitizeSecretName(keyName);
			var secret = new KeyVaultSecret(secretName, secretValue);

			// Apply metadata if provided
			if (metadata != null)
			{
				ApplyMetadataToSecret(secret, metadata);
			}

			// Set content type for better management
			secret.Properties.ContentType = "application/octet-stream";

			_ = await _secretClient.SetSecretAsync(secret, cancellationToken).ConfigureAwait(false);

			// Raise secret accessed event for auditing
			SecretAccessed?.Invoke(this, new SecretAccessedEventArgs(
				keyName, SecretOperation.Write, DateTimeOffset.UtcNow, GetCurrentUserId()));

			_logger.LogInformation("Secret {SecretName} stored successfully in Azure Key Vault", secretName);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to store secret {KeyName} in Azure Key Vault", keyName);
			return false;
		}
		finally
		{
			_ = _operationSemaphore.Release();
		}
	}

	/// <inheritdoc />
	public async Task<bool> DeleteSecretAsync(string keyName, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(keyName))
		{
			throw new ArgumentException("Key name cannot be null or empty", nameof(keyName));
		}

		await _operationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var secretName = SanitizeSecretName(keyName);
			var deleteOperation = await _secretClient.StartDeleteSecretAsync(secretName, cancellationToken).ConfigureAwait(false);

			// Wait for deletion to complete
			_ = await deleteOperation.WaitForCompletionAsync(cancellationToken).ConfigureAwait(false);

			// Purge the secret if configured to do so
			if (_options.PurgeOnDelete)
			{
				try
				{
					_ = await _secretClient.PurgeDeletedSecretAsync(secretName, cancellationToken).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					_logger.LogWarning(ex, "Failed to purge deleted secret {SecretName}", secretName);
				}
			}

			// Raise secret accessed event for auditing
			SecretAccessed?.Invoke(this, new SecretAccessedEventArgs(
				keyName, SecretOperation.Delete, DateTimeOffset.UtcNow, GetCurrentUserId()));

			_logger.LogInformation("Secret {SecretName} deleted successfully from Azure Key Vault", secretName);
			return true;
		}
		catch (RequestFailedException ex) when (ex.Status == 404)
		{
			_logger.LogDebug("Secret {KeyName} not found for deletion in Azure Key Vault", keyName);
			return false;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to delete secret {KeyName} from Azure Key Vault", keyName);
			return false;
		}
		finally
		{
			_ = _operationSemaphore.Release();
		}
	}

	/// <inheritdoc />
	public async Task<bool> SecretExistsAsync(string keyName, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(keyName))
		{
			throw new ArgumentException("Key name cannot be null or empty", nameof(keyName));
		}

		await _operationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var secretName = SanitizeSecretName(keyName);
			var properties = await _secretClient.GetSecretAsync(secretName, cancellationToken: cancellationToken).ConfigureAwait(false);
			return properties != null;
		}
		catch (RequestFailedException ex) when (ex.Status == 404)
		{
			return false;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to check secret existence for {KeyName} in Azure Key Vault", keyName);
			throw new SecurityException($"Failed to check secret existence for {keyName}", ex);
		}
		finally
		{
			_ = _operationSemaphore.Release();
		}
	}

	/// <inheritdoc />
	public async Task<SecretMetadata?> GetSecretMetadataAsync(string keyName, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(keyName))
		{
			throw new ArgumentException("Key name cannot be null or empty", nameof(keyName));
		}

		await _operationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var secretName = SanitizeSecretName(keyName);
			var response = await _secretClient.GetSecretAsync(secretName, cancellationToken: cancellationToken).ConfigureAwait(false);
			var properties = response.Value.Properties;

			// Raise secret accessed event for auditing
			SecretAccessed?.Invoke(this, new SecretAccessedEventArgs(
				keyName, SecretOperation.Metadata, DateTimeOffset.UtcNow, GetCurrentUserId()));

			return new SecretMetadata(
				description: properties.Tags?.TryGetValue("Description", out var desc) == true ? desc : null,
				expiresAt: properties.ExpiresOn,
				tags: properties.Tags != null ? new Dictionary<string, string>(properties.Tags, StringComparer.Ordinal) : [],
				rotationPolicy: ParseRotationPolicy(properties.Tags != null
					? new Dictionary<string, string>(properties.Tags, StringComparer.Ordinal)
					: []));
		}
		catch (RequestFailedException ex) when (ex.Status == 404)
		{
			return null;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to retrieve secret metadata for {KeyName} from Azure Key Vault", keyName);
			throw new SecurityException($"Failed to retrieve secret metadata for {keyName}", ex);
		}
		finally
		{
			_ = _operationSemaphore.Release();
		}
	}

	/// <inheritdoc />
	public async Task<KeyGenerationResult> GenerateEncryptionKeyAsync(
		string keyName,
		EncryptionKeyType keyType,
		int keySize,
		SecretMetadata? metadata,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(keyName))
		{
			throw new ArgumentException("Key name cannot be null or empty", nameof(keyName));
		}

		await _operationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var keyData = keyType switch
			{
				EncryptionKeyType.Aes => GenerateAesKey(keySize),
				EncryptionKeyType.Hmac => GenerateHmacKey(keySize),
				_ => throw new SecurityException($"Unsupported key type: {keyType}"),
			};

			var keyDataBase64 = Convert.ToBase64String(keyData);
			var success = await SetSecretAsync(keyName, keyDataBase64, metadata, cancellationToken).ConfigureAwait(false);

			if (!success)
			{
				return KeyGenerationResult.CreateFailure("Failed to store generated key in Azure Key Vault");
			}

			var keyVersion = await GetKeyVersionAsync(keyName, cancellationToken).ConfigureAwait(false);

			_logger.LogInformation(
				"Encryption key {KeyName} generated successfully with type {KeyType} and size {KeySize}",
				keyName, keyType, keySize);

			return KeyGenerationResult.CreateSuccess(keyName, keyType, keySize, keyVersion ?? "1.0");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to generate encryption key {KeyName}", keyName);
			return KeyGenerationResult.CreateFailure(ex.Message);
		}
		finally
		{
			_ = _operationSemaphore.Release();
		}
	}

	/// <inheritdoc />
	public async Task<KeyRotationResult> RotateEncryptionKeyAsync(string keyName, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(keyName))
		{
			throw new ArgumentException("Key name cannot be null or empty", nameof(keyName));
		}

		await _operationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			// Get current key metadata to determine key type and size
			var currentMetadata = await GetSecretMetadataAsync(keyName, cancellationToken).ConfigureAwait(false);
			if (currentMetadata == null)
			{
				return KeyRotationResult.CreateFailure(keyName, "Current key not found for rotation");
			}

			// Parse key type and size from metadata tags
			var keyTypeStr = currentMetadata.Tags.GetValueOrDefault("KeyType", "Aes");
			var keySizeStr = currentMetadata.Tags.GetValueOrDefault("KeySize", "256");

			if (!Enum.TryParse<EncryptionKeyType>(keyTypeStr, out var keyType) ||
				!int.TryParse(keySizeStr, out var keySize))
			{
				return KeyRotationResult.CreateFailure(keyName, "Invalid key metadata for rotation");
			}

			var previousVersion = await GetKeyVersionAsync(keyName, cancellationToken).ConfigureAwait(false) ?? "unknown";

			// Generate new key with same parameters
			var generationResult = await GenerateEncryptionKeyAsync(keyName, keyType, keySize, currentMetadata, cancellationToken)
				.ConfigureAwait(false);

			if (!generationResult.Success)
			{
				return KeyRotationResult.CreateFailure(keyName, generationResult.ErrorMessage ?? "Key generation failed");
			}

			var nextRotationDue = DateTimeOffset.UtcNow.Add(_options.KeyRotationInterval);

			// Raise key rotation event
			KeyRotated?.Invoke(this, new KeyRotatedEventArgs(
				keyName, generationResult.KeyVersion, DateTimeOffset.UtcNow, nextRotationDue));

			_logger.LogInformation(
				"Key {KeyName} rotated successfully from version {PreviousVersion} to {NewVersion}",
				keyName, previousVersion, generationResult.KeyVersion);

			return KeyRotationResult.CreateSuccess(keyName, generationResult.KeyVersion, previousVersion, nextRotationDue);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to rotate encryption key {KeyName}", keyName);
			return KeyRotationResult.CreateFailure(keyName, ex.Message);
		}
		finally
		{
			_ = _operationSemaphore.Release();
		}
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<SecretInfo>> ListSecretsAsync(
		string? prefix,
		bool includeMetadata,
		CancellationToken cancellationToken)
	{
		await _operationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var secrets = new List<SecretInfo>();

			await foreach (var secretProperties in _secretClient.GetPropertiesOfSecretsAsync(cancellationToken).ConfigureAwait(false))
			{
				if (!string.IsNullOrEmpty(prefix) && !secretProperties.Name.StartsWith(prefix, StringComparison.Ordinal))
				{
					continue;
				}

				SecretMetadata? metadata = null;
				if (includeMetadata)
				{
					_ = secretProperties.Tags.TryGetValue("Description", out var description);
					metadata = new SecretMetadata(
						description: description,
						expiresAt: secretProperties.ExpiresOn,
						tags: new Dictionary<string, string>(secretProperties.Tags, StringComparer.Ordinal));
				}

				secrets.Add(new SecretInfo(secretProperties.Name, metadata));
			}

			// Raise secret accessed event for auditing
			SecretAccessed?.Invoke(this, new SecretAccessedEventArgs(
				prefix ?? "*", SecretOperation.List, DateTimeOffset.UtcNow, GetCurrentUserId()));

			_logger.LogDebug("Listed {SecretCount} secrets with prefix '{Prefix}'", secrets.Count, prefix ?? "none");
			return secrets.AsReadOnly();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to list secrets in Azure Key Vault");
			throw new SecurityException("Failed to list secrets", ex);
		}
		finally
		{
			_ = _operationSemaphore.Release();
		}
	}

	/// <summary>
	/// Releases all resources used by the AzureKeyVaultProvider.
	/// </summary>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_operationSemaphore?.Dispose();
		_healthCheckTimer?.Dispose();
		_disposed = true;
	}

	/// <summary>
	/// Asynchronously releases resources, ensuring tracked timer callbacks have completed.
	/// </summary>
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		_operationSemaphore?.Dispose();
		await _healthCheckTimer.DisposeAsync().ConfigureAwait(false);

		try
		{
			await Task.WhenAll(_trackedTasks).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			// Expected during shutdown
		}
	}

	/// <summary>
	/// Sanitizes a secret name to comply with Azure Key Vault naming requirements.
	/// </summary>
	private static string SanitizeSecretName(string name)
	{
		// Azure Key Vault secret names must be 1-127 characters, alphanumeric and hyphens only
		var sanitized = MyRegex().Replace(name, "-");
		return sanitized.Length > 127 ? sanitized[..127] : sanitized;
	}

	/// <summary>
	/// Applies metadata to a Key Vault secret.
	/// </summary>
	private static void ApplyMetadataToSecret(KeyVaultSecret secret, SecretMetadata metadata)
	{
		if (!string.IsNullOrEmpty(metadata.Description))
		{
			secret.Properties.Tags["Description"] = metadata.Description;
		}

		if (metadata.ExpiresAt.HasValue)
		{
			secret.Properties.ExpiresOn = metadata.ExpiresAt;
		}

		foreach (var tag in metadata.Tags)
		{
			secret.Properties.Tags[tag.Key] = tag.Value;
		}

		if (metadata.RotationPolicy != null)
		{
			secret.Properties.Tags["RotationEnabled"] = metadata.RotationPolicy.Enabled.ToString();
			secret.Properties.Tags["RotationInterval"] = metadata.RotationPolicy.RotationInterval.ToString();
		}
	}

	/// <summary>
	/// Parses rotation policy from secret tags.
	/// </summary>
	private static SecretRotationPolicy? ParseRotationPolicy(IReadOnlyDictionary<string, string> tags)
	{
		if (!tags.TryGetValue("RotationEnabled", out var enabledStr) ||
			!bool.TryParse(enabledStr, out var enabled) || !enabled)
		{
			return null;
		}

		var rotationInterval = TimeSpan.FromDays(30);
		if (tags.TryGetValue("RotationInterval", out var intervalStr) &&
			TimeSpan.TryParse(intervalStr, out var parsedInterval))
		{
			rotationInterval = parsedInterval;
		}

		return new SecretRotationPolicy(enabled, rotationInterval);
	}

	/// <summary>
	/// Generates an AES encryption key of the specified size.
	/// </summary>
	/// <exception cref="ArgumentException"></exception>
	private static byte[] GenerateAesKey(int keySize)
	{
		if (keySize is not 128 and not 192 and not 256)
		{
			throw new ArgumentException("AES key size must be 128, 192, or 256 bits", nameof(keySize));
		}

		var key = new byte[keySize / 8];
		RandomNumberGenerator.Fill(key);
		return key;
	}

	/// <summary>
	/// Generates an HMAC key of the specified size.
	/// </summary>
	private static byte[] GenerateHmacKey(int keySize)
	{
		var key = new byte[keySize / 8];
		RandomNumberGenerator.Fill(key);
		return key;
	}

	/// <summary>
	/// Gets the current user identifier for audit logging.
	/// </summary>
	private static string? GetCurrentUserId() =>

		// This would typically be retrieved from the current security context
		Environment.UserName;

	[GeneratedRegex("[^a-zA-Z0-9-]")]
	private static partial Regex MyRegex();

	/// <summary>
	/// Creates the appropriate Azure credential based on configuration.
	/// </summary>
	private DefaultAzureCredential CreateCredential()
	{
		var options = new DefaultAzureCredentialOptions();

		if (!string.IsNullOrEmpty(_options.TenantId))
		{
			options.SharedTokenCacheTenantId = _options.TenantId;
			options.InteractiveBrowserTenantId = _options.TenantId;
		}

		if (!string.IsNullOrEmpty(_options.ClientId))
		{
			options.ManagedIdentityClientId = _options.ClientId;
		}

		return new DefaultAzureCredential(options);
	}

	/// <summary>
	/// Gets the current version of a key from Azure Key Vault.
	/// </summary>
	private async Task<string?> GetKeyVersionAsync(string keyName, CancellationToken cancellationToken)
	{
		try
		{
			var secretName = SanitizeSecretName(keyName);
			var response = await _secretClient.GetSecretAsync(secretName, cancellationToken: cancellationToken).ConfigureAwait(false);
			return response.Value.Properties.Version;
		}
		catch
		{
			return null;
		}
	}

	/// <summary>
	/// Performs periodic health checks on the Azure Key Vault connection.
	/// </summary>
	private void PerformHealthCheck(object? state)
	{
		if (_disposed)
		{
			return;
		}

		var task = Task.Factory.StartNew(async () =>
		{
			try
			{
				// Attempt to list secrets to verify connectivity
				await foreach (var _ in _secretClient.GetPropertiesOfSecretsAsync().ConfigureAwait(false))
				{
					break; // Just check the first one
				}

				_logger.LogDebug("Azure Key Vault health check passed");
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Azure Key Vault health check failed");
			}
		}, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default).Unwrap();
		_trackedTasks.Add(task);
	}
}
