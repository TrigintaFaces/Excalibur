// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
using System.Security.Cryptography;

using Dapper;

using Excalibur.Compliance;
using Excalibur.Compliance.Encryption;
using Excalibur.Compliance.SqlServer;
using Excalibur.Dispatch.Integration.Tests.Compliance.Fixtures;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

using SqlServerContainerFixture = Excalibur.Dispatch.Integration.Tests.Compliance.Fixtures.SqlServerContainerFixture;

namespace Excalibur.Dispatch.Integration.Tests.Compliance.SqlServer;

/// <summary>
/// What happens to an escrow row that was written BEFORE the stored tenant term became total, when the
/// table it lives in is upgraded.
/// </summary>
/// <remarks>
/// <para>
/// The stored tenant term is AEAD associated data, and the recovery path reads it BACK FROM THE ROW:
/// the write normalises through the untenanted partition, but every decrypt site takes the column value
/// raw. So the invariant that actually governs recovery is narrow and easy to state — <b>the value in
/// the column must equal the value the ciphertext was authenticated under</b> — and it has a consequence
/// that is the opposite of the intuitive one.
/// </para>
/// <para>
/// <b>A row written under the old shape is not broken by the new code.</b> It stored its term and
/// authenticated under the same term, and recovery re-reads that same term, so it round-trips. The arm
/// that proves this is the first one below, and it exists because the natural assumption is the reverse.
/// </para>
/// <para>
/// <b>What breaks such a row is the obvious migration.</b> An <c>UPDATE ... SET TenantId = sentinel</c>
/// — the backfill anyone would write to make the column total, and the one a schema-shape gate is
/// satisfied by — changes the associated data out from under a ciphertext that cannot be re-authenticated
/// without the master key. The row still looks correct. Recovery fails with a tag mismatch, at the one
/// moment escrow exists for. That arm is the reason this suite is worth its runtime: it makes the
/// destructive fix fail here instead of on a consumer's database.
/// </para>
/// <para>
/// Two further arms establish what a SAFE migration may do. Absent and empty produce IDENTICAL
/// associated data — the provider length-prefixes the term, so both are a zero-length field — which
/// makes <c>NULL</c> to <c>''</c> a pure-SQL, AAD-neutral step that closes the nullability half with no
/// crypto at all. Converging the rest of the way to the sentinel requires decrypting under the old term
/// and re-encrypting under the new one, which is not expressible in SQL but is perfectly expressible in
/// code — the last arm performs exactly that and recovers the key afterwards.
/// </para>
/// <para>
/// THE ENCRYPTION PROVIDER IS REAL, AND THAT IS THE WHOLE POINT. A faked provider computes no associated
/// data, so it returns the plaintext whatever term it is handed: every arm here would pass against a
/// mock while a real consumer's key was unrecoverable. A mock cannot fail the way this must be able to
/// fail. Real SQL Server via TestContainers, never skip-gated — whether a tag verifies is answered by
/// the algorithm and by nothing else.
/// </para>
/// </remarks>
[Collection(SqlServerTestCollection.Name)]
[Trait("Category", TestCategories.Integration)]
[Trait("Component", "Compliance")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerKeyEscrowUpgradeShould : IAsyncLifetime, IDisposable
{
	private const string Sentinel = "__untenanted__";

	private readonly SqlServerContainerFixture _fixture;
	private string _connectionString = null!;
	private InMemoryKeyManagementProvider _keyManagement = null!;
	private AesGcmEncryptionProvider _encryptionProvider = null!;
	private SqlServerKeyEscrowService _service = null!;

	public SqlServerKeyEscrowUpgradeShould(SqlServerContainerFixture fixture) => _fixture = fixture;

	public async ValueTask InitializeAsync()
	{
		// Its OWN database on the shared container. These arms deliberately reopen the tenant column to
		// NULL, and a sibling suite in this same collection asserts that the shipped column REFUSES NULL.
		// Mutating the shared table would make the two suites' verdicts depend on execution order, which
		// is the flakiness that gets a suite deleted rather than fixed. A separate catalogue also means
		// the shipped script runs here completely unmodified, so nothing about the shape is invented.
		_connectionString = await CreateIsolatedDatabaseAsync();

		await ShippedKeyEscrowSchema.EnsureCreatedAsync(_connectionString, CancellationToken.None);
		await RestoreTheLegacyColumnShapeAsync();

		_keyManagement = new InMemoryKeyManagementProvider(NullLogger<InMemoryKeyManagementProvider>.Instance);
		_encryptionProvider = new AesGcmEncryptionProvider(
			_keyManagement, NullLogger<AesGcmEncryptionProvider>.Instance);

		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerKeyEscrowOptions
		{
			ConnectionString = _connectionString,
		});

		_service = new SqlServerKeyEscrowService(
			options, _encryptionProvider, NullLogger<SqlServerKeyEscrowService>.Instance);
	}

	public ValueTask DisposeAsync()
	{
		Dispose();
		return default;
	}

	public void Dispose()
	{
		_service?.Dispose();
		_encryptionProvider?.Dispose();
	}

	/// <summary>
	/// LIVENESS, and the fact that decides how severe this defect actually is: a row written under the
	/// old shape still recovers under the new code, untouched.
	/// </summary>
	/// <remarks>
	/// This is the arm that would be forgotten, because the intuitive reading is that the code change
	/// broke these rows. It did not. Both the write and every decrypt derive the term from the same
	/// place — the column — so a legacy row is internally consistent and stays that way for as long as
	/// nobody rewrites the column. Without this arm, the destructive-migration arm below would look
	/// like it was merely failing to fix something already broken, rather than causing the break.
	/// </remarks>
	[Fact]
	public async Task RecoverALegacyRowWhoseStoredTenantTermIsStillNull()
	{
		RequireDocker();

		var keyId = $"legacy-untouched-{Guid.NewGuid():N}";
		await ProvisionMasterKeysAsync(keyId);
		var keyMaterial = await InsertLegacyEscrowRowAsync(keyId, legacyTerm: null);

		var recovered = await RecoverAsync(keyId);

		recovered.ShouldBe(
			keyMaterial,
			"a row written before the term became total authenticated under the same value it stored, and "
			+ "recovery re-reads that value — so the code change alone cannot have broken it");
	}

	/// <summary>
	/// SAFETY, and the finding: the obvious backfill is the data-loss event.
	/// </summary>
	/// <remarks>
	/// <c>UPDATE ... SET TenantId = sentinel</c> is what closes the nullability gap in every other table,
	/// what a reviewer would ask for, and what a schema-shape gate is satisfied by. Here it silently
	/// invalidates the associated data of every row it touches. The row afterwards is indistinguishable
	/// from a correct one by inspection: the column holds the right value, the ciphertext is intact, and
	/// the key is gone.
	/// </remarks>
	[Fact]
	public async Task RefuseToRecoverAfterTheTenantTermIsBackfilledToTheSentinel()
	{
		RequireDocker();

		var keyId = $"legacy-backfilled-{Guid.NewGuid():N}";
		await ProvisionMasterKeysAsync(keyId);
		_ = await InsertLegacyEscrowRowAsync(keyId, legacyTerm: null);

		// The migration anyone would write, and the one this suite exists to stop.
		await ExecuteAsync(
			"UPDATE [compliance].[KeyEscrow] SET TenantId = @Sentinel WHERE KeyId = @KeyId",
			new { Sentinel, KeyId = keyId });

		var recover = async () => await RecoverAsync(keyId);

		// The TYPE and the CODE are both asserted, and that is deliberate. A bare Exception assertion
		// here would be satisfied by a missing table, a bad connection string, or any setup mistake --
		// it would pass while proving nothing about associated data. Only an authentication failure
		// says the tag was checked and did not verify, which is the mechanism this arm is about.
		var failure = await recover.ShouldThrowAsync<EncryptionException>(
			"rewriting the stored term changes the associated data the ciphertext was authenticated "
			+ "under, so the key can no longer be produced — a backfill is not a migration here");

		failure.ErrorCode.ShouldBe(
			EncryptionErrorCode.AuthenticationFailed,
			"the failure must be a tag mismatch specifically: any other error would mean this arm went "
			+ "red for a reason that has nothing to do with the defect");
	}

	/// <summary>
	/// LIVENESS: absent and empty are the SAME associated data, so <c>NULL</c> to <c>''</c> is safe.
	/// </summary>
	/// <remarks>
	/// The provider writes the term as a length-prefixed field and omits the bytes when the length is
	/// zero, so a null term and an empty one are byte-identical in the associated data. That makes the
	/// nullability half of this migration expressible in pure SQL with no risk at all — which is worth
	/// knowing, because it is the half a NOT NULL constraint actually needs. It does not reach the
	/// sentinel, and this arm deliberately does not pretend otherwise.
	/// </remarks>
	[Fact]
	public async Task RecoverAfterTheTenantTermIsNormalisedFromNullToEmpty()
	{
		RequireDocker();

		var keyId = $"legacy-emptied-{Guid.NewGuid():N}";
		await ProvisionMasterKeysAsync(keyId);
		var keyMaterial = await InsertLegacyEscrowRowAsync(keyId, legacyTerm: null);

		await ExecuteAsync(
			"UPDATE [compliance].[KeyEscrow] SET TenantId = N'' WHERE KeyId = @KeyId",
			new { KeyId = keyId });

		var recovered = await RecoverAsync(keyId);

		recovered.ShouldBe(
			keyMaterial,
			"absent and empty are the same zero-length field in the associated data, so this rewrite is "
			+ "AAD-neutral and the key survives it");
	}

	/// <summary>
	/// The constructive answer: converging to the sentinel IS possible, by re-authenticating rather than
	/// by rewriting the column alone.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This performs the procedure a real migration tool would: read the row, decrypt under the term the
	/// row currently holds, re-encrypt under the term it should hold, and write the new ciphertext, IV,
	/// tag and term together. It is not expressible as a script — it needs the master key — but it is
	/// entirely expressible in code, which is the distinction that matters when deciding whether this
	/// defect is repairable or only disclosable.
	/// </para>
	/// <para>
	/// Scope stated rather than implied: this covers an OPEN escrow, whose key material is still in
	/// <c>EncryptedKey</c>. Once a token batch is generated the escrow is SEALED — <c>EncryptedKey</c>
	/// becomes NULL and the material lives in per-batch envelope wraps whose outer layer is authenticated
	/// under the same stored term, joined in at recovery. Converging a sealed escrow therefore has to
	/// re-wrap those rows in the same transaction. That is more work, not different work, and it is not
	/// covered here.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task RecoverAfterTheTenantTermIsConvergedByReAuthenticating()
	{
		RequireDocker();

		var keyId = $"legacy-converged-{Guid.NewGuid():N}";
		await ProvisionMasterKeysAsync(keyId);
		var keyMaterial = await InsertLegacyEscrowRowAsync(keyId, legacyTerm: null);

		await ConvergeStoredTenantTermAsync(keyId, toTerm: Sentinel);

		var stored = await ScalarAsync<string>(
			"SELECT TenantId FROM [compliance].[KeyEscrow] WHERE KeyId = @KeyId", new { KeyId = keyId });

		stored.ShouldBe(Sentinel, "the converge must actually land the new term, not merely leave the row recoverable");

		var recovered = await RecoverAsync(keyId);

		recovered.ShouldBe(
			keyMaterial,
			"re-authenticating the ciphertext under the new term converges the column AND keeps the key, "
			+ "which is what makes this repairable rather than only disclosable");
	}

	/// <summary>
	/// THE ARM THAT BINDS THE SHIPPED MIGRATION: running the script the package actually ships against a
	/// table holding a legacy row closes the column AND leaves the key recoverable.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The three arms above establish what is safe and what is not. This one asserts that the file we
	/// hand a consumer does the safe thing — which is a different claim, and the only one that protects
	/// anybody. It runs the shipped script verbatim rather than a restatement of it, so a future edit
	/// that "finishes the job" by backfilling the sentinel turns this red instead of turning a
	/// consumer's escrow into ciphertext nobody can open.
	/// </para>
	/// <para>
	/// Both halves are asserted together on purpose. A migration that refused to run at all would keep
	/// the key recoverable and close nothing, and would pass a recovery-only assertion forever.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task CloseTheColumnAndKeepTheKeyWhenTheShippedMigrationRuns()
	{
		RequireDocker();

		var keyId = $"legacy-migrated-{Guid.NewGuid():N}";
		await ProvisionMasterKeysAsync(keyId);
		var keyMaterial = await InsertLegacyEscrowRowAsync(keyId, legacyTerm: null);

		await ShippedKeyEscrowSchema.ApplyShippedScriptAsync(
			"005_MakeEscrowTenantTotal.sql", _connectionString, CancellationToken.None);

		var isNullable = await ScalarAsync<bool>(
			"""
			SELECT c.is_nullable FROM sys.columns c
			WHERE c.object_id = OBJECT_ID(N'[compliance].[KeyEscrow]') AND c.name = N'TenantId'
			""",
			null);

		isNullable.ShouldBeFalse("the shipped migration must actually close the column, or it has done nothing");

		var stored = await ScalarAsync<string>(
			"SELECT TenantId FROM [compliance].[KeyEscrow] WHERE KeyId = @KeyId", new { KeyId = keyId });

		stored.ShouldBe(
			string.Empty,
			"the legacy row must be moved to the EMPTY term, which is AAD-neutral — moving it to the "
			+ "sentinel would satisfy every schema check and destroy the key");

		var recovered = await RecoverAsync(keyId);

		recovered.ShouldBe(
			keyMaterial,
			"the key escrowed before the upgrade must still come back after the shipped migration has run");
	}

	// -------------------------------------------------------------------------------------------
	// Harness
	// -------------------------------------------------------------------------------------------

	/// <summary>
	/// Puts the shipped table back into the shape it had before the term became total.
	/// </summary>
	/// <remarks>
	/// Exactly the two properties that changed are reverted — the default is dropped and the column is
	/// reopened to NULL — so what these arms run against is the column a consumer provisioning from the
	/// earlier script would hold, rather than an invented one. Everything else about the table, including
	/// the binary collation, is left as shipped because it did not change.
	/// </remarks>
	private async Task RestoreTheLegacyColumnShapeAsync() =>
		await ExecuteAsync(
			"""
			IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = N'DF_KeyEscrow_TenantId')
			BEGIN
				ALTER TABLE [compliance].[KeyEscrow] DROP CONSTRAINT [DF_KeyEscrow_TenantId];
			END
			ALTER TABLE [compliance].[KeyEscrow]
				ALTER COLUMN TenantId NVARCHAR(255) COLLATE Latin1_General_BIN2 NULL;
			""",
			null);

	/// <summary>
	/// Writes a row the way the pre-change service wrote one, and returns the key material it holds.
	/// </summary>
	/// <remarks>
	/// The two lines that matter are replayed verbatim: the caller's term went into the encryption
	/// context RAW, and the same raw value went into the column. Nothing normalised either, which is
	/// precisely why they agreed. Using the real provider here is what makes the row genuinely
	/// authenticated under that term rather than merely labelled with it.
	/// </remarks>
	private async Task<byte[]> InsertLegacyEscrowRowAsync(string keyId, string? legacyTerm)
	{
		var keyMaterial = RandomNumberGenerator.GetBytes(32);

		var context = new EncryptionContext { Purpose = $"key-escrow:{keyId}", TenantId = legacyTerm };
		var encrypted = await _encryptionProvider
			.EncryptAsync(keyMaterial, context, CancellationToken.None)
			.ConfigureAwait(false);

		await ExecuteAsync(
			"""
			INSERT INTO [compliance].[KeyEscrow]
				(EscrowId, KeyId, EncryptedKey, KeyHash, Algorithm, Iv, AuthTag,
				 MasterKeyId, MasterKeyVersion, State, EscrowedAt, TenantId)
			VALUES
				(@EscrowId, @KeyId, @EncryptedKey, @KeyHash, @Algorithm, @Iv, @AuthTag,
				 @MasterKeyId, @MasterKeyVersion, @State, @EscrowedAt, @TenantId)
			""",
			new
			{
				EscrowId = Guid.NewGuid().ToString("N"),
				KeyId = keyId,
				EncryptedKey = encrypted.Ciphertext,
				KeyHash = Convert.ToHexString(SHA256.HashData(keyMaterial)),
				Algorithm = (int)encrypted.Algorithm,
				encrypted.Iv,
				encrypted.AuthTag,
				MasterKeyId = encrypted.KeyId,
				MasterKeyVersion = encrypted.KeyVersion,
				State = 0,
				EscrowedAt = DateTimeOffset.UtcNow,
				TenantId = legacyTerm,
			});

		return keyMaterial;
	}

	/// <summary>
	/// The converge procedure a migration tool would run for one open escrow row.
	/// </summary>
	private async Task ConvergeStoredTenantTermAsync(string keyId, string toTerm)
	{
		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync(CancellationToken.None);

		var row = await connection.QuerySingleAsync<LegacyRow>(
			"""
			SELECT EncryptedKey, Iv, AuthTag, Algorithm, MasterKeyId, MasterKeyVersion, TenantId
			FROM [compliance].[KeyEscrow] WHERE KeyId = @KeyId
			""",
			new { KeyId = keyId });

		var material = await _encryptionProvider.DecryptAsync(
			new EncryptedData
			{
				Ciphertext = row.EncryptedKey!,
				Iv = row.Iv,
				AuthTag = row.AuthTag,
				Algorithm = (EncryptionAlgorithm)row.Algorithm,
				KeyId = row.MasterKeyId,
				KeyVersion = row.MasterKeyVersion,
			},
			new EncryptionContext { Purpose = $"key-escrow:{keyId}", TenantId = row.TenantId },
			CancellationToken.None);

		var reAuthenticated = await _encryptionProvider.EncryptAsync(
			material,
			new EncryptionContext { Purpose = $"key-escrow:{keyId}", TenantId = toTerm },
			CancellationToken.None);

		// Ciphertext, IV, tag, key identity and term move together or not at all: a partial apply here
		// is the same unrecoverable row the naive backfill produces.
		_ = await connection.ExecuteAsync(
			"""
			UPDATE [compliance].[KeyEscrow]
			SET EncryptedKey = @EncryptedKey, Iv = @Iv, AuthTag = @AuthTag, Algorithm = @Algorithm,
				MasterKeyId = @MasterKeyId, MasterKeyVersion = @MasterKeyVersion, TenantId = @TenantId
			WHERE KeyId = @KeyId
			""",
			new
			{
				EncryptedKey = reAuthenticated.Ciphertext,
				reAuthenticated.Iv,
				reAuthenticated.AuthTag,
				Algorithm = (int)reAuthenticated.Algorithm,
				MasterKeyId = reAuthenticated.KeyId,
				MasterKeyVersion = reAuthenticated.KeyVersion,
				TenantId = toTerm,
				KeyId = keyId,
			});
	}

	/// <summary>
	/// Drives recovery through the real service, which master-decrypts using the term READ BACK FROM THE
	/// ROW — the exact point at which a divergence becomes observable.
	/// </summary>
	private async Task<byte[]> RecoverAsync(string keyId)
	{
		var tokens = await _service.GenerateRecoveryTokensAsync(
			keyId, custodianCount: 5, threshold: 3, null, CancellationToken.None);

		var recovered = await _service.RecoverKeyAsync(
			keyId, RecoveryToken.Combine(tokens.Take(3)), CancellationToken.None);

		return recovered.ToArray();
	}

	private async Task ProvisionMasterKeysAsync(string keyId)
	{
		_ = await _keyManagement.RotateKeyAsync(
			$"master-{keyId}", EncryptionAlgorithm.Aes256Gcm, $"key-escrow:{keyId}", null, CancellationToken.None);

		_ = await _keyManagement.RotateKeyAsync(
			$"envelope-{keyId}", EncryptionAlgorithm.Aes256Gcm, $"key-escrow-envelope:{keyId}", null,
			CancellationToken.None);
	}

	private void RequireDocker() =>
		_fixture.DockerAvailable.ShouldBeTrue(
			"these arms assert what real AES-GCM associated data does to a real row across an upgrade, and "
			+ "are deliberately never skipped — a green run that never reached a database would certify nothing.");

	/// <summary>
	/// Creates (idempotently) a catalogue of this suite's own on the shared container.
	/// </summary>
	private async Task<string> CreateIsolatedDatabaseAsync()
	{
		const string Database = "escrow_upgrade_lock";

		await using (var master = new SqlConnection(_fixture.ConnectionString))
		{
			await master.OpenAsync(CancellationToken.None);
			_ = await master.ExecuteAsync(
				$"IF DB_ID(N'{Database}') IS NULL CREATE DATABASE [{Database}];");
		}

		return new SqlConnectionStringBuilder(_fixture.ConnectionString)
		{
			InitialCatalog = Database,
		}.ConnectionString;
	}

	private async Task ExecuteAsync(string sql, object? parameters)
	{
		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync(CancellationToken.None);
		_ = await connection.ExecuteAsync(sql, parameters);
	}

	private async Task<T> ScalarAsync<T>(string sql, object? parameters)
	{
		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync(CancellationToken.None);
		return await connection.ExecuteScalarAsync<T>(sql, parameters);
	}

	private sealed record LegacyRow
	{
		public byte[]? EncryptedKey { get; init; }
		public byte[] Iv { get; init; } = [];
		public byte[]? AuthTag { get; init; }
		public int Algorithm { get; init; }
		public string MasterKeyId { get; init; } = string.Empty;
		public int MasterKeyVersion { get; init; }
		public string? TenantId { get; init; }
	}
}
