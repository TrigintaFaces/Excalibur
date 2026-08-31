// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Npgsql;

namespace Excalibur.Integration.Tests.Data.Migrations;

/// <summary>
/// Covers the shipped PostgreSQL migrations that NARROW a tenant column to the portable maximum.
/// </summary>
/// <remarks>
/// <para>
/// These shipped without any test at all. They are migrations: they run once, against a consumer's
/// real database, and they cannot be un-run. The properties below are the ones an upgrade actually
/// depends on — it converges a database at the prior version, it is safe to run twice, it refuses a
/// table it does not recognise instead of reporting success over it, and above all it REFUSES rather
/// than truncates.
/// </para>
/// <para>
/// The truncation arm is the one that matters most and the reason these are tested together. Both
/// columns hold a tenant identifier. Shortening one to 64 characters does not produce a slightly wrong
/// label — it merges two tenants whose identifiers agree in their first 64 characters into one. For the
/// snapshot table the column is part of <c>PRIMARY KEY (aggregate_id, aggregate_type, tenant_id)</c>, so
/// the merge is a KEY collision: one tenant's snapshot starts satisfying another tenant's upsert target.
/// A migration that truncated quietly would do that to live data with nothing to show for it.
/// </para>
/// <para>
/// Every arm applies the script exactly as shipped, through a plain <see cref="NpgsqlCommand"/>. Nothing
/// here reads through <c>ShippedSchemaScript</c>, for the reason given in <see cref="ShippedScript"/>.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Infrastructure", "Postgres")]
[Collection(PostgresTestCollection.CollectionName)]
public sealed class PostgresTenantNarrowingMigrationShould
{
	private const string AuditCreate =
		"src/Excalibur/Excalibur.AuditLogging.Postgres/Scripts/001_CreateAuditSchema.sql";

	private const string AuditNarrow =
		"src/Excalibur/Excalibur.AuditLogging.Postgres/Scripts/002_NarrowTenantIdToPortableMaximum.sql";

	private const string SnapshotCreate =
		"src/Excalibur/Excalibur.EventSourcing.Postgres/Scripts/001_CreateSnapshotSchema.sql";

	private const string SnapshotNarrow =
		"src/Excalibur/Excalibur.EventSourcing.Postgres/Scripts/007_NarrowSnapshotTenantIdToPortableMaximum.sql";

	private const string SagaCreate =
		"src/Excalibur/Excalibur.Saga.Postgres/Scripts/01-SagaSchema.sql";

	private const string SagaNarrow =
		"src/Excalibur/Excalibur.Saga.Postgres/Scripts/02-NarrowTenantIdToPortableMaximum.sql";

	private const string InboxCreate =
		"src/Excalibur/Excalibur.Inbox.Postgres/Scripts/001_CreateInboxSchema.MultiTenant.sql";

	private const string InboxNarrow =
		"src/Excalibur/Excalibur.Inbox.Postgres/Scripts/003_NarrowTenantIdToPortableMaximum.sql";

	private readonly PostgresContainerFixture _fixture;

	public PostgresTenantNarrowingMigrationShould(PostgresContainerFixture fixture) => _fixture = fixture;

	/// <summary>
	/// Gets the narrowing migrations, each with the table it narrows and the type the version before
	/// it declared.
	/// </summary>
	/// <remarks>
	/// The prior type is reconstructed by applying the CURRENT create script and widening the column back
	/// to what the earlier one declared. That is deliberate: pinning a copy of the old create script into
	/// the test would freeze a second definition of the table that nothing keeps in step with the shipped
	/// one, and the arms would then drift into testing a table this project no longer ships.
	/// </remarks>
	public static TheoryData<string, string, string, string, string> Migrations =>
		new()
		{
			{ "audit 002", AuditCreate, AuditNarrow, "\"audit\".\"audit_events\"", "TEXT" },
			{
				"snapshot 007", SnapshotCreate, SnapshotNarrow, "public.event_store_snapshots",
				"VARCHAR(255)"
			},
			{ "saga 02", SagaCreate, SagaNarrow, "dispatch.sagas", "VARCHAR(200)" },
			{ "inbox 003", InboxCreate, InboxNarrow, "public.inbox_messages", "TEXT" },
		};

	[Theory]
	[MemberData(nameof(Migrations))]
	public async Task Narrow_TheTenantColumn_OnADatabaseAtThePriorVersion(
		string label,
		string createScript,
		string narrowScript,
		string table,
		string priorType)
	{
		await using var db = await ArrangeAtPriorVersionAsync(label, createScript, table, priorType)
			.ConfigureAwait(false);

		await ApplyAsync(db.ConnectionString, narrowScript).ConfigureAwait(false);

		var actual = await TenantColumnTypeAsync(db.ConnectionString, table).ConfigureAwait(false);

		actual.ShouldBe(
			"character varying(64)",
			$"{label} did not converge a database at the prior version. That database is the only reason "
			+ "the script exists — the create script guards on the table's existence, so re-running it is "
			+ "not an upgrade path.");
	}

	[Theory]
	[MemberData(nameof(Migrations))]
	public async Task Change_Nothing_WhenRunAgainstAnAlreadyConvergedDatabase(
		string label,
		string createScript,
		string narrowScript,
		string table,
		string priorType)
	{
		await using var db = await ArrangeAtPriorVersionAsync(label, createScript, table, priorType)
			.ConfigureAwait(false);

		await ApplyAsync(db.ConnectionString, narrowScript).ConfigureAwait(false);
		var afterFirstRun = await TenantColumnTypeAsync(db.ConnectionString, table).ConfigureAwait(false);

		// An operator who cannot tell whether the first pass completed will run it again, and a database
		// provisioned by the CURRENT create script is already converged before anyone runs this at all.
		// Both are ordinary, and neither may fail or alter anything.
		var failure = await Record
			.ExceptionAsync(() => ApplyAsync(db.ConnectionString, narrowScript))
			.ConfigureAwait(false);

		failure.ShouldBeNull($"{label} failed on a second run against an already-converged database.");

		var afterSecondRun = await TenantColumnTypeAsync(db.ConnectionString, table).ConfigureAwait(false);
		afterSecondRun.ShouldBe(afterFirstRun, $"{label} altered an already-converged column on re-run.");
	}

	[Theory]
	[MemberData(nameof(Migrations))]
	public async Task Refuse_ATableItDoesNotRecognise_RatherThanReportingSuccessOverIt(
		string label,
		string createScript,
		string narrowScript,
		string table,
		string priorType)
	{
		await using var db = await ArrangeAtPriorVersionAsync(label, createScript, table, priorType)
			.ConfigureAwait(false);

		// The precondition these scripts state is that the table came from some version of the create
		// script. A table of the right NAME with no tenant column at all did not, and narrowing a column
		// that is not there is not something to shrug at: a silent success here is an operator ticking off
		// a migration that never ran, on a table nothing else is going to check.
		await ExecuteAsync(db.ConnectionString, $"ALTER TABLE {table} DROP COLUMN tenant_id")
			.ConfigureAwait(false);

		var failure = await Record
			.ExceptionAsync(() => ApplyAsync(db.ConnectionString, narrowScript))
			.ConfigureAwait(false);

		_ = failure.ShouldBeOfType<PostgresException>(
			$"{label} did not refuse a table with no tenant column. It must fail loudly rather than "
			+ "complete over a table it cannot have been meant for.");

		failure.Message.ShouldContain(
			"REFUSED",
			Case.Sensitive,
			$"{label} failed, but not with its own refusal — so the operator gets an error that names "
			+ "neither the cause nor the remedy.");
	}

	[Theory]
	[MemberData(nameof(Migrations))]
	public async Task Refuse_RatherThanTruncate_WhenARowHoldsAnOverlongTenantIdentifier(
		string label,
		string createScript,
		string narrowScript,
		string table,
		string priorType)
	{
		await using var db = await ArrangeAtPriorVersionAsync(label, createScript, table, priorType)
			.ConfigureAwait(false);

		// Two tenants that agree in their first 64 characters and differ after. Truncation makes them one.
		var shared = new string('t', 64);
		var tenantA = shared + "-alpha";
		var tenantB = shared + "-beta";

		await InsertRowAsync(db.ConnectionString, table, tenantA).ConfigureAwait(false);
		await InsertRowAsync(db.ConnectionString, table, tenantB).ConfigureAwait(false);

		var failure = await Record
			.ExceptionAsync(() => ApplyAsync(db.ConnectionString, narrowScript))
			.ConfigureAwait(false);

		_ = failure.ShouldBeOfType<PostgresException>(
			$"{label} did not refuse a row whose tenant identifier is longer than 64 characters. "
			+ "Narrowing it would have merged two tenants into one scope, silently, on live data.");

		failure.Message.ShouldContain(
			"REFUSED",
			Case.Sensitive,
			$"{label} must name what it refused and what to do about it.");

		// The refusal is only worth anything if it left the database alone. A script that raises AFTER
		// altering the column has still merged the tenants.
		var typeAfter = await TenantColumnTypeAsync(db.ConnectionString, table).ConfigureAwait(false);
		typeAfter.ShouldNotBe(
			"character varying(64)",
			$"{label} raised, but the column was narrowed anyway — the transaction did not roll the ALTER "
			+ "back, so the refusal is a message rather than a protection.");

		var survivors = await DistinctTenantsAsync(db.ConnectionString, table).ConfigureAwait(false);

		survivors.ShouldBe(
			[tenantA, tenantB],
			ignoreOrder: true,
			$"{label} lost or shortened a tenant identifier. Both rows must survive byte-for-byte: the "
			+ "whole point of refusing is that no tenant is quietly re-filed under another's identity.");
	}

	private async Task<ScratchDatabase> ArrangeAtPriorVersionAsync(
		string label,
		string createScript,
		string table,
		string priorType)
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			_fixture.InitializationError
			?? "PostgreSQL must be reachable: these arms cover migrations that run once against a "
			+ "consumer's real database, and a skip is indistinguishable from never having tested them.");

		var db = await ScratchDatabase
			.CreateAsync(_fixture.ConnectionString, $"narrow_{label}", TestContext.Current.CancellationToken)
			.ConfigureAwait(false);

		try
		{
			await ApplyAsync(db.ConnectionString, createScript).ConfigureAwait(false);
			await ExecuteAsync(db.ConnectionString, $"ALTER TABLE {table} ALTER COLUMN tenant_id TYPE {priorType}")
				.ConfigureAwait(false);

			// LIVENESS on the arrangement itself. Every arm below concludes something from what the script
			// did to this column, and if the widen-back silently did not happen, the database is already
			// converged and every arm passes while testing nothing.
			var arranged = await TenantColumnTypeAsync(db.ConnectionString, table).ConfigureAwait(false);

			arranged.ShouldNotBe(
				"character varying(64)",
				$"the {label} fixture failed to put the column back to its prior type, so these arms would "
				+ "run against an already-converged database and prove nothing.");

			return db;
		}
		catch
		{
			await db.DisposeAsync().ConfigureAwait(false);
			throw;
		}
	}

	private static async Task InsertRowAsync(string connectionString, string table, string tenantId)
	{
		// Only the NOT NULL columns are supplied; everything else takes its default. Both tables are
		// addressed by their shipped names, so a rename in the create script surfaces here.
		string sql;

		if (table.Contains("audit_events", StringComparison.Ordinal))
		{
			sql = $"""
				INSERT INTO {table} (event_id, event_type, action, outcome, timestamp, tenant_id)
				VALUES (@id, 1, 'act', 1, now(), @tenant)
				""";
		}
		else if (table.Contains("sagas", StringComparison.Ordinal))
		{
			// saga_id is a uuid, so the shared row id is cast rather than bound as text.
			sql = $"""
				INSERT INTO {table} (saga_id, saga_type, state_json, tenant_id)
				VALUES (@id::uuid, 'TestSaga', to_jsonb(1), @tenant)
				""";
		}
		else if (table.Contains("inbox_messages", StringComparison.Ordinal))
		{
			// tenant_id is the third component of the primary key, so two rows sharing this id but
			// carrying different tenants are distinct here and become one the moment it is truncated.
			sql = $"""
				INSERT INTO {table}
					(message_id, handler_type, message_type, payload, received_at, tenant_id)
				VALUES (@id, 'Handler', 'Msg', '\x00'::bytea, now(), @tenant)
				""";
		}
		else
		{
			sql = $"""
				INSERT INTO {table}
					(snapshot_id, aggregate_id, aggregate_type, version, data, created_at, tenant_id)
				VALUES (@id, @id, 'Agg', 1, '\x00'::bytea, now(), @tenant)
				""";
		}

		await using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

		// CA2100: the two command texts are const-shaped strings selected above; the tenant identifier and
		// row id are bound as parameters.
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
		await using var command = new NpgsqlCommand(sql, connection);
#pragma warning restore CA2100
		_ = command.Parameters.AddWithValue("id", Guid.NewGuid().ToString("N"));
		_ = command.Parameters.AddWithValue("tenant", tenantId);
		_ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
	}

	private static async Task<IReadOnlyList<string>> DistinctTenantsAsync(string connectionString, string table)
	{
		await using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

		// CA2100: the table name comes from this class's own const-backed theory data.
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
		await using var command = new NpgsqlCommand($"SELECT DISTINCT tenant_id FROM {table}", connection);
#pragma warning restore CA2100
		await using var reader = await command
			.ExecuteReaderAsync(TestContext.Current.CancellationToken)
			.ConfigureAwait(false);

		var tenants = new List<string>();

		while (await reader.ReadAsync(TestContext.Current.CancellationToken).ConfigureAwait(false))
		{
			tenants.Add(reader.GetString(0));
		}

		return tenants;
	}

	private static async Task<string> TenantColumnTypeAsync(string connectionString, string table)
	{
		await using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

		await using var command = new NpgsqlCommand(
			"""
			SELECT format_type(a.atttypid, a.atttypmod)
			  FROM pg_attribute a
			 WHERE a.attrelid = @table::regclass AND a.attname = 'tenant_id' AND NOT a.attisdropped
			""",
			connection);

		_ = command.Parameters.AddWithValue("table", table);

		var value = await command
			.ExecuteScalarAsync(TestContext.Current.CancellationToken)
			.ConfigureAwait(false);

		return value as string ?? "<absent>";
	}

	private static async Task ApplyAsync(string connectionString, string script)
	{
		await using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

		// CA2100: the package's own shipped DDL, sent exactly as it ships.
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
		await using var command = new NpgsqlCommand(ShippedScript.RawBytesOf(script), connection);
#pragma warning restore CA2100
		command.CommandTimeout = 120;
		_ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
	}

	private static async Task ExecuteAsync(string connectionString, string sql)
	{
		await using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

		// CA2100: fixture DDL built from this class's own const-backed theory data.
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
		await using var command = new NpgsqlCommand(sql, connection);
#pragma warning restore CA2100
		_ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
	}
}
