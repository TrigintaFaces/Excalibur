using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Compliance.Tests.KeyManagement;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class KeyEscrowBackupServiceShould
{
	private readonly IEncryptionProvider _encryptionProvider = A.Fake<IEncryptionProvider>();
	private readonly IKeyManagementProvider _keyManagementProvider = A.Fake<IKeyManagementProvider>();
	private readonly NullLogger<KeyEscrowBackupService> _logger = NullLogger<KeyEscrowBackupService>.Instance;

	[Fact]
	public async Task Backup_key_and_return_receipt()
	{
		A.CallTo(() => _encryptionProvider.EncryptAsync(A<byte[]>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(new EncryptedData
			{
				Ciphertext = [1, 2, 3],
				KeyId = "escrow-master-key",
				KeyVersion = 1,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				Iv = [4, 5, 6]
			});

		var sut = CreateService();
		var keyMaterial = new byte[] { 10, 20, 30 };

		var receipt = await sut.BackupKeyAsync("key-1", keyMaterial, null, CancellationToken.None).ConfigureAwait(false);

		receipt.ShouldNotBeNull();
		receipt.KeyId.ShouldBe("key-1");
		receipt.EscrowId.ShouldNotBeNullOrEmpty();
		receipt.KeyHash.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public async Task Backup_key_with_expiration()
	{
		A.CallTo(() => _encryptionProvider.EncryptAsync(A<byte[]>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(new EncryptedData
			{
				Ciphertext = [1, 2, 3],
				KeyId = "escrow-master-key",
				KeyVersion = 1,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				Iv = [4, 5, 6]
			});

		var sut = CreateService();
		var options = new EscrowOptions { ExpiresIn = TimeSpan.FromDays(30) };

		var receipt = await sut.BackupKeyAsync("key-1", new byte[] { 10 }, options, CancellationToken.None).ConfigureAwait(false);

		receipt.ExpiresAt.ShouldNotBeNull();
	}

	[Fact]
	public async Task Throw_for_empty_key_id_on_backup()
	{
		var sut = CreateService();

		await Should.ThrowAsync<ArgumentException>(
			() => sut.BackupKeyAsync("", new byte[] { 1 }, null, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task Recover_backed_up_key()
	{
		A.CallTo(() => _encryptionProvider.EncryptAsync(A<byte[]>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(new EncryptedData
			{
				Ciphertext = [1, 2, 3],
				KeyId = "escrow-master-key",
				KeyVersion = 1,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				Iv = [4, 5, 6]
			});

		var sut = CreateService();
		await sut.BackupKeyAsync("key-1", new byte[] { 10 }, null, CancellationToken.None).ConfigureAwait(false);

		var token = new RecoveryToken
		{
			TokenId = "token-1",
			KeyId = "key-1",
			EscrowId = "esc-1",
			ShareIndex = 1,
			ShareData = [1, 2],
			CreatedAt = DateTimeOffset.UtcNow,
			ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
			Threshold = 2,
			TotalShares = 3
		};

		var recovered = await sut.RecoverKeyAsync("key-1", token, CancellationToken.None).ConfigureAwait(false);

		recovered.Length.ShouldBeGreaterThan(0);
	}

	[Fact]
	public async Task Throw_for_unknown_key_on_recover()
	{
		var sut = CreateService();
		var token = new RecoveryToken
		{
			TokenId = "token-1",
			KeyId = "unknown",
			EscrowId = "esc-1",
			ShareIndex = 1,
			ShareData = [1],
			CreatedAt = DateTimeOffset.UtcNow,
			ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
			Threshold = 2,
			TotalShares = 3
		};

		await Should.ThrowAsync<KeyEscrowException>(
			() => sut.RecoverKeyAsync("unknown", token, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task Generate_recovery_tokens()
	{
		A.CallTo(() => _encryptionProvider.EncryptAsync(A<byte[]>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(new EncryptedData
			{
				Ciphertext = [1, 2, 3],
				KeyId = "escrow-master-key",
				KeyVersion = 1,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				Iv = [4, 5, 6]
			});

		var sut = CreateService();
		await sut.BackupKeyAsync("key-1", new byte[] { 10 }, null, CancellationToken.None).ConfigureAwait(false);

		var tokens = await sut.GenerateRecoveryTokensAsync(
			"key-1", 5, 3, TimeSpan.FromHours(24), CancellationToken.None).ConfigureAwait(false);

		tokens.Length.ShouldBe(5);
		tokens[0].Threshold.ShouldBe(3);
		tokens[0].TotalShares.ShouldBe(5);
	}

	[Fact]
	public async Task Throw_for_threshold_less_than_two()
	{
		var sut = CreateService();

		await Should.ThrowAsync<ArgumentOutOfRangeException>(
			() => sut.GenerateRecoveryTokensAsync("key-1", 5, 1, null, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task Throw_for_custodians_less_than_threshold()
	{
		var sut = CreateService();

		await Should.ThrowAsync<ArgumentOutOfRangeException>(
			() => sut.GenerateRecoveryTokensAsync("key-1", 2, 3, null, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task Revoke_escrow()
	{
		A.CallTo(() => _encryptionProvider.EncryptAsync(A<byte[]>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(new EncryptedData
			{
				Ciphertext = [1, 2, 3],
				KeyId = "escrow-master-key",
				KeyVersion = 1,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				Iv = [4, 5, 6]
			});

		var sut = CreateService();
		await sut.BackupKeyAsync("key-1", new byte[] { 10 }, null, CancellationToken.None).ConfigureAwait(false);

		var revoked = await sut.RevokeEscrowAsync("key-1", "compromised", CancellationToken.None).ConfigureAwait(false);

		revoked.ShouldBeTrue();
	}

	[Fact]
	public async Task Return_false_when_revoking_unknown_key()
	{
		var sut = CreateService();

		var result = await sut.RevokeEscrowAsync("unknown", "reason", CancellationToken.None).ConfigureAwait(false);

		result.ShouldBeFalse();
	}

	[Fact]
	public async Task Get_escrow_status()
	{
		A.CallTo(() => _encryptionProvider.EncryptAsync(A<byte[]>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(new EncryptedData
			{
				Ciphertext = [1, 2, 3],
				KeyId = "escrow-master-key",
				KeyVersion = 1,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				Iv = [4, 5, 6]
			});

		var sut = CreateService();
		await sut.BackupKeyAsync("key-1", new byte[] { 10 }, null, CancellationToken.None).ConfigureAwait(false);

		var status = await sut.GetEscrowStatusAsync("key-1", CancellationToken.None).ConfigureAwait(false);

		status.ShouldNotBeNull();
		status.KeyId.ShouldBe("key-1");
		status.State.ShouldBe(EscrowState.Active);
	}

	[Fact]
	public async Task Return_null_status_for_unknown_key()
	{
		var sut = CreateService();

		var status = await sut.GetEscrowStatusAsync("unknown", CancellationToken.None).ConfigureAwait(false);

		status.ShouldBeNull();
	}

	[Fact]
	public async Task Throw_when_recovering_revoked_escrow()
	{
		A.CallTo(() => _encryptionProvider.EncryptAsync(A<byte[]>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(new EncryptedData
			{
				Ciphertext = [1, 2, 3],
				KeyId = "escrow-master-key",
				KeyVersion = 1,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				Iv = [4, 5, 6]
			});

		var sut = CreateService();
		await sut.BackupKeyAsync("key-1", new byte[] { 10 }, null, CancellationToken.None).ConfigureAwait(false);
		await sut.RevokeEscrowAsync("key-1", "compromised", CancellationToken.None).ConfigureAwait(false);

		var token = new RecoveryToken
		{
			TokenId = "token-1",
			KeyId = "key-1",
			EscrowId = "esc-1",
			ShareIndex = 1,
			ShareData = [1],
			CreatedAt = DateTimeOffset.UtcNow,
			ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
			Threshold = 2,
			TotalShares = 3
		};

		await Should.ThrowAsync<KeyEscrowException>(
			() => sut.RecoverKeyAsync("key-1", token, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public void Throw_for_null_encryption_provider()
	{
		Should.Throw<ArgumentNullException>(() =>
			new KeyEscrowBackupService(null!, _keyManagementProvider, _logger));
	}

	[Fact]
	public void Throw_for_null_key_management_provider()
	{
		Should.Throw<ArgumentNullException>(() =>
			new KeyEscrowBackupService(_encryptionProvider, null!, _logger));
	}

	[Fact]
	public void Throw_for_null_logger()
	{
		Should.Throw<ArgumentNullException>(() =>
			new KeyEscrowBackupService(_encryptionProvider, _keyManagementProvider, null!));
	}

	private KeyEscrowBackupService CreateService() =>
		new(_encryptionProvider, _keyManagementProvider, _logger);
}
