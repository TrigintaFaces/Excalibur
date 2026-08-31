// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;
using System.Text.RegularExpressions;

using Microsoft.Data.SqlClient;

using Tests.Shared.Helpers;

#pragma warning disable CA2100 // Object names in the statements below are constants, not user input.

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Applies the outbox schema script the package ships the way a consumer applies it: through a plain
/// connection, with none of our tooling in the path.
/// </summary>
/// <remarks>
/// <para>
/// Every other SQL Server fixture provisions its schema through <see cref="ShippedSchemaScript"/>, which
/// resolves sqlcmd variables and strips client meta-commands before a driver ever sees the file. That is
/// correct for a fixture, and it is invisible to a consumer, who has no such helper — so for as long as the
/// shipped script needed one, our suites were green while a consumer using SSMS, Azure Data Studio, DbUp,
/// Flyway, or a connection of their own could not create the tables at all. The outbox delivers nothing
/// without those tables, which put a provisioning obstacle in front of the at-least-once guarantee on any
/// host with no sqlcmd.
/// </para>
/// <para>
/// So this reads the file with <see cref="File.ReadAllTextAsync(string, CancellationToken)"/> and splits it
/// only on <c>GO</c>, which every SQL Server runner does. Anything a helper would have removed reaches the
/// server here and fails the run, which is the point: these arms are RED against the script as it stood,
/// where the first batch dies with <c>Incorrect syntax near ':'</c>.
/// </para>
/// </remarks>
[Collection(SqlServerOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerOutboxSchemaScriptShould : IClassFixture<SqlServerOutboxStoreContainerFixture>
{
	private const string ScriptPath =
		"src/Excalibur/Excalibur.Outbox.SqlServer/Scripts/001_CreateOutboxSchema.sql";

	private static readonly Regex BatchSeparator =
		new(@"^[ \t]*GO[ \t]*\r?$", RegexOptions.Multiline | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(5));

	private readonly SqlServerOutboxStoreContainerFixture _fixture;

	/// <summary>Initializes a new instance of the <see cref="SqlServerOutboxSchemaScriptShould"/> class.</summary>
	/// <param name="fixture">The SQL Server container fixture.</param>
	public SqlServerOutboxSchemaScriptShould(SqlServerOutboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <summary>
	/// Provisions every object the outbox needs, from the shipped file, through a plain driver.
	/// </summary>
	/// <returns>A task representing the asynchronous operation.</returns>
	[Fact]
	public async Task Provision_TheWholeSchema_ThroughAPlainDriver()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"SQL Server container must be available - the shipped-script lock is never skipped. A skipped "
			+ "arm is how a script no consumer can run stays green through a release.");

		await using var connection = await ConnectToAnEmptyDatabaseAsync().ConfigureAwait(false);

		await ApplyShippedScriptAsync(connection).ConfigureAwait(false);

		foreach (var table in new[] { "OutboxMessages", "OutboxFence", "OutboxMessageTransports", "DeadLetterQueue" })
		{
			(await ScalarAsync(connection, $"SELECT OBJECT_ID(N'[dbo].[{table}]', N'U')").ConfigureAwait(false))
				.ShouldNotBe(
					DBNull.Value,
					$"[dbo].[{table}] is created by the shipped script, and the outbox does not run without it.");
		}

		// The tenant column is what most of the script's length exists to get right, and its shape decides
		// whether a tenant predicate can fail open. Assert what landed, not that a statement ran.
		foreach (var table in new[] { "OutboxMessages", "OutboxMessageTransports", "DeadLetterQueue" })
		{
			var shape = await ScalarAsync(
				connection,
				"SELECT CONCAT(ty.name COLLATE DATABASE_DEFAULT, '|', c.max_length, '|', c.is_nullable, '|', "
				+ "c.collation_name COLLATE DATABASE_DEFAULT) "
				+ "FROM sys.columns c "
				+ "JOIN sys.types ty ON ty.user_type_id = c.user_type_id "
				+ $"WHERE c.object_id = OBJECT_ID(N'[dbo].[{table}]', N'U') AND c.name = N'TenantId'")
				.ConfigureAwait(false);

			shape.ShouldBe(
				"nvarchar|128|0|Latin1_General_BIN2",
				$"[dbo].[{table}].TenantId must land as NVARCHAR(64) NOT NULL in a binary collation. A "
				+ "case-insensitive collation lets 'Acme' match 'acme', so the tenant predicate fails open; "
				+ "a nullable column lets an unreachable row exist. max_length is in bytes, so 64 is 128.");
		}

		// LIVENESS for the indexes: a script that created every table and silently skipped every index would
		// satisfy the arms above while leaving the claim predicate to scan.
		foreach (var (table, index) in new[]
		{
			("OutboxMessages", "IX_OutboxMessages_Status_CreatedAt"),
			("OutboxMessages", "IX_OutboxMessages_Claim"),
			("DeadLetterQueue", "IX_DeadLetterQueue_EnqueuedAt"),
		})
		{
			(await ScalarAsync(
				connection,
				$"SELECT name FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[{table}]', N'U') "
				+ $"AND name = N'{index}'").ConfigureAwait(false))
				.ShouldBe(index, $"'{index}' backs a predicate the store issues on every drain.");
		}

		(await ScalarAsync(
			connection,
			"SELECT name FROM sys.key_constraints WHERE parent_object_id = "
			+ "OBJECT_ID(N'[dbo].[DeadLetterQueue]', N'U') AND name = N'PK_DeadLetterQueue'")
			.ConfigureAwait(false))
			.ShouldBe(
				"PK_DeadLetterQueue",
				"The dead-letter key is (Id, TenantId), which is what makes a replay re-enter the tenant it "
				+ "came from rather than collide with another one.");

		(await ScalarAsync(
			connection,
			"SELECT name FROM sys.foreign_keys WHERE name = N'FK_OutboxMessageTransports_OutboxMessages'")
			.ConfigureAwait(false))
			.ShouldBe(
				"FK_OutboxMessageTransports_OutboxMessages",
				"A transport row without its parent message is a delivery record for nothing.");
	}

	/// <summary>
	/// Re-applies the shipped script, which a consumer does on every deploy, and finds the schema unchanged.
	/// </summary>
	/// <returns>A task representing the asynchronous operation.</returns>
	[Fact]
	public async Task Apply_Twice_WithoutChangingTheSchema()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"SQL Server container must be available - the idempotency lock is never skipped.");

		await using var connection = await ConnectToAnEmptyDatabaseAsync().ConfigureAwait(false);

		await ApplyShippedScriptAsync(connection).ConfigureAwait(false);
		var afterFirst = await FingerprintAsync(connection).ConfigureAwait(false);

		// The script both creates and upgrades, so a consumer runs it against a database it has already
		// provisioned. An unguarded upgrade block throws here rather than in theirs.
		await ApplyShippedScriptAsync(connection).ConfigureAwait(false);
		var afterSecond = await FingerprintAsync(connection).ConfigureAwait(false);

		afterFirst.ShouldNotBeNullOrWhiteSpace(
			"An empty fingerprint would let two empty schemas compare equal, so the arm below would pass "
			+ "over a script that created nothing.");
		afterSecond.ShouldBe(afterFirst, "Re-applying the shipped script must leave the schema untouched.");
	}

	/// <summary>
	/// Reads the shipped file and runs it as a driver-based runner does: no variable substitution, no
	/// directive stripping, split only on the batch separator.
	/// </summary>
	/// <param name="connection">The open connection to provision through.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	private static async Task ApplyShippedScriptAsync(SqlConnection connection)
	{
		var sql = await File.ReadAllTextAsync(
			ShippedSchemaScript.Resolve(ScriptPath),
			TestContext.Current.CancellationToken).ConfigureAwait(false);

		var batches = BatchSeparator.Split(sql)
			.Select(static batch => batch.Trim())
			.Where(static batch => batch.Length > 0)
			.ToList();

		batches.ShouldNotBeEmpty("Splitting the shipped script must leave statements to run.");

		foreach (var batch in batches)
		{
			await using var command = new SqlCommand(batch, connection);
			_ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>Renders the resulting schema as one stable string, so two applications can be compared.</summary>
	/// <param name="connection">The open connection to read from.</param>
	/// <returns>The schema fingerprint.</returns>
	private static async Task<string> FingerprintAsync(SqlConnection connection)
	{
		var fingerprint = await ScalarAsync(
			connection,
			"SELECT STRING_AGG(line, CHAR(10)) WITHIN GROUP (ORDER BY line) FROM ("
			+ "  SELECT CONCAT(t.name COLLATE DATABASE_DEFAULT, '.', c.name COLLATE DATABASE_DEFAULT, '|', "
			+ "         ty.name COLLATE DATABASE_DEFAULT, '|', c.max_length, '|', c.is_nullable, '|', "
			+ "         ISNULL(c.collation_name COLLATE DATABASE_DEFAULT, 'n/a')) AS line"
			+ "  FROM sys.columns c"
			+ "  JOIN sys.tables t ON t.object_id = c.object_id"
			+ "  JOIN sys.types ty ON ty.user_type_id = c.user_type_id"
			+ "  UNION ALL"
			+ "  SELECT CONCAT(t.name COLLATE DATABASE_DEFAULT, '|', i.name COLLATE DATABASE_DEFAULT, '|', "
			+ "         i.type_desc COLLATE DATABASE_DEFAULT)"
			+ "  FROM sys.indexes i"
			+ "  JOIN sys.tables t ON t.object_id = i.object_id"
			+ "  WHERE i.name IS NOT NULL"
			+ ") AS shape").ConfigureAwait(false);

		return fingerprint as string ?? string.Empty;
	}

	private static async Task<object> ScalarAsync(SqlConnection connection, string sql)
	{
		await using var command = new SqlCommand(sql, connection);
		return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken).ConfigureAwait(false)
			?? DBNull.Value;
	}

	/// <summary>
	/// Opens a connection to a database this test alone provisions, so it observes the shipped script's own
	/// effect rather than one the shared fixture already applied through the helper.
	/// </summary>
	/// <returns>An open connection to an empty database.</returns>
	private async Task<SqlConnection> ConnectToAnEmptyDatabaseAsync()
	{
		var name = "shipped_script_" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

		await using (var admin = new SqlConnection(_fixture.ConnectionString))
		{
			await admin.OpenAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
			await using var create = new SqlCommand($"CREATE DATABASE [{name}]", admin);
			_ = await create.ExecuteNonQueryAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
		}

		var connection = new SqlConnection(
			new SqlConnectionStringBuilder(_fixture.ConnectionString) { InitialCatalog = name }.ConnectionString);

		await connection.OpenAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

		return connection;
	}
}
