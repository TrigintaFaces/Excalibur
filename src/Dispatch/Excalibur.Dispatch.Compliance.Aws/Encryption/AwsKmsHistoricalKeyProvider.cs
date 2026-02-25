// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Compliance.Aws;

/// <summary>
/// AWS KMS implementation of <see cref="IHistoricalKeyProvider"/>.
/// </summary>
/// <remarks>
/// <para>
/// AWS KMS manages key versions internally through automatic key rotation.
/// This provider tracks key rotation events to build a version history,
/// enabling temporal key version lookups for decrypting data encrypted
/// with previous key versions.
/// </para>
/// <para>
/// Since AWS KMS handles decryption transparently with the correct key version
/// (the ciphertext blob includes the key version identifier), this provider
/// primarily serves metadata lookup and audit purposes.
/// </para>
/// </remarks>
public sealed partial class AwsKmsHistoricalKeyProvider : IHistoricalKeyProvider
{
	private readonly IAmazonKeyManagementService _kmsClient;
	private readonly AwsKmsHistoricalKeyOptions _options;
	private readonly AwsKmsOptions _kmsOptions;
	private readonly ILogger<AwsKmsHistoricalKeyProvider> _logger;
	private readonly ConcurrentDictionary<string, (IReadOnlyList<KeyMetadata> Versions, DateTimeOffset CachedAt)> _versionCache = new();

	/// <summary>
	/// Initializes a new instance of the <see cref="AwsKmsHistoricalKeyProvider"/> class.
	/// </summary>
	/// <param name="kmsClient">The AWS KMS client.</param>
	/// <param name="options">The historical key provider options.</param>
	/// <param name="kmsOptions">The base AWS KMS options for alias resolution.</param>
	/// <param name="logger">The logger for diagnostics.</param>
	public AwsKmsHistoricalKeyProvider(
		IAmazonKeyManagementService kmsClient,
		IOptions<AwsKmsHistoricalKeyOptions> options,
		IOptions<AwsKmsOptions> kmsOptions,
		ILogger<AwsKmsHistoricalKeyProvider> logger)
	{
		_kmsClient = kmsClient ?? throw new ArgumentNullException(nameof(kmsClient));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_kmsOptions = kmsOptions?.Value ?? throw new ArgumentNullException(nameof(kmsOptions));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public async Task<KeyMetadata?> GetKeyVersionAsync(
		string keyId,
		DateTimeOffset asOf,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(keyId);

		var versions = await ListKeyVersionsAsync(keyId, cancellationToken).ConfigureAwait(false);

		// Find the version that was active at the specified time.
		// Versions are ordered chronologically; the active version is the last one
		// created before or at the specified timestamp.
		KeyMetadata? activeVersion = null;

		foreach (var version in versions)
		{
			if (version.CreatedAt <= asOf)
			{
				activeVersion = version;
			}
			else
			{
				break;
			}
		}

		if (activeVersion is null)
		{
			LogNoVersionFoundForTimestamp(keyId, asOf);
		}

		return activeVersion;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<KeyMetadata>> ListKeyVersionsAsync(
		string keyId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(keyId);

		// Check cache first
		if (_options.CacheKeyVersions &&
			_versionCache.TryGetValue(keyId, out var cached) &&
			DateTimeOffset.UtcNow - cached.CachedAt < _options.MaxCacheAge)
		{
			return cached.Versions;
		}

		try
		{
			var alias = _kmsOptions.BuildKeyAlias(keyId);
			var versions = await FetchKeyVersionsFromKmsAsync(alias, cancellationToken).ConfigureAwait(false);

			if (_options.CacheKeyVersions)
			{
				_versionCache[keyId] = (versions, DateTimeOffset.UtcNow);
			}

			LogKeyVersionsListed(keyId, versions.Count);
			return versions;
		}
		catch (NotFoundException)
		{
			LogKeyNotFoundForVersionListing(keyId);
			return [];
		}
		catch (Exception ex)
		{
			LogFailedToListKeyVersions(keyId, ex);
			throw;
		}
	}

	private async Task<IReadOnlyList<KeyMetadata>> FetchKeyVersionsFromKmsAsync(
		string alias,
		CancellationToken cancellationToken)
	{
		// Describe the key to get its ARN and rotation status
		var describeResponse = await _kmsClient.DescribeKeyAsync(
			new DescribeKeyRequest { KeyId = alias },
			cancellationToken).ConfigureAwait(false);

		var kmsKeyId = describeResponse.KeyMetadata.KeyId;
		var versions = new List<KeyMetadata>();

		// List key rotations to get version history
		// AWS KMS ListKeyRotations returns the rotation events for a key
		try
		{
			var rotationsRequest = new ListKeyRotationsRequest { KeyId = kmsKeyId };
			var rotationsResponse = await _kmsClient.ListKeyRotationsAsync(
				rotationsRequest,
				cancellationToken).ConfigureAwait(false);

			// The original key creation is version 1
			versions.Add(new KeyMetadata
			{
				KeyId = kmsKeyId,
				Version = 1,
				Status = KeyStatus.Active,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				CreatedAt = describeResponse.KeyMetadata.CreationDate is { } creationDate
					? new DateTimeOffset(creationDate)
					: DateTimeOffset.UtcNow,
			});

			// Each rotation event represents a new version
			var versionNumber = 2;
			foreach (var rotation in rotationsResponse.Rotations.OrderBy(static r => r.RotationDate))
			{
				versions.Add(new KeyMetadata
				{
					KeyId = kmsKeyId,
					Version = versionNumber++,
					Status = KeyStatus.DecryptOnly,
					Algorithm = EncryptionAlgorithm.Aes256Gcm,
					CreatedAt = rotation.RotationDate is { } rotationDate
						? new DateTimeOffset(rotationDate)
						: DateTimeOffset.UtcNow,
				});
			}

			// Mark the latest version as active
			if (versions.Count > 0)
			{
				var latest = versions[^1];
				versions[^1] = latest with { Status = KeyStatus.Active };

				// Mark all others as DecryptOnly
				for (var i = 0; i < versions.Count - 1; i++)
				{
					versions[i] = versions[i] with { Status = KeyStatus.DecryptOnly };
				}
			}
		}
		catch (AmazonKeyManagementServiceException ex) when (ex.ErrorCode == "UnsupportedOperationException")
		{
			// ListKeyRotations may not be supported for all key types
			// Fall back to just the current key info
			LogRotationHistoryUnavailable(kmsKeyId);

			versions.Add(new KeyMetadata
			{
				KeyId = kmsKeyId,
				Version = 1,
				Status = KeyStatus.Active,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				CreatedAt = describeResponse.KeyMetadata.CreationDate is { } fallbackCreationDate
					? new DateTimeOffset(fallbackCreationDate)
					: DateTimeOffset.UtcNow,
			});
		}

		return versions;
	}

	[LoggerMessage(LogLevel.Debug, "No key version found for key {KeyId} at timestamp {AsOf}")]
	private partial void LogNoVersionFoundForTimestamp(string keyId, DateTimeOffset asOf);

	[LoggerMessage(LogLevel.Debug, "Listed {Count} key versions for key {KeyId}")]
	private partial void LogKeyVersionsListed(string keyId, int count);

	[LoggerMessage(LogLevel.Warning, "Key {KeyId} not found when listing versions")]
	private partial void LogKeyNotFoundForVersionListing(string keyId);

	[LoggerMessage(LogLevel.Error, "Failed to list key versions for key {KeyId}")]
	private partial void LogFailedToListKeyVersions(string keyId, Exception ex);

	[LoggerMessage(LogLevel.Warning, "Key rotation history unavailable for key {KmsKeyId}, returning current version only")]
	private partial void LogRotationHistoryUnavailable(string kmsKeyId);
}
