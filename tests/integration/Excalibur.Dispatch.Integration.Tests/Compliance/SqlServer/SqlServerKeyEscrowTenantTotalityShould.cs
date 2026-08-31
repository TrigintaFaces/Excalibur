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
/// Locks the TOTALITY of <c>KeyEscrow.TenantId</c> — and, more importantly, locks the invariant that
/// making it total did not make an untenanted key unrecoverable.
/// </summary>
/// <remarks>
/// <para>
/// This column is not a query predicate anywhere in the escrow path. It is AEAD ASSOCIATED DATA. The
/// service hashes the tenant term into the AAD when it encrypts the key, and at recovery it feeds the
/// term back into the decryption context <em>from what this column holds</em>. So the value written
/// into the column and the value hashed at encryption time must be the same bytes, or the key does not
/// come back — at the one moment escrow exists to survive.
/// </para>
/// <para>
/// A null tenant and the reserved sentinel produce DIFFERENT AAD: the provider length-prefixes the
/// term, so absent is a zero-length field and the sentinel is a fourteen-byte one. That is precisely
/// why this suite exists. Normalising the term at the write boundary is safe only while BOTH the
/// column and the encryption context are fed from the same value, and the arm below that round-trips
/// an untenanted key is the only thing that can detect them drifting apart.
/// </para>
/// <para>
/// THE ENCRYPTION PROVIDER IS REAL, AND THAT IS THE WHOLE POINT. Every sibling escrow suite fakes
/// <see cref="IEncryptionProvider"/> — reasonably, since it is a consumer-supplied component and not
/// their subject. But a faked provider does not compute an AAD, so it returns the plaintext whatever
/// tenant term it is handed, and every one of those suites would stay green while a real consumer's
/// key became permanently unrecoverable. A mock cannot fail the way this must be able to fail.
/// </para>
/// <para>
/// Real SQL Server via TestContainers, provisioned from the DDL the package actually SHIPS, and never
/// skip-gated: whether a <c>DEFAULT</c> fires on an omitted column and whether the column refuses a
/// NULL are answered by the server and by nothing else.
/// </para>
/// <para>
/// The upgrade arms live in a sibling suite, and the reasoning that once said they could not exist is
/// corrected here because it was half right in a way that matters. Rewriting a stored tenant term DOES
/// invalidate the associated data of every row written under the old one, so no SQL script may backfill
/// this column — that half stands, and it is why the shipped migration deliberately does not. But "none
/// is possible" does not follow. Absent and empty are the SAME associated data, so closing the column to
/// NULL is expressible in SQL and inert; and converging the rest of the way is expressible in code, by
/// decrypting under the old term and re-encrypting under the new one, which the sibling suite performs
/// and then recovers the key from. Repairable, not merely disclosable. What remains true without
/// qualification is that the destructive act is the OBVIOUS one: an UPDATE that sets the sentinel
/// satisfies every schema check and leaves ciphertext nobody can open.
/// </para>
/// </remarks>
[Collection(SqlServerTestCollection.Name)]
[Trait("Category", TestCategories.Integration)]
[Trait("Component", "Compliance")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerKeyEscrowTenantTotalityShould : IAsyncLifetime, IDisposable
{
	private const string Sentinel = "__untenanted__";

	private readonly SqlServerContainerFixture _fixture;
	private InMemoryKeyManagementProvider _keyManagement = null!;
	private SqlServerKeyEscrowService _service = null!;

	public SqlServerKeyEscrowTenantTotalityShould(SqlServerContainerFixture fixture) => _fixture = fixture;

	public async ValueTask InitializeAsync()
	{
		await ShippedKeyEscrowSchema.EnsureCreatedAsync(_fixture.ConnectionString, CancellationToken.None);

		// REAL provider over a real key ring. See the class remarks: a fake computes no AAD, so it
		// cannot reproduce the failure this suite exists to detect.
		_keyManagement = new InMemoryKeyManagementProvider(NullLogger<InMemoryKeyManagementProvider>.Instance);

		var encryptionProvider = new AesGcmEncryptionProvider(
			_keyManagement, NullLogger<AesGcmEncryptionProvider>.Instance);

		// Only the connection string: the object names must be agreed between the shipped script and
		// the service without this test restating either.
		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerKeyEscrowOptions
		{
			ConnectionString = _fixture.ConnectionString,
		});

		_service = new SqlServerKeyEscrowService(
			options, encryptionProvider, NullLogger<SqlServerKeyEscrowService>.Instance);
	}

	public ValueTask DisposeAsync()
	{
		Dispose();
		return default;
	}

	public void Dispose() => _service?.Dispose();

	/// <summary>
	/// THE ARM THAT MATTERS: a key escrowed with no tenant still recovers, byte for byte, now that the
	/// stored term is the sentinel rather than NULL.
	/// </summary>
	/// <remarks>
	/// This is the arm that goes RED if the encryption context and the stored column are ever fed from
	/// different values. The failure is not subtle at the seam and completely silent at the surface:
	/// <c>BackupKeyAsync</c> succeeds, the row looks right, and recovery throws a tag mismatch — or, in
	/// the earlier shape, the write simply stored one spelling and hashed another.
	/// <para>
	/// The round trip runs through <c>GenerateRecoveryTokensAsync</c> on purpose. That is where the
	/// service master-decrypts the escrowed key using the tenant term READ BACK FROM THE ROW, so it is
	/// the exact point at which a divergence between written and hashed terms becomes observable.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task RecoverAnUntenantedKeyWhoseStoredTenantTermIsNowTheSentinel()
	{
		RequireDocker();

		var keyId = $"untenanted-{Guid.NewGuid():N}";
		await ProvisionMasterKeysAsync(keyId);
		var keyMaterial = RandomNumberGenerator.GetBytes(32);

		// No EscrowOptions at all: the untenanted path a single-tenant consumer takes.
		_ = await _service.BackupKeyAsync(keyId, keyMaterial, null, CancellationToken.None);

		var stored = await ScalarAsync<string>(
			"SELECT TenantId FROM [compliance].[KeyEscrow] WHERE KeyId = @KeyId", new { KeyId = keyId });

		stored.ShouldBe(
			Sentinel,
			"an escrow written without a tenant must store the untenanted PARTITION, not an absent value");

		var tokens = await _service.GenerateRecoveryTokensAsync(
			keyId, custodianCount: 5, threshold: 3, null, CancellationToken.None);

		var recovered = await _service.RecoverKeyAsync(
			keyId, RecoveryToken.Combine(tokens.Take(3)), CancellationToken.None);

		recovered.ToArray().ShouldBe(
			keyMaterial,
			"the tenant term is AEAD associated data: if the value stored in the column and the value "
			+ "hashed into the AAD at encryption time ever diverge, the key is unrecoverable — which is "
			+ "the one failure escrow cannot survive");
	}

	/// <summary>
	/// LIVENESS: a real tenant is stored verbatim, is not absorbed by the default, and still recovers.
	/// </summary>
	/// <remarks>
	/// Paired with the arm above deliberately. A write path that stamped EVERY escrow with the sentinel
	/// would satisfy "no row holds NULL" and would round-trip perfectly, while having destroyed tenant
	/// identity — and nothing else in this suite would notice.
	/// </remarks>
	[Fact]
	public async Task StoreARealTenantVerbatimAndStillRecoverIt()
	{
		RequireDocker();

		var keyId = $"tenanted-{Guid.NewGuid():N}";
		await ProvisionMasterKeysAsync(keyId);
		var keyMaterial = RandomNumberGenerator.GetBytes(32);

		_ = await _service.BackupKeyAsync(
			keyId, keyMaterial, new EscrowOptions { TenantId = "acme" }, CancellationToken.None);

		var stored = await ScalarAsync<string>(
			"SELECT TenantId FROM [compliance].[KeyEscrow] WHERE KeyId = @KeyId", new { KeyId = keyId });

		stored.ShouldBe("acme", "a real tenant must survive the write unchanged, not be folded to the sentinel");

		var tokens = await _service.GenerateRecoveryTokensAsync(
			keyId, custodianCount: 3, threshold: 2, null, CancellationToken.None);

		var recovered = await _service.RecoverKeyAsync(
			keyId, RecoveryToken.Combine(tokens.Take(2)), CancellationToken.None);

		recovered.ToArray().ShouldBe(keyMaterial, "a tenanted escrow must round-trip exactly as an untenanted one does");
	}

	/// <summary>
	/// FRESH INSTALL: a hand-written INSERT that omits the tenant column gets the sentinel from the
	/// DEFAULT rather than NULL.
	/// </summary>
	/// <remarks>
	/// The service always binds the term explicitly, so the DEFAULT is a backstop for exactly this
	/// case — a consumer, a support script, or a data fix writing the table directly. Without it the
	/// column would be total only for writers who already knew to make it so.
	/// </remarks>
	[Fact]
	public async Task DefaultAnOmittedTenantColumnToTheSentinel()
	{
		RequireDocker();

		var keyId = $"omitted-{Guid.NewGuid():N}";
		await using var connection = new SqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(CancellationToken.None);

		_ = await connection.ExecuteAsync(
			"""
			INSERT INTO [compliance].[KeyEscrow]
				(EscrowId, KeyId, EncryptedKey, KeyHash, Algorithm, Iv, MasterKeyId, MasterKeyVersion,
				 State, EscrowedAt)
			VALUES
				(@EscrowId, @KeyId, @EncryptedKey, @KeyHash, @Algorithm, @Iv, @MasterKeyId, @MasterKeyVersion,
				 @State, @EscrowedAt)
			""",
			NewEscrowRow(keyId));

		var stored = await ScalarAsync<string>(
			"SELECT TenantId FROM [compliance].[KeyEscrow] WHERE KeyId = @KeyId", new { KeyId = keyId });

		stored.ShouldBe(Sentinel, "an omitted tenant column must default to the untenanted term, not to NULL");
	}

	/// <summary>
	/// FRESH INSTALL: the column actually refuses NULL rather than merely being described as closed.
	/// </summary>
	/// <remarks>
	/// Asserting the catalog flag alone would pass against a column whose constraint was not in force.
	/// This drives a real INSERT and requires the server to reject it — and the rejection is the loud
	/// failure that replaces the silent one, since a NULL here would be a tenant term no recovery could
	/// reproduce.
	/// </remarks>
	[Fact]
	public async Task RefuseAnExplicitNullTenant()
	{
		RequireDocker();

		await using var connection = new SqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(CancellationToken.None);

		var write = async () => await connection.ExecuteAsync(
			"""
			INSERT INTO [compliance].[KeyEscrow]
				(EscrowId, KeyId, EncryptedKey, KeyHash, Algorithm, Iv, MasterKeyId, MasterKeyVersion,
				 State, EscrowedAt, TenantId)
			VALUES
				(@EscrowId, @KeyId, @EncryptedKey, @KeyHash, @Algorithm, @Iv, @MasterKeyId, @MasterKeyVersion,
				 @State, @EscrowedAt, NULL)
			""",
			NewEscrowRow($"explicit-null-{Guid.NewGuid():N}"));

		_ = await write.ShouldThrowAsync<SqlException>(
			"an explicit NULL tenant must be refused by the database, not silently accepted");
	}

	/// <summary>
	/// The column is closed in the catalog, and keeps the binary collation.
	/// </summary>
	/// <remarks>
	/// The collation is not incidental. Under the server's usual case-insensitive default, a predicate
	/// later added over this column would treat <c>'Acme'</c> and <c>'acme'</c> as one tenant. It is
	/// asserted here so that a future ALTER which drops it fails loudly rather than silently widening
	/// the term.
	/// </remarks>
	[Fact]
	public async Task DeclareTheTenantColumnNotNullWithABinaryCollation()
	{
		RequireDocker();

		var isNullable = await ScalarAsync<bool>(
			"""
			SELECT c.is_nullable FROM sys.columns c
			WHERE c.object_id = OBJECT_ID(N'[compliance].[KeyEscrow]') AND c.name = N'TenantId'
			""",
			null);

		isNullable.ShouldBeFalse("the shipped schema must not be able to represent a NULL tenant at all");

		var collation = await ScalarAsync<string>(
			"""
			SELECT c.collation_name FROM sys.columns c
			WHERE c.object_id = OBJECT_ID(N'[compliance].[KeyEscrow]') AND c.name = N'TenantId'
			""",
			null);

		collation.ShouldBe(
			"Latin1_General_BIN2",
			"losing the binary collation would make 'Acme' and 'acme' the same tenant");
	}

	/// <summary>
	/// Creates the master keys the escrow path resolves, for both of the purposes it uses.
	/// </summary>
	/// <remarks>
	/// The escrow service scopes its master key per escrowed key — <c>key-escrow:{id}</c> for the key
	/// itself and <c>key-escrow-envelope:{id}</c> for the quorum envelope — and the provider resolves a
	/// key by ACTIVE-for-purpose. The in-memory key ring auto-generates only its default key, which has a
	/// different purpose, so both must be created explicitly or encryption fails before the tenant term is
	/// ever reached. Rotation is the creation path on this interface: there is no CreateKey.
	/// </remarks>
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
			"this lock asserts a property of the SHIPPED SCHEMA and of real AES-GCM associated data, and is "
			+ "deliberately never skipped — a green run that never reached a database would certify nothing.");

	private static object NewEscrowRow(string keyId) => new
	{
		EscrowId = Guid.NewGuid().ToString("N"),
		KeyId = keyId,
		EncryptedKey = new byte[] { 1, 2, 3, 4 },
		KeyHash = new string('a', 64),
		Algorithm = 1,
		Iv = new byte[] { 5, 6, 7, 8 },
		MasterKeyId = "master",
		MasterKeyVersion = 1,
		State = 0,
		EscrowedAt = DateTimeOffset.UtcNow,
	};

	private async Task<T> ScalarAsync<T>(string sql, object? parameters)
	{
		await using var connection = new SqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(CancellationToken.None);
		return await connection.ExecuteScalarAsync<T>(sql, parameters);
	}
}
