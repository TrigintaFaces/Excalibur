// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Dapper;

using Excalibur.Compliance;
using Excalibur.Compliance.SqlServer.Erasure;
using Excalibur.Dispatch.Integration.Tests.Compliance.Fixtures;

using Microsoft.Data.SqlClient;

using SqlServerContainerFixture = Excalibur.Dispatch.Integration.Tests.Compliance.Fixtures.SqlServerContainerFixture;

namespace Excalibur.Dispatch.Integration.Tests.Compliance.SqlServer;

/// <summary>
/// Proves a database still on the pre-renumber schema cannot misread a legal basis: it either reports the
/// ground as not established, or refuses to serve at all.
/// </summary>
/// <remarks>
/// <para>
/// The legal-basis enumerations gained a zero member meaning "no ground was established", so every other
/// member moved up by one. The values are persisted as <c>INT</c>, which makes every row written before that
/// change hold an ordinal that now names a different legal basis — a stored <c>1</c> that meant "legal
/// obligation" would read back as "freedom of expression" on a signed compliance record, silently.
/// </para>
/// <para>
/// <b>Two mechanisms stand between that and a consumer, and both are asserted here.</b> The migration
/// REPLACES the column and backfills it with the stored ordinal shifted onto the member it named, so a
/// pre-upgrade row keeps the ground it was written under. And the column has a NEW NAME, so a database that
/// never ran the migration fails the store's schema probe by name instead of starting and misreading its
/// rows.
/// </para>
/// <para>
/// <b>A rename would have been the wrong shape, and that is asserted rather than assumed.</b> Renaming
/// preserves values WITHOUT shifting them, so the old ordinals would survive under the new name and be read
/// through the new numbering.
/// <see cref="ReadAPreUpgradeRowAsTheGroundItWasWrittenUnder_RatherThanDiscardingIt"/> is the arm that
/// fails if the migration is ever changed to a rename, and equally if the backfill is dropped so every row
/// lands on the default.
/// </para>
/// <para>
/// Real SQL Server via TestContainers, never skipped. Every property here is server-side — whether the
/// column was actually replaced, whether the backfill reached every row, whether the probe reads the column
/// catalogue — and no fake connection reproduces any of them.
/// </para>
/// </remarks>
[Collection(SqlServerTestCollection.Name)]
[Trait("Category", TestCategories.Integration)]
[Trait("Component", "Compliance")]
[Trait("Database", "SqlServer")]
public sealed class APreRenumberSchemaCannotMisreadALegalBasisShould : IAsyncLifetime
{
	private const string MigrationScript = "003_ReplaceLegalBasisColumns.sql";

	// Ordinals as the PREVIOUS version wrote them. Under the old numbering 1 was "legal obligation"; under
	// the new one it is "freedom of expression", so a row that survived unconverted would be readable AND
	// wrong, which is the failure these arms exist to make impossible.
	private static readonly int[] OrdinalsWrittenByThePreviousVersion = [0, 1, 4, 6];

	private readonly SqlServerContainerFixture _fixture;
	private readonly string _databaseName = $"basis_replace_{Guid.NewGuid():N}";

	private string _databaseConnectionString = string.Empty;

	public APreRenumberSchemaCannotMisreadALegalBasisShould(SqlServerContainerFixture fixture) =>
		_fixture = fixture;

	public async ValueTask InitializeAsync()
	{
		_fixture.EnsureAvailable();

		await using (var master = new SqlConnection(_fixture.ConnectionString))
		{
			await master.OpenAsync(CancellationToken.None);

			// CA2100: the only interpolated value is _databaseName, built here from a GUID "N" format --
			// 32 hex characters, no user input and no quotable character. A database name cannot be
			// parameterised in T-SQL.
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
			await using var create = new SqlCommand($"CREATE DATABASE [{_databaseName}]", master);
#pragma warning restore CA2100
			_ = await create.ExecuteNonQueryAsync(CancellationToken.None);
		}

		_databaseConnectionString = new SqlConnectionStringBuilder(_fixture.ConnectionString)
		{
			InitialCatalog = _databaseName,
		}.ConnectionString;

		await GivenADatabaseProvisionedByThePreviousVersionAsync();
	}

	public async ValueTask DisposeAsync()
	{
		if (!_fixture.DockerAvailable || _databaseConnectionString.Length == 0)
		{
			return;
		}

		// Scoped to this suite's own connection string rather than ClearAllPools(): the pool is keyed by
		// connection string, so clearing globally would evict connections belonging to suites running
		// alongside this one.
		using (var pooled = new SqlConnection(_databaseConnectionString))
		{
			SqlConnection.ClearPool(pooled);
		}

		await using var master = new SqlConnection(_fixture.ConnectionString);
		await master.OpenAsync(CancellationToken.None);

#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities -- GUID-derived name
		await using var drop = new SqlCommand(
			$"ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_databaseName}]",
			master);
#pragma warning restore CA2100
		_ = await drop.ExecuteNonQueryAsync(CancellationToken.None);
	}

	/// <summary>
	/// SAFETY, and it is the arm the whole design rests on: an UNMIGRATED database does not serve a legal
	/// basis at all. The column the store binds is absent, so a read fails rather than returning a value.
	/// </summary>
	/// <remarks>
	/// <para>
	/// With pre-upgrade ordinals no longer converted, this is the only thing standing between a stale
	/// database and a misread ground. If the column kept its old name the store would bind it, find it, read
	/// an old ordinal through the new numbering, and report a different legal basis with nothing anywhere
	/// saying so.
	/// </para>
	/// <para>
	/// <b>RED input:</b> revert the column names in the create script and the store back to
	/// <c>LegalBasis</c> / <c>Basis</c>. The pre-upgrade column then resolves, the read succeeds, and this
	/// arm fails because a value came back where a refusal was owed. That is the mutation that turns the
	/// rename from a protection into decoration.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task RefuseToServeALegalBasis_WhenTheDatabaseStillHasThePreRenumberColumn()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"the fail-closed property is a real-SqlServer invariant -- never skipped.");

		(await ColumnExistsAsync("LegalBasis")).ShouldBeTrue(
			"the arm is vacuous unless the database really is still on the pre-renumber schema");

		// THE REAL STORE, not a query this arm composed. The requirement is that the STORE refuses, and a
		// hand-written SELECT would test the engine's opinion of a column name this arm chose -- it would
		// stay green even if the store had been reverted to binding the old column, which is the exact
		// mutation this arm exists to catch.
		var store = PreRenumberDatabaseStore();

		var refused = await Should.ThrowAsync<ErasureStoreNotProvisionedException>(
			async () => await store.GetStatusAsync(Guid.NewGuid(), CancellationToken.None));

		refused.Message.ShouldContain(
			"LegalBasisV2",
			Case.Insensitive,
			"the refusal has to NAME the column, or an operator cannot tell which migration to apply");
	}

	/// <summary>
	/// SAFETY. After the migration a pre-upgrade row reports the ground it was actually written under, and
	/// never the different ground its old ordinal names under the new numbering.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>This arm asserted the opposite until the renumbering was measured, and the measurement reversed
	/// it.</b> It required every pre-upgrade row to read as <c>NotEstablished</c>, on the stated grounds
	/// that the framework could no longer say which ground a stored ordinal named. The enumeration diff
	/// refutes that: across the commit that introduced the zero member, every member of both affected
	/// enumerations moved up by exactly one and none was renamed, reordered or removed. So <c>old + 1</c> is
	/// a total mapping onto the new numbering, the stored ground was always recoverable, and discarding it
	/// destroyed an Article 17(1) record that conversion preserves exactly.
	/// </para>
	/// <para>
	/// <b>RED input, and it is the same mutation as before:</b> change the migration to RENAME the column. A
	/// rename preserves the ordinal WITHOUT shifting it, so <c>1</c> comes back as freedom of expression
	/// where it meant legal obligation, and the expected set below does not match. Dropping the backfill
	/// entirely reddens it too — every row would land on the default and read as not established. The arm
	/// now fails for both mistakes rather than requiring one of them.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task ReadAPreUpgradeRowAsTheGroundItWasWrittenUnder_RatherThanDiscardingIt()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"the conversion is a real-SqlServer invariant -- never skipped.");

		await ShippedKeyEscrowSchema.ApplyShippedScriptAsync(
			MigrationScript, _databaseConnectionString, CancellationToken.None);

		(await ColumnExistsAsync("LegalBasis")).ShouldBeFalse(
			"the pre-renumber column must be gone, or a later reader could still bind it");
		(await ColumnExistsAsync("LegalBasisV2")).ShouldBeTrue();

		var stored = await StoredGroundsAsync();

		stored.Length.ShouldBe(
			OrdinalsWrittenByThePreviousVersion.Length,
			"the rows themselves must survive the migration");

		// Every ordinal the previous version wrote, shifted onto the member it named. Asserted as an
		// ordered set rather than row-by-row because the table has no ordering guarantee, and asserting
		// per-row would pass on a query that happened to return them in insertion order.
		var expected = OrdinalsWrittenByThePreviousVersion
			.Select(static o => o + 1)
			.OrderBy(static o => o)
			.ToArray();

		stored
			.Select(static g => (int)g)
			.OrderBy(static o => o)
			.ToArray()
			.ShouldBe(
				expected,
				"each pre-upgrade ordinal must come back as the ground it was written under. The old "
				+ "numbering is recoverable by construction -- every member moved up by exactly one -- so "
				+ "a row reading as not established here is a destroyed compliance record, not an honest "
				+ "admission of ignorance");

		stored.ShouldNotContain(
			ErasureLegalBasis.NotEstablished,
			"no row in this fixture held a value outside the old enumeration, so none of them has any "
			+ "reason to read as not established");
	}

	/// <summary>
	/// SAFETY. A stored value that named no member of the old enumeration is the one case that becomes
	/// not-established, because it is the one case where nothing can be said.
	/// </summary>
	/// <remarks>
	/// The conversion is <c>old + 1</c> over the old range only. Shifting a value the old enumeration never
	/// named would invent a ground, which is the failure the previous behaviour was reaching for and applied
	/// to every row instead of only to this one.
	/// <b>RED input:</b> drop the <c>BETWEEN</c> bound from the migration's <c>CASE</c>, so an
	/// out-of-range ordinal is shifted like any other and comes back naming a member nobody wrote.
	/// </remarks>
	[Fact]
	public async Task ReadAValueTheOldEnumerationNeverNamedAsNotEstablished()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"the fallback is a real-SqlServer invariant -- never skipped.");

		await using (var seed = new SqlConnection(_databaseConnectionString))
		{
			await seed.OpenAsync(CancellationToken.None);

			// 99 named no member of the old enumeration, which ran 0..6.
			_ = await seed.ExecuteAsync(
				"INSERT INTO [compliance].[ErasureRequests] (RequestId, LegalBasis) VALUES (@Id, 99);",
				new { Id = Guid.NewGuid() });
		}

		await ShippedKeyEscrowSchema.ApplyShippedScriptAsync(
			MigrationScript, _databaseConnectionString, CancellationToken.None);

		var stored = await StoredGroundsAsync();

		stored.Count(static g => g == ErasureLegalBasis.NotEstablished).ShouldBe(
			1,
			"exactly the unnameable row becomes not established -- the convertible ones must not join it");
	}

	/// <summary>
	/// The old column is the ONLY thing the guard reads, so re-running changes nothing.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Neither package ships a migration runner — consumers apply these scripts by hand, so "ran it again to
	/// be sure" is an ordinary input. Each block runs only if the OLD column is present and the block drops
	/// that column, so the precondition is consumed by the act it guards.
	/// </para>
	/// <para>
	/// <b>RED input:</b> delete the <c>IF COL_LENGTH(...) IS NOT NULL</c> guard. The second application then
	/// tries to add a column that already exists and the script fails outright, so a consumer who re-runs it
	/// gets an error on a database that was already correct.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task LeaveAnAlreadyMigratedDatabaseUntouched_WhenAppliedTwice()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"the shipped migration is a real-SqlServer invariant -- never skipped.");

		await ShippedKeyEscrowSchema.ApplyShippedScriptAsync(
			MigrationScript, _databaseConnectionString, CancellationToken.None);

		await using var connection = new SqlConnection(_databaseConnectionString);
		await connection.OpenAsync(CancellationToken.None);

		// A row written by the NEW version, carrying a real ground. It must survive a re-run untouched --
		// this is the case a consumer hits when they re-apply the script after going live.
		//
		// Read back BY id. Selecting `TOP 1 ... ORDER BY RequestId DESC` over a random GUID is a correct
		// query about a row this arm never inserted, and an earlier version of it did exactly that.
		var newSchemeRowId = Guid.NewGuid();

		_ = await connection.ExecuteAsync(
			"INSERT INTO [compliance].[ErasureRequests] (RequestId, LegalBasisV2) VALUES (@Id, @Basis);",
			new { Id = newSchemeRowId, Basis = (int)ErasureLegalBasis.LegalObligation });

		await ShippedKeyEscrowSchema.ApplyShippedScriptAsync(
			MigrationScript, _databaseConnectionString, CancellationToken.None);

		var survivor = await connection.QuerySingleAsync<int>(
			"SELECT LegalBasisV2 FROM [compliance].[ErasureRequests] WHERE RequestId = @Id;",
			new { Id = newSchemeRowId });

		((ErasureLegalBasis)survivor).ShouldBe(
			ErasureLegalBasis.LegalObligation,
			"a row already written in the new scheme must not be disturbed by a later re-run");
	}

	/// <summary>
	/// A real store pointed at this suite's pre-renumber database.
	/// </summary>
	/// <remarks>
	/// <c>AutoCreateSchema</c> is deliberately OFF. With it on the store would create its own tables and the
	/// stale one would never be consulted, so the arm would assert nothing about a pre-renumber database --
	/// it would pass against a freshly provisioned one and prove only that the happy path works.
	/// </remarks>
	private static SqlServerErasureStore CreateStore(string connectionString) =>
		new(Microsoft.Extensions.Options.Options.Create(new SqlServerErasureStoreOptions
			{
				ConnectionString = connectionString,
				SchemaName = "compliance",
				RequestsTableName = "ErasureRequests",
				CertificatesTableName = "ErasureCertificates",
				CommandTimeoutSeconds = 30,
				AutoCreateSchema = false,
			}),
			ConformanceDataSubjectHasher.Instance,
			EnabledTestLogger.Create<SqlServerErasureStore>(),
			UntenantedContext.Instance,
			tenantContextOptions: Microsoft.Extensions.Options.Options.Create(
				new Excalibur.Dispatch.TenantContextOptions()));

	private SqlServerErasureStore PreRenumberDatabaseStore() => CreateStore(_databaseConnectionString);

	/// <summary>
	/// The pre-migration schema, as the previous package version created it.
	/// </summary>
	/// <remarks>
	/// Only the columns these arms read. Provisioning from the current create script would be misleading: it
	/// declares the NEW column, so the migration is correctly a no-op against it and the arms would assert
	/// nothing about the replacement.
	/// </remarks>
	private async Task GivenADatabaseProvisionedByThePreviousVersionAsync()
	{
		await using var connection = new SqlConnection(_databaseConnectionString);
		await connection.OpenAsync(CancellationToken.None);

		_ = await connection.ExecuteAsync(
			"""
			IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'compliance')
			BEGIN
			    EXEC('CREATE SCHEMA [compliance]');
			END
			""");

		_ = await connection.ExecuteAsync(
			"""
			CREATE TABLE [compliance].[ErasureRequests] (
			    RequestId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
			    LegalBasis INT NOT NULL
			);
			""");

		foreach (var ordinal in OrdinalsWrittenByThePreviousVersion)
		{
			_ = await connection.ExecuteAsync(
				"INSERT INTO [compliance].[ErasureRequests] (RequestId, LegalBasis) VALUES (@Id, @Basis);",
				new { Id = Guid.NewGuid(), Basis = ordinal });
		}
	}

	private async Task<bool> ColumnExistsAsync(string columnName)
	{
		await using var connection = new SqlConnection(_databaseConnectionString);
		await connection.OpenAsync(CancellationToken.None);

		return await connection.ExecuteScalarAsync<int>(
			"""
			SELECT COUNT(1) FROM sys.columns
			WHERE object_id = OBJECT_ID('compliance.ErasureRequests', 'U') AND name = @ColumnName;
			""",
			new { ColumnName = columnName }) > 0;
	}

	private async Task<ErasureLegalBasis[]> StoredGroundsAsync()
	{
		await using var connection = new SqlConnection(_databaseConnectionString);
		await connection.OpenAsync(CancellationToken.None);

		var rows = await connection.QueryAsync<int>(
			"SELECT LegalBasisV2 FROM [compliance].[ErasureRequests];");

		return [.. rows.Select(static r => (ErasureLegalBasis)r)];
	}
}
