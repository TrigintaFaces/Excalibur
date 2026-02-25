// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using DispatchEncryptionAlgorithm = Excalibur.Dispatch.Compliance.EncryptionAlgorithm;

namespace Excalibur.Dispatch.Compliance.Azure;

/// <summary>
/// Azure Key Vault implementation of <see cref="IKeyManagementProvider" />.
/// </summary>
/// <remarks>
/// <para> This provider integrates with Azure Key Vault for enterprise-grade key management:
/// <list type="bullet">
/// <item> HSM-backed keys with FIPS 140-2 Level 2 validation (Premium tier) </item>
/// <item> Automatic key rotation support </item>
/// <item> Multi-region disaster recovery </item>
/// <item> RBAC-based access control </item>
/// </list>
/// </para>
/// <para>
/// <strong> Important: </strong> This provider performs server-side cryptographic operations. Key material never leaves Azure Key Vault,
/// providing maximum security.
/// </para>
/// </remarks>
public sealed partial class AzureKeyVaultProvider : IKeyManagementProvider, IDisposable
{
	private readonly KeyClient _keyClient;
	private readonly ConcurrentDictionary<string, CryptographyClient> _cryptoClients = new();
	private readonly IMemoryCache _cache;
	private readonly ILogger<AzureKeyVaultProvider> _logger;
	private readonly AzureKeyVaultOptions _options;
	private readonly SemaphoreSlim _rateLimitSemaphore = new(10, 10); // Limit concurrent operations
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="AzureKeyVaultProvider" /> class.
	/// </summary>
	/// <param name="options"> The Azure Key Vault configuration options. </param>
	/// <param name="cache"> The memory cache for caching key metadata. </param>
	/// <param name="logger"> The logger for diagnostics. </param>
	/// <exception cref="ArgumentNullException"> Thrown when options, cache, or logger is null. </exception>
	/// <exception cref="ArgumentException"> Thrown when VaultUri is not configured. </exception>
	public AzureKeyVaultProvider(
		IOptions<AzureKeyVaultOptions> options,
		IMemoryCache cache,
		ILogger<AzureKeyVaultProvider> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(cache);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_cache = cache;
		_logger = logger;

		if (_options.VaultUri is null)
		{
			throw new ArgumentException(Resources.AzureKeyVaultProvider_VaultUriRequired, nameof(options));
		}

		var credential = _options.Credential ?? new DefaultAzureCredential();
		_keyClient = new KeyClient(_options.VaultUri, credential);

		LogProviderInitialized(_options.VaultUri);
	}

	/// <inheritdoc />
	public async Task<KeyMetadata?> GetKeyAsync(string keyId, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrEmpty(keyId);

		var cacheKey = GetCacheKey(keyId);
		if (_cache.TryGetValue(cacheKey, out KeyMetadata? cached))
		{
			return cached;
		}

		await _rateLimitSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var keyName = GetKeyName(keyId);
			var response = await _keyClient.GetKeyAsync(keyName, cancellationToken: cancellationToken).ConfigureAwait(false);

			var metadata = MapToKeyMetadata(keyId, response.Value);
			CacheMetadata(cacheKey, metadata);

			return metadata;
		}
		catch (RequestFailedException ex) when (ex.Status == 404)
		{
			LogKeyNotFound(keyId);
			return null;
		}
		finally
		{
			_ = _rateLimitSemaphore.Release();
		}
	}

	/// <inheritdoc />
	public async Task<KeyMetadata?> GetKeyVersionAsync(string keyId, int version, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrEmpty(keyId);

		var cacheKey = GetCacheKey(keyId, version);
		if (_cache.TryGetValue(cacheKey, out KeyMetadata? cached))
		{
			return cached;
		}

		await _rateLimitSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var keyName = GetKeyName(keyId);

			// Azure Key Vault uses string versions, we map integer versions by iterating through key versions
			await foreach (var keyProperties in _keyClient.GetPropertiesOfKeyVersionsAsync(keyName, cancellationToken))
			{
				// Extract version number from the key version string
				var versionNumber = ExtractVersionNumber(keyProperties);
				if (versionNumber == version)
				{
					var response = await _keyClient.GetKeyAsync(keyName, keyProperties.Version, cancellationToken).ConfigureAwait(false);
					var metadata = MapToKeyMetadata(keyId, response.Value, version);
					CacheMetadata(cacheKey, metadata);
					return metadata;
				}
			}

			LogKeyVersionNotFound(keyId, version);
			return null;
		}
		catch (RequestFailedException ex) when (ex.Status == 404)
		{
			LogKeyNotFound(keyId);
			return null;
		}
		finally
		{
			_ = _rateLimitSemaphore.Release();
		}
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<KeyMetadata>> ListKeysAsync(
		KeyStatus? status,
		string? purpose,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		var results = new List<KeyMetadata>();

		await _rateLimitSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			await foreach (var keyProperties in _keyClient.GetPropertiesOfKeysAsync(cancellationToken))
			{
				// Only include keys with our prefix
				if (!keyProperties.Name.StartsWith(_options.KeyNamePrefix, StringComparison.Ordinal))
				{
					continue;
				}

				var keyId = keyProperties.Name[_options.KeyNamePrefix.Length..];

				// Get full key details
				try
				{
					var response = await _keyClient.GetKeyAsync(keyProperties.Name, cancellationToken: cancellationToken)
						.ConfigureAwait(false);
					var metadata = MapToKeyMetadata(keyId, response.Value);

					// Apply filters
					if (status.HasValue && metadata.Status != status.Value)
					{
						continue;
					}

					if (purpose is not null && metadata.Purpose != purpose)
					{
						continue;
					}

					results.Add(metadata);
				}
				catch (RequestFailedException ex) when (ex.Status == 404)
				{
					// Key was deleted between listing and getting details
					continue;
				}
			}
		}
		finally
		{
			_ = _rateLimitSemaphore.Release();
		}

		return results;
	}

	/// <inheritdoc />
	public async Task<KeyRotationResult> RotateKeyAsync(
		string keyId,
		DispatchEncryptionAlgorithm algorithm,
		string? purpose,
		DateTimeOffset? expiresAt,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrEmpty(keyId);

		await _rateLimitSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var keyName = GetKeyName(keyId);
			KeyMetadata? previousKeyMetadata = null;

			// Check if key exists
			KeyVaultKey? existingKey = null;
			try
			{
				var existingResponse = await _keyClient.GetKeyAsync(keyName, cancellationToken: cancellationToken).ConfigureAwait(false);
				existingKey = existingResponse.Value;
				previousKeyMetadata = MapToKeyMetadata(keyId, existingKey);
			}
			catch (RequestFailedException ex) when (ex.Status == 404)
			{
				// Key doesn't exist, will create new
			}

			// Create new key version
			var keyType = _options.UseSoftwareKeys ? KeyType.Rsa : KeyType.RsaHsm;
			if (algorithm == DispatchEncryptionAlgorithm.Aes256Gcm)
			{
				// For AES operations, we use RSA keys to wrap/unwrap symmetric keys Azure Key Vault doesn't directly support AES key storage
				keyType = _options.UseSoftwareKeys ? KeyType.Rsa : KeyType.RsaHsm;
			}

			var createOptions = new CreateRsaKeyOptions(keyName, hardwareProtected: !_options.UseSoftwareKeys)
			{
				KeySize = 2048, // RSA key size for wrapping
				ExpiresOn = expiresAt,
				Enabled = true
			};

			// Set tags for metadata
			createOptions.Tags["excalibur:purpose"] = purpose ?? "general";
			createOptions.Tags["excalibur:algorithm"] = algorithm.ToString();
			createOptions.Tags["excalibur:created"] = DateTimeOffset.UtcNow.ToString("O");

			if (existingKey is not null)
			{
				// Rotate existing key by creating new version
				var rotateResponse = await _keyClient.RotateKeyAsync(keyName, cancellationToken).ConfigureAwait(false);
				var newMetadata = MapToKeyMetadata(keyId, rotateResponse.Value);

				// Invalidate cache
				InvalidateCache(keyId);

				LogKeyRotated(keyId, rotateResponse.Value.Properties.Version);

				return KeyRotationResult.Succeeded(newMetadata, previousKeyMetadata);
			}
			else
			{
				// Create new key
				var createResponse = await _keyClient.CreateRsaKeyAsync(createOptions, cancellationToken).ConfigureAwait(false);
				var newMetadata = MapToKeyMetadata(keyId, createResponse.Value);

				LogKeyCreated(keyId);

				return KeyRotationResult.Succeeded(newMetadata);
			}
		}
		catch (RequestFailedException ex)
		{
			LogKeyRotationFailed(ex, keyId);
			return KeyRotationResult.Failed($"Azure Key Vault error: {ex.Message}");
		}
		catch (Exception ex)
		{
			LogKeyRotationUnexpectedError(ex, keyId);
			return KeyRotationResult.Failed(ex.Message);
		}
		finally
		{
			_ = _rateLimitSemaphore.Release();
		}
	}

	/// <inheritdoc />
	public async Task<bool> DeleteKeyAsync(string keyId, int retentionDays, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrEmpty(keyId);

		await _rateLimitSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var keyName = GetKeyName(keyId);

			// Azure Key Vault has built-in soft-delete with configurable retention The retention period is configured at the vault level
			var operation = await _keyClient.StartDeleteKeyAsync(keyName, cancellationToken).ConfigureAwait(false);

			// Wait for deletion to complete
			_ = await operation.WaitForCompletionAsync(cancellationToken).ConfigureAwait(false);

			// Invalidate cache
			InvalidateCache(keyId);

			// Remove crypto client
			_ = _cryptoClients.TryRemove(keyId, out _);

			LogKeyScheduledForDeletion(keyId);

			return true;
		}
		catch (RequestFailedException ex) when (ex.Status == 404)
		{
			LogKeyNotFoundForDeletion(keyId);
			return false;
		}
		finally
		{
			_ = _rateLimitSemaphore.Release();
		}
	}

	/// <inheritdoc />
	public async Task<bool> SuspendKeyAsync(string keyId, string reason, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrEmpty(keyId);
		ArgumentException.ThrowIfNullOrEmpty(reason);

		await _rateLimitSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var keyName = GetKeyName(keyId);

			// Get current key
			var response = await _keyClient.GetKeyAsync(keyName, cancellationToken: cancellationToken).ConfigureAwait(false);
			var key = response.Value;

			// Disable the key
			var properties = key.Properties;
			properties.Enabled = false;
			properties.Tags["excalibur:suspended"] = "true";
			properties.Tags["excalibur:suspension_reason"] = reason;
			properties.Tags["excalibur:suspended_at"] = DateTimeOffset.UtcNow.ToString("O");

			_ = await _keyClient.UpdateKeyPropertiesAsync(properties, cancellationToken: cancellationToken).ConfigureAwait(false);

			// Invalidate cache
			InvalidateCache(keyId);

			// Remove crypto client
			_ = _cryptoClients.TryRemove(keyId, out _);

			LogKeySuspended(keyId, reason);

			return true;
		}
		catch (RequestFailedException ex) when (ex.Status == 404)
		{
			LogKeyNotFoundForSuspension(keyId);
			return false;
		}
		finally
		{
			_ = _rateLimitSemaphore.Release();
		}
	}

	/// <inheritdoc />
	public async Task<KeyMetadata?> GetActiveKeyAsync(string? purpose, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		var cacheKey = $"active:{purpose ?? "default"}";
		if (_cache.TryGetValue(cacheKey, out KeyMetadata? cached))
		{
			return cached;
		}

		var keys = await ListKeysAsync(KeyStatus.Active, purpose, cancellationToken).ConfigureAwait(false);

		// Return the most recently created active key
		var activeKey = keys
			.Where(k => !k.ExpiresAt.HasValue || k.ExpiresAt.Value > DateTimeOffset.UtcNow)
			.OrderByDescending(k => k.CreatedAt)
			.FirstOrDefault();

		if (activeKey is not null)
		{
			_ = _cache.Set(cacheKey, activeKey, _options.MetadataCacheDuration);
		}

		return activeKey;
	}

	/// <summary>
	/// Gets a cryptography client for performing operations with a specific key.
	/// </summary>
	/// <param name="keyId"> The key identifier. </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> A cryptography client for the specified key. </returns>
	public async Task<CryptographyClient> GetCryptographyClientAsync(string keyId, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrEmpty(keyId);

		if (_cryptoClients.TryGetValue(keyId, out var existingClient))
		{
			return existingClient;
		}

		var keyName = GetKeyName(keyId);
		var response = await _keyClient.GetKeyAsync(keyName, cancellationToken: cancellationToken).ConfigureAwait(false);

		var credential = _options.Credential ?? new DefaultAzureCredential();
		var client = new CryptographyClient(response.Value.Id, credential);

		_ = _cryptoClients.TryAdd(keyId, client);

		return client;
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_rateLimitSemaphore.Dispose();
		_cryptoClients.Clear();
		_disposed = true;

		LogProviderDisposed();
	}

	private static int ExtractVersionNumber(KeyProperties properties)
	{
		// Azure Key Vault versions are hex strings representing creation time We use a hash to create a stable integer version number
		if (string.IsNullOrEmpty(properties.Version))
		{
			return 1;
		}

		// Use the version string hash as a pseudo-version number In production, you might want to maintain a mapping
		return (Math.Abs(properties.Version.GetHashCode(StringComparison.Ordinal)) % 100000) + 1;
	}

	private static KeyStatus DetermineKeyStatus(KeyProperties properties)
	{
		if (properties.Tags.TryGetValue("excalibur:suspended", out var suspended) && suspended == "true")
		{
			return KeyStatus.Suspended;
		}

		if (properties.Enabled != true)
		{
			return KeyStatus.Suspended;
		}

		if (properties.ExpiresOn.HasValue && properties.ExpiresOn.Value <= DateTimeOffset.UtcNow)
		{
			return KeyStatus.DecryptOnly;
		}

		// Check if there's a newer version (this would make current version decrypt-only) For simplicity, we assume the latest version is
		// always active
		return KeyStatus.Active;
	}

	private string GetKeyName(string keyId) => $"{_options.KeyNamePrefix}{keyId}";

	private string GetCacheKey(string keyId, int? version = null) =>
		version.HasValue ? $"akv:{keyId}:v{version}" : $"akv:{keyId}:latest";

	private void CacheMetadata(string cacheKey, KeyMetadata metadata) =>
		_cache.Set(cacheKey, metadata, _options.MetadataCacheDuration);

	private void InvalidateCache(string keyId)
	{
		// Remove all cached versions for this key IMemoryCache doesn't support pattern removal, so we rely on expiration
		_cache.Remove(GetCacheKey(keyId));
		_cache.Remove($"active:default");
	}

	private KeyMetadata MapToKeyMetadata(string keyId, KeyVaultKey key, int? overrideVersion = null)
	{
		var status = DetermineKeyStatus(key.Properties);
		var version = overrideVersion ?? ExtractVersionNumber(key.Properties);

		// Extract purpose from tags
		string? purpose = null;
		if (key.Properties.Tags.TryGetValue("excalibur:purpose", out var purposeTag))
		{
			purpose = purposeTag;
		}

		// Determine algorithm from tags or key type
		var algorithm = DispatchEncryptionAlgorithm.Aes256Gcm;
		if (key.Properties.Tags.TryGetValue("excalibur:algorithm", out var algoTag) &&
			Enum.TryParse<DispatchEncryptionAlgorithm>(algoTag, out var parsedAlgo))
		{
			algorithm = parsedAlgo;
		}

		// Check for FIPS compliance (HSM-backed keys are FIPS compliant)
		var isFipsCompliant = key.KeyType == KeyType.RsaHsm ||
							  key.KeyType == KeyType.EcHsm ||
							  key.KeyType == KeyType.OctHsm;

		// Log warning for Standard tier in production
		if (_options.WarnOnStandardTierInProduction && !isFipsCompliant)
		{
			LogStandardTierWarning(keyId);
		}

		return new KeyMetadata
		{
			KeyId = keyId,
			Version = version,
			Status = status,
			Algorithm = algorithm,
			CreatedAt = key.Properties.CreatedOn ?? DateTimeOffset.UtcNow,
			ExpiresAt = key.Properties.ExpiresOn,
			LastRotatedAt = key.Properties.UpdatedOn,
			Purpose = purpose,
			IsFipsCompliant = isFipsCompliant
		};
	}

	[LoggerMessage(AzureKeyVaultEventId.ProviderInitialized, LogLevel.Information,
		"AzureKeyVaultProvider initialized for vault {VaultUri}")]
	private partial void LogProviderInitialized(Uri vaultUri);

	[LoggerMessage(AzureKeyVaultEventId.KeyNotFound, LogLevel.Debug,
		"Key {KeyId} not found in Azure Key Vault")]
	private partial void LogKeyNotFound(string keyId);

	[LoggerMessage(AzureKeyVaultEventId.KeyVersionNotFound, LogLevel.Debug,
		"Key {KeyId} version {Version} not found")]
	private partial void LogKeyVersionNotFound(string keyId, int version);

	[LoggerMessage(AzureKeyVaultEventId.KeyRotated, LogLevel.Information,
		"Rotated Azure Key Vault key {KeyId} to version {Version}")]
	private partial void LogKeyRotated(string keyId, string? version);

	[LoggerMessage(AzureKeyVaultEventId.KeyCreated, LogLevel.Information,
		"Created new Azure Key Vault key {KeyId}")]
	private partial void LogKeyCreated(string keyId);

	[LoggerMessage(AzureKeyVaultEventId.KeyRotationFailed, LogLevel.Error,
		"Failed to rotate key {KeyId} in Azure Key Vault")]
	private partial void LogKeyRotationFailed(Exception exception, string keyId);

	[LoggerMessage(AzureKeyVaultEventId.KeyRotationUnexpectedError, LogLevel.Error,
		"Unexpected error rotating key {KeyId}")]
	private partial void LogKeyRotationUnexpectedError(Exception exception, string keyId);

	[LoggerMessage(AzureKeyVaultEventId.KeyScheduledForDeletion, LogLevel.Warning,
		"Scheduled Azure Key Vault key {KeyId} for deletion (crypto-shredding). Recoverable during soft-delete period.")]
	private partial void LogKeyScheduledForDeletion(string keyId);

	[LoggerMessage(AzureKeyVaultEventId.KeyNotFoundForDeletion, LogLevel.Warning,
		"Key {KeyId} not found for deletion")]
	private partial void LogKeyNotFoundForDeletion(string keyId);

	[LoggerMessage(AzureKeyVaultEventId.KeySuspended, LogLevel.Warning,
		"Suspended Azure Key Vault key {KeyId}: {Reason}")]
	private partial void LogKeySuspended(string keyId, string reason);

	[LoggerMessage(AzureKeyVaultEventId.KeyNotFoundForSuspension, LogLevel.Warning,
		"Key {KeyId} not found for suspension")]
	private partial void LogKeyNotFoundForSuspension(string keyId);

	[LoggerMessage(AzureKeyVaultEventId.ProviderDisposed, LogLevel.Debug,
		"AzureKeyVaultProvider disposed")]
	private partial void LogProviderDisposed();

	[LoggerMessage(AzureKeyVaultEventId.StandardTierWarning, LogLevel.Warning,
		"Key {KeyId} is using software-protected keys (Standard tier). Consider using HSM-backed keys (Premium tier) for production compliance workloads.")]
	private partial void LogStandardTierWarning(string keyId);
}
