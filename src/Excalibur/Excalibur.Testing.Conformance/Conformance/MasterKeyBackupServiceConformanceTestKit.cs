// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Compliance;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Conformance test kit for <see cref="IMasterKeyBackupService"/> implementations.
/// </summary>
/// <remarks>
/// <para>
/// This kit verifies that implementations correctly handle master key backup and disaster recovery
/// operations, including direct backup/import and Shamir's Secret Sharing for split-knowledge recovery.
/// </para>
/// <para>
/// <strong>COMPLIANCE-CRITICAL:</strong> IMasterKeyBackupService implements secure master key backup
/// patterns for regulatory compliance with disaster recovery requirements.
/// </para>
/// <para>
/// <strong>BACKUP-RECOVERY PATTERN:</strong>
/// <list type="number">
/// <item><description>ExportMasterKeyAsync - Create encrypted backup</description></item>
/// <item><description>VerifyBackupAsync - Validate backup integrity without import</description></item>
/// <item><description>ImportMasterKeyAsync - Restore from backup</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>SECRET SHARING PATTERN:</strong>
/// <list type="number">
/// <item><description>GenerateRecoverySplitAsync - Create N shares with threshold K</description></item>
/// <item><description>ReconstructFromSharesAsync - Reconstruct with >= K shares</description></item>
/// </list>
/// </para>
/// <para>
/// To use this kit:
/// <list type="number">
/// <item><description>Create a derived class for your implementation</description></item>
/// <item><description>Override <see cref="CreateService"/> to return your implementation</description></item>
/// <item><description>Override <see cref="RegisterTestKeyAsync"/> to set up test keys</description></item>
/// <item><description>Add [Fact] test methods that call the protected test methods</description></item>
/// </list>
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
	Justification = "Test method naming convention")]
public abstract class MasterKeyBackupServiceConformanceTestKit
{
	/// <summary>
	/// Creates a new instance of the service under test.
	/// </summary>
	/// <returns>A new service instance.</returns>
	protected abstract IMasterKeyBackupService CreateService();

	/// <summary>
	/// Registers a test key with known material for backup operations.
	/// </summary>
	/// <param name="service">The service instance.</param>
	/// <param name="keyId">The key identifier to register.</param>
	/// <param name="keyMaterial">The key material bytes.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the async operation.</returns>
	/// <remarks>
	/// Implementations must register the key so that ExportMasterKeyAsync can find it.
	/// For InMemoryMasterKeyBackupService, call RegisterKeyMaterial.
	/// </remarks>
	protected abstract Task RegisterTestKeyAsync(
		IMasterKeyBackupService service,
		string keyId,
		byte[] keyMaterial,
		CancellationToken cancellationToken);

	#region ExportMasterKeyAsync Tests

	/// <summary>
	/// Tests that ExportMasterKeyAsync throws ArgumentException for null keyId.
	/// </summary>
	protected virtual async Task ExportMasterKeyAsync_NullKeyId_ShouldThrowArgumentException()
	{
		// Arrange
		var service = CreateService();

		// Act & Assert
		try
		{
			_ = await service.ExportMasterKeyAsync(null!, null, CancellationToken.None).ConfigureAwait(false);
			throw new TestFixtureAssertionException("Expected ArgumentException was not thrown");
		}
		catch (ArgumentException)
		{
			// Expected
		}
	}

	/// <summary>
	/// Tests that ExportMasterKeyAsync throws MasterKeyBackupException for non-existent key.
	/// </summary>
	protected virtual async Task ExportMasterKeyAsync_NonExistentKey_ShouldThrowMasterKeyBackupException()
	{
		// Arrange
		var service = CreateService();
		var keyId = $"non-existent-{Guid.NewGuid():N}";

		// Act & Assert
		try
		{
			_ = await service.ExportMasterKeyAsync(keyId, null, CancellationToken.None).ConfigureAwait(false);
			throw new TestFixtureAssertionException("Expected MasterKeyBackupException was not thrown");
		}
		catch (MasterKeyBackupException ex)
		{
			if (ex.ErrorCode != MasterKeyBackupErrorCode.KeyNotFound)
			{
				throw new TestFixtureAssertionException(
					$"Expected ErrorCode KeyNotFound but got {ex.ErrorCode}");
			}
		}
	}

	/// <summary>
	/// Tests that ExportMasterKeyAsync returns a valid backup for an existing key.
	/// </summary>
	protected virtual async Task ExportMasterKeyAsync_ValidKey_ShouldReturnBackup()
	{
		// Arrange
		var service = CreateService();
		var keyId = $"test-key-{Guid.NewGuid():N}";
		var keyMaterial = new byte[32];
		System.Security.Cryptography.RandomNumberGenerator.Fill(keyMaterial);

		await RegisterTestKeyAsync(service, keyId, keyMaterial, CancellationToken.None).ConfigureAwait(false);

		// Act
		var backup = await service.ExportMasterKeyAsync(keyId, null, CancellationToken.None).ConfigureAwait(false);

		// Assert
		ArgumentNullException.ThrowIfNull(backup);

		if (string.IsNullOrEmpty(backup.BackupId))
		{
			throw new TestFixtureAssertionException("BackupId should not be null or empty");
		}

		if (backup.KeyId != keyId)
		{
			throw new TestFixtureAssertionException(
				$"Expected KeyId '{keyId}' but got '{backup.KeyId}'");
		}

		if (string.IsNullOrEmpty(backup.KeyHash))
		{
			throw new TestFixtureAssertionException("KeyHash should not be null or empty");
		}

		if (backup.EncryptedKeyMaterial == null || backup.EncryptedKeyMaterial.Length == 0)
		{
			throw new TestFixtureAssertionException("EncryptedKeyMaterial should not be null or empty");
		}
	}

	#endregion

	#region ImportMasterKeyAsync Tests

	/// <summary>
	/// Tests that ImportMasterKeyAsync throws ArgumentNullException for null backup.
	/// </summary>
	protected virtual async Task ImportMasterKeyAsync_NullBackup_ShouldThrowArgumentNullException()
	{
		// Arrange
		var service = CreateService();

		// Act & Assert
		try
		{
			_ = await service.ImportMasterKeyAsync(null!, null, CancellationToken.None).ConfigureAwait(false);
			throw new TestFixtureAssertionException("Expected ArgumentNullException was not thrown");
		}
		catch (ArgumentNullException)
		{
			// Expected
		}
	}

	/// <summary>
	/// Tests that ImportMasterKeyAsync throws MasterKeyBackupException for expired backup.
	/// </summary>
	protected virtual async Task ImportMasterKeyAsync_ExpiredBackup_ShouldThrowMasterKeyBackupException()
	{
		// Arrange
		var service = CreateService();
		var backup = new MasterKeyBackup
		{
			BackupId = Guid.NewGuid().ToString("N"),
			KeyId = "test-key",
			KeyVersion = 1,
			EncryptedKeyMaterial = new byte[32],
			WrappingAlgorithm = EncryptionAlgorithm.Aes256Gcm,
			KeyAlgorithm = EncryptionAlgorithm.Aes256Gcm,
			KeyHash = "test-hash",
			CreatedAt = DateTimeOffset.UtcNow.AddDays(-10),
			ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1) // Expired
		};

		// Act & Assert
		try
		{
			_ = await service.ImportMasterKeyAsync(backup, null, CancellationToken.None).ConfigureAwait(false);
			throw new TestFixtureAssertionException("Expected MasterKeyBackupException was not thrown");
		}
		catch (MasterKeyBackupException ex)
		{
			if (ex.ErrorCode != MasterKeyBackupErrorCode.BackupExpired)
			{
				throw new TestFixtureAssertionException(
					$"Expected ErrorCode BackupExpired but got {ex.ErrorCode}");
			}
		}
	}

	/// <summary>
	/// Tests that ImportMasterKeyAsync successfully imports a valid backup.
	/// </summary>
	protected virtual async Task ImportMasterKeyAsync_ValidBackup_ShouldSucceed()
	{
		// Arrange
		var service = CreateService();
		var keyId = $"test-key-{Guid.NewGuid():N}";
		var keyMaterial = new byte[32];
		System.Security.Cryptography.RandomNumberGenerator.Fill(keyMaterial);

		await RegisterTestKeyAsync(service, keyId, keyMaterial, CancellationToken.None).ConfigureAwait(false);

		var backup = await service.ExportMasterKeyAsync(keyId, null, CancellationToken.None).ConfigureAwait(false);

		// Import with new key ID
		var newKeyId = $"imported-{Guid.NewGuid():N}";
		var options = new MasterKeyImportOptions { NewKeyId = newKeyId, VerifyKeyHash = true };

		// Act
		var result = await service.ImportMasterKeyAsync(backup, options, CancellationToken.None).ConfigureAwait(false);

		// Assert
		if (!result.Success)
		{
			throw new TestFixtureAssertionException("Import should succeed");
		}

		if (result.KeyId != newKeyId)
		{
			throw new TestFixtureAssertionException(
				$"Expected KeyId '{newKeyId}' but got '{result.KeyId}'");
		}
	}

	/// <summary>
	/// Tests that ImportMasterKeyAsync throws MasterKeyBackupException when key exists and AllowOverwrite is false.
	/// </summary>
	protected virtual async Task ImportMasterKeyAsync_KeyExists_ShouldThrowMasterKeyBackupException()
	{
		// Arrange
		var service = CreateService();
		var keyId = $"test-key-{Guid.NewGuid():N}";
		var keyMaterial = new byte[32];
		System.Security.Cryptography.RandomNumberGenerator.Fill(keyMaterial);

		await RegisterTestKeyAsync(service, keyId, keyMaterial, CancellationToken.None).ConfigureAwait(false);

		var backup = await service.ExportMasterKeyAsync(keyId, null, CancellationToken.None).ConfigureAwait(false);

		// Try to import with same key ID without AllowOverwrite
		var options = new MasterKeyImportOptions { AllowOverwrite = false };

		// Act & Assert
		try
		{
			_ = await service.ImportMasterKeyAsync(backup, options, CancellationToken.None).ConfigureAwait(false);
			throw new TestFixtureAssertionException("Expected MasterKeyBackupException was not thrown");
		}
		catch (MasterKeyBackupException ex)
		{
			if (ex.ErrorCode != MasterKeyBackupErrorCode.KeyAlreadyExists)
			{
				throw new TestFixtureAssertionException(
					$"Expected ErrorCode KeyAlreadyExists but got {ex.ErrorCode}");
			}
		}
	}

	#endregion

	#region GenerateRecoverySplitAsync Tests

	/// <summary>
	/// Tests that GenerateRecoverySplitAsync throws ArgumentException for null keyId.
	/// </summary>
	protected virtual async Task GenerateRecoverySplitAsync_NullKeyId_ShouldThrowArgumentException()
	{
		// Arrange
		var service = CreateService();

		// Act & Assert
		try
		{
			_ = await service.GenerateRecoverySplitAsync(null!, 5, 3, null, CancellationToken.None).ConfigureAwait(false);
			throw new TestFixtureAssertionException("Expected ArgumentException was not thrown");
		}
		catch (ArgumentException)
		{
			// Expected
		}
	}

	/// <summary>
	/// Tests that GenerateRecoverySplitAsync throws ArgumentOutOfRangeException for threshold less than 2.
	/// </summary>
	protected virtual async Task GenerateRecoverySplitAsync_ThresholdLessThan2_ShouldThrowArgumentOutOfRangeException()
	{
		// Arrange
		var service = CreateService();
		var keyId = $"test-key-{Guid.NewGuid():N}";
		var keyMaterial = new byte[32];
		System.Security.Cryptography.RandomNumberGenerator.Fill(keyMaterial);

		await RegisterTestKeyAsync(service, keyId, keyMaterial, CancellationToken.None).ConfigureAwait(false);

		// Act & Assert
		try
		{
			_ = await service.GenerateRecoverySplitAsync(keyId, 5, 1, null, CancellationToken.None).ConfigureAwait(false);
			throw new TestFixtureAssertionException("Expected ArgumentOutOfRangeException was not thrown");
		}
		catch (ArgumentOutOfRangeException)
		{
			// Expected
		}
	}

	/// <summary>
	/// Tests that GenerateRecoverySplitAsync throws ArgumentOutOfRangeException for totalShares less than 2.
	/// </summary>
	protected virtual async Task GenerateRecoverySplitAsync_TotalSharesLessThan2_ShouldThrowArgumentOutOfRangeException()
	{
		// Arrange
		var service = CreateService();
		var keyId = $"test-key-{Guid.NewGuid():N}";
		var keyMaterial = new byte[32];
		System.Security.Cryptography.RandomNumberGenerator.Fill(keyMaterial);

		await RegisterTestKeyAsync(service, keyId, keyMaterial, CancellationToken.None).ConfigureAwait(false);

		// Act & Assert
		try
		{
			_ = await service.GenerateRecoverySplitAsync(keyId, 1, 1, null, CancellationToken.None).ConfigureAwait(false);
			throw new TestFixtureAssertionException("Expected ArgumentOutOfRangeException was not thrown");
		}
		catch (ArgumentOutOfRangeException)
		{
			// Expected
		}
	}

	/// <summary>
	/// Tests that GenerateRecoverySplitAsync throws ArgumentOutOfRangeException when threshold exceeds totalShares.
	/// </summary>
	protected virtual async Task GenerateRecoverySplitAsync_ThresholdExceedsTotalShares_ShouldThrowArgumentOutOfRangeException()
	{
		// Arrange
		var service = CreateService();
		var keyId = $"test-key-{Guid.NewGuid():N}";
		var keyMaterial = new byte[32];
		System.Security.Cryptography.RandomNumberGenerator.Fill(keyMaterial);

		await RegisterTestKeyAsync(service, keyId, keyMaterial, CancellationToken.None).ConfigureAwait(false);

		// Act & Assert
		try
		{
			_ = await service.GenerateRecoverySplitAsync(keyId, 3, 5, null, CancellationToken.None).ConfigureAwait(false);
			throw new TestFixtureAssertionException("Expected ArgumentOutOfRangeException was not thrown");
		}
		catch (ArgumentOutOfRangeException)
		{
			// Expected
		}
	}

	/// <summary>
	/// Tests that GenerateRecoverySplitAsync generates the correct number of shares.
	/// </summary>
	protected virtual async Task GenerateRecoverySplitAsync_ValidParams_ShouldGenerateCorrectCount()
	{
		// Arrange
		var service = CreateService();
		var keyId = $"test-key-{Guid.NewGuid():N}";
		var keyMaterial = new byte[32];
		System.Security.Cryptography.RandomNumberGenerator.Fill(keyMaterial);

		await RegisterTestKeyAsync(service, keyId, keyMaterial, CancellationToken.None).ConfigureAwait(false);

		const int totalShares = 5;
		const int threshold = 3;

		// Act
		var shares = await service.GenerateRecoverySplitAsync(keyId, totalShares, threshold, null, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		ArgumentNullException.ThrowIfNull(shares);

		if (shares.Length != totalShares)
		{
			throw new TestFixtureAssertionException(
				$"Expected {totalShares} shares but got {shares.Length}");
		}

		// Verify each share has correct metadata
		foreach (var share in shares)
		{
			if (share.KeyId != keyId)
			{
				throw new TestFixtureAssertionException(
					$"Expected KeyId '{keyId}' but got '{share.KeyId}'");
			}

			if (share.Threshold != threshold)
			{
				throw new TestFixtureAssertionException(
					$"Expected Threshold {threshold} but got {share.Threshold}");
			}

			if (share.TotalShares != totalShares)
			{
				throw new TestFixtureAssertionException(
					$"Expected TotalShares {totalShares} but got {share.TotalShares}");
			}
		}
	}

	#endregion

	#region ReconstructFromSharesAsync Tests

	/// <summary>
	/// Tests that ReconstructFromSharesAsync throws ArgumentNullException for null shares.
	/// </summary>
	protected virtual async Task ReconstructFromSharesAsync_NullShares_ShouldThrowArgumentNullException()
	{
		// Arrange
		var service = CreateService();

		// Act & Assert
		try
		{
			_ = await service.ReconstructFromSharesAsync(null!, null, CancellationToken.None).ConfigureAwait(false);
			throw new TestFixtureAssertionException("Expected ArgumentNullException was not thrown");
		}
		catch (ArgumentNullException)
		{
			// Expected
		}
	}

	/// <summary>
	/// Tests that ReconstructFromSharesAsync throws ArgumentException for empty shares array.
	/// </summary>
	protected virtual async Task ReconstructFromSharesAsync_EmptyShares_ShouldThrowArgumentException()
	{
		// Arrange
		var service = CreateService();

		// Act & Assert
		try
		{
			_ = await service.ReconstructFromSharesAsync([], null, CancellationToken.None).ConfigureAwait(false);
			throw new TestFixtureAssertionException("Expected ArgumentException was not thrown");
		}
		catch (ArgumentException)
		{
			// Expected
		}
	}

	/// <summary>
	/// Tests that ReconstructFromSharesAsync throws MasterKeyBackupException for insufficient shares.
	/// </summary>
	protected virtual async Task ReconstructFromSharesAsync_InsufficientShares_ShouldThrowMasterKeyBackupException()
	{
		// Arrange
		var service = CreateService();
		var keyId = $"test-key-{Guid.NewGuid():N}";
		var keyMaterial = new byte[32];
		System.Security.Cryptography.RandomNumberGenerator.Fill(keyMaterial);

		await RegisterTestKeyAsync(service, keyId, keyMaterial, CancellationToken.None).ConfigureAwait(false);

		var shares = await service.GenerateRecoverySplitAsync(keyId, 5, 3, null, CancellationToken.None)
			.ConfigureAwait(false);

		// Take only 2 shares when threshold is 3
		var insufficientShares = shares.Take(2).ToArray();

		// Act & Assert
		try
		{
			_ = await service.ReconstructFromSharesAsync(insufficientShares, null, CancellationToken.None)
				.ConfigureAwait(false);
			throw new TestFixtureAssertionException("Expected MasterKeyBackupException was not thrown");
		}
		catch (MasterKeyBackupException ex)
		{
			if (ex.ErrorCode != MasterKeyBackupErrorCode.InsufficientShares)
			{
				throw new TestFixtureAssertionException(
					$"Expected ErrorCode InsufficientShares but got {ex.ErrorCode}");
			}
		}
	}

	/// <summary>
	/// Tests that ReconstructFromSharesAsync successfully reconstructs the key with sufficient shares.
	/// </summary>
	protected virtual async Task ReconstructFromSharesAsync_ValidShares_ShouldReconstruct()
	{
		// Arrange
		var service = CreateService();
		var keyId = $"test-key-{Guid.NewGuid():N}";
		var keyMaterial = new byte[32];
		System.Security.Cryptography.RandomNumberGenerator.Fill(keyMaterial);

		await RegisterTestKeyAsync(service, keyId, keyMaterial, CancellationToken.None).ConfigureAwait(false);

		var shares = await service.GenerateRecoverySplitAsync(keyId, 5, 3, null, CancellationToken.None)
			.ConfigureAwait(false);

		// Take exactly threshold shares
		var thresholdShares = shares.Take(3).ToArray();
		var newKeyId = $"reconstructed-{Guid.NewGuid():N}";
		var options = new MasterKeyImportOptions { NewKeyId = newKeyId };

		// Act
		var result = await service.ReconstructFromSharesAsync(thresholdShares, options, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		if (!result.Success)
		{
			throw new TestFixtureAssertionException("Reconstruction should succeed");
		}

		if (result.KeyId != newKeyId)
		{
			throw new TestFixtureAssertionException(
				$"Expected KeyId '{newKeyId}' but got '{result.KeyId}'");
		}
	}

	/// <summary>
	/// Tests that ReconstructFromSharesAsync throws MasterKeyBackupException for mismatched shares.
	/// </summary>
	protected virtual async Task ReconstructFromSharesAsync_MismatchedShares_ShouldThrowMasterKeyBackupException()
	{
		// Arrange
		var service = CreateService();
		var keyId1 = $"test-key-1-{Guid.NewGuid():N}";
		var keyId2 = $"test-key-2-{Guid.NewGuid():N}";
		var keyMaterial1 = new byte[32];
		var keyMaterial2 = new byte[32];
		System.Security.Cryptography.RandomNumberGenerator.Fill(keyMaterial1);
		System.Security.Cryptography.RandomNumberGenerator.Fill(keyMaterial2);

		await RegisterTestKeyAsync(service, keyId1, keyMaterial1, CancellationToken.None).ConfigureAwait(false);
		await RegisterTestKeyAsync(service, keyId2, keyMaterial2, CancellationToken.None).ConfigureAwait(false);

		var shares1 = await service.GenerateRecoverySplitAsync(keyId1, 5, 3, null, CancellationToken.None)
			.ConfigureAwait(false);
		var shares2 = await service.GenerateRecoverySplitAsync(keyId2, 5, 3, null, CancellationToken.None)
			.ConfigureAwait(false);

		// Mix shares from different keys
		var mixedShares = new[] { shares1[0], shares1[1], shares2[0] };

		// Act & Assert
		try
		{
			_ = await service.ReconstructFromSharesAsync(mixedShares, null, CancellationToken.None)
				.ConfigureAwait(false);
			throw new TestFixtureAssertionException("Expected MasterKeyBackupException was not thrown");
		}
		catch (MasterKeyBackupException ex)
		{
			if (ex.ErrorCode != MasterKeyBackupErrorCode.ShareMismatch)
			{
				throw new TestFixtureAssertionException(
					$"Expected ErrorCode ShareMismatch but got {ex.ErrorCode}");
			}
		}
	}

	#endregion

	#region VerifyBackupAsync Tests

	/// <summary>
	/// Tests that VerifyBackupAsync throws ArgumentNullException for null backup.
	/// </summary>
	protected virtual async Task VerifyBackupAsync_NullBackup_ShouldThrowArgumentNullException()
	{
		// Arrange
		var service = CreateService();

		// Act & Assert
		try
		{
			_ = await service.VerifyBackupAsync(null!, CancellationToken.None).ConfigureAwait(false);
			throw new TestFixtureAssertionException("Expected ArgumentNullException was not thrown");
		}
		catch (ArgumentNullException)
		{
			// Expected
		}
	}

	/// <summary>
	/// Tests that VerifyBackupAsync returns IsValid=false for expired backup.
	/// </summary>
	protected virtual async Task VerifyBackupAsync_ExpiredBackup_ShouldReturnInvalid()
	{
		// Arrange
		var service = CreateService();
		var backup = new MasterKeyBackup
		{
			BackupId = Guid.NewGuid().ToString("N"),
			KeyId = "test-key",
			KeyVersion = 1,
			EncryptedKeyMaterial = new byte[32],
			WrappingAlgorithm = EncryptionAlgorithm.Aes256Gcm,
			KeyAlgorithm = EncryptionAlgorithm.Aes256Gcm,
			KeyHash = "test-hash",
			CreatedAt = DateTimeOffset.UtcNow.AddDays(-10),
			ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1) // Expired
		};

		// Act
		var result = await service.VerifyBackupAsync(backup, CancellationToken.None).ConfigureAwait(false);

		// Assert
		if (result.IsValid)
		{
			throw new TestFixtureAssertionException("Expired backup should not be valid");
		}

		if (!result.IsExpired)
		{
			throw new TestFixtureAssertionException("IsExpired should be true for expired backup");
		}
	}

	/// <summary>
	/// Tests that VerifyBackupAsync returns IsValid=true for a valid backup.
	/// </summary>
	protected virtual async Task VerifyBackupAsync_ValidBackup_ShouldReturnValid()
	{
		// Arrange
		var service = CreateService();
		var keyId = $"test-key-{Guid.NewGuid():N}";
		var keyMaterial = new byte[32];
		System.Security.Cryptography.RandomNumberGenerator.Fill(keyMaterial);

		await RegisterTestKeyAsync(service, keyId, keyMaterial, CancellationToken.None).ConfigureAwait(false);

		var backup = await service.ExportMasterKeyAsync(keyId, null, CancellationToken.None).ConfigureAwait(false);

		// Act
		var result = await service.VerifyBackupAsync(backup, CancellationToken.None).ConfigureAwait(false);

		// Assert
		if (!result.IsValid)
		{
			var errors = result.Errors != null ? string.Join(", ", result.Errors) : "none";
			throw new TestFixtureAssertionException($"Valid backup should be valid. Errors: {errors}");
		}

		if (result.IsExpired)
		{
			throw new TestFixtureAssertionException("IsExpired should be false for non-expired backup");
		}

		if (!result.IntegrityCheckPassed)
		{
			throw new TestFixtureAssertionException("IntegrityCheckPassed should be true for valid backup");
		}
	}

	#endregion

	#region GetBackupStatusAsync Tests

	/// <summary>
	/// Tests that GetBackupStatusAsync throws ArgumentException for null keyId.
	/// </summary>
	protected virtual async Task GetBackupStatusAsync_NullKeyId_ShouldThrowArgumentException()
	{
		// Arrange
		var service = CreateService();

		// Act & Assert
		try
		{
			_ = await service.GetBackupStatusAsync(null!, CancellationToken.None).ConfigureAwait(false);
			throw new TestFixtureAssertionException("Expected ArgumentException was not thrown");
		}
		catch (ArgumentException)
		{
			// Expected
		}
	}

	/// <summary>
	/// Tests that GetBackupStatusAsync returns null for a key without backups.
	/// </summary>
	protected virtual async Task GetBackupStatusAsync_NonExistentKey_ShouldReturnNull()
	{
		// Arrange
		var service = CreateService();
		var keyId = $"non-existent-{Guid.NewGuid():N}";

		// Act
		var status = await service.GetBackupStatusAsync(keyId, CancellationToken.None).ConfigureAwait(false);

		// Assert
		if (status != null)
		{
			throw new TestFixtureAssertionException("Status should be null for key without backups");
		}
	}

	/// <summary>
	/// Tests that GetBackupStatusAsync returns status for a key with backups.
	/// </summary>
	protected virtual async Task GetBackupStatusAsync_ExistingBackup_ShouldReturnStatus()
	{
		// Arrange
		var service = CreateService();
		var keyId = $"test-key-{Guid.NewGuid():N}";
		var keyMaterial = new byte[32];
		System.Security.Cryptography.RandomNumberGenerator.Fill(keyMaterial);

		await RegisterTestKeyAsync(service, keyId, keyMaterial, CancellationToken.None).ConfigureAwait(false);
		_ = await service.ExportMasterKeyAsync(keyId, null, CancellationToken.None).ConfigureAwait(false);

		// Act
		var status = await service.GetBackupStatusAsync(keyId, CancellationToken.None).ConfigureAwait(false);

		// Assert
		ArgumentNullException.ThrowIfNull(status);

		if (status.KeyId != keyId)
		{
			throw new TestFixtureAssertionException(
				$"Expected KeyId '{keyId}' but got '{status.KeyId}'");
		}

		if (!status.HasBackup)
		{
			throw new TestFixtureAssertionException("HasBackup should be true");
		}
	}

	#endregion
}
