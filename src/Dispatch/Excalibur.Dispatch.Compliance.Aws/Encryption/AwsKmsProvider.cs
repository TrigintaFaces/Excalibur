// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using AwsKeyMetadata = Amazon.KeyManagementService.Model.KeyMetadata;
using DispatchKeyMetadata = Excalibur.Dispatch.Compliance.KeyMetadata;

namespace Excalibur.Dispatch.Compliance.Aws;

/// <summary>
/// AWS KMS implementation of <see cref="IKeyManagementProvider"/>.
/// </summary>
/// <remarks>
/// <para>
/// This provider uses AWS Key Management Service for key lifecycle management.
/// Key material never leaves AWS KMS - only data keys or encrypted data are returned.
/// </para>
/// <para>
/// Features:
/// <list type="bullet">
/// <item>Key alias management with configurable prefix</item>
/// <item>Multi-region key support for disaster recovery</item>
/// <item>FIPS 140-2 endpoint support</item>
/// <item>Metadata caching to reduce API costs</item>
/// <item>Automatic key rotation via AWS KMS</item>
/// </list>
/// </para>
/// </remarks>
public sealed partial class AwsKmsProvider : IKeyManagementProvider, IDisposable
{
	private readonly IAmazonKeyManagementService _kmsClient;
	private readonly IMemoryCache _metadataCache;
	private readonly AwsKmsOptions _options;
	private readonly ILogger<AwsKmsProvider> _logger;
	private readonly ConcurrentDictionary<string, string> _aliasToKeyIdMap = new();
	private readonly SemaphoreSlim _keyCreationLock = new(1, 1);
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="AwsKmsProvider"/> class.
	/// </summary>
	/// <param name="kmsClient">The AWS KMS client.</param>
	/// <param name="options">The configuration options.</param>
	/// <param name="logger">The logger for diagnostics.</param>
	/// <param name="metadataCache">Optional memory cache for key metadata.</param>
	public AwsKmsProvider(
		IAmazonKeyManagementService kmsClient,
		IOptions<AwsKmsOptions> options,
		ILogger<AwsKmsProvider> logger,
		IMemoryCache? metadataCache = null)
	{
		_kmsClient = kmsClient ?? throw new ArgumentNullException(nameof(kmsClient));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_metadataCache = metadataCache ?? new MemoryCache(new MemoryCacheOptions());
	}

	/// <inheritdoc/>
	public async Task<DispatchKeyMetadata?> GetKeyAsync(string keyId, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrEmpty(keyId);

		var cacheKey = $"key:{keyId}";
		if (_metadataCache.TryGetValue(cacheKey, out DispatchKeyMetadata? cached))
		{
			return cached;
		}

		try
		{
			var alias = _options.BuildKeyAlias(keyId);
			var response = await _kmsClient.DescribeKeyAsync(
				new DescribeKeyRequest { KeyId = alias },
				cancellationToken).ConfigureAwait(false);

			var metadata = MapToKeyMetadata(keyId, response.KeyMetadata);

			_ = _metadataCache.Set(cacheKey, metadata, TimeSpan.FromSeconds(_options.MetadataCacheDurationSeconds));
			_aliasToKeyIdMap[keyId] = response.KeyMetadata.KeyId;

			return metadata;
		}
		catch (NotFoundException)
		{
			LogKeyNotFound(keyId);
			return null;
		}
		catch (Exception ex)
		{
			LogFailedToRetrieveKey(keyId, ex);
			throw;
		}
	}

	/// <inheritdoc/>
	public async Task<DispatchKeyMetadata?> GetKeyVersionAsync(string keyId, int version, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrEmpty(keyId);

		// AWS KMS handles key versions internally via key rotation
		// Each DescribeKey returns the current (latest) key metadata
		// For versioning, we track rotation events in the key's metadata
		var metadata = await GetKeyAsync(keyId, cancellationToken).ConfigureAwait(false);

		if (metadata is null)
		{
			return null;
		}

		// AWS KMS doesn't expose version numbers directly
		// Version 1 is always the current active key
		if (version == metadata.Version)
		{
			return metadata;
		}

		// For older versions, we cannot retrieve them directly from AWS KMS
		// as key material versions are managed internally
		LogVersionsNotAvailable(version, keyId);
		return null;
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<DispatchKeyMetadata>> ListKeysAsync(
		KeyStatus? status,
		string? purpose,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		var results = new List<DispatchKeyMetadata>();
		string? marker = null;

		try
		{
			do
			{
				var listRequest = new ListAliasesRequest { Limit = 100, Marker = marker };

				var aliasResponse = await _kmsClient.ListAliasesAsync(listRequest, cancellationToken)
					.ConfigureAwait(false);

				foreach (var alias in aliasResponse.Aliases)
				{
					// Filter to our aliases only
					if (!alias.AliasName.StartsWith($"alias/{_options.KeyAliasPrefix}", StringComparison.Ordinal))
					{
						continue;
					}

					if (string.IsNullOrEmpty(alias.TargetKeyId))
					{
						continue;
					}

					var keyId = ExtractKeyIdFromAlias(alias.AliasName);
					if (string.IsNullOrEmpty(keyId))
					{
						continue;
					}

					// Filter by purpose if specified
					if (purpose is not null && !keyId.Contains(purpose, StringComparison.OrdinalIgnoreCase))
					{
						continue;
					}

					try
					{
						var describeResponse = await _kmsClient.DescribeKeyAsync(
							new DescribeKeyRequest { KeyId = alias.TargetKeyId },
							cancellationToken).ConfigureAwait(false);

						var metadata = MapToKeyMetadata(keyId, describeResponse.KeyMetadata);

						// Filter by status if specified
						if (status.HasValue && metadata.Status != status.Value)
						{
							continue;
						}

						results.Add(metadata);
					}
					catch (NotFoundException)
					{
						// Key was deleted, skip
					}
				}

				marker = aliasResponse.Truncated == true ? aliasResponse.NextMarker : null;
			} while (marker is not null);
		}
		catch (Exception ex)
		{
			LogFailedToListKeys(ex);
			throw;
		}

		return results;
	}

	/// <inheritdoc/>
	public async Task<KeyRotationResult> RotateKeyAsync(
		string keyId,
		EncryptionAlgorithm algorithm,
		string? purpose,
		DateTimeOffset? expiresAt,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrEmpty(keyId);

		await _keyCreationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var alias = _options.BuildKeyAlias(keyId);
			var existingKey = await GetKeyAsync(keyId, cancellationToken).ConfigureAwait(false);

			if (existingKey is not null)
			{
				// Key exists - trigger AWS KMS rotation
				return await RotateExistingKeyAsync(keyId, existingKey, cancellationToken).ConfigureAwait(false);
			}
			else
			{
				// Create new key
				return await CreateNewKeyAsync(keyId, alias, purpose, cancellationToken).ConfigureAwait(false);
			}
		}
		catch (Exception ex)
		{
			LogFailedToRotateKey(keyId, ex);
			return KeyRotationResult.Failed(ex.Message);
		}
		finally
		{
			_ = _keyCreationLock.Release();
		}
	}

	/// <inheritdoc/>
	public async Task<bool> DeleteKeyAsync(string keyId, int retentionDays, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrEmpty(keyId);

		// AWS KMS requires 7-30 days retention
		retentionDays = Math.Clamp(retentionDays, 7, 30);

		try
		{
			var alias = _options.BuildKeyAlias(keyId);

			// First, resolve the alias to the actual key ID
			string? kmsKeyId = null;
			if (_aliasToKeyIdMap.TryGetValue(keyId, out var cachedKeyId))
			{
				kmsKeyId = cachedKeyId;
			}
			else
			{
				var key = await GetKeyAsync(keyId, cancellationToken).ConfigureAwait(false);
				if (key is null)
				{
					return false;
				}

				kmsKeyId = _aliasToKeyIdMap.GetValueOrDefault(keyId);
			}

			if (string.IsNullOrEmpty(kmsKeyId))
			{
				LogKeyIdResolutionFailed(keyId);
				return false;
			}

			// Schedule key deletion
			_ = await _kmsClient.ScheduleKeyDeletionAsync(
				new ScheduleKeyDeletionRequest { KeyId = kmsKeyId, PendingWindowInDays = retentionDays },
				cancellationToken).ConfigureAwait(false);

			// Delete the alias
			try
			{
				_ = await _kmsClient.DeleteAliasAsync(
					new DeleteAliasRequest { AliasName = alias },
					cancellationToken).ConfigureAwait(false);
			}
			catch (NotFoundException)
			{
				// Alias already deleted
			}

			// Clear cache
			_metadataCache.Remove($"key:{keyId}");
			_ = _aliasToKeyIdMap.TryRemove(keyId, out _);

			LogKeyScheduledForDeletion(keyId, retentionDays);

			return true;
		}
		catch (NotFoundException)
		{
			return false;
		}
		catch (Exception ex)
		{
			LogFailedToScheduleDeletion(keyId, ex);
			throw;
		}
	}

	/// <inheritdoc/>
	public async Task<bool> SuspendKeyAsync(string keyId, string reason, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrEmpty(keyId);
		ArgumentException.ThrowIfNullOrEmpty(reason);

		try
		{
			var alias = _options.BuildKeyAlias(keyId);

			// Disable the key
			_ = await _kmsClient.DisableKeyAsync(
				new DisableKeyRequest { KeyId = alias },
				cancellationToken).ConfigureAwait(false);

			// Update tags with suspension reason
			string? kmsKeyId = null;
			if (_aliasToKeyIdMap.TryGetValue(keyId, out var cachedKeyId))
			{
				kmsKeyId = cachedKeyId;
			}

			if (!string.IsNullOrEmpty(kmsKeyId))
			{
				_ = await _kmsClient.TagResourceAsync(
					new TagResourceRequest
					{
						KeyId = kmsKeyId,
						Tags = new List<Tag>
						{
							new() { TagKey = "SuspensionReason", TagValue = reason },
							new() { TagKey = "SuspendedAt", TagValue = DateTimeOffset.UtcNow.ToString("O") }
						}
					},
					cancellationToken).ConfigureAwait(false);
			}

			// Clear cache
			_metadataCache.Remove($"key:{keyId}");

			LogKeySuspended(keyId, reason);

			return true;
		}
		catch (NotFoundException)
		{
			return false;
		}
		catch (Exception ex)
		{
			LogFailedToSuspendKey(keyId, ex);
			throw;
		}
	}

	/// <inheritdoc/>
	public async Task<DispatchKeyMetadata?> GetActiveKeyAsync(string? purpose, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		// Build key ID based on purpose
		var keyId = purpose ?? "default";
		var metadata = await GetKeyAsync(keyId, cancellationToken).ConfigureAwait(false);

		if (metadata?.Status == KeyStatus.Active)
		{
			return metadata;
		}

		// No active key found - create one if purpose is null (default key)
		if (purpose is null && metadata is null)
		{
			var result = await RotateKeyAsync(keyId, EncryptionAlgorithm.Aes256Gcm, null, null, cancellationToken).ConfigureAwait(false);
			return result.NewKey;
		}

		return null;
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_keyCreationLock.Dispose();
		_aliasToKeyIdMap.Clear();

		// Don't dispose _kmsClient or _metadataCache - they may be shared/injected
		_disposed = true;
		LogDisposed();
	}

	[LoggerMessage(LogLevel.Debug, "Key {KeyId} not found in AWS KMS")]
	private partial void LogKeyNotFound(string keyId);

	[LoggerMessage(LogLevel.Error, "Failed to retrieve key {KeyId} from AWS KMS")]
	private partial void LogFailedToRetrieveKey(string keyId, Exception ex);

	[LoggerMessage(LogLevel.Warning,
		"AWS KMS does not expose historical key versions. Requested v{Version} for key {KeyId}")]
	private partial void LogVersionsNotAvailable(int version, string keyId);

	[LoggerMessage(LogLevel.Error, "Failed to list keys from AWS KMS")]
	private partial void LogFailedToListKeys(Exception ex);

	[LoggerMessage(LogLevel.Error, "Failed to rotate key {KeyId}")]
	private partial void LogFailedToRotateKey(string keyId, Exception ex);

	[LoggerMessage(LogLevel.Warning, "Could not resolve KMS key ID for {KeyId}")]
	private partial void LogKeyIdResolutionFailed(string keyId);

	[LoggerMessage(LogLevel.Warning,
		"Key {KeyId} scheduled for deletion in {RetentionDays} days (crypto-shredding)")]
	private partial void LogKeyScheduledForDeletion(string keyId, int retentionDays);

	[LoggerMessage(LogLevel.Error, "Failed to schedule deletion for key {KeyId}")]
	private partial void LogFailedToScheduleDeletion(string keyId, Exception ex);

	[LoggerMessage(LogLevel.Warning, "Key {KeyId} suspended: {Reason}")]
	private partial void LogKeySuspended(string keyId, string reason);

	[LoggerMessage(LogLevel.Error, "Failed to suspend key {KeyId}")]
	private partial void LogFailedToSuspendKey(string keyId, Exception ex);

	[LoggerMessage(LogLevel.Debug, "AwsKmsProvider disposed")]
	private partial void LogDisposed();

	[LoggerMessage(LogLevel.Information, "Enabled automatic key rotation for {KeyId}")]
	private partial void LogEnabledAutoRotation(string keyId);

	[LoggerMessage(LogLevel.Information, "Rotated key {KeyId} to new KMS key {KmsKeyId}")]
	private partial void LogRotatedKey(string keyId, string kmsKeyId);

	[LoggerMessage(LogLevel.Information, "Created new key {KeyId} with KMS key {KmsKeyId}")]
	private partial void LogCreatedKey(string keyId, string kmsKeyId);

	private async Task<KeyRotationResult> RotateExistingKeyAsync(
		string keyId,
		DispatchKeyMetadata existingKey,
		CancellationToken cancellationToken)
	{
		var alias = _options.BuildKeyAlias(keyId);

		// AWS KMS supports automatic rotation for symmetric keys
		// EnableKeyRotation triggers rotation within the next year
		// For immediate rotation, we need to create a new key and update the alias
		if (!_aliasToKeyIdMap.TryGetValue(keyId, out var kmsKeyId))
		{
			return KeyRotationResult.Failed("Could not resolve KMS key ID");
		}

		// Check if automatic rotation is enabled
		var rotationStatus = await _kmsClient.GetKeyRotationStatusAsync(
			new GetKeyRotationStatusRequest { KeyId = kmsKeyId },
			cancellationToken).ConfigureAwait(false);

		if (rotationStatus.KeyRotationEnabled != true && _options.EnableAutoRotation)
		{
			// Enable automatic rotation
			_ = await _kmsClient.EnableKeyRotationAsync(
				new EnableKeyRotationRequest { KeyId = kmsKeyId },
				cancellationToken).ConfigureAwait(false);

			LogEnabledAutoRotation(keyId);
		}

		// For immediate rotation, create a new key and update the alias
		var newKey = await CreateKmsKeyAsync(keyId, existingKey.Purpose, cancellationToken).ConfigureAwait(false);

		// Update alias to point to new key
		_ = await _kmsClient.UpdateAliasAsync(
			new UpdateAliasRequest { AliasName = alias, TargetKeyId = newKey.KeyId },
			cancellationToken).ConfigureAwait(false);

		// Disable old key (keeps it for decryption)
		_ = await _kmsClient.DisableKeyAsync(
			new DisableKeyRequest { KeyId = kmsKeyId },
			cancellationToken).ConfigureAwait(false);

		// Clear cache and update mapping
		_metadataCache.Remove($"key:{keyId}");
		_aliasToKeyIdMap[keyId] = newKey.KeyId;

		var newMetadata = MapToKeyMetadata(keyId, newKey);
		var previousMetadata = existingKey with { Status = KeyStatus.DecryptOnly };

		LogRotatedKey(keyId, newKey.KeyId);

		return KeyRotationResult.Succeeded(newMetadata, previousMetadata);
	}

	private async Task<KeyRotationResult> CreateNewKeyAsync(
		string keyId,
		string alias,
		string? purpose,
		CancellationToken cancellationToken)
	{
		var kmsKey = await CreateKmsKeyAsync(keyId, purpose, cancellationToken).ConfigureAwait(false);

		// Create alias
		_ = await _kmsClient.CreateAliasAsync(
			new CreateAliasRequest { AliasName = alias, TargetKeyId = kmsKey.KeyId },
			cancellationToken).ConfigureAwait(false);

		// Enable auto-rotation if configured
		if (_options.EnableAutoRotation)
		{
			_ = await _kmsClient.EnableKeyRotationAsync(
				new EnableKeyRotationRequest { KeyId = kmsKey.KeyId },
				cancellationToken).ConfigureAwait(false);
		}

		_aliasToKeyIdMap[keyId] = kmsKey.KeyId;

		var metadata = MapToKeyMetadata(keyId, kmsKey);
		_ = _metadataCache.Set($"key:{keyId}", metadata, TimeSpan.FromSeconds(_options.MetadataCacheDurationSeconds));

		LogCreatedKey(keyId, kmsKey.KeyId);

		return KeyRotationResult.Succeeded(metadata);
	}

	private async Task<AwsKeyMetadata> CreateKmsKeyAsync(
		string keyId,
		string? purpose,
		CancellationToken cancellationToken)
	{
		var request = new CreateKeyRequest
		{
			KeySpec = _options.DefaultKeySpec,
			KeyUsage = KeyUsageType.ENCRYPT_DECRYPT,
			Description = $"Excalibur Dispatch encryption key: {keyId}",
			MultiRegion = _options.CreateMultiRegionKeys,
			Tags = new List<Tag>
			{
				new() { TagKey = "Application", TagValue = "Excalibur.Dispatch" },
				new() { TagKey = "KeyId", TagValue = keyId },
				new() { TagKey = "CreatedAt", TagValue = DateTimeOffset.UtcNow.ToString("O") }
			}
		};

		if (!string.IsNullOrEmpty(purpose))
		{
			request.Tags.Add(new Tag { TagKey = "Purpose", TagValue = purpose });
		}

		if (!string.IsNullOrEmpty(_options.Environment))
		{
			request.Tags.Add(new Tag { TagKey = "Environment", TagValue = _options.Environment });
		}

		var response = await _kmsClient.CreateKeyAsync(request, cancellationToken).ConfigureAwait(false);
		return response.KeyMetadata;
	}

	private DispatchKeyMetadata MapToKeyMetadata(string keyId, AwsKeyMetadata kmsMetadata)
	{
		// AWS SDK KeyState is a ConstantClass, not an enum - use Value comparison
		var stateValue = kmsMetadata.KeyState?.Value;
		var status = stateValue switch
		{
			"Enabled" => KeyStatus.Active,
			"Disabled" => KeyStatus.Suspended,
			"PendingDeletion" => KeyStatus.PendingDestruction,
			"PendingImport" => KeyStatus.Active,
			"PendingReplicaDeletion" => KeyStatus.PendingDestruction,
			"Unavailable" => KeyStatus.Suspended,
			_ => KeyStatus.Active
		};

		return new DispatchKeyMetadata
		{
			KeyId = keyId,
			Version = 1, // AWS KMS manages versions internally
			Status = status,
			Algorithm = EncryptionAlgorithm.Aes256Gcm, // SYMMETRIC_DEFAULT is AES-256-GCM
			CreatedAt = kmsMetadata.CreationDate is { } creationDate
				? new DateTimeOffset(creationDate)
				: throw new InvalidOperationException(Resources.AwsKmsProvider_MissingCreationDate),
			ExpiresAt = kmsMetadata.DeletionDate is { } deletionDate
				? new DateTimeOffset(deletionDate)
				: null,
			LastRotatedAt = null, // AWS KMS doesn't expose this directly
			Purpose = null, // Would need to fetch from tags
			IsFipsCompliant = _options.UseFipsEndpoint
		};
	}

	private string? ExtractKeyIdFromAlias(string aliasName)
	{
		// alias/excalibur-dispatch-{environment}-{keyId} -> {keyId}
		var prefix = $"alias/{_options.KeyAliasPrefix}";
		if (!string.IsNullOrEmpty(_options.Environment))
		{
			prefix += $"-{_options.Environment}";
		}

		prefix += "-";

		if (!aliasName.StartsWith(prefix, StringComparison.Ordinal))
		{
			return null;
		}

		return aliasName[prefix.Length..];
	}
}
