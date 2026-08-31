// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;

using Npgsql;

namespace Excalibur.Integration.Tests.Data.Migrations;

/// <summary>
/// Applies every shipped PostgreSQL script through a plain driver, in the order a consumer applies
/// them, and requires the server to accept all of it.
/// </summary>
/// <remarks>
/// <para>
/// This suite deliberately does NOT use <c>ShippedSchemaScript</c>. That reader strips client
/// meta-commands, and a test that reads through it proves the reader copes with a directive rather than
/// that the file runs — which is how six scripts came to ship carrying <c>\set ON_ERROR_STOP on</c>
/// under a green guard. A consumer has no reader: they hand the file to Npgsql, JDBC, Flyway or
/// Liquibase as shipped. So these arms locate the file themselves and send its bytes unaltered.
/// </para>
/// <para>
/// Each package gets a database of its own, created fresh, and its scripts are applied in shipped
/// numeric order — the create scripts and the migrations, one <see cref="NpgsqlCommand"/> per file, no
/// splitting, no substitution, no stripping. That is the consumer's path end to end, and a directive
/// anywhere in it fails the run with <c>42601 syntax error at or near "\"</c>.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Infrastructure", "Postgres")]
[Collection(PostgresTestCollection.CollectionName)]
public sealed class ShippedPostgresScriptsShould
{
	private readonly PostgresContainerFixture _fixture;

	public ShippedPostgresScriptsShould(PostgresContainerFixture fixture) => _fixture = fixture;

	/// <summary>
	/// Gets each shipped PostgreSQL package as the ordered script sequence a consumer applies.
	/// </summary>
	public static TheoryData<string, string[]> Packages =>
		new()
		{
			{
				"Excalibur.AuditLogging.Postgres",
				[
					"src/Excalibur/Excalibur.AuditLogging.Postgres/Scripts/001_CreateAuditSchema.sql",
					"src/Excalibur/Excalibur.AuditLogging.Postgres/Scripts/002_NarrowTenantIdToPortableMaximum.sql",
				]
			},
			{
				"Excalibur.Compliance.Postgres",
				[
					"src/Excalibur/Excalibur.Compliance.Postgres/Scripts/001_CreateComplianceSchema.sql",
					"src/Excalibur/Excalibur.Compliance.Postgres/Scripts/002_MakeComplianceTenantTotal.sql",
					"src/Excalibur/Excalibur.Compliance.Postgres/Scripts/003_MakeDataInventoryTenantTotal.sql",
					"src/Excalibur/Excalibur.Compliance.Postgres/Scripts/004_ConvergeDefaultToUntenanted.sql",
				]
			},
			{
				"Excalibur.EventSourcing.Postgres",
				[
					"src/Excalibur/Excalibur.EventSourcing.Postgres/Scripts/001_CreateSnapshotSchema.sql",
					"src/Excalibur/Excalibur.EventSourcing.Postgres/Scripts/002_MigrateSnapshotsToKeyedSentinel.sql",
					"src/Excalibur/Excalibur.EventSourcing.Postgres/Scripts/003_MakeMaterializedViewsTenantTotal.sql",
					"src/Excalibur/Excalibur.EventSourcing.Postgres/Scripts/004_CreateEventStoreSchema.sql",
					"src/Excalibur/Excalibur.EventSourcing.Postgres/Scripts/005_MakeEventStreamIdentityTenantScoped.sql",
					"src/Excalibur/Excalibur.EventSourcing.Postgres/Scripts/006_ConvergeUntenantedToDefaultTenant.sql",
					"src/Excalibur/Excalibur.EventSourcing.Postgres/Scripts/007_NarrowSnapshotTenantIdToPortableMaximum.sql",
				]
			},
		};

	[Theory]
	[MemberData(nameof(Packages))]
	public async Task Apply_EveryShippedScript_ThroughAPlainNpgsqlConnection(string package, string[] scripts)
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			_fixture.InitializationError
			?? "PostgreSQL must be reachable: this arm is the only thing that proves a consumer can run "
			+ "the shipped scripts at all, and a skip would restore the silence it exists to break.");

		await using var database = await ScratchDatabase
			.CreateAsync(_fixture.ConnectionString, package, TestContext.Current.CancellationToken)
			.ConfigureAwait(false);

		await using var connection = new NpgsqlConnection(database.ConnectionString);
		await connection.OpenAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

		foreach (var script in scripts)
		{
			var sql = ShippedScript.RawBytesOf(script);

			// One command, the file's bytes, nothing in between. This is what NpgsqlCommand does with a
			// multi-statement string and what every driver-based migration runner does with a file.
			//
			// CA2100: the command text is a script this repository ships, read from its own source tree.
			// Parameterising it is not available and not meaningful -- the whole point of the arm is that
			// the file reaches the server unaltered.
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
			await using var command = new NpgsqlCommand(sql, connection);
#pragma warning restore CA2100
			command.CommandTimeout = 120;

			var failure = await Record
				.ExceptionAsync(() => command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken))
				.ConfigureAwait(false);

			failure.ShouldBeNull(
				$"'{script}' did not apply through a plain Npgsql connection. Every consumer who is not "
				+ "running psql — Npgsql, JDBC, Flyway, Liquibase, a migration runner — sends the file as "
				+ "it ships, so a line only psql understands stops the run having provisioned nothing.");
		}
	}

	[Theory]
	[MemberData(nameof(Packages))]
	public async Task Reach_TheSameSchema_WhenEveryScriptIsAppliedTwice(string package, string[] scripts)
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			_fixture.InitializationError ?? "PostgreSQL must be reachable for the re-run arm.");

		await using var database = await ScratchDatabase
			.CreateAsync(_fixture.ConnectionString, $"{package}_rerun", TestContext.Current.CancellationToken)
			.ConfigureAwait(false);

		await ApplyAllAsync(database.ConnectionString, scripts).ConfigureAwait(false);
		var afterFirstPass = await CatalogFingerprintAsync(database.ConnectionString).ConfigureAwait(false);

		// A consumer re-runs a migration set: a retried pipeline, a second environment, an operator who is
		// not sure whether it took. Every one of these scripts claims to be re-runnable, and a claim that
		// is only in a header is not a property.
		await ApplyAllAsync(database.ConnectionString, scripts).ConfigureAwait(false);
		var afterSecondPass = await CatalogFingerprintAsync(database.ConnectionString).ConfigureAwait(false);

		afterSecondPass.ShouldBe(
			afterFirstPass,
			$"applying {package}'s scripts a second time changed the schema. These scripts are documented "
			+ "as safe to re-run, and an operator who cannot tell whether the first pass completed has "
			+ "nothing but that promise to go on.");
	}

	private static async Task ApplyAllAsync(string connectionString, IEnumerable<string> scripts)
	{
		await using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

		foreach (var script in scripts)
		{
			// CA2100: as above -- the package's own shipped DDL, read from this repository's source tree.
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
			await using var command = new NpgsqlCommand(ShippedScript.RawBytesOf(script), connection);
#pragma warning restore CA2100
			command.CommandTimeout = 120;
			_ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Reads back the shape a consumer depends on: every column with its type, declared width,
	/// nullability, default and collation, plus every key and index, ordered so the text is comparable.
	/// </summary>
	private static async Task<string> CatalogFingerprintAsync(string connectionString)
	{
		const string ColumnsSql = """
			SELECT n.nspname, c.relname, a.attname,
			       format_type(a.atttypid, a.atttypmod) AS coltype,
			       a.attnotnull,
			       COALESCE(pg_get_expr(d.adbin, d.adrelid), '') AS coldefault,
			       COALESCE(co.collname, '') AS collation
			  FROM pg_attribute a
			  JOIN pg_class c ON c.oid = a.attrelid
			  JOIN pg_namespace n ON n.oid = c.relnamespace
			  LEFT JOIN pg_attrdef d ON d.adrelid = a.attrelid AND d.adnum = a.attnum
			  LEFT JOIN pg_collation co ON co.oid = a.attcollation
			 WHERE n.nspname NOT IN ('pg_catalog', 'information_schema', 'pg_toast')
			   AND a.attnum > 0 AND NOT a.attisdropped AND c.relkind IN ('r', 'm', 'v', 'p')
			 ORDER BY n.nspname, c.relname, a.attname
			""";

		// Constraint and index NAMES are excluded on purpose where PostgreSQL generates them: this script
		// set drops and recreates unnamed primary keys, so the generated name is run-to-run noise. The
		// DEFINITION is what a consumer depends on and it is what is compared.
		const string ConstraintsSql = """
			SELECT n.nspname, c.relname, con.contype, pg_get_constraintdef(con.oid) AS definition
			  FROM pg_constraint con
			  JOIN pg_class c ON c.oid = con.conrelid
			  JOIN pg_namespace n ON n.oid = c.relnamespace
			 WHERE n.nspname NOT IN ('pg_catalog', 'information_schema')
			 ORDER BY n.nspname, c.relname, con.contype, pg_get_constraintdef(con.oid)
			""";

		const string IndexesSql = """
			SELECT schemaname, tablename,
			       REGEXP_REPLACE(indexdef, ' INDEX [^ ]+ ON ', ' INDEX ON ') AS indexdef
			  FROM pg_indexes
			 WHERE schemaname NOT IN ('pg_catalog', 'information_schema')
			 ORDER BY schemaname, tablename,
			          REGEXP_REPLACE(indexdef, ' INDEX [^ ]+ ON ', ' INDEX ON ')
			""";

		await using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

		var lines = new List<string>();

		foreach (var (section, sql) in new[]
		{
			("COLUMN", ColumnsSql), ("CONSTRAINT", ConstraintsSql), ("INDEX", IndexesSql),
		})
		{
			// CA2100: the three catalogue queries are const strings declared immediately above.
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
			await using var command = new NpgsqlCommand(sql, connection);
#pragma warning restore CA2100
			await using var reader = await command
				.ExecuteReaderAsync(TestContext.Current.CancellationToken)
				.ConfigureAwait(false);

			while (await reader.ReadAsync(TestContext.Current.CancellationToken).ConfigureAwait(false))
			{
				var values = new string[reader.FieldCount];

				for (var i = 0; i < reader.FieldCount; i++)
				{
					values[i] = reader.IsDBNull(i)
						? "<null>"
						: Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture) ?? string.Empty;
				}

				lines.Add($"{section}|{string.Join('|', values)}");
			}
		}

		return string.Join('\n', lines);
	}
}
