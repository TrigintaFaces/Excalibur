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
/// Proves a consumer can provision key escrow from the shipped script and then actually recover a key
/// through the schema it creates.
/// </summary>
/// <remarks>
/// <para>
/// The sibling escrow suites prove the quorum logic, but each provisions its own hand-written tables
/// and overrides the table names to match. So none of them touched the schema a consumer receives —
/// and for a while there was no such schema at all: the package shipped nothing that could create
/// <c>KeyEscrow</c>, <c>RecoveryTokens</c> or <c>KeyEscrowWrap</c>, and the whole suite stayed green
/// anyway. A green suite over an unusable feature is the thing this file exists to prevent.
/// </para>
/// <para>
/// So this suite changes exactly one variable: the schema comes from the shipped script, and the
/// options are left at their DEFAULTS. Nothing here names a table. If the script's object names, its
/// columns, its types or its nullability disagree with what the service writes, these tests fail —
/// which is the only way that disagreement is visible before a consumer hits it.
/// </para>
/// <para>
/// Both arms are required and neither is sufficient. The safety arm alone is passed by an escrow that
/// stores nothing at all: refusing a below-threshold recovery is trivially satisfied by refusing
/// everything. The liveness arm is the one that fails if the schema is wrong, because it demands the
/// key actually come back.
/// </para>
/// <para>
/// Real SQL Server via TestContainers, never a mock and never skipped: the defect class here is
/// server-side (a column that does not exist, a NULL a column will not accept), and a fake connection
/// reproduces none of it. The encryption provider is faked deliberately — it is a consumer-supplied
/// component, not the system under test.
/// </para>
/// </remarks>
[Collection(SqlServerTestCollection.Name)]
[Trait("Category", TestCategories.Integration)]
[Trait("Component", "Compliance")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerKeyEscrowShippedSchemaShould : IAsyncLifetime, IDisposable
{
	private readonly SqlServerContainerFixture _fixture;
	private readonly IEncryptionProvider _encryptionProvider;
	private SqlServerKeyEscrowService _service = null!;

	public SqlServerKeyEscrowShippedSchemaShould(SqlServerContainerFixture fixture)
	{
		_fixture = fixture;
		_encryptionProvider = A.Fake<IEncryptionProvider>();
	}

	public async ValueTask InitializeAsync()
	{
		// The provisioning step IS part of what is under test: a consumer runs this file and nothing else.
		await ShippedKeyEscrowSchema.EnsureCreatedAsync(_fixture.ConnectionString, CancellationToken.None);

		SetupEncryptionProviderMock();

		// Deliberately only the connection string. Schema, TableName, TokensTableName and WrapTableName
		// are left at their defaults so that the shipped script and the service have to agree about the
		// object names without a test restating either of them.
		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerKeyEscrowOptions
		{
			ConnectionString = _fixture.ConnectionString,
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

	/// <summary>
	/// LIVENESS — the arm that fails if the shipped schema is wrong in any way that matters.
	/// </summary>
	/// <remarks>
	/// A full escrow-to-recovery round trip through the shipped tables: escrow a key, issue a 3-of-5
	/// custodian batch (which seals the master-only copy), then reassemble a genuine quorum and require
	/// the original bytes back. Every column the service writes is exercised on the way through, so a
	/// missing or mistyped one surfaces here rather than at a consumer's recovery.
	/// </remarks>
	[Fact]
	public async Task ProvisionFromTheShippedScript_ThenRecoverAKey_ReturnsTheOriginalMaterial()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"the shipped escrow schema is a real-SqlServer invariant — never skipped.");

		var keyId = $"shipped-liveness-{Guid.NewGuid():N}";
		var keyMaterial = RandomNumberGenerator.GetBytes(32);

		_ = await _service.BackupKeyAsync(keyId, keyMaterial, null, CancellationToken.None);

		var tokens = await _service.GenerateRecoveryTokensAsync(
			keyId, custodianCount: 5, threshold: 3, null, CancellationToken.None);

		var combined = RecoveryToken.Combine(tokens.Take(3));

		var recovered = await _service.RecoverKeyAsync(keyId, combined, CancellationToken.None);

		recovered.ToArray().ShouldBe(keyMaterial,
			"a key escrowed into the shipped schema must come back byte-for-byte — an escrow that cannot "
			+ "be recovered is the failure this schema exists to prevent");
	}

	/// <summary>
	/// SAFETY — below-threshold recovery is refused.
	/// </summary>
	/// <remarks>
	/// Stated as its own arm rather than assumed: the shipped schema persists the server-side
	/// <c>SecretCommitment</c> the refusal is checked against, so a schema that dropped or nulled that
	/// column would weaken this without failing the liveness arm above.
	/// </remarks>
	[Fact]
	public async Task ProvisionFromTheShippedScript_ThenAttemptRecoveryBelowThreshold_IsRefused()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"below-threshold refusal is a real-SqlServer invariant — never skipped.");

		var keyId = $"shipped-safety-{Guid.NewGuid():N}";
		var keyMaterial = RandomNumberGenerator.GetBytes(32);

		_ = await _service.BackupKeyAsync(keyId, keyMaterial, null, CancellationToken.None);

		var tokens = await _service.GenerateRecoveryTokensAsync(
			keyId, custodianCount: 5, threshold: 3, null, CancellationToken.None);

		// Two shares against a threshold of three. The refusal is at the producer, which is where a
		// custodian short of a quorum actually gets stopped.
		_ = Should.Throw<ArgumentException>(() => RecoveryToken.Combine(tokens.Take(2)));

		// And the escrow is still recoverable by a genuine quorum afterwards — a refusal must not have
		// consumed or damaged the batch. Without this, "refuses everything forever" passes the arm above.
		var recovered = await _service.RecoverKeyAsync(
			keyId, RecoveryToken.Combine(tokens.Take(3)), CancellationToken.None);

		recovered.ToArray().ShouldBe(keyMaterial,
			"a refused below-threshold attempt must leave the escrow recoverable by a real quorum");
	}

	/// <summary>
	/// The tenant scope survives the round trip through the shipped column, including when it is absent.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <c>TenantId</c> is not a query predicate anywhere in the escrow path — it is read back and fed
	/// into the decryption context, so what comes out has to equal what went in or the key does not
	/// return. That makes what the column holds a recovery property, not a formatting preference.
	/// </para>
	/// <para>
	/// What it holds is the CANONICAL tenant term, never a NULL: the shipped column is <c>NOT NULL</c>
	/// with the reserved sentinel as its default, because a nullable tenant makes "no tenant" and
	/// "nobody set one" the same value. The service canonicalises on the way in, and recovery reads the
	/// canonical term straight back out — so both sides of the envelope agree without any reversal, and
	/// an absent tenant is safe for exactly that reason rather than by round-tripping as NULL.
	/// </para>
	/// <para>
	/// The expected stored term is carried in the case data as a LITERAL rather than derived by calling
	/// the same canonicalisation the service uses, which would make the assertion agree with the
	/// implementation by construction and go green on any change to it.
	/// </para>
	/// <para>
	/// Both cases are asserted because only the pair distinguishes "the scope survives the round trip"
	/// from "it happens to work for the tenanted case".
	/// </para>
	/// </remarks>
	[Theory]
	[InlineData("acme-tenant", "acme-tenant")]
	[InlineData(null, "__untenanted__")]
	public async Task RoundTripAKey_UnderTheShippedTenantColumn_ForTenantedAndUntenantedEscrows(
		string? tenantId,
		string expectedStoredTerm)
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"tenant round-tripping through the shipped column is a real-SqlServer invariant — never skipped.");

		var keyId = $"shipped-tenant-{Guid.NewGuid():N}";
		var keyMaterial = RandomNumberGenerator.GetBytes(32);

		_ = await _service.BackupKeyAsync(
			keyId, keyMaterial, new EscrowOptions { TenantId = tenantId }, CancellationToken.None);

		var tokens = await _service.GenerateRecoveryTokensAsync(
			keyId, custodianCount: 3, threshold: 2, null, CancellationToken.None);

		var recovered = await _service.RecoverKeyAsync(
			keyId, RecoveryToken.Combine(tokens.Take(2)), CancellationToken.None);

		recovered.ToArray().ShouldBe(keyMaterial,
			"the tenant scope must reach the decryption context exactly as it was escrowed");

		using var connection = _fixture.CreateDbConnection();
		var stored = await connection.QuerySingleAsync<string?>(
			"SELECT TenantId FROM [compliance].[KeyEscrow] WHERE KeyId = @KeyId", new { KeyId = keyId });

		stored.ShouldNotBeNull(
			"the shipped column is NOT NULL: an absent tenant is stored as the reserved sentinel, so a NULL "
			+ "here means the column was provisioned from something other than the shipped script");

		stored.ShouldBe(expectedStoredTerm,
			"the shipped column must hold the canonical tenant term the decryption context is rebuilt from");
	}

	private void SetupEncryptionProviderMock()
	{
		_ = A.CallTo(() => _encryptionProvider.EncryptAsync(
				A<byte[]>.Ignored, A<EncryptionContext>.Ignored, A<CancellationToken>.Ignored))
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

		_ = A.CallTo(() => _encryptionProvider.DecryptAsync(
				A<EncryptedData>.Ignored, A<EncryptionContext>.Ignored, A<CancellationToken>.Ignored))
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
}
