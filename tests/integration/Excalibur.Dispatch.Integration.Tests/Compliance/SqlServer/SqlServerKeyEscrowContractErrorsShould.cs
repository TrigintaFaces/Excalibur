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
/// Binds the two places where the SQL Server escrow reported something other than what
/// <see cref="IKeyEscrowService"/> documents: a duplicate escrow, and a revoked one.
/// </summary>
/// <remarks>
/// <para>
/// Both defects were found by deriving the shipped conformance kit, whose arms had never executed against
/// this implementation. Neither was a data-integrity fault — the escrowed keys were safe throughout. Both
/// were the store telling a consumer something untrue about why an operation failed, which a consumer then
/// writes error handling against.
/// </para>
/// <para>
/// <strong>Duplicate escrow.</strong> A second escrow of a still-active key is refused by the
/// <c>UX_KeyEscrow_ActiveKeyId</c> filtered unique index, and that is the right home for the invariant.
/// But the rejection surfaced as a raw provider exception naming an index, not the documented
/// <see cref="KeyEscrowException"/> with <see cref="KeyEscrowErrorCode.EscrowAlreadyExists"/> — and
/// <see cref="EscrowOptions.AllowOverwrite"/>, which is public, shipped, and documented as controlling
/// exactly this, was never read at all. A consumer who set it got the same failure as one who did not.
/// </para>
/// <para>
/// <strong>Revoked escrow.</strong> Recovery on a deliberately revoked escrow reported
/// <see cref="KeyEscrowErrorCode.EscrowExpired"/>, telling an operator the escrow had lapsed on its own
/// when someone had in fact revoked it. <see cref="KeyEscrowErrorCode.EscrowRevoked"/> was declared and
/// thrown by nothing.
/// </para>
/// <para>
/// Real SQL Server via TestContainers, never a mock and never skipped. The duplicate-escrow defect is
/// server-side by nature — it only exists because a unique index rejects the row — and no fake connection
/// reproduces it. The encryption provider is faked deliberately: it is consumer-supplied, not the system
/// under test. The schema comes from the shipped script with the options left at their defaults, so
/// nothing here restates an object name.
/// </para>
/// <para>
/// Every safety arm below is paired with a liveness arm. "Refuses a duplicate" is satisfied by a store
/// that refuses every backup; "reports revoked" is satisfied by one that reports revoked for everything.
/// Only the pairs distinguish a correct store from an inert one.
/// </para>
/// </remarks>
[Collection(SqlServerTestCollection.Name)]
[Trait("Category", TestCategories.Integration)]
[Trait("Component", "Compliance")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerKeyEscrowContractErrorsShould : IAsyncLifetime, IDisposable
{
	private readonly SqlServerContainerFixture _fixture;
	private readonly IEncryptionProvider _encryptionProvider;
	private SqlServerKeyEscrowService _service = null!;

	public SqlServerKeyEscrowContractErrorsShould(SqlServerContainerFixture fixture)
	{
		_fixture = fixture;
		_encryptionProvider = A.Fake<IEncryptionProvider>();
	}

	public async ValueTask InitializeAsync()
	{
		await ShippedKeyEscrowSchema.EnsureCreatedAsync(_fixture.ConnectionString, CancellationToken.None);

		SetupEncryptionProviderMock();

		// Connection string only. Schema, TableName, TokensTableName and WrapTableName stay at their
		// defaults so the shipped script and the service must agree without a test restating either.
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
	/// SAFETY — a second escrow of a still-active key is refused as <c>EscrowAlreadyExists</c>.
	/// </summary>
	/// <remarks>
	/// The arm asserts the documented exception type and error code, not merely that something was thrown:
	/// the pre-fix store did throw, but it threw a raw provider exception naming a database index, which no
	/// consumer can be expected to catch. The first backup is asserted to succeed in the same arm so that a
	/// store which refuses every escrow cannot pass.
	/// </remarks>
	[Fact]
	public async Task RefuseASecondEscrowOfAnActiveKey_AsEscrowAlreadyExists()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"duplicate-escrow rejection is enforced by a server-side unique index — never skipped.");

		var keyId = $"dup-safety-{Guid.NewGuid():N}";
		var keyMaterial = RandomNumberGenerator.GetBytes(32);

		// LIVENESS guard, in-arm: the first escrow of a fresh key must succeed, or "refuses everything"
		// would satisfy the assertion below.
		var receipt = await _service.BackupKeyAsync(keyId, keyMaterial, null, CancellationToken.None);
		receipt.EscrowId.ShouldNotBeNullOrEmpty("the first escrow of a fresh key must succeed");

		var second = await Should.ThrowAsync<KeyEscrowException>(
			async () => await _service.BackupKeyAsync(
				keyId, RandomNumberGenerator.GetBytes(32), null, CancellationToken.None),
			"a duplicate escrow must be refused through the documented exception, not a raw provider error");

		second.ErrorCode.ShouldBe(KeyEscrowErrorCode.EscrowAlreadyExists,
			"the contract names EscrowAlreadyExists for this case; anything else is a consumer writing "
			+ "error handling against a code that never arrives");

		second.KeyId.ShouldBe(keyId);
	}

	/// <summary>
	/// LIVENESS — <c>AllowOverwrite</c> supersedes the existing escrow, and the NEW key is what recovers.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is the arm the documented option exists for, and the one that fails if the option is ignored.
	/// It demands more than "no exception": the replacement escrow must be the one that comes back, so a
	/// store that accepted the call and quietly kept the old key would still fail here.
	/// </para>
	/// <para>
	/// The superseded row is asserted to be Revoked rather than gone. Overwrite must not delete escrow
	/// history — an operator investigating which key was in escrow when needs the prior row, and the
	/// filtered unique index only requires that it stop being Active.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task SupersedeAnExistingEscrow_WhenAllowOverwriteIsSet_AndRecoverTheReplacementKey()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"AllowOverwrite is a real-SqlServer invariant — never skipped.");

		var keyId = $"dup-liveness-{Guid.NewGuid():N}";
		var originalMaterial = RandomNumberGenerator.GetBytes(32);
		var replacementMaterial = RandomNumberGenerator.GetBytes(32);

		var first = await _service.BackupKeyAsync(keyId, originalMaterial, null, CancellationToken.None);

		var second = await _service.BackupKeyAsync(
			keyId,
			replacementMaterial,
			new EscrowOptions { AllowOverwrite = true },
			CancellationToken.None);

		second.EscrowId.ShouldNotBe(first.EscrowId,
			"superseding issues a new escrow rather than mutating the old one in place");

		// The replacement is what recovers. Nothing weaker distinguishes an honoured overwrite from a
		// call that returned successfully and left the original key in escrow.
		var tokens = await _service.GenerateRecoveryTokensAsync(
			keyId, custodianCount: 5, threshold: 3, null, CancellationToken.None);

		var recovered = await _service.RecoverKeyAsync(
			keyId, RecoveryToken.Combine(tokens.Take(3)), CancellationToken.None);

		recovered.ToArray().ShouldBe(replacementMaterial,
			"the escrow that recovers after an overwrite must be the replacement, not the superseded key");

		using var connection = _fixture.CreateDbConnection();

		var supersededState = await connection.QuerySingleOrDefaultAsync<int?>(
			"SELECT State FROM [compliance].[KeyEscrow] WHERE EscrowId = @EscrowId",
			new { first.EscrowId });

		supersededState.ShouldNotBeNull(
			"overwrite must supersede the prior escrow, not delete it — escrow history is audit evidence");

		supersededState.Value.ShouldBe((int)EscrowState.Revoked,
			"the superseded escrow must be recorded as revoked so the active-key unique index admits its "
			+ "replacement while the prior row remains for audit");
	}

	/// <summary>
	/// SAFETY — recovery on a revoked escrow reports <c>EscrowRevoked</c>, not <c>EscrowExpired</c>.
	/// </summary>
	/// <remarks>
	/// A genuine threshold quorum is presented so that revocation is the only reason this recovery can
	/// fail. Presenting a lone custodian share instead would let a store that never checks revocation pass
	/// on the below-threshold rejection, which is the vacuity the shipped kit's own arm used to carry.
	/// </remarks>
	[Fact]
	public async Task ReportARevokedEscrowAsRevoked_WhenRecoveringWithAGenuineQuorum()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"revocation reporting is a real-SqlServer invariant — never skipped.");

		var keyId = $"revoked-safety-{Guid.NewGuid():N}";
		var keyMaterial = RandomNumberGenerator.GetBytes(32);

		_ = await _service.BackupKeyAsync(keyId, keyMaterial, null, CancellationToken.None);

		var tokens = await _service.GenerateRecoveryTokensAsync(
			keyId, custodianCount: 5, threshold: 3, null, CancellationToken.None);

		var quorum = RecoveryToken.Combine(tokens.Take(3));

		var revoked = await _service.RevokeEscrowAsync(keyId, "operator revoked", CancellationToken.None);
		revoked.ShouldBeTrue("revoking an existing escrow reports true");

		var ex = await Should.ThrowAsync<KeyEscrowException>(
			async () => await _service.RecoverKeyAsync(keyId, quorum, CancellationToken.None),
			"a revoked escrow must refuse recovery even to a full quorum");

		ex.ErrorCode.ShouldBe(KeyEscrowErrorCode.EscrowRevoked,
			"reporting a deliberately revoked escrow as EscrowExpired tells an operator it lapsed on its "
			+ "own, sending an incident investigation the wrong way");
	}

	/// <summary>
	/// LIVENESS — the same quorum recovers the key while the escrow is still active.
	/// </summary>
	/// <remarks>
	/// The pair for the arm above. Without it, a store that reported <c>EscrowRevoked</c> for every
	/// recovery — including successful ones it then refused — would pass.
	/// </remarks>
	[Fact]
	public async Task RecoverWithAGenuineQuorum_WhileTheEscrowIsStillActive()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"quorum recovery is a real-SqlServer invariant — never skipped.");

		var keyId = $"revoked-liveness-{Guid.NewGuid():N}";
		var keyMaterial = RandomNumberGenerator.GetBytes(32);

		_ = await _service.BackupKeyAsync(keyId, keyMaterial, null, CancellationToken.None);

		var tokens = await _service.GenerateRecoveryTokensAsync(
			keyId, custodianCount: 5, threshold: 3, null, CancellationToken.None);

		var recovered = await _service.RecoverKeyAsync(
			keyId, RecoveryToken.Combine(tokens.Take(3)), CancellationToken.None);

		recovered.ToArray().ShouldBe(keyMaterial,
			"an active escrow must still recover to a genuine quorum — otherwise the revocation arm above "
			+ "is satisfied by a store that refuses everything");
	}

	/// <summary>
	/// SAFETY — issuing custodian tokens against a revoked escrow reports <c>EscrowRevoked</c>.
	/// </summary>
	/// <remarks>
	/// The same mislabelling lived on the token-issuing path. It matters more here than it looks: an
	/// operator re-provisioning custodians after an incident is told the escrow expired, and may reasonably
	/// conclude the revocation never took effect.
	/// </remarks>
	[Fact]
	public async Task ReportARevokedEscrowAsRevoked_WhenIssuingRecoveryTokens()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"revocation reporting on the token path is a real-SqlServer invariant — never skipped.");

		var keyId = $"revoked-tokens-{Guid.NewGuid():N}";
		var keyMaterial = RandomNumberGenerator.GetBytes(32);

		_ = await _service.BackupKeyAsync(keyId, keyMaterial, null, CancellationToken.None);
		_ = await _service.RevokeEscrowAsync(keyId, "operator revoked", CancellationToken.None);

		var ex = await Should.ThrowAsync<KeyEscrowException>(
			async () => await _service.GenerateRecoveryTokensAsync(
				keyId, custodianCount: 5, threshold: 3, null, CancellationToken.None),
			"a revoked escrow must not issue new custodian shares");

		ex.ErrorCode.ShouldBe(KeyEscrowErrorCode.EscrowRevoked,
			"the token path must name revocation for the same reason the recovery path does");
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
