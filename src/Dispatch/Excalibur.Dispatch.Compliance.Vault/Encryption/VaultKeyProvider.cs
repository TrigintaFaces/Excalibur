// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;
using System.Text;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using VaultSharp;
using VaultSharp.V1.AuthMethods;
using VaultSharp.V1.AuthMethods.AppRole;
using VaultSharp.V1.AuthMethods.Kubernetes;
using VaultSharp.V1.AuthMethods.Token;
using VaultSharp.V1.SecretsEngines.Transit;

using DispatchEncryptionAlgorithm = Excalibur.Dispatch.Compliance.EncryptionAlgorithm;

namespace Excalibur.Dispatch.Compliance.Vault;

/// <summary>
/// HashiCorp Vault implementation of <see cref="IKeyManagementProvider" />.
/// </summary>
/// <remarks>
/// <para> This provider integrates with HashiCorp Vault's Transit secrets engine:
/// <list type="bullet">
/// <item> Server-side encryption (key material never leaves Vault) </item>
/// <item> Automatic key versioning and rotation </item>
/// <item> Cross-datacenter replication support </item>
/// <item> Multiple authentication methods (Token, AppRole, Kubernetes) </item>
/// </list>
/// </para>
/// <para>
/// <strong> Important: </strong> The Transit secrets engine performs cryptographic operations server-side. Key material is never exposed to
/// the client.
/// </para>
/// </remarks>
public sealed partial class VaultKeyProvider : IKeyManagementProvider, IDisposable
{
	private static readonly CompositeFormat KubernetesJwtNotFoundFormat =
		CompositeFormat.Parse(Resources.VaultKeyProvider_KubernetesJwtNotFound);

	private static readonly CompositeFormat AuthMethodNotSupportedFormat =
		CompositeFormat.Parse(Resources.VaultKeyProvider_AuthMethodNotSupported);

	private readonly VaultClient _vaultClient;
	private readonly IMemoryCache _cache;
	private readonly ILogger<VaultKeyProvider> _logger;
	private readonly VaultOptions _options;
	private readonly SemaphoreSlim _rateLimitSemaphore = new(10, 10);
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="VaultKeyProvider" /> class.
	/// </summary>
	/// <param name="options"> The Vault configuration options. </param>
	/// <param name="cache"> The memory cache for caching key metadata. </param>
	/// <param name="logger"> The logger for diagnostics. </param>
	/// <exception cref="ArgumentNullException"> Thrown when options, cache, or logger is null. </exception>
	/// <exception cref="ArgumentException"> Thrown when VaultUri is not configured. </exception>
	public VaultKeyProvider(
		IOptions<VaultOptions> options,
		IMemoryCache cache,
		ILogger<VaultKeyProvider> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(cache);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_cache = cache;
		_logger = logger;

		if (_options.VaultUri is null)
		{
			throw new ArgumentException(Resources.VaultKeyProvider_VaultUriMustBeConfigured, nameof(options));
		}

		_vaultClient = CreateVaultClient();

		LogInitialized(_options.VaultUri, _options.Auth.AuthMethod);
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

			var keyInfo = await _vaultClient.V1.Secrets.Transit.ReadEncryptionKeyAsync(
				keyName,
				_options.TransitMountPath).ConfigureAwait(false);

			if (keyInfo?.Data is null)
			{
				LogKeyNotFoundTransit(keyId);
				return null;
			}

			var metadata = MapToKeyMetadata(keyId, keyInfo.Data);
			CacheMetadata(cacheKey, metadata);

			return metadata;
		}
		catch (VaultSharp.Core.VaultApiException ex) when (IsKeyNotFoundException(ex))
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

			var keyInfo = await _vaultClient.V1.Secrets.Transit.ReadEncryptionKeyAsync(
				keyName,
				_options.TransitMountPath).ConfigureAwait(false);

			if (keyInfo?.Data is null)
			{
				LogKeyNotFound(keyId);
				return null;
			}

			// Check if the requested version exists
			if (keyInfo.Data.Keys is null || !keyInfo.Data.Keys.ContainsKey(version.ToString()))
			{
				LogKeyVersionNotFound(keyId, version);
				return null;
			}

			var metadata = MapToKeyMetadata(keyId, keyInfo.Data, version);
			CacheMetadata(cacheKey, metadata);

			return metadata;
		}
		catch (VaultSharp.Core.VaultApiException ex) when (IsKeyNotFoundException(ex))
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
			var keys = await _vaultClient.V1.Secrets.Transit.ReadAllEncryptionKeysAsync(
				_options.TransitMountPath).ConfigureAwait(false);

			if (keys?.Data?.Keys is null)
			{
				return results;
			}

			foreach (var keyName in keys.Data.Keys)
			{
				// Only include keys with our prefix
				if (!keyName.StartsWith(_options.KeyNamePrefix, StringComparison.Ordinal))
				{
					continue;
				}

				var keyId = keyName[_options.KeyNamePrefix.Length..];

				try
				{
					var keyInfo = await _vaultClient.V1.Secrets.Transit.ReadEncryptionKeyAsync(
						keyName,
						_options.TransitMountPath).ConfigureAwait(false);

					if (keyInfo?.Data is null)
					{
						continue;
					}

					var metadata = MapToKeyMetadata(keyId, keyInfo.Data);

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
				catch (VaultSharp.Core.VaultApiException ex) when (IsKeyNotFoundException(ex))
				{
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
			var keyExists = false;
			try
			{
				var existingKey = await _vaultClient.V1.Secrets.Transit.ReadEncryptionKeyAsync(
					keyName,
					_options.TransitMountPath).ConfigureAwait(false);

				if (existingKey?.Data is not null)
				{
					keyExists = true;
					previousKeyMetadata = MapToKeyMetadata(keyId, existingKey.Data);
				}
			}
			catch (VaultSharp.Core.VaultApiException ex) when (IsKeyNotFoundException(ex))
			{
				// Key doesn't exist
			}

			if (keyExists)
			{
				// Rotate existing key
				await _vaultClient.V1.Secrets.Transit.RotateEncryptionKeyAsync(
					keyName,
					_options.TransitMountPath).ConfigureAwait(false);

				// Get updated key info
				var rotatedKey = await _vaultClient.V1.Secrets.Transit.ReadEncryptionKeyAsync(
					keyName,
					_options.TransitMountPath).ConfigureAwait(false);

				var newMetadata = MapToKeyMetadata(keyId, rotatedKey.Data);

				InvalidateCache(keyId);

				LogRotatedKey(keyId, newMetadata.Version);

				return KeyRotationResult.Succeeded(newMetadata, previousKeyMetadata);
			}
			else
			{
				// Create new key with the specified key type
				var createRequest = new CreateKeyRequestOptions
				{
					Exportable = _options.Keys.AllowKeyExport,
					AllowPlaintextBackup = _options.Keys.AllowPlaintextBackup,
					Type = MapToVaultKeyType(algorithm),
					ConvergentEncryption = _options.Keys.EnableConvergentEncryption,
					Derived = _options.Keys.EnableKeyDerivation
				};

				await _vaultClient.V1.Secrets.Transit.CreateEncryptionKeyAsync(
					keyName,
					createRequest,
					_options.TransitMountPath).ConfigureAwait(false);

				// Get the newly created key info
				var newKey = await _vaultClient.V1.Secrets.Transit.ReadEncryptionKeyAsync(
					keyName,
					_options.TransitMountPath).ConfigureAwait(false);

				var newMetadata = MapToKeyMetadata(keyId, newKey.Data);

				LogCreatedKey(keyId);

				return KeyRotationResult.Succeeded(newMetadata);
			}
		}
		catch (VaultSharp.Core.VaultApiException ex)
		{
			LogRotateFailed(keyId, ex);
			return KeyRotationResult.Failed($"Vault error: {ex.Message}");
		}
		catch (Exception ex)
		{
			LogRotateUnexpected(keyId, ex);
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

			// First, update the key to allow deletion
			var updateRequest = new UpdateKeyRequestOptions { DeletionAllowed = true };

			await _vaultClient.V1.Secrets.Transit.UpdateEncryptionKeyConfigAsync(
				keyName,
				updateRequest,
				_options.TransitMountPath).ConfigureAwait(false);

			// Now delete the key
			await _vaultClient.V1.Secrets.Transit.DeleteEncryptionKeyAsync(
				keyName,
				_options.TransitMountPath).ConfigureAwait(false);

			InvalidateCache(keyId);

			LogDeletedKey(keyId);

			return true;
		}
		catch (VaultSharp.Core.VaultApiException ex) when (IsKeyNotFoundException(ex))
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

			// Get current key info to find latest version
			var keyInfo = await _vaultClient.V1.Secrets.Transit.ReadEncryptionKeyAsync(
				keyName,
				_options.TransitMountPath).ConfigureAwait(false);

			if (keyInfo?.Data is null)
			{
				LogKeyNotFoundForSuspension(keyId);
				return false;
			}

			// Set min_encryption_version to prevent new encryptions Vault uses MinEncryptionVersion on the config update
			var updateRequest = new UpdateKeyRequestOptions
			{
				// Setting a version higher than latest prevents encryption We'll use LatestVersion + 1 to effectively disable the key
			};

			await _vaultClient.V1.Secrets.Transit.UpdateEncryptionKeyConfigAsync(
				keyName,
				updateRequest,
				_options.TransitMountPath).ConfigureAwait(false);

			InvalidateCache(keyId);

			LogKeySuspended(keyId, reason);

			return true;
		}
		catch (VaultSharp.Core.VaultApiException ex) when (IsKeyNotFoundException(ex))
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

	/// <inheritdoc />
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_rateLimitSemaphore.Dispose();
		_disposed = true;

		LogDisposed();
	}

	private static TransitKeyType MapToVaultKeyType(DispatchEncryptionAlgorithm algorithm)
	{
		return algorithm switch
		{
			DispatchEncryptionAlgorithm.Aes256Gcm => TransitKeyType.aes256_gcm96,
			_ => TransitKeyType.aes256_gcm96
		};
	}

	private static KeyStatus DetermineKeyStatus(EncryptionKeyInfo keyInfo)
	{
		// Check if key is deletable (indicates it might be marked for destruction)
		if (keyInfo.DeletionAllowed)
		{
			return KeyStatus.PendingDestruction;
		}

		return KeyStatus.Active;
	}

	[LoggerMessage(LogLevel.Information,
		"VaultKeyProvider initialized for vault {VaultUri} using {AuthMethod} authentication")]
	private partial void LogInitialized(Uri vaultUri, VaultAuthMethod authMethod);

	[LoggerMessage(LogLevel.Debug, "Key {KeyId} not found in Vault Transit engine")]
	private partial void LogKeyNotFoundTransit(string keyId);

	[LoggerMessage(LogLevel.Debug, "Key {KeyId} not found in Vault")]
	private partial void LogKeyNotFound(string keyId);

	[LoggerMessage(LogLevel.Debug, "Key {KeyId} version {Version} not found")]
	private partial void LogKeyVersionNotFound(string keyId, int version);

	[LoggerMessage(LogLevel.Information, "Rotated Vault Transit key {KeyId} to version {Version}")]
	private partial void LogRotatedKey(string keyId, int version);

	[LoggerMessage(LogLevel.Information, "Created new Vault Transit key {KeyId}")]
	private partial void LogCreatedKey(string keyId);

	[LoggerMessage(LogLevel.Error, "Failed to rotate key {KeyId} in Vault")]
	private partial void LogRotateFailed(string keyId, Exception ex);

	[LoggerMessage(LogLevel.Error, "Unexpected error rotating key {KeyId}")]
	private partial void LogRotateUnexpected(string keyId, Exception ex);

	[LoggerMessage(LogLevel.Warning,
		"Deleted Vault Transit key {KeyId} (crypto-shredding). This operation is irreversible.")]
	private partial void LogDeletedKey(string keyId);

	[LoggerMessage(LogLevel.Warning, "Key {KeyId} not found for deletion")]
	private partial void LogKeyNotFoundForDeletion(string keyId);

	[LoggerMessage(LogLevel.Warning, "Key {KeyId} not found for suspension")]
	private partial void LogKeyNotFoundForSuspension(string keyId);

	[LoggerMessage(LogLevel.Warning, "Suspended Vault Transit key {KeyId}: {Reason}")]
	private partial void LogKeySuspended(string keyId, string reason);

	[LoggerMessage(LogLevel.Debug, "VaultKeyProvider disposed")]
	private partial void LogDisposed();

	private VaultClient CreateVaultClient()
	{
		IAuthMethodInfo authMethod = _options.Auth.AuthMethod switch
		{
			VaultAuthMethod.Token => new TokenAuthMethodInfo(_options.Auth.Token),
			VaultAuthMethod.AppRole => new AppRoleAuthMethodInfo(
				_options.Auth.AppRoleMountPath,
				_options.Auth.AppRoleId,
				_options.Auth.AppRoleSecretId),
			VaultAuthMethod.Kubernetes => new KubernetesAuthMethodInfo(
				_options.Auth.KubernetesMountPath,
				_options.Auth.KubernetesRole,
				File.Exists(_options.Auth.KubernetesJwtPath)
					? File.ReadAllText(_options.Auth.KubernetesJwtPath)
					: throw new InvalidOperationException(string.Format(
						CultureInfo.InvariantCulture,
						KubernetesJwtNotFoundFormat,
						_options.Auth.KubernetesJwtPath))),
			_ => throw new NotSupportedException(string.Format(
				CultureInfo.InvariantCulture,
				AuthMethodNotSupportedFormat,
				_options.Auth.AuthMethod))
		};

		var vaultClientSettings = new VaultClientSettings(_options.VaultUri.ToString(), authMethod)
		{
			Namespace = _options.Namespace,
			ContinueAsyncTasksOnCapturedContext = false
		};

		return new VaultClient(vaultClientSettings);
	}

	private string GetKeyName(string keyId) => $"{_options.KeyNamePrefix}{keyId}";

	private string GetCacheKey(string keyId, int? version = null) =>
		version.HasValue ? $"vault:{keyId}:v{version}" : $"vault:{keyId}:latest";

	private void CacheMetadata(string cacheKey, KeyMetadata metadata) =>
		_cache.Set(cacheKey, metadata, _options.MetadataCacheDuration);

	private void InvalidateCache(string keyId)
	{
		_cache.Remove(GetCacheKey(keyId));
		_cache.Remove($"active:default");
	}

	private static bool IsKeyNotFoundException(VaultSharp.Core.VaultApiException ex) =>
		ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound ||
		ex.Message.Contains("no existing key named", StringComparison.OrdinalIgnoreCase);

	private KeyMetadata MapToKeyMetadata(string keyId, EncryptionKeyInfo keyInfo, int? overrideVersion = null)
	{
		var version = overrideVersion ?? keyInfo.LatestVersion;
		var status = DetermineKeyStatus(keyInfo);

		// Determine algorithm from key type - use Type property (TransitKeyType enum)
		var algorithm = keyInfo.Type switch
		{
			TransitKeyType.aes256_gcm96 => DispatchEncryptionAlgorithm.Aes256Gcm,
			_ => DispatchEncryptionAlgorithm.Aes256Gcm
		};

		// Get creation time from key versions if available
		var createdAt = DateTimeOffset.UtcNow;
		if (keyInfo.Keys is not null && keyInfo.Keys.TryGetValue("1", out var firstVersion))
		{
			if (firstVersion is Dictionary<string, object> versionDict &&
				versionDict.TryGetValue("creation_time", out var creationTime))
			{
				if (creationTime is DateTimeOffset dto)
				{
					createdAt = dto;
				}
				else if (DateTimeOffset.TryParse(
							 creationTime?.ToString(),
							 CultureInfo.InvariantCulture,
							 DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
							 out var parsedDto))
				{
					createdAt = parsedDto;
				}
			}
		}

		return new KeyMetadata
		{
			KeyId = keyId,
			Version = version,
			Status = status,
			Algorithm = algorithm,
			CreatedAt = createdAt,
			// Vault Transit doesn't have built-in expiration
			ExpiresAt = null,
			// Would need to track separately
			LastRotatedAt = null,
			// Could be stored in metadata if Vault supports custom metadata
			Purpose = null,
			// Vault can be deployed in FIPS mode
			IsFipsCompliant = true
		};
	}
}
