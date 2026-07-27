using Excalibur.Compliance;
using Excalibur.Compliance.KeyManagement;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Compliance.Tests.KeyManagement;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class KeyEscrowBackupServiceShould
{
	private readonly IEncryptionProvider _encryptionProvider = A.Fake<IEncryptionProvider>();
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

	// rzr5zs — M-of-N recovery HAPPY path: combine >= threshold real shares, recover the REAL key.
	// (Flipped from the old bypass, which recovered from a single fabricated token — that path is closed.)
	[Fact]
	public async Task Recover_backed_up_key_from_a_combined_quorum_of_threshold_shares()
	{
		StubEncryptRoundTrip();

		var sut = CreateService();
		await sut.BackupKeyAsync("key-1", new byte[] { 10 }, null, CancellationToken.None).ConfigureAwait(false);

		var tokens = await sut.GenerateRecoveryTokensAsync(
			"key-1", 5, 3, TimeSpan.FromHours(24), CancellationToken.None).ConfigureAwait(false);

		// Combine exactly the threshold number of real shares into the quorum token.
		var quorum = RecoveryToken.Combine(tokens.Take(3));

		var recovered = await sut.RecoverKeyAsync("key-1", quorum, CancellationToken.None).ConfigureAwait(false);

		// The real key material is returned (decrypted), never the raw ciphertext.
		recovered.ToArray().ShouldBe(new byte[] { 10 });
	}

	// rzr5zs — reject-single: the closed bypass. A single (non-combined, ShareIndex != 0) token must NOT
	// recover; the quorum seam rejects it (InsufficientShares) and returns zero material.
	[Fact]
	public async Task Reject_a_single_token_recovery_attempt()
	{
		StubEncryptRoundTrip();

		var sut = CreateService();
		await sut.BackupKeyAsync("key-1", new byte[] { 10 }, null, CancellationToken.None).ConfigureAwait(false);
		var tokens = await sut.GenerateRecoveryTokensAsync(
			"key-1", 5, 3, TimeSpan.FromHours(24), CancellationToken.None).ConfigureAwait(false);

		await Should.ThrowAsync<KeyEscrowException>(
			() => sut.RecoverKeyAsync("key-1", tokens[0], CancellationToken.None)).ConfigureAwait(false);
	}

	// rzr5zs — fail-closed below M: fewer than threshold shares cannot even be combined (blocked at
	// construction), so an under-quorum recovery is impossible to attempt.
	[Fact]
	public async Task Fail_to_combine_fewer_than_threshold_shares()
	{
		StubEncryptRoundTrip();

		var sut = CreateService();
		await sut.BackupKeyAsync("key-1", new byte[] { 10 }, null, CancellationToken.None).ConfigureAwait(false);
		var tokens = await sut.GenerateRecoveryTokensAsync(
			"key-1", 5, 3, TimeSpan.FromHours(24), CancellationToken.None).ConfigureAwait(false);

		_ = Should.Throw<ArgumentException>(() => RecoveryToken.Combine(tokens.Take(2)));
	}

	// rzr5zs — forged share at full count: a tampered share reconstructs the WRONG secret, which fails the
	// server-side SHA-256 commitment check → recovery throws with no key (the fabricated-share bypass).
	[Fact]
	public async Task Reject_a_forged_share_even_at_full_quorum_count()
	{
		StubEncryptRoundTrip();

		var sut = CreateService();
		await sut.BackupKeyAsync("key-1", new byte[] { 10 }, null, CancellationToken.None).ConfigureAwait(false);
		var tokens = await sut.GenerateRecoveryTokensAsync(
			"key-1", 5, 3, TimeSpan.FromHours(24), CancellationToken.None).ConfigureAwait(false);

		var shares = tokens.Take(3).ToList();
		var tampered = shares[0].ShareData.ToArray();
		tampered[0] ^= 0xFF; // flip a byte of one share
		shares[0] = shares[0] with { ShareData = tampered };
		var quorum = RecoveryToken.Combine(shares);

		await Should.ThrowAsync<KeyEscrowException>(
			() => sut.RecoverKeyAsync("key-1", quorum, CancellationToken.None)).ConfigureAwait(false);
	}

	// Identity round-trip fake: Encrypt(x) -> {Ciphertext = x}; Decrypt({Ciphertext = x}) -> x. The e6batc
	// envelope master-wraps the DEK, then on generate/recover master-UNWRAPS it to re-wrap under the quorum
	// KEK, so the master layer MUST round-trip for the inner AES-GCM (real crypto in QuorumRecoverySeam) to
	// recover the DEK. A fixed-value stub loses the payload and cannot round-trip the two-layer envelope.
	private void StubEncryptRoundTrip()
	{
		// Return fresh COPIES on both sides — a real provider never aliases the caller's buffer. The escrow
		// service zeroes its transient plaintext/DEK after wrapping, so aliasing the stored ciphertext to that
		// buffer would corrupt it to zeros. Copying mimics real crypto and keeps the round-trip intact.
		A.CallTo(() => _encryptionProvider.EncryptAsync(A<byte[]>._, A<EncryptionContext>._, A<CancellationToken>._))
			.ReturnsLazily((byte[] plaintext, EncryptionContext _, CancellationToken _) => new EncryptedData
			{
				Ciphertext = plaintext.ToArray(),
				KeyId = "escrow-master-key",
				KeyVersion = 1,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				Iv = [4, 5, 6]
			});
		A.CallTo(() => _encryptionProvider.DecryptAsync(A<EncryptedData>._, A<EncryptionContext>._, A<CancellationToken>._))
			.ReturnsLazily((EncryptedData ed, EncryptionContext _, CancellationToken _) => ed.Ciphertext.ToArray());
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
	public async Task Generate_recovery_tokens_with_csprng_share_material()
	{
		// Regression lock (fxn2z3): recovery-share material MUST come from a CSPRNG
		// (RandomNumberGenerator), NOT Guid.NewGuid().ToByteArray() (non-cryptographic, 16 bytes).
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

		foreach (var token in tokens)
		{
			// REAL Shamir share (rzr5zs): 37-byte header + 32-byte secret payload = 69 bytes — RED on the
			// pre-fix bare 32-byte CSPRNG fake-share (and the earlier 16-byte Guid material).
			token.ShareData.Length.ShouldBe(69);
			// Has entropy — a real share is never an all-zero block in practice.
			token.ShareData.ShouldContain(b => b != 0);
		}

		// Every custodian's share is independently random — no two shares collide.
		tokens.Select(t => Convert.ToBase64String(t.ShareData)).Distinct().Count().ShouldBe(tokens.Length);
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
			new KeyEscrowBackupService(null!, _logger));
	}

	[Fact]
	public void Throw_for_null_logger()
	{
		Should.Throw<ArgumentNullException>(() =>
			new KeyEscrowBackupService(_encryptionProvider, null!));
	}

	private KeyEscrowBackupService CreateService() =>
		new(_encryptionProvider, _logger);
}
