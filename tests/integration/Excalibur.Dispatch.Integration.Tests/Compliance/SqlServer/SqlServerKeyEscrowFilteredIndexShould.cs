// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.RegularExpressions;

using Dapper;

using Excalibur.Compliance;
using Excalibur.Dispatch.Integration.Tests.Compliance.Fixtures;

using Microsoft.Data.SqlClient;

using SqlServerContainerFixture = Excalibur.Dispatch.Integration.Tests.Compliance.Fixtures.SqlServerContainerFixture;

namespace Excalibur.Dispatch.Integration.Tests.Compliance.SqlServer;

/// <summary>
/// Proves the shipped key-escrow script creates its filtered index on a database provisioned the way
/// our own instructions describe, and that the index actually refuses a second live escrow of a key.
/// </summary>
/// <remarks>
/// <para>
/// SQL Server refuses to create a filtered index unless <c>QUOTED_IDENTIFIER</c> and <c>ANSI_NULLS</c>
/// are ON. A client that leaves either OFF gets <c>Msg 1934</c> on that one statement while every other
/// statement in the file succeeds — and the tool reports success. So the schema comes out missing
/// exactly one object: the unique index that permits only one active escrow per key. A second escrow of
/// a still-live key then does not fail; it silently makes BOTH copies unrecoverable, and nobody learns
/// that until someone needs the key.
/// </para>
/// <para>
/// The reason this suite exists separately, rather than as an assertion bolted onto the sibling
/// shipped-schema suite, is that the sibling one CANNOT catch this. It provisions through
/// <see cref="SqlConnection"/>, which turns <c>QUOTED_IDENTIFIER</c> ON at connect time, so the index is
/// created there whether or not the script asks for it. An assertion added there would pass with the
/// script's SET statements deleted — it would be a test of the client's defaults, not of the file we
/// ship.
/// </para>
/// <para>
/// So this suite changes exactly one variable: the session starts with both options OFF, and the script
/// has to turn them on itself. That is a strict superset of the sqlcmd default (which starts
/// <c>QUOTED_IDENTIFIER</c> OFF and <c>ANSI_NULLS</c> ON), and it is what makes BOTH of the script's SET
/// statements load-bearing here — deleting either one alone is enough to fail this suite.
/// </para>
/// <para>
/// It also provisions into its OWN database rather than the shared fixture one. The sibling suite runs
/// in the same collection against the same container and creates the index as a side effect of its
/// QUOTED_IDENTIFIER-ON connection; every statement in the shipped script is guarded by
/// <c>IF NOT EXISTS</c>, so running here against that same database would find the index already present
/// and pass no matter what the script said. A fresh database is what keeps this suite honest.
/// </para>
/// <para>
/// Errors are collected per batch rather than thrown, because that is what the consumer's tool does:
/// sqlcmd reports the failed statement, continues, and exits 0. Reproducing that is the point — a
/// version of this that threw on the first error would be asserting something no consumer experiences.
/// Any collected error is reported in the failure message so a swallowed error can never look like a
/// clean run.
/// </para>
/// <para>
/// Real SQL Server via TestContainers, never skipped: the defect is entirely server-side statement
/// validation, and no fake connection reproduces it.
/// </para>
/// </remarks>
[Collection(SqlServerTestCollection.Name)]
[Trait("Category", TestCategories.Integration)]
[Trait("Component", "Compliance")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerKeyEscrowFilteredIndexShould : IAsyncLifetime
{
	private const string IndexName = "UX_KeyEscrow_ActiveKeyId";

	private readonly SqlServerContainerFixture _fixture;
	private readonly string _databaseName = $"escrow_ddl_{Guid.NewGuid():N}";
	private readonly List<string> _provisioningErrors = [];

	private string _databaseConnectionString = string.Empty;

	public SqlServerKeyEscrowFilteredIndexShould(SqlServerContainerFixture fixture) => _fixture = fixture;

	public async ValueTask InitializeAsync()
	{
		if (!_fixture.DockerAvailable)
		{
			return;
		}

		await using (var master = new SqlConnection(_fixture.ConnectionString))
		{
			await master.OpenAsync(CancellationToken.None);

			// CA2100: the only interpolated value is _databaseName, which this class builds from a GUID
			// "N" format — 32 hex characters, no user input and no quotable character. A database name
			// cannot be parameterised in T-SQL. Scoped to this statement so a genuine concatenation added
			// later is still reported.
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
			await using var create = new SqlCommand($"CREATE DATABASE [{_databaseName}]", master);
#pragma warning restore CA2100
			_ = await create.ExecuteNonQueryAsync(CancellationToken.None);
		}

		_databaseConnectionString = new SqlConnectionStringBuilder(_fixture.ConnectionString)
		{
			InitialCatalog = _databaseName,
		}.ConnectionString;

		await ProvisionUnderClientDefaultsThatOmitBothOptionsAsync();
	}

	public async ValueTask DisposeAsync()
	{
		if (!_fixture.DockerAvailable || _databaseConnectionString.Length == 0)
		{
			return;
		}

		// Scoped to this suite's own connection string, never ClearAllPools(): the pool is keyed by
		// connection string, so clearing globally would evict pooled connections belonging to suites in
		// other collections running alongside this one, to fix a problem only this database has.
		using (var pooled = new SqlConnection(_databaseConnectionString))
		{
			SqlConnection.ClearPool(pooled);
		}

		await using var master = new SqlConnection(_fixture.ConnectionString);
		await master.OpenAsync(CancellationToken.None);

		// CA2100: as above — _databaseName is GUID-derived hex, and a database name cannot be a parameter.
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
		await using var drop = new SqlCommand(
			$"ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_databaseName}]",
			master);
#pragma warning restore CA2100
		_ = await drop.ExecuteNonQueryAsync(CancellationToken.None);
	}

	/// <summary>
	/// LIVENESS — the provisioning actually happened.
	/// </summary>
	/// <remarks>
	/// Stated as its own arm because every other assertion here is about an object being present or an
	/// insert being refused, and a run in which the script did nothing at all would otherwise be
	/// indistinguishable from a run in which it worked. This arm fails if the batches never executed.
	/// </remarks>
	[Fact]
	public async Task CreateTheEscrowTables_WhenProvisionedFromTheShippedScript()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"the shipped escrow schema is a real-SqlServer invariant — never skipped.");

		await using var connection = new SqlConnection(_databaseConnectionString);

		var tables = await connection.QueryAsync<string>(
			"""
			SELECT t.name FROM sys.tables t
			JOIN sys.schemas s ON t.schema_id = s.schema_id
			WHERE s.name = 'compliance'
			""");

		tables.ShouldBe(
			["KeyEscrow", "KeyEscrowWrap", "RecoveryTokens"],
			ignoreOrder: true,
			CollectedErrors("the shipped script must create all three escrow tables"));
	}

	/// <summary>
	/// The regression lock: the filtered index exists after provisioning from the shipped script.
	/// </summary>
	/// <remarks>
	/// This is the arm that goes red if either <c>SET</c> statement is deleted from the shipped file.
	/// Both were measured to be individually load-bearing under this session: removing only
	/// <c>ANSI_NULLS</c>, or only <c>QUOTED_IDENTIFIER</c>, leaves the index absent while the script
	/// otherwise succeeds.
	/// </remarks>
	[Fact]
	public async Task CreateTheFilteredIndex_WhenTheSessionStartsWithBothOptionsOff()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"the filtered index is a real-SqlServer invariant — never skipped.");

		await using var connection = new SqlConnection(_databaseConnectionString);

		var present = await connection.ExecuteScalarAsync<int>(
			"""
			SELECT COUNT(*) FROM sys.indexes
			WHERE name = @IndexName
			  AND object_id = OBJECT_ID('[compliance].[KeyEscrow]')
			  AND has_filter = 1
			  AND is_unique = 1
			""",
			new { IndexName });

		present.ShouldBe(
			1,
			CollectedErrors(
				$"'{IndexName}' must exist as a UNIQUE FILTERED index after running the shipped script. "
				+ "SQL Server rejects a filtered index unless QUOTED_IDENTIFIER and ANSI_NULLS are ON, and it "
				+ "rejects only that one statement — the rest of the file succeeds and the tool exits 0. If this "
				+ "arm is red, the script no longer sets them and a consumer's database is missing the only "
				+ "object standing between them and two live escrows of one key"));
	}

	/// <summary>
	/// SAFETY — a second live escrow of the same key is refused, which is what the index is for.
	/// </summary>
	/// <remarks>
	/// Asserted as behaviour rather than as the presence of an index, because the presence of an index
	/// is a fact about <c>sys.indexes</c> and this is a fact about what happens to a consumer. An index
	/// created with the wrong filter or the wrong column would satisfy the arm above and fail this one.
	/// </remarks>
	[Fact]
	public async Task RefuseASecondLiveEscrow_OfAKeyThatIsAlreadyEscrowed()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"refusing a duplicate live escrow is a real-SqlServer invariant — never skipped.");

		var keyId = $"dup-live-{Guid.NewGuid():N}";

		await using var connection = new SqlConnection(_databaseConnectionString);
		await InsertEscrowAsync(connection, keyId, EscrowState.Active);

		var second = await Should.ThrowAsync<SqlException>(
			async () => await InsertEscrowAsync(connection, keyId, EscrowState.Active));

		second.Message.ShouldContain(
			IndexName,
			Case.Insensitive,
			"the refusal must name the constraint, so an operator hitting it at escrow time is told which "
			+ "invariant they crossed rather than being left to infer it");
	}

	/// <summary>
	/// LIVENESS — escrow history still accumulates once the earlier escrow is revoked.
	/// </summary>
	/// <remarks>
	/// The pair matters. A plain unique index on <c>KeyId</c> with no filter would also refuse the
	/// duplicate above, and would additionally make re-escrowing a rotated key impossible forever. Only
	/// this arm distinguishes "one ACTIVE escrow per key" from "one escrow per key, ever", which is why
	/// the shipped index carries <c>WHERE State = 0</c> and why deleting that filter has to fail here.
	/// </remarks>
	[Fact]
	public async Task PermitANewActiveEscrow_OnceTheEarlierOneIsRevoked()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"re-escrow after revocation is a real-SqlServer invariant — never skipped.");

		var keyId = $"re-escrow-{Guid.NewGuid():N}";

		await using var connection = new SqlConnection(_databaseConnectionString);
		await InsertEscrowAsync(connection, keyId, EscrowState.Revoked);
		await InsertEscrowAsync(connection, keyId, EscrowState.Recovered);

		await Should.NotThrowAsync(
			async () => await InsertEscrowAsync(connection, keyId, EscrowState.Active));

		var rows = await connection.ExecuteScalarAsync<int>(
			"SELECT COUNT(*) FROM [compliance].[KeyEscrow] WHERE KeyId = @KeyId",
			new { KeyId = keyId });

		rows.ShouldBe(
			3,
			"the filter covers only the active state, so revoked and recovered escrows for the same key "
			+ "must accumulate normally alongside the new one");
	}

	private static async Task InsertEscrowAsync(SqlConnection connection, string keyId, EscrowState state) =>
		_ = await connection.ExecuteAsync(
			"""
			INSERT INTO [compliance].[KeyEscrow]
				(EscrowId, KeyId, EncryptedKey, KeyHash, Algorithm, Iv, MasterKeyId, MasterKeyVersion,
				 State, EscrowedAt)
			VALUES
				(@EscrowId, @KeyId, @EncryptedKey, @KeyHash, @Algorithm, @Iv, @MasterKeyId, @MasterKeyVersion,
				 @State, @EscrowedAt)
			""",
			new
			{
				EscrowId = Guid.NewGuid().ToString("N"),
				KeyId = keyId,
				EncryptedKey = new byte[] { 1, 2, 3, 4 },
				KeyHash = new string('a', 64),
				Algorithm = 1,
				Iv = new byte[] { 5, 6, 7, 8 },
				MasterKeyId = "master",
				MasterKeyVersion = 1,
				State = (int)state,
				EscrowedAt = DateTimeOffset.UtcNow,
			});

	/// <summary>
	/// Runs the shipped DDL on a session that starts with both required options OFF, collecting per-batch
	/// errors instead of throwing — the behaviour of the tool a consumer actually runs.
	/// </summary>
	private async Task ProvisionUnderClientDefaultsThatOmitBothOptionsAsync()
	{
		await using var connection = new SqlConnection(_databaseConnectionString);
		await connection.OpenAsync(CancellationToken.None);

		// The one variable under test. SqlClient connects with QUOTED_IDENTIFIER ON, which would create
		// the index regardless of what the script says and make every assertion below vacuous. Turning
		// both off hands the responsibility back to the shipped file, where it belongs.
		await using (var relax = new SqlCommand("SET ANSI_NULLS OFF; SET QUOTED_IDENTIFIER OFF;", connection))
		{
			_ = await relax.ExecuteNonQueryAsync(CancellationToken.None);
		}

		foreach (var batch in SplitBatches(ShippedKeyEscrowSchema.Ddl))
		{
			try
			{
				// CA2100: the command text is the package's own DDL, embedded at compile time. Scoped to
				// this statement so a genuine concatenation added later is still reported.
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
				await using var command = new SqlCommand(batch, connection);
#pragma warning restore CA2100
				_ = await command.ExecuteNonQueryAsync(CancellationToken.None);
			}
			catch (SqlException ex)
			{
				// Collected, not thrown: sqlcmd prints the message, continues, and exits 0. Every arm
				// reports these, so a swallowed error cannot be mistaken for a clean provisioning run.
				_provisioningErrors.Add($"Msg {ex.Number}: {ex.Message}");
			}
		}
	}

	private string CollectedErrors(string because) =>
		_provisioningErrors.Count == 0
			? $"{because}. The shipped script reported no errors on this session."
			: $"{because}. The shipped script reported {_provisioningErrors.Count} error(s) while provisioning, "
				+ "each of which a consumer's tool would print before exiting 0: "
				+ string.Join(" | ", _provisioningErrors);

	private static IEnumerable<string> SplitBatches(string script) =>
		Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)
			.Select(static batch => batch.Trim())
			.Where(static batch => batch.Length > 0);
}
