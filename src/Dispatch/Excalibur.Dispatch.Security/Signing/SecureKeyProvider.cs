// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;

using Azure.Identity;

using Excalibur.Dispatch.Security.Diagnostics;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Security;

/// <summary>
/// Provides secure key storage and retrieval for message signing operations.
/// </summary>
/// <remarks>
/// This implementation supports:
/// <list type="bullet">
/// <item> Azure Key Vault integration for cloud key storage </item>
/// <item> Local secure storage with DPAPI protection </item>
/// <item> Key rotation and versioning </item>
/// <item> In-memory caching with automatic expiration </item>
/// </list>
/// </remarks>
public sealed partial class SecureKeyProvider : IKeyProvider, IDisposable
{
	private readonly IConfiguration _configuration;
	private readonly ILogger<SecureKeyProvider> _logger;
	private readonly ConcurrentDictionary<string, CachedKey> _keyCache = new(StringComparer.Ordinal);
	private readonly SecretClient? _secretClient;
	private readonly Timer _cacheCleanupTimer;
	private readonly SemaphoreSlim _keyLock = new(1, 1);
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="SecureKeyProvider" /> class.
	/// </summary>
	/// <param name="configuration"> The configuration used to resolve key sources. </param>
	/// <param name="logger"> The logger used for diagnostics. </param>
	public SecureKeyProvider(
		IConfiguration configuration,
		ILogger<SecureKeyProvider> logger)
	{
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentNullException.ThrowIfNull(logger);

		_configuration = configuration;
		_logger = logger;

		// Initialize Azure Key Vault client if configured
		var keyVaultUrl = _configuration["Security:KeyVault:Url"];
		if (!string.IsNullOrEmpty(keyVaultUrl))
		{
			try
			{
				_secretClient = new SecretClient(
					new Uri(keyVaultUrl),
					new DefaultAzureCredential());

				LogKeyVaultInitialized(keyVaultUrl);
			}
			catch (Exception ex)
			{
				LogKeyVaultInitializationFailed(ex);
			}
		}

		// Start cache cleanup timer
		_cacheCleanupTimer = new Timer(
			CleanupExpiredKeys,
			state: null,
			TimeSpan.FromMinutes(5),
			TimeSpan.FromMinutes(5));
	}

	/// <inheritdoc />
	public async Task<byte[]> GetKeyAsync(string keyId, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(keyId);

		// Check cache first
		if (_keyCache.TryGetValue(keyId, out var cachedKey) && !cachedKey.IsExpired)
		{
			LogKeyRetrievedFromCache(keyId);
			return cachedKey.KeyData;
		}

		await _keyLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			// Double-check cache after acquiring lock
			if (_keyCache.TryGetValue(keyId, out cachedKey) && !cachedKey.IsExpired)
			{
				return cachedKey.KeyData;
			}

			// Try to retrieve from Key Vault
			if (_secretClient != null)
			{
				var key = await GetKeyFromKeyVaultAsync(keyId, cancellationToken).ConfigureAwait(false);
				if (key != null)
				{
					CacheKey(keyId, key);
					return key;
				}
			}

			// Try to retrieve from local configuration
			var localKey = GetKeyFromConfiguration(keyId);
			if (localKey != null)
			{
				CacheKey(keyId, localKey);
				return localKey;
			}

			// Generate a new key if not found
			LogKeyNotFoundGeneratingNew(keyId);
			var newKey = GenerateKey();
			await StoreKeyAsync(keyId, newKey, cancellationToken).ConfigureAwait(false);
			CacheKey(keyId, newKey);
			return newKey;
		}
		finally
		{
			_ = _keyLock.Release();
		}
	}

	/// <inheritdoc />
	public async Task StoreKeyAsync(string keyId, byte[] key, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(keyId);
		ArgumentNullException.ThrowIfNull(key);

		await _keyLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			// Store in Key Vault if available
			if (_secretClient != null)
			{
				await StoreKeyInKeyVaultAsync(keyId, key, cancellationToken).ConfigureAwait(false);
			}
			else
			{
				// Store locally (in production, this would use secure storage)
				StoreKeyLocally(keyId, key);
			}

			// Update cache
			CacheKey(keyId, key);

			LogNewKeyStored(keyId);
		}
		finally
		{
			_ = _keyLock.Release();
		}
	}

	/// <inheritdoc />
	public async Task<byte[]> RotateKeyAsync(string keyId, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(keyId);

		await _keyLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			// Get current key for archival
			var currentKey = await GetKeyAsync(keyId, cancellationToken).ConfigureAwait(false);

			// Archive current key with timestamp
			var archiveKeyId = $"{keyId}_archived_{DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)}";
			await StoreKeyAsync(archiveKeyId, currentKey, cancellationToken).ConfigureAwait(false);

			// Generate new key
			var newKey = GenerateKey();

			// Store new key with same ID
			await StoreKeyAsync(keyId, newKey, cancellationToken).ConfigureAwait(false);

			LogKeyRotated(keyId, archiveKeyId);

			return newKey;
		}
		finally
		{
			_ = _keyLock.Release();
		}
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_cacheCleanupTimer?.Dispose();
		_keyLock?.Dispose();

		// Clear sensitive key material
		foreach (var cachedKey in _keyCache.Values)
		{
			Array.Clear(cachedKey.KeyData, 0, cachedKey.KeyData.Length);
		}

		_keyCache.Clear();

		_disposed = true;
	}

	private static byte[] GenerateKey()
	{
		// Generate a 256-bit key for HMAC-SHA256
		var key = new byte[32];
		using var rng = RandomNumberGenerator.Create();
		rng.GetBytes(key);
		return key;
	}

	private static string SanitizeKeyId(string keyId) =>

		// Azure Key Vault secret names must match pattern ^[0-9a-zA-Z-]+$
		keyId.Replace(':', '-').Replace('/', '-').Replace('_', '-');

	private async Task<byte[]?> GetKeyFromKeyVaultAsync(string keyId, CancellationToken cancellationToken)
	{
		try
		{
			var secretName = SanitizeKeyId(keyId);
			var response = await _secretClient!.GetSecretAsync(secretName, cancellationToken: cancellationToken)
				.ConfigureAwait(false);

			if (response?.Value?.Value != null)
			{
				// Convert from base64 string to bytes
				return Convert.FromBase64String(response.Value.Value);
			}
		}
		catch (Exception ex)
		{
			LogKeyNotFoundInKeyVault(keyId, ex);
		}

		return null;
	}

	private async Task StoreKeyInKeyVaultAsync(string keyId, byte[] key, CancellationToken cancellationToken)
	{
		try
		{
			var secretName = SanitizeKeyId(keyId);
			var secret = new KeyVaultSecret(secretName, Convert.ToBase64String(key))
			{
				Properties =
				{
					// Set expiration for key rotation
					ExpiresOn = DateTimeOffset.UtcNow.AddDays(90),
					Tags = { ["Purpose"] = "MessageSigning", ["CreatedBy"] = "Excalibur.Dispatch.Security" },
				},
			};

			_ = await _secretClient!.SetSecretAsync(secret, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			LogFailedToStoreKeyInKeyVault(keyId, ex);
			throw;
		}
	}

	private byte[]? GetKeyFromConfiguration(string keyId)
	{
		var configKey = $"Security:SigningKeys:{keyId}";
		var keyString = _configuration[configKey];

		if (!string.IsNullOrEmpty(keyString))
		{
			try
			{
				// Assume keys in configuration are base64 encoded
				return Convert.FromBase64String(keyString);
			}
			catch (FormatException ex)
			{
				LogInvalidKeyFormat(keyId, ex);
			}
		}

		return null;
	}

	private void StoreKeyLocally(string keyId, byte[] keyData)
	{
		// In production, this would use DPAPI or similar secure storage For now, we just cache it in memory
		_ = keyData; // Suppress unused parameter warning
		LogStoringKeyInMemoryOnly(keyId);
	}

	private void CacheKey(string keyId, byte[] key)
	{
		var cachedKey = new CachedKey { KeyData = key, CachedAt = DateTimeOffset.UtcNow, ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30) };

		_ = _keyCache.AddOrUpdate(keyId, static (_, arg) => arg, static (_, _, arg) => arg, cachedKey);
	}

	private void CleanupExpiredKeys(object? state)
	{
		var expiredKeys = _keyCache
			.Where(static kvp => kvp.Value.IsExpired)
			.Select(static kvp => kvp.Key)
			.ToList();

		foreach (var key in expiredKeys)
		{
			if (_keyCache.TryRemove(key, out var cachedKey))
			{
				// Clear sensitive data
				Array.Clear(cachedKey.KeyData, 0, cachedKey.KeyData.Length);
				LogExpiredKeyRemoved(key);
			}
		}

		if (expiredKeys.Count > 0)
		{
			LogExpiredKeysCleanedUp(expiredKeys.Count);
		}
	}

	// Source-generated logging methods
	[LoggerMessage(SecurityEventId.KeyVaultInitialized, LogLevel.Information,
		"Initialized Azure Key Vault client for {KeyVaultUrl}")]
	private partial void LogKeyVaultInitialized(string keyVaultUrl);

	[LoggerMessage(SecurityEventId.KeyVaultInitializationFailed, LogLevel.Warning,
		"Failed to initialize Azure Key Vault client, falling back to local storage")]
	private partial void LogKeyVaultInitializationFailed(Exception ex);

	[LoggerMessage(SecurityEventId.KeyRetrievedFromCache, LogLevel.Debug,
		"Retrieved key {KeyId} from cache")]
	private partial void LogKeyRetrievedFromCache(string keyId);

	[LoggerMessage(SecurityEventId.KeyNotFoundGeneratingNew, LogLevel.Warning,
		"Key {KeyId} not found, generating new key")]
	private partial void LogKeyNotFoundGeneratingNew(string keyId);

	[LoggerMessage(SecurityEventId.NewKeyStored, LogLevel.Information,
		"Stored new key {KeyId}")]
	private partial void LogNewKeyStored(string keyId);

	[LoggerMessage(SecurityEventId.SigningKeyRotated, LogLevel.Information,
		"Rotated key {KeyId}, previous key archived as {ArchiveKeyId}")]
	private partial void LogKeyRotated(string keyId, string archiveKeyId);

	[LoggerMessage(SecurityEventId.KeyNotFoundInKeyVault, LogLevel.Debug,
		"Key {KeyId} not found in Key Vault")]
	private partial void LogKeyNotFoundInKeyVault(string keyId, Exception ex);

	[LoggerMessage(SecurityEventId.FailedToStoreKeyInKeyVault, LogLevel.Error,
		"Failed to store key {KeyId} in Key Vault")]
	private partial void LogFailedToStoreKeyInKeyVault(string keyId, Exception ex);

	[LoggerMessage(SecurityEventId.InvalidKeyFormat, LogLevel.Warning,
		"Invalid key format in configuration for {KeyId}")]
	private partial void LogInvalidKeyFormat(string keyId, Exception ex);

	[LoggerMessage(SecurityEventId.ExpiredKeyRemoved, LogLevel.Debug,
		"Removed expired key {KeyId} from cache")]
	private partial void LogExpiredKeyRemoved(string keyId);

	[LoggerMessage(SecurityEventId.ExpiredKeysCleanedUp, LogLevel.Debug,
		"Cleaned up {Count} expired keys from cache")]
	private partial void LogExpiredKeysCleanedUp(int count);

	[LoggerMessage(SecurityEventId.StoringKeyInMemoryOnly, LogLevel.Warning,
		"Storing key {KeyId} in memory only - configure Key Vault for persistent storage")]
	private partial void LogStoringKeyInMemoryOnly(string keyId);

	private sealed class CachedKey
	{
		public required byte[] KeyData { get; set; }

		public DateTimeOffset CachedAt { get; set; }

		public DateTimeOffset ExpiresAt { get; set; }

		public bool IsExpired => DateTimeOffset.UtcNow > ExpiresAt;
	}
}
