// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Integration.Tests.Data.Outbox;

using Microsoft.Data.SqlClient;

using Shouldly;

using Xunit;

namespace Excalibur.Integration.Tests.EventSourcing.SqlServer;

/// <summary>
/// Binds the requirement that a shipped in-place migration REFUSES when its table is not present under
/// the name the script addresses, rather than completing silently having done nothing.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect this locks is a lie, not a gap.</b> Every shipped migration addresses its table as a
/// hardcoded <c>[dbo].[Default]</c> literal, while the store that owns it accepts a table-name override.
/// On a deployment that used the override, every guarded block evaluated false, the script completed
/// SILENTLY, and it reported SUCCESS. The operator then deployed code that binds a column the table does
/// not have, and learned about it at the first write — from the wrong end, with nothing in between.
/// </para>
/// <para>
/// <b>Both arms, and the liveness one is what makes this lock worth having.</b> A migration that refused
/// unconditionally would satisfy the safety arm below and be useless. So the same script is run against a
/// correctly-named pre-tenancy table and asserted to complete AND to have actually added the column.
/// Without that arm, "refuses on a renamed table" is indistinguishable from "refuses always".
/// </para>
/// <para>
/// <b>verify-against-real-infra-not-mock:</b> real SQL Server (TestContainers), NON-SKIPPED. The behaviour
/// under test is a <c>THROW</c> evaluated by the engine against <c>OBJECT_ID</c>, and the thing being
/// protected is a script a consumer runs against their own production database. There is no code path here
/// a mock could stand in for.
/// </para>
/// <para>
/// The script is located by walking up to the repository root, exactly as the saga fixtures do, and is
/// never copied inline: a lock that carried its own copy of the DDL would only ever agree with itself, and
/// the whole point is to catch a change to the shipped artifact.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class ShippedMigrationTableAbsenceShould : IClassFixture<SqlServerOutboxStoreContainerFixture>
{
	/// <summary>The error number the materialized-view migration raises when its table is absent.</summary>
	private const int TableAbsentErrorNumber = 51005;

	private const string MigrationRelativePath =
		"src/Excalibur/Excalibur.EventSourcing.SqlServer/Scripts/005_MakeMaterializedViewsTenantTotal.sql";

	private readonly SqlServerOutboxStoreContainerFixture _fixture;

	public ShippedMigrationTableAbsenceShould(SqlServerOutboxStoreContainerFixture fixture) => _fixture = fixture;

	/// <summary>
	/// SAFETY: with the tables absent under the names the script addresses — which is exactly what a
	/// deployment that overrode the table name looks like — the migration must REFUSE, naming the override.
	/// </summary>
	[Fact]
	public async Task RefuseWhenItsTableIsAbsentUnderTheNameItAddresses()
	{
		EnsureReady();

		var database = await NewDatabaseAsync().ConfigureAwait(false);

		// No tables created: the script's literals address names that are not present here.
		var thrown = await Should.ThrowAsync<SqlException>(
			() => ExecuteMigrationAsync(database)).ConfigureAwait(false);

		thrown.Number.ShouldBe(
			TableAbsentErrorNumber,
			"the migration must refuse with its own diagnosable error rather than either completing "
			+ "silently or failing with SQL Server's generic 'Invalid object name'");

		thrown.Message.ShouldContain(
			"override",
			Case.Insensitive,
			"the message must name the table-name override, because that is the ONE thing the operator "
			+ "needs to know and cannot infer from the failure");
	}

	/// <summary>
	/// LIVENESS: against a correctly-named pre-tenancy table the same script must complete AND actually
	/// perform the migration. Without this, "refuses on a renamed table" cannot be told apart from
	/// "refuses always", and a migration that never runs would pass the safety arm above.
	/// </summary>
	[Fact]
	public async Task StillMigrateACorrectlyNamedPreTenancyTable()
	{
		EnsureReady();

		var database = await NewDatabaseAsync().ConfigureAwait(false);
		await CreatePreTenancyTablesAsync(database).ConfigureAwait(false);

		// Must not throw: the tables are present under the names the script addresses.
		await ExecuteMigrationAsync(database).ConfigureAwait(false);

		// LIVENESS proper: the column is there afterwards, so the script did the work rather than merely
		// declining to fail.
		(await ColumnExistsAsync(database, "MaterializedViews", "TenantId").ConfigureAwait(false))
			.ShouldBeTrue("the migration must add the tenant column to the view table");
		(await ColumnExistsAsync(database, "MaterializedViewPositions", "TenantId").ConfigureAwait(false))
			.ShouldBeTrue("the migration must add the tenant column to the checkpoint table");
	}

	/// <summary>
	/// LIVENESS: the script is re-runnable. A guard added at the top of a migration is the easiest place to
	/// accidentally break idempotence, and a migration that fails on a second run is one an operator cannot
	/// safely retry after an unrelated interruption.
	/// </summary>
	[Fact]
	public async Task StillBeReRunnableAfterASuccessfulMigration()
	{
		EnsureReady();

		var database = await NewDatabaseAsync().ConfigureAwait(false);
		await CreatePreTenancyTablesAsync(database).ConfigureAwait(false);

		await ExecuteMigrationAsync(database).ConfigureAwait(false);
		await ExecuteMigrationAsync(database).ConfigureAwait(false);

		(await ColumnExistsAsync(database, "MaterializedViews", "TenantId").ConfigureAwait(false))
			.ShouldBeTrue("a second run must leave the migrated shape intact");
	}

	private void EnsureReady() =>
		_fixture.DockerAvailable.ShouldBeTrue(
			"a shipped migration that reports success having done nothing is a consumer-facing correctness "
			+ "control — this real-SqlServer lock must never be skipped");

	/// <summary>Creates an isolated database so each arm starts from a known, empty state.</summary>
	private async Task<string> NewDatabaseAsync()
	{
		var name = "mig_" + Guid.NewGuid().ToString("N");

		await using var connection = new SqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync().ConfigureAwait(false);
		await using var command = connection.CreateCommand();
		// CA2100: the identifier is a GUID this method just minted, not caller input. A parameter cannot
		// carry a database name in CREATE DATABASE, so interpolation is the only available form.
#pragma warning disable CA2100
		command.CommandText = $"CREATE DATABASE [{name}];";
#pragma warning restore CA2100
		_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);

		return name;
	}

	/// <summary>The pre-tenancy shape: the tables as they existed before the tenant column.</summary>
	private async Task CreatePreTenancyTablesAsync(string database)
	{
		await ExecuteAsync(database, """
			CREATE TABLE [dbo].[MaterializedViews] (
				ViewName   NVARCHAR(256)  NOT NULL,
				ViewId     NVARCHAR(256)  NOT NULL,
				Data       NVARCHAR(MAX)  NOT NULL,
				CreatedAt  DATETIMEOFFSET NOT NULL,
				UpdatedAt  DATETIMEOFFSET NOT NULL,
				CONSTRAINT PK_MaterializedViews PRIMARY KEY CLUSTERED (ViewName, ViewId)
			);
			CREATE TABLE [dbo].[MaterializedViewPositions] (
				ViewName   NVARCHAR(256)  NOT NULL,
				Position   BIGINT         NOT NULL,
				CreatedAt  DATETIMEOFFSET NOT NULL,
				UpdatedAt  DATETIMEOFFSET NOT NULL,
				CONSTRAINT PK_MaterializedViewPositions PRIMARY KEY CLUSTERED (ViewName)
			);
			""").ConfigureAwait(false);
	}

	/// <summary>
	/// Runs the shipped script, split on its batch separators because <c>GO</c> is a client directive that
	/// <see cref="SqlCommand"/> cannot execute.
	/// </summary>
	private async Task ExecuteMigrationAsync(string database)
	{
		var sql = await File.ReadAllTextAsync(ResolveShippedScriptPath()).ConfigureAwait(false);
		await ExecuteAsync(database, sql).ConfigureAwait(false);
	}

	private async Task ExecuteAsync(string database, string sql)
	{
		await using var connection = new SqlConnection(ConnectionStringFor(database));
		await connection.OpenAsync().ConfigureAwait(false);

		foreach (var batch in SplitOnGo(sql))
		{
			await using var command = connection.CreateCommand();
			// CA2100: the batch is either the shipped migration script read from the repository or this
			// class's own pre-tenancy DDL literal. Executing the shipped script verbatim IS the thing under
			// test — parameterising it would test something else.
#pragma warning disable CA2100
			command.CommandText = batch;
#pragma warning restore CA2100
			_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
		}
	}

	private string ConnectionStringFor(string database) =>
		new SqlConnectionStringBuilder(_fixture.ConnectionString) { InitialCatalog = database }.ConnectionString;

	private static IEnumerable<string> SplitOnGo(string sql) =>
		sql.Split(["\nGO\n", "\nGO\r\n", "\r\nGO\r\n"], StringSplitOptions.None)
			.Select(batch => batch.Trim())
			.Where(batch => batch.Length > 0);

	private async Task<bool> ColumnExistsAsync(string database, string table, string column)
	{
		await using var connection = new SqlConnection(ConnectionStringFor(database));
		await connection.OpenAsync().ConfigureAwait(false);
		await using var command = connection.CreateCommand();
		command.CommandText =
			"SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(@table) AND name = @column;";
		_ = command.Parameters.AddWithValue("@table", $"[dbo].[{table}]");
		_ = command.Parameters.AddWithValue("@column", column);

		return (int)(await command.ExecuteScalarAsync().ConfigureAwait(false))! > 0;
	}

	/// <summary>
	/// Locates the shipped script by walking up from the test binary to the repository root. Fails loudly
	/// rather than falling back to an inline copy: a lock that carries its own copy of the DDL only ever
	/// agrees with itself, and the artifact under test is the script the package ships.
	/// </summary>
	private static string ResolveShippedScriptPath()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory is not null)
		{
			var candidate = Path.Combine(
				directory.FullName,
				MigrationRelativePath.Replace('/', Path.DirectorySeparatorChar));
			if (File.Exists(candidate))
			{
				return candidate;
			}

			directory = directory.Parent;
		}

		throw new FileNotFoundException(
			$"The shipped materialized-view migration was not found by walking up from "
			+ $"'{AppContext.BaseDirectory}' looking for '{MigrationRelativePath}'. This lock asserts the "
			+ "behaviour of the script the package ships; it deliberately does not carry its own copy.");
	}
}
