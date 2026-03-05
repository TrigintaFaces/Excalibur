// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Encryption;

/// <summary>
/// Unit tests for <see cref="InMemoryMasterKeyBackupService"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class InMemoryMasterKeyBackupServiceShould
{
	private readonly InMemoryMasterKeyBackupService _sut;
	private readonly IKeyManagementProvider _keyManagementProvider;
	private readonly IEncryptionProvider _encryptionProvider;
	private readonly HashSet<string> _registeredKeys = [];

	public InMemoryMasterKeyBackupServiceShould()
	{
		_keyManagementProvider = A.Fake<IKeyManagementProvider>();
		_encryptionProvider = A.Fake<IEncryptionProvider>();

		// Configure fake encryption provider
		_ = A.CallTo(() => _encryptionProvider.EncryptAsync(
				A<byte[]>.Ignored,
				A<EncryptionContext>.Ignored,
				A<CancellationToken>.Ignored))
			.ReturnsLazily((byte[] plaintext, EncryptionContext ctx, CancellationToken ct) =>
				Task.FromResult(new EncryptedData
				{
					Ciphertext = plaintext,
					KeyId = "wrapping-key",
					KeyVersion = 1,
					Algorithm = EncryptionAlgorithm.Aes256Gcm,
					Iv = new byte[12],
					AuthTag = new byte[16]
				}));

		_ = A.CallTo(() => _encryptionProvider.DecryptAsync(
				A<EncryptedData>.Ignored,
				A<EncryptionContext>.Ignored,
				A<CancellationToken>.Ignored))
			.ReturnsLazily((EncryptedData encrypted, EncryptionContext ctx, CancellationToken ct) =>
				Task.FromResult(encrypted.Ciphertext));

		// Configure fake key management provider - return null for non-registered keys
		_ = A.CallTo(() => _keyManagementProvider.GetKeyAsync(
				A<string>.Ignored,
				A<CancellationToken>.Ignored))
			.ReturnsLazily((string keyId, CancellationToken ct) =>
			{
				// Return null for non-registered keys
				if (!_registeredKeys.Contains(keyId))
				{
					return Task.FromResult<KeyMetadata?>(null);
				}

				return Task.FromResult<KeyMetadata?>(new KeyMetadata
				{
					KeyId = keyId,
					Algorithm = EncryptionAlgorithm.Aes256Gcm,
					Status = KeyStatus.Active,
					Version = 1,
					CreatedAt = DateTimeOffset.UtcNow.AddDays(-30),
					ExpiresAt = DateTimeOffset.UtcNow.AddYears(1)
				});
			});

		_sut = new InMemoryMasterKeyBackupService(
			_keyManagementProvider,
			_encryptionProvider,
			NullLogger<InMemoryMasterKeyBackupService>.Instance);
	}

	/// <summary>
	/// Helper method to register key material and track the key as registered.
	/// </summary>
	private void RegisterKey(string keyId, byte[] keyMaterial)
	{
		_ = _registeredKeys.Add(keyId);
		_sut.RegisterKeyMaterial(keyId, keyMaterial);
	}

	/// <summary>
	/// Helper method to simulate a key being deleted (for import testing).
	/// </summary>
	private void UnregisterKey(string keyId)
	{
		_ = _registeredKeys.Remove(keyId);
	}

	#region ExportMasterKeyAsync Tests

	[Fact]
	public async Task ExportMasterKeyAsync_ReturnsBackup_WhenKeyExists()
	{
		// Arrange
		const string keyId = "test-key-1";
		var keyMaterial = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 };
		RegisterKey(keyId, keyMaterial);
		var lowerBound = DateTimeOffset.UtcNow;

		// Act
		var backup = await _sut.ExportMasterKeyAsync(keyId, null, CancellationToken.None);
		var upperBound = DateTimeOffset.UtcNow;

		// Assert
		_ = backup.ShouldNotBeNull();
		backup.KeyId.ShouldBe(keyId);
		backup.BackupId.ShouldNotBeNullOrEmpty();
		backup.BackupId.Length.ShouldBe(32); // GUID without dashes
		backup.KeyVersion.ShouldBeGreaterThan(0);
		backup.EncryptedKeyMaterial.ShouldNotBeEmpty();
		backup.KeyHash.ShouldNotBeNullOrEmpty();
		backup.CreatedAt.ShouldBeGreaterThanOrEqualTo(lowerBound);
		backup.CreatedAt.ShouldBeLessThanOrEqualTo(upperBound);
	}

	[Fact]
	public async Task ExportMasterKeyAsync_ThrowsException_WhenKeyIdIsEmpty()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_sut.ExportMasterKeyAsync("", null, CancellationToken.None));
	}

	[Fact]
	public async Task ExportMasterKeyAsync_ThrowsException_WhenKeyNotFound()
	{
		// Act & Assert
		var exception = await Should.ThrowAsync<MasterKeyBackupException>(() =>
			_sut.ExportMasterKeyAsync("nonexistent-key", null, CancellationToken.None));

		exception.ErrorCode.ShouldBe(MasterKeyBackupErrorCode.KeyNotFound);
	}

	[Fact]
	public async Task ExportMasterKeyAsync_SetsExpiration_WhenProvided()
	{
		// Arrange
		const string keyId = "test-key-1";
		var keyMaterial = new byte[] { 0x01, 0x02, 0x03, 0x04 };
		RegisterKey(keyId, keyMaterial);
		var options = new MasterKeyExportOptions { ExpiresIn = TimeSpan.FromDays(30) };
		var minExpectedExpiry = DateTimeOffset.UtcNow.AddDays(29);

		// Act
		var backup = await _sut.ExportMasterKeyAsync(keyId, options, CancellationToken.None);
		var maxExpectedExpiry = DateTimeOffset.UtcNow.AddDays(31);

		// Assert
		_ = backup.ExpiresAt.ShouldNotBeNull();
		backup.ExpiresAt.Value.ShouldBeGreaterThanOrEqualTo(minExpectedExpiry);
		backup.ExpiresAt.Value.ShouldBeLessThanOrEqualTo(maxExpectedExpiry);
	}

	[Fact]
	public async Task ExportMasterKeyAsync_SetsWrappingAlgorithm()
	{
		// Arrange
		const string keyId = "test-key-1";
		var keyMaterial = new byte[] { 0x01, 0x02, 0x03, 0x04 };
		RegisterKey(keyId, keyMaterial);
		var options = new MasterKeyExportOptions { WrappingAlgorithm = EncryptionAlgorithm.Aes256Gcm };

		// Act
		var backup = await _sut.ExportMasterKeyAsync(keyId, options, CancellationToken.None);

		// Assert
		backup.WrappingAlgorithm.ShouldBe(EncryptionAlgorithm.Aes256Gcm);
	}

	#endregion ExportMasterKeyAsync Tests

	#region ImportMasterKeyAsync Tests

	[Fact]
	public async Task ImportMasterKeyAsync_ReturnsSuccess_WhenBackupIsValid()
	{
		// Arrange
		const string keyId = "test-key-1";
		var keyMaterial = new byte[] { 0x01, 0x02, 0x03, 0x04 };
		RegisterKey(keyId, keyMaterial);

		var backup = await _sut.ExportMasterKeyAsync(keyId, null, CancellationToken.None);

		// Simulate disaster recovery - key is lost
		UnregisterKey(keyId);
		var lowerBound = DateTimeOffset.UtcNow;

		// Act
		var result = await _sut.ImportMasterKeyAsync(backup, null, CancellationToken.None);
		var upperBound = DateTimeOffset.UtcNow;

		// Assert
		_ = result.ShouldNotBeNull();
		result.Success.ShouldBeTrue();
		result.KeyId.ShouldBe(keyId);
		result.ImportedAt.ShouldBeGreaterThanOrEqualTo(lowerBound);
		result.ImportedAt.ShouldBeLessThanOrEqualTo(upperBound);
	}

	[Fact]
	public async Task ImportMasterKeyAsync_ThrowsException_WhenBackupIsNull()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.ImportMasterKeyAsync(null!, null, CancellationToken.None));
	}

	[Fact]
	public async Task ImportMasterKeyAsync_AllowsOverwrite_WhenOptionSet()
	{
		// Arrange
		const string keyId = "test-key-1";
		var keyMaterial = new byte[] { 0x01, 0x02, 0x03, 0x04 };
		RegisterKey(keyId, keyMaterial);
		var backup = await _sut.ExportMasterKeyAsync(keyId, null, CancellationToken.None);

		// Simulate disaster recovery - key is lost
		UnregisterKey(keyId);

		// Import once
		_ = await _sut.ImportMasterKeyAsync(backup, null, CancellationToken.None);

		// Track that key now exists again
		_ = _registeredKeys.Add(keyId);

		// Act - import again with overwrite
		var result = await _sut.ImportMasterKeyAsync(backup,
			new MasterKeyImportOptions { AllowOverwrite = true }, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
		result.WasOverwritten.ShouldBeTrue();
	}

	[Fact]
	public async Task ImportMasterKeyAsync_ThrowsException_WhenKeyExists_AndOverwriteNotAllowed()
	{
		// Arrange
		const string keyId = "test-key-1";
		var keyMaterial = new byte[] { 0x01, 0x02, 0x03, 0x04 };
		RegisterKey(keyId, keyMaterial);
		var backup = await _sut.ExportMasterKeyAsync(keyId, null, CancellationToken.None);

		// Key still exists (no disaster recovery scenario) - import should fail

		// Act & Assert
		var exception = await Should.ThrowAsync<MasterKeyBackupException>(() =>
			_sut.ImportMasterKeyAsync(backup,
				new MasterKeyImportOptions { AllowOverwrite = false }, CancellationToken.None));

		exception.ErrorCode.ShouldBe(MasterKeyBackupErrorCode.KeyAlreadyExists);
	}

	[Fact]
	public async Task ImportMasterKeyAsync_UsesNewKeyId_WhenProvided()
	{
		// Arrange
		const string originalKeyId = "test-key-1";
		const string newKeyId = "imported-key-1";
		var keyMaterial = new byte[] { 0x01, 0x02, 0x03, 0x04 };
		RegisterKey(originalKeyId, keyMaterial);
		var backup = await _sut.ExportMasterKeyAsync(originalKeyId, null, CancellationToken.None);

		// Simulate disaster recovery - original key is lost
		UnregisterKey(originalKeyId);

		// Act - import with a new key ID
		var result = await _sut.ImportMasterKeyAsync(backup,
			new MasterKeyImportOptions { NewKeyId = newKeyId }, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
		result.KeyId.ShouldBe(newKeyId);
	}

	#endregion ImportMasterKeyAsync Tests

	#region GenerateRecoverySplitAsync Tests

	[Fact]
	public async Task GenerateRecoverySplitAsync_ReturnsCorrectNumberOfShares()
	{
		// Arrange
		const string keyId = "test-key-1";
		var keyMaterial = new byte[] { 0x01, 0x02, 0x03, 0x04 };
		RegisterKey(keyId, keyMaterial);

		// Act
		var shares = await _sut.GenerateRecoverySplitAsync(keyId, 5, 3, null, CancellationToken.None);

		// Assert
		shares.Length.ShouldBe(5);
	}

	[Fact]
	public async Task GenerateRecoverySplitAsync_SharesHaveCorrectMetadata()
	{
		// Arrange
		const string keyId = "test-key-1";
		var keyMaterial = new byte[] { 0x01, 0x02, 0x03, 0x04 };
		RegisterKey(keyId, keyMaterial);

		// Act
		var shares = await _sut.GenerateRecoverySplitAsync(keyId, 5, 3, null, CancellationToken.None);

		// Assert
		foreach (var share in shares)
		{
			share.KeyId.ShouldBe(keyId);
			share.TotalShares.ShouldBe(5);
			share.Threshold.ShouldBe(3);
			share.ShareData.ShouldNotBeEmpty();
			share.KeyHash.ShouldNotBeNullOrEmpty();
		}
	}

	[Fact]
	public async Task GenerateRecoverySplitAsync_SharesHaveUniqueIndices()
	{
		// Arrange
		const string keyId = "test-key-1";
		var keyMaterial = new byte[] { 0x01, 0x02, 0x03, 0x04 };
		RegisterKey(keyId, keyMaterial);

		// Act
		var shares = await _sut.GenerateRecoverySplitAsync(keyId, 5, 3, null, CancellationToken.None);

		// Assert
		var indices = shares.Select(s => s.ShareIndex).ToList();
		indices.Distinct().Count().ShouldBe(5);
		indices.ShouldBe([1, 2, 3, 4, 5]);
	}

	[Fact]
	public async Task GenerateRecoverySplitAsync_ThrowsException_WhenKeyNotFound()
	{
		// Act & Assert
		var exception = await Should.ThrowAsync<MasterKeyBackupException>(() =>
			_sut.GenerateRecoverySplitAsync("nonexistent-key", 5, 3, null, CancellationToken.None));

		exception.ErrorCode.ShouldBe(MasterKeyBackupErrorCode.KeyNotFound);
	}

	[Fact]
	public async Task GenerateRecoverySplitAsync_ThrowsException_WhenThresholdTooLow()
	{
		// Arrange
		const string keyId = "test-key-1";
		var keyMaterial = new byte[] { 0x01, 0x02, 0x03, 0x04 };
		RegisterKey(keyId, keyMaterial);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentOutOfRangeException>(() =>
			_sut.GenerateRecoverySplitAsync(keyId, 5, 1, null, CancellationToken.None));
	}

	[Fact]
	public async Task GenerateRecoverySplitAsync_ThrowsException_WhenThresholdExceedsTotalShares()
	{
		// Arrange
		const string keyId = "test-key-1";
		var keyMaterial = new byte[] { 0x01, 0x02, 0x03, 0x04 };
		RegisterKey(keyId, keyMaterial);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentOutOfRangeException>(() =>
			_sut.GenerateRecoverySplitAsync(keyId, 3, 5, null, CancellationToken.None));
	}

	[Fact]
	public async Task GenerateRecoverySplitAsync_ThrowsException_WhenTotalSharesTooLow()
	{
		// Arrange
		const string keyId = "test-key-1";
		var keyMaterial = new byte[] { 0x01, 0x02, 0x03, 0x04 };
		RegisterKey(keyId, keyMaterial);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentOutOfRangeException>(() =>
			_sut.GenerateRecoverySplitAsync(keyId, 1, 1, null, CancellationToken.None));
	}

	[Fact]
	public async Task GenerateRecoverySplitAsync_AssignsCustodians_WhenProvided()
	{
		// Arrange
		const string keyId = "test-key-1";
		var keyMaterial = new byte[] { 0x01, 0x02, 0x03, 0x04 };
		RegisterKey(keyId, keyMaterial);
		var custodians = new[] { "Alice", "Bob", "Charlie", "Dave", "Eve" };
		var options = new BackupShareOptions { CustodianIds = custodians };

		// Act
		var shares = await _sut.GenerateRecoverySplitAsync(keyId, 5, 3, options, CancellationToken.None);

		// Assert
		shares[0].CustodianId.ShouldBe("Alice");
		shares[1].CustodianId.ShouldBe("Bob");
		shares[2].CustodianId.ShouldBe("Charlie");
		shares[3].CustodianId.ShouldBe("Dave");
		shares[4].CustodianId.ShouldBe("Eve");
	}

	[Fact]
	public async Task GenerateRecoverySplitAsync_ThrowsException_WhenCustodianCountMismatch()
	{
		// Arrange
		const string keyId = "test-key-1";
		var keyMaterial = new byte[] { 0x01, 0x02, 0x03, 0x04 };
		RegisterKey(keyId, keyMaterial);
		var options = new BackupShareOptions { CustodianIds = ["Alice", "Bob"] }; // Only 2 for 5 shares

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_sut.GenerateRecoverySplitAsync(keyId, 5, 3, options, CancellationToken.None));
	}

	#endregion GenerateRecoverySplitAsync Tests

	#region ReconstructFromSharesAsync Tests

	[Fact]
	public async Task ReconstructFromSharesAsync_ReturnsSuccess_WithValidShares()
	{
		// Arrange
		const string keyId = "test-key-1";
		var keyMaterial = new byte[] { 0x01, 0x02, 0x03, 0x04 };
		RegisterKey(keyId, keyMaterial);

		var shares = await _sut.GenerateRecoverySplitAsync(keyId, 5, 3, null, CancellationToken.None);
		var selectedShares = shares.Take(3).ToArray();

		// Act
		var result = await _sut.ReconstructFromSharesAsync(selectedShares, null, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Success.ShouldBeTrue();
		result.KeyId.ShouldBe(keyId);
	}

	[Fact]
	public async Task ReconstructFromSharesAsync_ThrowsException_WhenSharesEmpty()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_sut.ReconstructFromSharesAsync([], null, CancellationToken.None));
	}

	[Fact]
	public async Task ReconstructFromSharesAsync_ThrowsException_WhenSharesNull()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.ReconstructFromSharesAsync(null!, null, CancellationToken.None));
	}

	[Fact]
	public async Task ReconstructFromSharesAsync_ThrowsException_WhenInsufficientShares()
	{
		// Arrange
		const string keyId = "test-key-1";
		var keyMaterial = new byte[] { 0x01, 0x02, 0x03, 0x04 };
		RegisterKey(keyId, keyMaterial);

		var shares = await _sut.GenerateRecoverySplitAsync(keyId, 5, 3, null, CancellationToken.None);
		var selectedShares = shares.Take(2).ToArray(); // Only 2, need 3

		// Act & Assert
		var exception = await Should.ThrowAsync<MasterKeyBackupException>(() =>
			_sut.ReconstructFromSharesAsync(selectedShares, null, CancellationToken.None));

		exception.ErrorCode.ShouldBe(MasterKeyBackupErrorCode.InsufficientShares);
	}

	[Fact]
	public async Task ReconstructFromSharesAsync_ReconstructsCorrectKey_WithExactThreshold()
	{
		// Arrange
		const string keyId = "test-key-1";
		var keyMaterial = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 };
		RegisterKey(keyId, keyMaterial);

		var shares = await _sut.GenerateRecoverySplitAsync(keyId, 5, 3, null, CancellationToken.None);
		var selectedShares = shares.Take(3).ToArray(); // Exactly threshold

		// Act
		var result = await _sut.ReconstructFromSharesAsync(selectedShares, null, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
	}

	[Fact]
	public async Task ReconstructFromSharesAsync_ReconstructsCorrectKey_WithMoreThanThreshold()
	{
		// Arrange
		const string keyId = "test-key-1";
		var keyMaterial = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 };
		RegisterKey(keyId, keyMaterial);

		var shares = await _sut.GenerateRecoverySplitAsync(keyId, 5, 3, null, CancellationToken.None);
		var selectedShares = shares.Take(4).ToArray(); // More than threshold

		// Act
		var result = await _sut.ReconstructFromSharesAsync(selectedShares, null, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
	}

	[Fact]
	public async Task ReconstructFromSharesAsync_ThrowsException_WhenSharesFromDifferentKeys()
	{
		// Arrange
		const string keyId1 = "test-key-1";
		const string keyId2 = "test-key-2";
		var keyMaterial = new byte[] { 0x01, 0x02, 0x03, 0x04 };
		_sut.RegisterKeyMaterial(keyId1, keyMaterial);
		_sut.RegisterKeyMaterial(keyId2, keyMaterial);

		var shares1 = await _sut.GenerateRecoverySplitAsync(keyId1, 5, 3, null, CancellationToken.None);
		var shares2 = await _sut.GenerateRecoverySplitAsync(keyId2, 5, 3, null, CancellationToken.None);

		// Mix shares from different keys
		var mixedShares = new[] { shares1[0], shares1[1], shares2[0] };

		// Act & Assert
		var exception = await Should.ThrowAsync<MasterKeyBackupException>(() =>
			_sut.ReconstructFromSharesAsync(mixedShares, null, CancellationToken.None));

		exception.ErrorCode.ShouldBe(MasterKeyBackupErrorCode.ShareMismatch);
	}

	#endregion ReconstructFromSharesAsync Tests

	#region VerifyBackupAsync Tests

	[Fact]
	public async Task VerifyBackupAsync_ReturnsValid_WhenBackupIsValid()
	{
		// Arrange
		const string keyId = "test-key-1";
		var keyMaterial = new byte[] { 0x01, 0x02, 0x03, 0x04 };
		RegisterKey(keyId, keyMaterial);
		var backup = await _sut.ExportMasterKeyAsync(keyId, null, CancellationToken.None);

		// Act
		var result = await _sut.VerifyBackupAsync(backup, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.IsValid.ShouldBeTrue();
		result.KeyId.ShouldBe(keyId);
		result.FormatSupported.ShouldBeTrue();
		result.IntegrityCheckPassed.ShouldBeTrue();
	}

	[Fact]
	public async Task VerifyBackupAsync_ThrowsException_WhenBackupIsNull()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.VerifyBackupAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task VerifyBackupAsync_ReturnsExpired_WhenBackupExpired()
	{
		// Arrange
		const string keyId = "test-key-1";
		var keyMaterial = new byte[] { 0x01, 0x02, 0x03, 0x04 };
		RegisterKey(keyId, keyMaterial);

		var backup = new MasterKeyBackup
		{
			BackupId = "backup-1",
			KeyId = keyId,
			KeyVersion = 1,
			EncryptedKeyMaterial = new byte[] { 0x01 },
			WrappingAlgorithm = EncryptionAlgorithm.Aes256Gcm,
			KeyAlgorithm = EncryptionAlgorithm.Aes256Gcm,
			KeyHash = "abc123",
			CreatedAt = DateTimeOffset.UtcNow.AddDays(-100),
			ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1) // Expired
		};

		// Act
		var result = await _sut.VerifyBackupAsync(backup, CancellationToken.None);

		// Assert
		result.IsExpired.ShouldBeTrue();
		result.IsValid.ShouldBeFalse();
	}

	#endregion VerifyBackupAsync Tests

	#region GetBackupStatusAsync Tests

	[Fact]
	public async Task GetBackupStatusAsync_ReturnsStatus_WhenBackupExists()
	{
		// Arrange
		const string keyId = "test-key-1";
		var keyMaterial = new byte[] { 0x01, 0x02, 0x03, 0x04 };
		RegisterKey(keyId, keyMaterial);
		_ = await _sut.ExportMasterKeyAsync(keyId, null, CancellationToken.None);

		// Act
		var status = await _sut.GetBackupStatusAsync(keyId, CancellationToken.None);

		// Assert
		_ = status.ShouldNotBeNull();
		status.KeyId.ShouldBe(keyId);
		status.HasBackup.ShouldBeTrue();
		_ = status.LastBackupAt.ShouldNotBeNull();
	}

	[Fact]
	public async Task GetBackupStatusAsync_ReturnsNull_WhenNoBackupExists()
	{
		// Act
		var status = await _sut.GetBackupStatusAsync("nonexistent-key", CancellationToken.None);

		// Assert
		status.ShouldBeNull();
	}

	[Fact]
	public async Task GetBackupStatusAsync_IncludesShareCount_WhenSharesGenerated()
	{
		// Arrange
		const string keyId = "test-key-1";
		var keyMaterial = new byte[] { 0x01, 0x02, 0x03, 0x04 };
		RegisterKey(keyId, keyMaterial);
		_ = await _sut.GenerateRecoverySplitAsync(keyId, 5, 3, null, CancellationToken.None);

		// Act
		var status = await _sut.GetBackupStatusAsync(keyId, CancellationToken.None);

		// Assert
		_ = status.ShouldNotBeNull();
		status.ActiveShareCount.ShouldBe(5);
		status.ShareThreshold.ShouldBe(3);
	}

	[Fact]
	public async Task GetBackupStatusAsync_ThrowsException_WhenKeyIdIsEmpty()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_sut.GetBackupStatusAsync("", CancellationToken.None));
	}

	#endregion GetBackupStatusAsync Tests

	#region RegisterKeyMaterial Tests

	[Fact]
	public void RegisterKeyMaterial_AllowsExportOfKey()
	{
		// Arrange
		const string keyId = "test-key-1";
		var keyMaterial = new byte[] { 0x01, 0x02, 0x03, 0x04 };

		// Act
		RegisterKey(keyId, keyMaterial);

		// Assert - should not throw
		_ = Should.NotThrow(async () => await _sut.ExportMasterKeyAsync(keyId, null, CancellationToken.None));
	}

	[Fact]
	public void RegisterKeyMaterial_ThrowsException_WhenKeyIdEmpty()
	{
		// Arrange
		var keyMaterial = new byte[] { 0x01, 0x02, 0x03, 0x04 };

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			_sut.RegisterKeyMaterial("", keyMaterial));
	}

	[Fact]
	public void RegisterKeyMaterial_ThrowsException_WhenKeyMaterialNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			_sut.RegisterKeyMaterial("test-key", null!));
	}

	#endregion RegisterKeyMaterial Tests
}
