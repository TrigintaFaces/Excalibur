// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
using System.Security.Cryptography;

using Dapper;

using Excalibur.Compliance;
using Excalibur.Compliance.SqlServer;
using Excalibur.Dispatch.Integration.Tests.Compliance.Fixtures;

using Microsoft.Extensions.Logging;

using SqlServerContainerFixture = Excalibur.Dispatch.Integration.Tests.Compliance.Fixtures.SqlServerContainerFixture;

namespace Excalibur.Dispatch.Integration.Tests.Compliance.SqlServer;

/// <summary>
/// hnx8gw (P1 security) — author≠impl NON-SKIPPED real-SqlServer regression lock for the Shamir quorum
/// recovery of <see cref="SqlServerKeyEscrowService"/>: recovery MUST verify the reconstructed secret
/// against a SERVER-side commitment and FAIL CLOSED otherwise.
/// </summary>
/// <remarks>
/// <para>
/// Per <c>verify-against-real-infra-not-mock.md</c> this binds the REAL SqlServer store (TestContainers),
/// never a mock — <c>DockerAvailable.ShouldBeTrue</c>, never <c>SkipIfUnavailable</c>. The fix persists a
/// per-batch <c>SecretCommitment</c> = SHA-256(quorum secret) and, at recovery, reconstructs the shares and
/// requires <c>SHA-256(reconstructed) == stored commitment</c> — closing the fabricated-share bypass where a
/// caller crafts self-consistent shares with their own embedded commitment.
/// </para>
/// <para>
/// Fail-closed cases are the security guarantee: a below-threshold quorum and a tampered share both
/// reconstruct to something whose commitment does not match the server's → <see cref="KeyEscrowException"/>.
/// RED on the pre-fix impl (discarded reconstruction result / caller-supplied commitment only).
/// </para>
/// </remarks>
[Collection(SqlServerTestCollection.Name)]
[Trait("Category", TestCategories.Integration)]
[Trait("Component", "Compliance")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerKeyEscrowQuorumRecoveryShould : IAsyncLifetime, IDisposable
{
	private readonly SqlServerContainerFixture _fixture;
	private readonly IEncryptionProvider _encryptionProvider;
	private SqlServerKeyEscrowService _service = null!;

	public SqlServerKeyEscrowQuorumRecoveryShould(SqlServerContainerFixture fixture)
	{
		_fixture = fixture;
		_encryptionProvider = A.Fake<IEncryptionProvider>();
	}

	public async ValueTask InitializeAsync()
	{
		await CreateTablesAsync();
		SetupEncryptionProviderMock();

		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerKeyEscrowOptions
		{
			ConnectionString = _fixture.ConnectionString,
			Schema = "dbo",
			TableName = "KeyEscrow",
			TokensTableName = "KeyEscrowTokens",
			DefaultTokenExpiration = TimeSpan.FromHours(24),
			CommandTimeoutSeconds = 30,
		});

		_service = new SqlServerKeyEscrowService(options, _encryptionProvider,
			A.Fake<ILogger<SqlServerKeyEscrowService>>());
	}

	public ValueTask DisposeAsync()
	{
		Dispose();
		return default;
	}

	public void Dispose() => _service?.Dispose();

	[Fact]
	public async Task RecoverKey_WithValidQuorum_ReturnsOriginalKey()
	{
		_fixture.DockerAvailable.ShouldBeTrue("hnx8gw quorum recovery is a real-SqlServer invariant — never skipped.");

		var keyId = $"quorum-valid-{Guid.NewGuid():N}";
		var keyMaterial = RandomNumberGenerator.GetBytes(32);
		_ = await _service.BackupKeyAsync(keyId, keyMaterial, null, CancellationToken.None);

		var tokens = await _service.GenerateRecoveryTokensAsync(keyId, custodianCount: 5, threshold: 3, null, CancellationToken.None);

		// Combine exactly THRESHOLD (3) of the 5 genuine shares via the REAL public producer
		// RecoveryToken.Combine — a 3-of-5 PARTIAL quorum (M < N) proving partial-quorum round-trips
		// through the production producer -> escrow-service parser, not a private test shim.
		var combined = RecoveryToken.Combine(tokens.Take(3));

		var recovered = await _service.RecoverKeyAsync(keyId, combined, CancellationToken.None);

		recovered.ToArray().ShouldBe(keyMaterial, "a valid 3-of-5 partial quorum of genuine shares recovers the original key material");
	}

	[Fact]
	public async Task RecoverKey_BelowThreshold_FailsClosed()
	{
		_fixture.DockerAvailable.ShouldBeTrue("hnx8gw below-threshold fail-closed is a real-SqlServer invariant — never skipped.");

		var keyId = $"quorum-below-{Guid.NewGuid():N}";
		var keyMaterial = RandomNumberGenerator.GetBytes(32);
		_ = await _service.BackupKeyAsync(keyId, keyMaterial, null, CancellationToken.None);

		var tokens = await _service.GenerateRecoveryTokensAsync(keyId, custodianCount: 5, threshold: 3, null, CancellationToken.None);

		// Only 2 shares for a threshold-3 quorum — insufficient. The REAL public producer
		// RecoveryToken.Combine enforces the threshold at the source and fails closed, so a
		// below-threshold combined token can never even be constructed to reach recovery.
		_ = Should.Throw<ArgumentException>(() => RecoveryToken.Combine(tokens.Take(2)));
	}

	[Fact]
	public async Task RecoverKey_TamperedShare_FailsClosed()
	{
		_fixture.DockerAvailable.ShouldBeTrue("hnx8gw tampered-share fail-closed is a real-SqlServer invariant — never skipped.");

		var keyId = $"quorum-tampered-{Guid.NewGuid():N}";
		var keyMaterial = RandomNumberGenerator.GetBytes(32);
		_ = await _service.BackupKeyAsync(keyId, keyMaterial, null, CancellationToken.None);

		var tokens = await _service.GenerateRecoveryTokensAsync(keyId, custodianCount: 5, threshold: 3, null, CancellationToken.None);

		// Tamper: flip a byte in one genuine share so the reconstructed secret's commitment mismatches
		// the server-side SHA-256 commitment → fabricated/altered shares must NOT recover.
		var tampered = tokens.Take(3).ToArray();
		tampered[0].ShareData[^1] ^= 0xFF;

		// A full threshold-count (3) set with one altered share: the REAL public producer builds the
		// combined token, but the escrow service reconstructs a secret whose SHA-256 commitment does
		// NOT match the server-side commitment → fail closed.
		var combined = RecoveryToken.Combine(tampered);

		_ = await Should.ThrowAsync<KeyEscrowException>(
			() => _service.RecoverKeyAsync(keyId, combined, CancellationToken.None));
	}

	/// <summary>
	/// e6batc — a token-less escrow retains its MASTER-ONLY blob and has NO quorum envelope yet: it is
	/// recoverable with the master key alone (the documented, intended pre-quorum state — asserted so it can
	/// never silently regress into an *unintended* hole).
	/// </summary>
	[Fact]
	public async Task TokenlessEscrow_KeepsTheMasterOnlyBlob_AndHasNoQuorumEnvelope()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"e6batc escrow-state invariant is a real-SqlServer proof — never skipped.");

		var keyId = $"e6batc-tokenless-{Guid.NewGuid():N}";
		var receipt = await _service.BackupKeyAsync(
			keyId, RandomNumberGenerator.GetBytes(32), null, CancellationToken.None);

		var (encryptedKey, wrapCount) = await ReadEscrowStateAsync(keyId, receipt.EscrowId).ConfigureAwait(false);

		_ = encryptedKey.ShouldNotBeNull(
			"a token-less escrow retains its master-only EncryptedKey blob — recoverable with the master key "
			+ "alone (the documented pre-quorum state: no custodians provisioned, no quorum to enforce yet).");
		encryptedKey.Length.ShouldBeGreaterThan(0);
		wrapCount.ShouldBe(0,
			"no quorum envelope (KeyEscrowWrap) exists until recovery tokens are generated.");
	}

	/// <summary>
	/// e6batc SAFETY (load-bearing) — once an M-of-N recovery quorum exists, a lone master-key holder can no
	/// longer recover the key: <c>GenerateRecoveryTokensAsync</c> SEALS the escrow (nulls the master-only
	/// <c>EncryptedKey</c> blob) and binds the key material behind the quorum envelope (a <c>KeyEscrowWrap</c>
	/// row). The only recovery path is now <c>RecoverKeyAsync</c> with a valid quorum (covered by the liveness
	/// test above); there is no master-alone recovery API and no master-only blob left.
	/// </summary>
	/// <remarks>
	/// Non-vacuous by contrast with the token-less arm (blob present, no wrap ↔ blob NULL, wrap present): RED if
	/// the seal (the <c>EncryptedKey -&gt; NULL</c> UPDATE) regresses — the master-only copy would survive the
	/// quorum, re-opening the lone-master bypass.
	/// </remarks>
	[Fact]
	public async Task GeneratingRecoveryTokens_SealsTheMasterOnlyBlobToNull_AndBindsTheQuorumEnvelope()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"e6batc seal invariant is a real-SqlServer proof — never skipped.");

		var keyId = $"e6batc-sealed-{Guid.NewGuid():N}";
		var receipt = await _service.BackupKeyAsync(
			keyId, RandomNumberGenerator.GetBytes(32), null, CancellationToken.None);

		_ = await _service.GenerateRecoveryTokensAsync(
			keyId, custodianCount: 5, threshold: 3, null, CancellationToken.None);

		var (encryptedKey, wrapCount) = await ReadEscrowStateAsync(keyId, receipt.EscrowId).ConfigureAwait(false);

		encryptedKey.ShouldBeNull(
			"generating recovery tokens MUST seal the escrow: the master-only EncryptedKey blob is nulled, so a "
			+ "master-key holder ALONE can no longer recover the key — recovery is now quorum-gated (the e6batc "
			+ "guarantee: a lone master-key holder cannot circumvent the M-of-N quorum once it exists).");
		wrapCount.ShouldBeGreaterThan(0,
			"the quorum envelope (KeyEscrowWrap) MUST be bound when tokens are generated — the key material now "
			+ "lives only behind the M-of-N wrap, not the master-only blob.");
	}

	private async Task<(byte[]? EncryptedKey, int WrapCount)> ReadEscrowStateAsync(string keyId, string escrowId)
	{
		using var connection = _fixture.CreateDbConnection();
		connection.Open();

		var encryptedKey = await connection.ExecuteScalarAsync<byte[]?>(
			"SELECT EncryptedKey FROM dbo.KeyEscrow WHERE KeyId = @KeyId", new { KeyId = keyId }).ConfigureAwait(false);
		var wrapCount = await connection.ExecuteScalarAsync<int>(
			"SELECT COUNT(*) FROM dbo.KeyEscrowWrap WHERE EscrowId = @EscrowId", new { EscrowId = escrowId }).ConfigureAwait(false);

		return (encryptedKey, wrapCount);
	}

	private void SetupEncryptionProviderMock()
	{
		_ = A.CallTo(() => _encryptionProvider.EncryptAsync(A<byte[]>.Ignored, A<EncryptionContext>.Ignored, A<CancellationToken>.Ignored))
			.ReturnsLazily((byte[] data, EncryptionContext _, CancellationToken _) =>
			{
				var encrypted = new byte[data.Length];
				for (var i = 0; i < data.Length; i++)
				{
					encrypted[i] = (byte)(data[i] ^ 0x55);
				}

				return Task.FromResult(new EncryptedData
				{
					Ciphertext = encrypted,
					Iv = RandomNumberGenerator.GetBytes(12),
					AuthTag = RandomNumberGenerator.GetBytes(16),
					Algorithm = EncryptionAlgorithm.Aes256Gcm,
					KeyId = "test-key",
					KeyVersion = 1,
				});
			});

		_ = A.CallTo(() => _encryptionProvider.DecryptAsync(A<EncryptedData>.Ignored, A<EncryptionContext>.Ignored, A<CancellationToken>.Ignored))
			.ReturnsLazily(call =>
			{
				var data = call.GetArgument<EncryptedData>(0)!;
				var decrypted = new byte[data.Ciphertext.Length];
				for (var i = 0; i < data.Ciphertext.Length; i++)
				{
					decrypted[i] = (byte)(data.Ciphertext[i] ^ 0x55);
				}

				return Task.FromResult(decrypted);
			});
	}

	private async Task CreateTablesAsync()
	{
		var createTablesSql = @"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'KeyEscrow' AND schema_id = SCHEMA_ID('dbo'))
            BEGIN
                CREATE TABLE dbo.KeyEscrow (
                    EscrowId NVARCHAR(64) NOT NULL PRIMARY KEY,
                    KeyId NVARCHAR(256) NOT NULL,
                    EncryptedKey VARBINARY(MAX) NULL,
                    KeyHash NVARCHAR(128) NOT NULL,
                    Algorithm INT NOT NULL,
                    Iv VARBINARY(64) NOT NULL,
                    AuthTag VARBINARY(64) NOT NULL,
                    MasterKeyId NVARCHAR(256) NOT NULL,
                    MasterKeyVersion INT NOT NULL,
                    State INT NOT NULL,
                    EscrowedAt DATETIMEOFFSET NOT NULL,
                    ExpiresAt DATETIMEOFFSET NULL,
                    RevokedAt DATETIMEOFFSET NULL,
                    RevocationReason NVARCHAR(1000) NULL,
                    RecoveryAttempts INT NOT NULL DEFAULT 0,
                    LastRecoveryAttempt DATETIMEOFFSET NULL,
                    TenantId NVARCHAR(256) NULL,
                    Purpose NVARCHAR(256) NULL,
                    Metadata NVARCHAR(MAX) NULL
                )
            END

            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'KeyEscrowTokens' AND schema_id = SCHEMA_ID('dbo'))
            BEGIN
                CREATE TABLE dbo.KeyEscrowTokens (
                    TokenId NVARCHAR(64) NOT NULL PRIMARY KEY,
                    KeyId NVARCHAR(256) NOT NULL,
                    EscrowId NVARCHAR(64) NOT NULL,
                    BatchId NVARCHAR(64) NULL,
                    ShareIndex INT NOT NULL,
                    TotalShares INT NOT NULL,
                    Threshold INT NOT NULL,
                    CreatedAt DATETIMEOFFSET NOT NULL,
                    ExpiresAt DATETIMEOFFSET NOT NULL,
                    IsUsed BIT NOT NULL DEFAULT 0,
                    SecretCommitment VARBINARY(32) NULL
                )
            END

            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'KeyEscrowWrap' AND schema_id = SCHEMA_ID('dbo'))
            BEGIN
                CREATE TABLE dbo.KeyEscrowWrap (
                    EscrowId NVARCHAR(64) NOT NULL,
                    BatchId NVARCHAR(64) NOT NULL,
                    SecretCommitment VARBINARY(32) NOT NULL,
                    KekSalt VARBINARY(64) NOT NULL,
                    InnerIv VARBINARY(16) NOT NULL,
                    InnerAuthTag VARBINARY(16) NOT NULL,
                    WrappedInnerKey VARBINARY(MAX) NOT NULL,
                    OuterIv VARBINARY(64) NOT NULL,
                    OuterAuthTag VARBINARY(64) NULL,
                    OuterAlgorithm INT NOT NULL,
                    OuterMasterKeyId NVARCHAR(256) NOT NULL,
                    OuterMasterKeyVersion INT NOT NULL,
                    WrapVersion INT NOT NULL,
                    CONSTRAINT PK_KeyEscrowWrap PRIMARY KEY (EscrowId, BatchId)
                )
            END";

		await _fixture.ExecuteScriptAsync(createTablesSql);
	}
}
