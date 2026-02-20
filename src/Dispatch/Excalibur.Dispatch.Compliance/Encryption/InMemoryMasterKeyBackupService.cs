// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// In-memory implementation of <see cref="IMasterKeyBackupService"/> for testing and development.
/// </summary>
/// <remarks>
/// <para>
/// WARNING: This implementation stores data in memory only. All backups are lost when the
/// application restarts. Use only for testing and development purposes.
/// </para>
/// <para>
/// For production, use a persistent implementation backed by secure storage such as
/// an HSM, cloud KMS, or encrypted database.
/// </para>
/// </remarks>
public sealed partial class InMemoryMasterKeyBackupService : IMasterKeyBackupService
{
	private static readonly CompositeFormat KeyNotFoundFormat =
		CompositeFormat.Parse(Resources.InMemoryMasterKeyBackupService_KeyNotFound);

	private static readonly CompositeFormat KeyAlreadyExistsFormat =
		CompositeFormat.Parse(Resources.InMemoryMasterKeyBackupService_KeyAlreadyExists);

	private static readonly CompositeFormat ShareExpiredFormat =
		CompositeFormat.Parse(Resources.InMemoryMasterKeyBackupService_ShareExpired);

	private static readonly CompositeFormat InsufficientSharesFormat =
		CompositeFormat.Parse(Resources.InMemoryMasterKeyBackupService_InsufficientShares);

	private static readonly CompositeFormat UnsupportedFormatVersionFormat =
		CompositeFormat.Parse(Resources.InMemoryMasterKeyBackupService_UnsupportedFormatVersion);

	private readonly IKeyManagementProvider _keyManagementProvider;
	private readonly IEncryptionProvider _encryptionProvider;
	private readonly ILogger<InMemoryMasterKeyBackupService> _logger;

	private readonly ConcurrentDictionary<string, MasterKeyBackup> _backups = new();
	private readonly ConcurrentDictionary<string, List<BackupShare>> _shares = new();
	private readonly ConcurrentDictionary<string, byte[]> _keyMaterial = new(); // Simulated key store

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryMasterKeyBackupService"/> class.
	/// </summary>
	public InMemoryMasterKeyBackupService(
		IKeyManagementProvider keyManagementProvider,
		IEncryptionProvider encryptionProvider,
		ILogger<InMemoryMasterKeyBackupService> logger)
	{
		_keyManagementProvider = keyManagementProvider ?? throw new ArgumentNullException(nameof(keyManagementProvider));
		_encryptionProvider = encryptionProvider ?? throw new ArgumentNullException(nameof(encryptionProvider));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public async Task<MasterKeyBackup> ExportMasterKeyAsync(
		string keyId,
		MasterKeyExportOptions? options,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(keyId);

		options ??= new MasterKeyExportOptions();

		// Get key metadata
		var keyMetadata = await _keyManagementProvider.GetKeyAsync(keyId, cancellationToken).ConfigureAwait(false)
						  ?? throw new MasterKeyBackupException(
							  string.Format(
								  CultureInfo.InvariantCulture,
								  KeyNotFoundFormat,
								  keyId))
						  { KeyId = keyId, ErrorCode = MasterKeyBackupErrorCode.KeyNotFound };

		// Get or simulate key material
		if (!_keyMaterial.TryGetValue(keyId, out var keyMaterial))
		{
			// Generate simulated key material for testing
			keyMaterial = RandomNumberGenerator.GetBytes(32);
			_keyMaterial[keyId] = keyMaterial;
		}

		// Encrypt the key material using the default encryption key
		// Purpose is set for audit/logging, but we use KeyId for key selection
		var context = new EncryptionContext { KeyId = "default", Purpose = $"master-key-backup:{keyId}" };

		var encryptedData = await _encryptionProvider
			.EncryptAsync(keyMaterial, context, cancellationToken)
			.ConfigureAwait(false);

		var backupId = Guid.NewGuid().ToString("N");
		var createdAt = DateTimeOffset.UtcNow;
		var expiresAt = options.ExpiresIn.HasValue
			? createdAt.Add(options.ExpiresIn.Value)
			: (DateTimeOffset?)null;

		var backup = new MasterKeyBackup
		{
			BackupId = backupId,
			KeyId = keyId,
			KeyVersion = keyMetadata.Version,
			EncryptedKeyMaterial = encryptedData.Ciphertext,
			WrappingAlgorithm = options.WrappingAlgorithm,
			WrappingKeyId = encryptedData.KeyId,
			KeyAlgorithm = keyMetadata.Algorithm,
			Iv = encryptedData.Iv,
			AuthTag = encryptedData.AuthTag,
			KeyHash = Convert.ToHexString(SHA256.HashData(keyMaterial)),
			CreatedAt = createdAt,
			ExpiresAt = expiresAt,
			Purpose = keyMetadata.Purpose,
			Metadata = options.Metadata?.AsReadOnly()
		};

		_backups[keyId] = backup;

		LogMasterKeyExported(keyId, backupId);

		return backup;
	}

	/// <inheritdoc />
	public async Task<MasterKeyImportResult> ImportMasterKeyAsync(
		MasterKeyBackup backup,
		MasterKeyImportOptions? options,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(backup);

		options ??= new MasterKeyImportOptions();

		// Validate backup
		if (backup.IsExpired)
		{
			throw new MasterKeyBackupException(Resources.InMemoryMasterKeyBackupService_BackupExpired)
			{
				KeyId = backup.KeyId,
				BackupId = backup.BackupId,
				ErrorCode = MasterKeyBackupErrorCode.BackupExpired
			};
		}

		var keyId = options.NewKeyId ?? backup.KeyId;

		// Check if key already exists
		var existingKey = await _keyManagementProvider.GetKeyAsync(keyId, cancellationToken).ConfigureAwait(false);
		if (existingKey != null && !options.AllowOverwrite)
		{
			throw new MasterKeyBackupException(
				string.Format(
					CultureInfo.InvariantCulture,
					KeyAlreadyExistsFormat,
					keyId))
			{ KeyId = keyId, BackupId = backup.BackupId, ErrorCode = MasterKeyBackupErrorCode.KeyAlreadyExists };
		}

		// Decrypt the key material
		var encryptedData = new EncryptedData
		{
			Ciphertext = backup.EncryptedKeyMaterial,
			Iv = backup.Iv ?? [],
			AuthTag = backup.AuthTag ?? [],
			Algorithm = backup.WrappingAlgorithm,
			KeyId = backup.WrappingKeyId ?? string.Empty,
			KeyVersion = 1
		};

		// Use KeyId from the encrypted data, or default if not set
		var context = new EncryptionContext { KeyId = encryptedData.KeyId ?? "default", Purpose = $"master-key-backup:{backup.KeyId}" };

		byte[] decryptedKeyMaterial;
		try
		{
			decryptedKeyMaterial = await _encryptionProvider
				.DecryptAsync(encryptedData, context, cancellationToken)
				.ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			throw new MasterKeyBackupException(Resources.InMemoryMasterKeyBackupService_DecryptBackupFailed, ex)
			{
				KeyId = keyId,
				BackupId = backup.BackupId,
				ErrorCode = MasterKeyBackupErrorCode.CryptographicError
			};
		}

		// Verify key hash
		if (options.VerifyKeyHash)
		{
			var computedHash = Convert.ToHexString(SHA256.HashData(decryptedKeyMaterial));
			if (!string.Equals(computedHash, backup.KeyHash, StringComparison.OrdinalIgnoreCase))
			{
				throw new MasterKeyBackupException(Resources.InMemoryMasterKeyBackupService_KeyHashVerificationFailed)
				{
					KeyId = keyId,
					BackupId = backup.BackupId,
					ErrorCode = MasterKeyBackupErrorCode.HashVerificationFailed
				};
			}
		}

		// Store the key material
		_keyMaterial[keyId] = decryptedKeyMaterial;

		// Create the key in the key management provider if it doesn't exist
		if (existingKey == null || options.AllowOverwrite)
		{
			_ = await _keyManagementProvider.RotateKeyAsync(
				keyId,
				backup.KeyAlgorithm,
				backup.Purpose,
				expiresAt: null,
				cancellationToken: cancellationToken).ConfigureAwait(false);
		}

		var keyMetadata = await _keyManagementProvider.GetKeyAsync(keyId, cancellationToken).ConfigureAwait(false);

		LogMasterKeyImported(keyId, backup.BackupId);

		return new MasterKeyImportResult
		{
			Success = true,
			KeyId = keyId,
			KeyVersion = keyMetadata?.Version ?? backup.KeyVersion,
			KeyMetadata = keyMetadata,
			ImportedAt = DateTimeOffset.UtcNow,
			WasOverwritten = existingKey != null
		};
	}

	/// <inheritdoc />
	public async Task<BackupShare[]> GenerateRecoverySplitAsync(
		string keyId,
		int totalShares,
		int threshold,
		BackupShareOptions? options,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(keyId);

		if (totalShares < 2)
		{
			throw new ArgumentOutOfRangeException(
				nameof(totalShares),
				Resources.InMemoryMasterKeyBackupService_TotalSharesTooLow);
		}

		if (threshold < 2)
		{
			throw new ArgumentOutOfRangeException(
				nameof(threshold),
				Resources.InMemoryMasterKeyBackupService_ThresholdTooLow);
		}

		if (threshold > totalShares)
		{
			throw new ArgumentOutOfRangeException(
				nameof(threshold),
				Resources.InMemoryMasterKeyBackupService_ThresholdExceedsTotalShares);
		}

		options ??= new BackupShareOptions();

		if (options.CustodianIds != null && options.CustodianIds.Count != totalShares)
		{
			throw new ArgumentException(
				Resources.InMemoryMasterKeyBackupService_CustodianIdsCountMismatch,
				nameof(options));
		}

		// Get key material
		if (!_keyMaterial.TryGetValue(keyId, out var keyMaterial))
		{
			throw new MasterKeyBackupException(
				string.Format(
					CultureInfo.InvariantCulture,
					KeyNotFoundFormat,
					keyId))
			{ KeyId = keyId, ErrorCode = MasterKeyBackupErrorCode.KeyNotFound };
		}

		// Get key metadata
		var keyMetadata = await _keyManagementProvider.GetKeyAsync(keyId, cancellationToken).ConfigureAwait(false);

		// Split using Shamir's Secret Sharing
		var shamirShares = ShamirSecretSharing.Split(keyMaterial, totalShares, threshold);
		var keyHash = Convert.ToHexString(SHA256.HashData(keyMaterial));

		var createdAt = DateTimeOffset.UtcNow;
		var expiresAt = options.ExpiresIn.HasValue
			? createdAt.Add(options.ExpiresIn.Value)
			: (DateTimeOffset?)null;

		var backupShares = new BackupShare[totalShares];

		for (var i = 0; i < totalShares; i++)
		{
			backupShares[i] = new BackupShare
			{
				ShareId = Guid.NewGuid().ToString("N"),
				KeyId = keyId,
				KeyVersion = keyMetadata?.Version ?? 1,
				ShareIndex = i + 1, // 1-based
				ShareData = shamirShares[i],
				TotalShares = totalShares,
				Threshold = threshold,
				CreatedAt = createdAt,
				ExpiresAt = expiresAt,
				CustodianId = options.CustodianIds?[i],
				KeyHash = keyHash
			};
		}

		// Store shares for tracking
		_shares[keyId] = [.. backupShares];

		LogRecoverySharesGenerated(totalShares, keyId, threshold);

		return backupShares;
	}

	/// <inheritdoc />
	public Task<MasterKeyImportResult> ReconstructFromSharesAsync(
		BackupShare[] shares,
		MasterKeyImportOptions? options,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(shares);

		if (shares.Length == 0)
		{
			throw new ArgumentException(
				Resources.InMemoryMasterKeyBackupService_AtLeastOneShareRequired,
				nameof(shares));
		}

		options ??= new MasterKeyImportOptions();

		var firstShare = shares[0];

		// Validate shares are from the same key
		foreach (var share in shares.Skip(1))
		{
			if (share.KeyId != firstShare.KeyId)
			{
				throw new MasterKeyBackupException(
					Resources.InMemoryMasterKeyBackupService_SharesKeyMismatch)
				{
					KeyId = firstShare.KeyId,
					ErrorCode = MasterKeyBackupErrorCode.ShareMismatch
				};
			}

			if (share.KeyVersion != firstShare.KeyVersion)
			{
				throw new MasterKeyBackupException(
					Resources.InMemoryMasterKeyBackupService_SharesKeyVersionMismatch)
				{
					KeyId = firstShare.KeyId,
					ErrorCode = MasterKeyBackupErrorCode.ShareMismatch
				};
			}
		}

		// Check threshold
		if (shares.Length < firstShare.Threshold)
		{
			throw new MasterKeyBackupException(
				string.Format(
					CultureInfo.InvariantCulture,
					InsufficientSharesFormat,
					shares.Length,
					firstShare.Threshold))
			{ KeyId = firstShare.KeyId, ErrorCode = MasterKeyBackupErrorCode.InsufficientShares };
		}

		// Check for expired shares
		foreach (var share in shares)
		{
			if (share.IsExpired)
			{
				throw new MasterKeyBackupException(
					string.Format(
						CultureInfo.InvariantCulture,
						ShareExpiredFormat,
						share.ShareId))
				{ KeyId = firstShare.KeyId, ErrorCode = MasterKeyBackupErrorCode.BackupExpired };
			}
		}

		// Extract share data for reconstruction
		var shareData = shares
			.Select(s => s.ShareData)
			.ToArray();

		// Reconstruct using Shamir's Secret Sharing
		byte[] reconstructedKey;
		try
		{
			reconstructedKey = ShamirSecretSharing.Reconstruct(shareData);
		}
		catch (Exception ex)
		{
			throw new MasterKeyBackupException(
				Resources.InMemoryMasterKeyBackupService_ReconstructionFailed,
				ex)
			{ KeyId = firstShare.KeyId, ErrorCode = MasterKeyBackupErrorCode.CryptographicError };
		}

		// Verify key hash
		if (options.VerifyKeyHash)
		{
			var computedHash = Convert.ToHexString(SHA256.HashData(reconstructedKey));
			if (!string.Equals(computedHash, firstShare.KeyHash, StringComparison.OrdinalIgnoreCase))
			{
				throw new MasterKeyBackupException(
					Resources.InMemoryMasterKeyBackupService_KeyHashVerificationFailedAfterReconstruction)
				{
					KeyId = firstShare.KeyId,
					ErrorCode = MasterKeyBackupErrorCode.HashVerificationFailed
				};
			}
		}

		var keyId = options.NewKeyId ?? firstShare.KeyId;

		// Store the reconstructed key
		_keyMaterial[keyId] = reconstructedKey;

		LogMasterKeyReconstructed(keyId, shares.Length);

		return Task.FromResult(new MasterKeyImportResult
		{
			Success = true,
			KeyId = keyId,
			KeyVersion = firstShare.KeyVersion,
			ImportedAt = DateTimeOffset.UtcNow,
			WasOverwritten = false
		});
	}

	/// <inheritdoc />
	public Task<BackupVerificationResult> VerifyBackupAsync(
		MasterKeyBackup backup,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(backup);

		var errors = new List<string>();
		var warnings = new List<string>();

		var isExpired = backup.IsExpired;
		if (isExpired)
		{
			errors.Add(Resources.InMemoryMasterKeyBackupService_BackupExpired);
		}

		var formatSupported = backup.FormatVersion <= 1;
		if (!formatSupported)
		{
			errors.Add(string.Format(
				CultureInfo.InvariantCulture,
				UnsupportedFormatVersionFormat,
				backup.FormatVersion));
		}

		var integrityCheckPassed = !string.IsNullOrEmpty(backup.KeyHash) &&
								   backup.EncryptedKeyMaterial.Length > 0;

		if (!integrityCheckPassed)
		{
			errors.Add(Resources.InMemoryMasterKeyBackupService_BackupIntegrityCheckFailed);
		}

		// Check for expiration warning
		if (backup.ExpiresAt.HasValue &&
			backup.ExpiresAt.Value <= DateTimeOffset.UtcNow.AddDays(30))
		{
			warnings.Add(Resources.InMemoryMasterKeyBackupService_BackupExpiresSoon);
		}

		return Task.FromResult(new BackupVerificationResult
		{
			IsValid = errors.Count == 0,
			KeyId = backup.KeyId,
			KeyVersion = backup.KeyVersion,
			IsExpired = isExpired,
			FormatSupported = formatSupported,
			IntegrityCheckPassed = integrityCheckPassed,
			Warnings = warnings.Count > 0 ? warnings : null,
			Errors = errors.Count > 0 ? errors : null,
			BackupCreatedAt = backup.CreatedAt,
			BackupExpiresAt = backup.ExpiresAt
		});
	}

	/// <inheritdoc />
	public async Task<MasterKeyBackupStatus?> GetBackupStatusAsync(
		string keyId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(keyId);

		var hasBackup = _backups.TryGetValue(keyId, out var backup);
		var hasShares = _shares.TryGetValue(keyId, out var shares);

		if (!hasBackup && !hasShares)
		{
			return null;
		}

		var warnings = new List<string>();

		// Check if backup is expiring soon
		if (backup?.ExpiresAt != null && backup.ExpiresAt.Value <= DateTimeOffset.UtcNow.AddDays(30))
		{
			warnings.Add(Resources.InMemoryMasterKeyBackupService_BackupExpiresSoon);
		}

		// Count active (non-expired) shares
		var activeShareCount = shares?.Count(s => !s.IsExpired) ?? 0;
		var threshold = shares?.FirstOrDefault()?.Threshold;

		var isAtRisk = backup?.IsExpired == true ||
					   (hasShares && activeShareCount < (threshold ?? 0));

		if (isAtRisk)
		{
			warnings.Add(Resources.InMemoryMasterKeyBackupService_BackupAtRisk);
		}

		var keyMetadata = await _keyManagementProvider.GetKeyAsync(keyId, cancellationToken).ConfigureAwait(false);

		return new MasterKeyBackupStatus
		{
			KeyId = keyId,
			CurrentVersion = keyMetadata?.Version ?? 1,
			HasBackup = hasBackup,
			LastBackupAt = backup?.CreatedAt,
			BackupExpiresAt = backup?.ExpiresAt,
			ActiveShareCount = activeShareCount,
			ShareThreshold = threshold,
			IsAtRisk = isAtRisk,
			Warnings = warnings.Count > 0 ? warnings : null
		};
	}

	/// <summary>
	/// Registers simulated key material for testing purposes.
	/// </summary>
	/// <param name="keyId">The key identifier.</param>
	/// <param name="keyMaterial">The key material bytes.</param>
	/// <remarks>
	/// This method is for testing only to allow injection of known key material.
	/// </remarks>
	public void RegisterKeyMaterial(string keyId, byte[] keyMaterial)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(keyId);
		ArgumentNullException.ThrowIfNull(keyMaterial);

		_keyMaterial[keyId] = keyMaterial;
	}

	[LoggerMessage(LogLevel.Information, "Exported master key {KeyId} to backup {BackupId}")]
	private partial void LogMasterKeyExported(string keyId, string backupId);

	[LoggerMessage(LogLevel.Information, "Imported master key {KeyId} from backup {BackupId}")]
	private partial void LogMasterKeyImported(string keyId, string backupId);

	[LoggerMessage(LogLevel.Information, "Generated {TotalShares} recovery shares for key {KeyId} with threshold {Threshold}")]
	private partial void LogRecoverySharesGenerated(int totalShares, string keyId, int threshold);

	[LoggerMessage(LogLevel.Information, "Reconstructed master key {KeyId} from {ShareCount} shares")]
	private partial void LogMasterKeyReconstructed(string keyId, int shareCount);
}
