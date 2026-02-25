using System.Security.Cryptography;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Compliance.Tests.Encryption;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class InMemoryMasterKeyBackupServiceShould
{
	private readonly IKeyManagementProvider _keyManagement = A.Fake<IKeyManagementProvider>();
	private readonly IEncryptionProvider _encryptionProvider = A.Fake<IEncryptionProvider>();
	private readonly InMemoryMasterKeyBackupService _sut;

	public InMemoryMasterKeyBackupServiceShould()
	{
		_sut = new InMemoryMasterKeyBackupService(
			_keyManagement,
			_encryptionProvider,
			NullLogger<InMemoryMasterKeyBackupService>.Instance);
	}

	[Fact]
	public async Task Export_master_key_successfully()
	{
		// Arrange
		var keyMetadata = new KeyMetadata
		{
			KeyId = "master-key-1",
			Version = 1,
			Status = KeyStatus.Active,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			CreatedAt = DateTimeOffset.UtcNow,
		};

		A.CallTo(() => _keyManagement.GetKeyAsync("master-key-1", A<CancellationToken>._))
			.Returns(Task.FromResult<KeyMetadata?>(keyMetadata));

		A.CallTo(() => _encryptionProvider.EncryptAsync(A<byte[]>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new EncryptedData
			{
				Ciphertext = [1, 2, 3],
				Iv = new byte[12],
				KeyId = "default",
				KeyVersion = 1,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
			}));

		// Act
		var result = await _sut.ExportMasterKeyAsync("master-key-1", null, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.ShouldNotBeNull();
		result.KeyId.ShouldBe("master-key-1");
		result.KeyVersion.ShouldBe(1);
		result.BackupId.ShouldNotBeNullOrEmpty();
		result.EncryptedKeyMaterial.ShouldNotBeNull();
		result.KeyHash.ShouldNotBeNullOrEmpty();
		result.CreatedAt.ShouldBeGreaterThan(DateTimeOffset.MinValue);
	}

	[Fact]
	public async Task Export_with_expiration_option()
	{
		// Arrange
		SetupKeyAndEncryption("k1");

		var options = new MasterKeyExportOptions
		{
			ExpiresIn = TimeSpan.FromDays(30),
		};

		// Act
		var result = await _sut.ExportMasterKeyAsync("k1", options, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.ExpiresAt.ShouldNotBeNull();
		result.ExpiresAt.Value.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddDays(29));
	}

	[Fact]
	public async Task Throw_when_key_not_found_on_export()
	{
		// Arrange
		A.CallTo(() => _keyManagement.GetKeyAsync("missing", A<CancellationToken>._))
			.Returns(Task.FromResult<KeyMetadata?>(null));

		// Act & Assert
		var ex = await Should.ThrowAsync<MasterKeyBackupException>(
			() => _sut.ExportMasterKeyAsync("missing", null, CancellationToken.None))
			.ConfigureAwait(false);
		ex.ErrorCode.ShouldBe(MasterKeyBackupErrorCode.KeyNotFound);
	}

	[Fact]
	public async Task Throw_on_null_or_whitespace_key_id_for_export()
	{
		await Should.ThrowAsync<ArgumentException>(
			() => _sut.ExportMasterKeyAsync("", null, CancellationToken.None))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task Generate_recovery_shares_successfully()
	{
		// Arrange
		_sut.RegisterKeyMaterial("k1", RandomNumberGenerator.GetBytes(32));
		A.CallTo(() => _keyManagement.GetKeyAsync("k1", A<CancellationToken>._))
			.Returns(Task.FromResult<KeyMetadata?>(new KeyMetadata
			{
				KeyId = "k1",
				Version = 1,
				Status = KeyStatus.Active,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				CreatedAt = DateTimeOffset.UtcNow,
			}));

		// Act
		var shares = await _sut.GenerateRecoverySplitAsync("k1", 5, 3, null, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		shares.Length.ShouldBe(5);
		foreach (var share in shares)
		{
			share.KeyId.ShouldBe("k1");
			share.TotalShares.ShouldBe(5);
			share.Threshold.ShouldBe(3);
			share.ShareData.ShouldNotBeNull();
			share.KeyHash.ShouldNotBeNullOrEmpty();
		}
	}

	[Fact]
	public async Task Generate_recovery_shares_with_custodian_ids()
	{
		// Arrange
		_sut.RegisterKeyMaterial("k1", RandomNumberGenerator.GetBytes(32));
		A.CallTo(() => _keyManagement.GetKeyAsync("k1", A<CancellationToken>._))
			.Returns(Task.FromResult<KeyMetadata?>(new KeyMetadata
			{
				KeyId = "k1",
				Version = 1,
				Status = KeyStatus.Active,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				CreatedAt = DateTimeOffset.UtcNow,
			}));

		var options = new BackupShareOptions
		{
			CustodianIds = ["alice", "bob", "carol"],
		};

		// Act
		var shares = await _sut.GenerateRecoverySplitAsync("k1", 3, 2, options, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		shares[0].CustodianId.ShouldBe("alice");
		shares[1].CustodianId.ShouldBe("bob");
		shares[2].CustodianId.ShouldBe("carol");
	}

	[Fact]
	public async Task Throw_when_custodian_ids_count_mismatch()
	{
		// Arrange
		_sut.RegisterKeyMaterial("k1", RandomNumberGenerator.GetBytes(32));
		var options = new BackupShareOptions
		{
			CustodianIds = ["alice", "bob"], // 2 instead of 3
		};

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(
			() => _sut.GenerateRecoverySplitAsync("k1", 3, 2, options, CancellationToken.None))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task Throw_when_total_shares_less_than_two()
	{
		await Should.ThrowAsync<ArgumentOutOfRangeException>(
			() => _sut.GenerateRecoverySplitAsync("k1", 1, 1, null, CancellationToken.None))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task Throw_when_threshold_less_than_two()
	{
		await Should.ThrowAsync<ArgumentOutOfRangeException>(
			() => _sut.GenerateRecoverySplitAsync("k1", 3, 1, null, CancellationToken.None))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task Throw_when_threshold_exceeds_total_shares()
	{
		await Should.ThrowAsync<ArgumentOutOfRangeException>(
			() => _sut.GenerateRecoverySplitAsync("k1", 3, 4, null, CancellationToken.None))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task Throw_when_key_material_not_found_for_split()
	{
		var ex = await Should.ThrowAsync<MasterKeyBackupException>(
			() => _sut.GenerateRecoverySplitAsync("missing", 3, 2, null, CancellationToken.None))
			.ConfigureAwait(false);
		ex.ErrorCode.ShouldBe(MasterKeyBackupErrorCode.KeyNotFound);
	}

	[Fact]
	public async Task Reconstruct_from_shares_successfully()
	{
		// Arrange
		var keyMaterial = RandomNumberGenerator.GetBytes(32);
		_sut.RegisterKeyMaterial("k1", keyMaterial);
		A.CallTo(() => _keyManagement.GetKeyAsync("k1", A<CancellationToken>._))
			.Returns(Task.FromResult<KeyMetadata?>(new KeyMetadata
			{
				KeyId = "k1",
				Version = 1,
				Status = KeyStatus.Active,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				CreatedAt = DateTimeOffset.UtcNow,
			}));

		var shares = await _sut.GenerateRecoverySplitAsync("k1", 5, 3, null, CancellationToken.None)
			.ConfigureAwait(false);

		// Act — use first 3 shares (meets threshold)
		var result = await _sut.ReconstructFromSharesAsync(shares[..3], null, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeTrue();
		result.KeyId.ShouldBe("k1");
	}

	[Fact]
	public async Task Throw_when_insufficient_shares()
	{
		// Arrange - create shares with threshold=3
		var keyMaterial = RandomNumberGenerator.GetBytes(32);
		_sut.RegisterKeyMaterial("k1", keyMaterial);
		A.CallTo(() => _keyManagement.GetKeyAsync("k1", A<CancellationToken>._))
			.Returns(Task.FromResult<KeyMetadata?>(new KeyMetadata
			{
				KeyId = "k1",
				Version = 1,
				Status = KeyStatus.Active,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				CreatedAt = DateTimeOffset.UtcNow,
			}));

		var shares = await _sut.GenerateRecoverySplitAsync("k1", 5, 3, null, CancellationToken.None)
			.ConfigureAwait(false);

		// Act & Assert — only 2 shares, need 3
		var ex = await Should.ThrowAsync<MasterKeyBackupException>(
			() => _sut.ReconstructFromSharesAsync(shares[..2], null, CancellationToken.None))
			.ConfigureAwait(false);
		ex.ErrorCode.ShouldBe(MasterKeyBackupErrorCode.InsufficientShares);
	}

	[Fact]
	public async Task Throw_when_shares_from_different_keys()
	{
		// Arrange
		var shares = new[]
		{
			new BackupShare
			{
				ShareId = "s1", KeyId = "key-a", KeyVersion = 1, ShareIndex = 1,
				ShareData = [1, 10], TotalShares = 3, Threshold = 2,
				CreatedAt = DateTimeOffset.UtcNow, KeyHash = "abc",
			},
			new BackupShare
			{
				ShareId = "s2", KeyId = "key-b", KeyVersion = 1, ShareIndex = 2,
				ShareData = [2, 20], TotalShares = 3, Threshold = 2,
				CreatedAt = DateTimeOffset.UtcNow, KeyHash = "abc",
			},
		};

		// Act & Assert
		var ex = await Should.ThrowAsync<MasterKeyBackupException>(
			() => _sut.ReconstructFromSharesAsync(shares, null, CancellationToken.None))
			.ConfigureAwait(false);
		ex.ErrorCode.ShouldBe(MasterKeyBackupErrorCode.ShareMismatch);
	}

	[Fact]
	public async Task Throw_when_shares_from_different_versions()
	{
		// Arrange
		var shares = new[]
		{
			new BackupShare
			{
				ShareId = "s1", KeyId = "k1", KeyVersion = 1, ShareIndex = 1,
				ShareData = [1, 10], TotalShares = 3, Threshold = 2,
				CreatedAt = DateTimeOffset.UtcNow, KeyHash = "abc",
			},
			new BackupShare
			{
				ShareId = "s2", KeyId = "k1", KeyVersion = 2, ShareIndex = 2,
				ShareData = [2, 20], TotalShares = 3, Threshold = 2,
				CreatedAt = DateTimeOffset.UtcNow, KeyHash = "abc",
			},
		};

		// Act & Assert
		var ex = await Should.ThrowAsync<MasterKeyBackupException>(
			() => _sut.ReconstructFromSharesAsync(shares, null, CancellationToken.None))
			.ConfigureAwait(false);
		ex.ErrorCode.ShouldBe(MasterKeyBackupErrorCode.ShareMismatch);
	}

	[Fact]
	public async Task Throw_when_share_is_expired()
	{
		// Arrange
		var shares = new[]
		{
			new BackupShare
			{
				ShareId = "s1", KeyId = "k1", KeyVersion = 1, ShareIndex = 1,
				ShareData = [1, 10], TotalShares = 3, Threshold = 2,
				CreatedAt = DateTimeOffset.UtcNow.AddDays(-10),
				ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1), // Expired
				KeyHash = "abc",
			},
			new BackupShare
			{
				ShareId = "s2", KeyId = "k1", KeyVersion = 1, ShareIndex = 2,
				ShareData = [2, 20], TotalShares = 3, Threshold = 2,
				CreatedAt = DateTimeOffset.UtcNow,
				KeyHash = "abc",
			},
		};

		// Act & Assert
		var ex = await Should.ThrowAsync<MasterKeyBackupException>(
			() => _sut.ReconstructFromSharesAsync(shares, null, CancellationToken.None))
			.ConfigureAwait(false);
		ex.ErrorCode.ShouldBe(MasterKeyBackupErrorCode.BackupExpired);
	}

	[Fact]
	public async Task Throw_when_empty_shares_for_reconstruct()
	{
		await Should.ThrowAsync<ArgumentException>(
			() => _sut.ReconstructFromSharesAsync([], null, CancellationToken.None))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task Throw_when_null_shares_for_reconstruct()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.ReconstructFromSharesAsync(null!, null, CancellationToken.None))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task Verify_valid_backup_returns_is_valid()
	{
		// Arrange
		var backup = new MasterKeyBackup
		{
			BackupId = "b1",
			KeyId = "k1",
			KeyVersion = 1,
			EncryptedKeyMaterial = [1, 2, 3],
			WrappingAlgorithm = EncryptionAlgorithm.Aes256Gcm,
			KeyAlgorithm = EncryptionAlgorithm.Aes256Gcm,
			KeyHash = "abc123",
			CreatedAt = DateTimeOffset.UtcNow,
			ExpiresAt = DateTimeOffset.UtcNow.AddDays(90),
		};

		// Act
		var result = await _sut.VerifyBackupAsync(backup, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.IsValid.ShouldBeTrue();
		result.IsExpired.ShouldBeFalse();
		result.FormatSupported.ShouldBeTrue();
		result.IntegrityCheckPassed.ShouldBeTrue();
	}

	[Fact]
	public async Task Verify_expired_backup_returns_invalid()
	{
		// Arrange
		var backup = new MasterKeyBackup
		{
			BackupId = "b1",
			KeyId = "k1",
			KeyVersion = 1,
			EncryptedKeyMaterial = [1, 2, 3],
			WrappingAlgorithm = EncryptionAlgorithm.Aes256Gcm,
			KeyAlgorithm = EncryptionAlgorithm.Aes256Gcm,
			KeyHash = "abc123",
			CreatedAt = DateTimeOffset.UtcNow.AddDays(-100),
			ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1),
		};

		// Act
		var result = await _sut.VerifyBackupAsync(backup, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.IsExpired.ShouldBeTrue();
		result.Errors.ShouldNotBeNull();
	}

	[Fact]
	public async Task Verify_unsupported_format_version_returns_invalid()
	{
		// Arrange
		var backup = new MasterKeyBackup
		{
			BackupId = "b1",
			KeyId = "k1",
			KeyVersion = 1,
			EncryptedKeyMaterial = [1, 2, 3],
			WrappingAlgorithm = EncryptionAlgorithm.Aes256Gcm,
			KeyAlgorithm = EncryptionAlgorithm.Aes256Gcm,
			KeyHash = "abc123",
			CreatedAt = DateTimeOffset.UtcNow,
			FormatVersion = 99,
		};

		// Act
		var result = await _sut.VerifyBackupAsync(backup, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.FormatSupported.ShouldBeFalse();
	}

	[Fact]
	public async Task Verify_empty_key_material_returns_invalid()
	{
		// Arrange
		var backup = new MasterKeyBackup
		{
			BackupId = "b1",
			KeyId = "k1",
			KeyVersion = 1,
			EncryptedKeyMaterial = [],
			WrappingAlgorithm = EncryptionAlgorithm.Aes256Gcm,
			KeyAlgorithm = EncryptionAlgorithm.Aes256Gcm,
			KeyHash = "abc123",
			CreatedAt = DateTimeOffset.UtcNow,
		};

		// Act
		var result = await _sut.VerifyBackupAsync(backup, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.IntegrityCheckPassed.ShouldBeFalse();
	}

	[Fact]
	public async Task Verify_expiring_soon_backup_returns_warning()
	{
		// Arrange
		var backup = new MasterKeyBackup
		{
			BackupId = "b1",
			KeyId = "k1",
			KeyVersion = 1,
			EncryptedKeyMaterial = [1, 2, 3],
			WrappingAlgorithm = EncryptionAlgorithm.Aes256Gcm,
			KeyAlgorithm = EncryptionAlgorithm.Aes256Gcm,
			KeyHash = "abc123",
			CreatedAt = DateTimeOffset.UtcNow.AddDays(-80),
			ExpiresAt = DateTimeOffset.UtcNow.AddDays(5), // Expires in 5 days
		};

		// Act
		var result = await _sut.VerifyBackupAsync(backup, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.IsValid.ShouldBeTrue();
		result.Warnings.ShouldNotBeNull();
	}

	[Fact]
	public async Task Get_backup_status_returns_null_when_no_backup()
	{
		// Act
		var status = await _sut.GetBackupStatusAsync("nonexistent", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		status.ShouldBeNull();
	}

	[Fact]
	public async Task Get_backup_status_after_export()
	{
		// Arrange
		SetupKeyAndEncryption("k1");
		await _sut.ExportMasterKeyAsync("k1", null, CancellationToken.None).ConfigureAwait(false);

		// Act
		var status = await _sut.GetBackupStatusAsync("k1", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		status.ShouldNotBeNull();
		status.KeyId.ShouldBe("k1");
		status.HasBackup.ShouldBeTrue();
		status.LastBackupAt.ShouldNotBeNull();
	}

	[Fact]
	public async Task Get_backup_status_after_generating_shares()
	{
		// Arrange
		_sut.RegisterKeyMaterial("k1", RandomNumberGenerator.GetBytes(32));
		A.CallTo(() => _keyManagement.GetKeyAsync("k1", A<CancellationToken>._))
			.Returns(Task.FromResult<KeyMetadata?>(new KeyMetadata
			{
				KeyId = "k1",
				Version = 1,
				Status = KeyStatus.Active,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				CreatedAt = DateTimeOffset.UtcNow,
			}));

		await _sut.GenerateRecoverySplitAsync("k1", 5, 3, null, CancellationToken.None)
			.ConfigureAwait(false);

		// Act
		var status = await _sut.GetBackupStatusAsync("k1", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		status.ShouldNotBeNull();
		status.ActiveShareCount.ShouldBe(5);
		status.ShareThreshold.ShouldBe(3);
	}

	[Fact]
	public void Register_key_material()
	{
		// Act - should not throw
		_sut.RegisterKeyMaterial("test-key", new byte[32]);
	}

	[Fact]
	public void Throw_for_null_key_id_on_register()
	{
		Should.Throw<ArgumentException>(() => _sut.RegisterKeyMaterial("", new byte[32]));
	}

	[Fact]
	public void Throw_for_null_key_material_on_register()
	{
		Should.Throw<ArgumentNullException>(() => _sut.RegisterKeyMaterial("k1", null!));
	}

	[Fact]
	public void Throw_for_null_key_management()
	{
		Should.Throw<ArgumentNullException>(() =>
			new InMemoryMasterKeyBackupService(null!, _encryptionProvider, NullLogger<InMemoryMasterKeyBackupService>.Instance));
	}

	[Fact]
	public void Throw_for_null_encryption_provider()
	{
		Should.Throw<ArgumentNullException>(() =>
			new InMemoryMasterKeyBackupService(_keyManagement, null!, NullLogger<InMemoryMasterKeyBackupService>.Instance));
	}

	[Fact]
	public void Throw_for_null_logger()
	{
		Should.Throw<ArgumentNullException>(() =>
			new InMemoryMasterKeyBackupService(_keyManagement, _encryptionProvider, null!));
	}

	[Fact]
	public async Task Throw_on_null_backup_for_verify()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.VerifyBackupAsync(null!, CancellationToken.None))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task Throw_on_null_or_whitespace_key_id_for_status()
	{
		await Should.ThrowAsync<ArgumentException>(
			() => _sut.GetBackupStatusAsync("", CancellationToken.None))
			.ConfigureAwait(false);
	}

	[Fact]
	public void Have_default_export_options_values()
	{
		var options = new MasterKeyExportOptions();
		options.WrappingAlgorithm.ShouldBe(EncryptionAlgorithm.Aes256Gcm);
		options.ExpiresIn.ShouldBe(TimeSpan.FromDays(90));
		options.WrappingKeyId.ShouldBeNull();
		options.Metadata.ShouldBeNull();
		options.Reason.ShouldBeNull();
	}

	[Fact]
	public void Have_default_import_options_values()
	{
		var options = new MasterKeyImportOptions();
		options.AllowOverwrite.ShouldBeFalse();
		options.ActivateImmediately.ShouldBeTrue();
		options.NewKeyId.ShouldBeNull();
		options.Reason.ShouldBeNull();
		options.VerifyKeyHash.ShouldBeTrue();
	}

	[Fact]
	public void Have_default_share_options_values()
	{
		var options = new BackupShareOptions();
		options.ExpiresIn.ShouldBe(TimeSpan.FromDays(365));
		options.CustodianIds.ShouldBeNull();
		options.Reason.ShouldBeNull();
	}

	[Fact]
	public void BackupShare_combine_should_merge_shares()
	{
		// Arrange
		var shares = new[]
		{
			new BackupShare
			{
				ShareId = "s1", KeyId = "k1", KeyVersion = 1, ShareIndex = 1,
				ShareData = [1, 10], TotalShares = 3, Threshold = 2,
				CreatedAt = DateTimeOffset.UtcNow, KeyHash = "hash1",
			},
			new BackupShare
			{
				ShareId = "s2", KeyId = "k1", KeyVersion = 1, ShareIndex = 2,
				ShareData = [2, 20], TotalShares = 3, Threshold = 2,
				CreatedAt = DateTimeOffset.UtcNow, KeyHash = "hash1",
			},
		};

		// Act
		var combined = BackupShare.Combine(shares);

		// Assert
		combined.ShareIndex.ShouldBe(0); // Combined marker
		combined.KeyId.ShouldBe("k1");
		combined.ShareData.ShouldNotBeNull();
	}

	[Fact]
	public void BackupShare_combine_should_throw_for_empty_shares()
	{
		Should.Throw<ArgumentException>(() =>
			BackupShare.Combine(Enumerable.Empty<BackupShare>()));
	}

	[Fact]
	public void BackupShare_combine_should_throw_for_different_keys()
	{
		var shares = new[]
		{
			new BackupShare
			{
				ShareId = "s1", KeyId = "key-a", KeyVersion = 1, ShareIndex = 1,
				ShareData = [1, 10], TotalShares = 3, Threshold = 2,
				CreatedAt = DateTimeOffset.UtcNow, KeyHash = "abc",
			},
			new BackupShare
			{
				ShareId = "s2", KeyId = "key-b", KeyVersion = 1, ShareIndex = 2,
				ShareData = [2, 20], TotalShares = 3, Threshold = 2,
				CreatedAt = DateTimeOffset.UtcNow, KeyHash = "abc",
			},
		};

		Should.Throw<ArgumentException>(() => BackupShare.Combine(shares));
	}

	[Fact]
	public void BackupShare_combine_should_throw_for_different_versions()
	{
		var shares = new[]
		{
			new BackupShare
			{
				ShareId = "s1", KeyId = "k1", KeyVersion = 1, ShareIndex = 1,
				ShareData = [1, 10], TotalShares = 3, Threshold = 2,
				CreatedAt = DateTimeOffset.UtcNow, KeyHash = "abc",
			},
			new BackupShare
			{
				ShareId = "s2", KeyId = "k1", KeyVersion = 2, ShareIndex = 2,
				ShareData = [2, 20], TotalShares = 3, Threshold = 2,
				CreatedAt = DateTimeOffset.UtcNow, KeyHash = "abc",
			},
		};

		Should.Throw<ArgumentException>(() => BackupShare.Combine(shares));
	}

	[Fact]
	public void BackupShare_combine_should_throw_for_different_thresholds()
	{
		var shares = new[]
		{
			new BackupShare
			{
				ShareId = "s1", KeyId = "k1", KeyVersion = 1, ShareIndex = 1,
				ShareData = [1, 10], TotalShares = 3, Threshold = 2,
				CreatedAt = DateTimeOffset.UtcNow, KeyHash = "abc",
			},
			new BackupShare
			{
				ShareId = "s2", KeyId = "k1", KeyVersion = 1, ShareIndex = 2,
				ShareData = [2, 20], TotalShares = 5, Threshold = 3,
				CreatedAt = DateTimeOffset.UtcNow, KeyHash = "abc",
			},
		};

		Should.Throw<ArgumentException>(() => BackupShare.Combine(shares));
	}

	[Fact]
	public void Have_default_error_code_values()
	{
		var ex = new MasterKeyBackupException();
		ex.ErrorCode.ShouldBe(MasterKeyBackupErrorCode.Unknown);
		ex.KeyId.ShouldBeNull();
		ex.BackupId.ShouldBeNull();
	}

	[Fact]
	public void Create_exception_with_message()
	{
		var ex = new MasterKeyBackupException("test") { ErrorCode = MasterKeyBackupErrorCode.KeyNotFound };
		ex.Message.ShouldBe("test");
		ex.ErrorCode.ShouldBe(MasterKeyBackupErrorCode.KeyNotFound);
	}

	[Fact]
	public void Create_exception_with_inner()
	{
		var inner = new InvalidOperationException("inner");
		var ex = new MasterKeyBackupException("outer", inner);
		ex.Message.ShouldBe("outer");
		ex.InnerException.ShouldBeSameAs(inner);
	}

	private void SetupKeyAndEncryption(string keyId)
	{
		A.CallTo(() => _keyManagement.GetKeyAsync(keyId, A<CancellationToken>._))
			.Returns(Task.FromResult<KeyMetadata?>(new KeyMetadata
			{
				KeyId = keyId,
				Version = 1,
				Status = KeyStatus.Active,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				CreatedAt = DateTimeOffset.UtcNow,
			}));

		A.CallTo(() => _encryptionProvider.EncryptAsync(A<byte[]>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new EncryptedData
			{
				Ciphertext = [1, 2, 3],
				Iv = new byte[12],
				KeyId = "default",
				KeyVersion = 1,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
			}));
	}
}
