// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;
using System.Reflection;

using Excalibur.Integration.Tests.Data.EventStore;

using Microsoft.Data.SqlClient;

namespace Excalibur.Integration.Tests.EventSourcing.SqlServer;

/// <summary>
/// Locks the TOTALITY of <c>EventStoreEvents.TenantId</c> against a real SQL Server, for both the
/// fresh-install schema and the upgrade script.
/// </summary>
/// <remarks>
/// <para>
/// The property under test is that there is exactly ONE way to say "this event has no tenant" — the
/// reserved <c>__untenanted__</c> sentinel — rather than two (the sentinel and SQL <c>NULL</c>). While
/// both spellings exist, every read has to fold them together, and a fold applied on one path and not
/// another is the defect class this is guarding.
/// </para>
/// <para>
/// It runs against a real database on purpose. Totality here is a property of the SCHEMA, not of any
/// C# type: whether a <c>DEFAULT</c> fires on an omitted column, whether <c>ALTER COLUMN ... NOT NULL</c>
/// is accepted after a backfill, and whether a column keeps its binary collation across that ALTER are
/// all answered by the server and by nothing else. A mocked or in-memory store cannot reproduce any of
/// them, and would certify this as working while the shipped DDL did the opposite.
/// </para>
/// <para>
/// It provisions from the DDL the package actually SHIPS, for the reason given on
/// <see cref="ShippedEventStoreSchema"/>: a hand-written copy in the test would drift from the script
/// consumers run, and the drift would be invisible until it wasn't.
/// </para>
/// <para>
/// NOT skip-gated. A Docker-unavailable run FAILS rather than passing vacuously — a schema lock that
/// goes green by never executing is the failure mode it exists to prevent.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Database", "SqlServer")]
[Trait("Component", "EventStore")]
public sealed class SqlServerEventTenantTotalityShould(SqlServerEventStoreContainerFixture fixture)
	: IClassFixture<SqlServerEventStoreContainerFixture>
{
	private const string Sentinel = "__untenanted__";
	private const string MigrationScriptFileName = "004_MakeEventTenantTotal.sql";

	private readonly SqlServerEventStoreContainerFixture _fixture = fixture;

	private string _connectionString => _fixture.ConnectionString;

	/// <summary>
	/// FRESH INSTALL: a writer that omits the tenant column entirely gets the sentinel, not NULL.
	/// </summary>
	/// <remarks>
	/// This is the arm that fails against the previous schema. The column was declared
	/// <c>NVARCHAR(255) ... NULL</c> with no default, so an INSERT omitting it stored NULL and the row
	/// came back holding the second spelling of "untenanted".
	/// </remarks>
	[Fact]
	public async Task DefaultAnOmittedTenantToTheSentinelOnAFreshInstall()
	{
		RequireDocker();
		await CreateFreshShippedSchemaAsync().ConfigureAwait(false);

		await ExecuteAsync(
			"""
			INSERT INTO [dbo].[EventStoreEvents]
				([EventId], [AggregateId], [AggregateType], [EventType], [EventData], [Version], [Timestamp])
			VALUES (N'e-1', N'agg-1', N'Order', N'Created', 0x01, 1, SYSDATETIMEOFFSET());
			""").ConfigureAwait(false);

		var stored = await ScalarAsync<string>(
			"SELECT [TenantId] FROM [dbo].[EventStoreEvents] WHERE [EventId] = N'e-1';").ConfigureAwait(false);

		stored.ShouldBe(
			Sentinel,
			"an event written without a tenant must be stored as the untenanted PARTITION, not as an absent value");

		(await IsTenantColumnNullableAsync().ConfigureAwait(false)).ShouldBeFalse(
			"the fresh-install schema must not be able to represent a NULL tenant at all");
	}

	/// <summary>
	/// FRESH INSTALL, liveness: a real tenant is stored verbatim and is not absorbed by the default.
	/// </summary>
	/// <remarks>
	/// Paired with the arm above on purpose. A schema that stamped EVERY row with the sentinel would
	/// satisfy "no row holds NULL" perfectly while destroying tenant identity, and nothing else here
	/// would notice.
	/// </remarks>
	[Fact]
	public async Task StoreARealTenantVerbatimRatherThanApplyingTheDefault()
	{
		RequireDocker();
		await CreateFreshShippedSchemaAsync().ConfigureAwait(false);

		await ExecuteAsync(
			"""
			INSERT INTO [dbo].[EventStoreEvents]
				([EventId], [AggregateId], [AggregateType], [EventType], [EventData], [Version], [Timestamp], [TenantId])
			VALUES (N'e-1', N'agg-1', N'Order', N'Created', 0x01, 1, SYSDATETIMEOFFSET(), N'acme');
			""").ConfigureAwait(false);

		var stored = await ScalarAsync<string>(
			"SELECT [TenantId] FROM [dbo].[EventStoreEvents] WHERE [EventId] = N'e-1';").ConfigureAwait(false);

		stored.ShouldBe("acme", "a real tenant must survive the write unchanged");
	}

	/// <summary>
	/// FRESH INSTALL: the closed column actually refuses NULL, rather than merely looking closed.
	/// </summary>
	/// <remarks>
	/// Asserting the catalog flag alone would pass against a column whose constraint was somehow not in
	/// force. This drives a real INSERT and requires the server to reject it.
	/// </remarks>
	[Fact]
	public async Task RejectAnExplicitNullTenantOnceTheColumnIsTotal()
	{
		RequireDocker();
		await CreateFreshShippedSchemaAsync().ConfigureAwait(false);

		var write = async () => await ExecuteAsync(
			"""
			INSERT INTO [dbo].[EventStoreEvents]
				([EventId], [AggregateId], [AggregateType], [EventType], [EventData], [Version], [Timestamp], [TenantId])
			VALUES (N'e-null', N'agg-1', N'Order', N'Created', 0x01, 1, SYSDATETIMEOFFSET(), NULL);
			""").ConfigureAwait(false);

		_ = await write.ShouldThrowAsync<SqlException>(
			"an explicit NULL tenant must be refused by the database, not silently accepted");
	}

	/// <summary>
	/// UPGRADE: a row written as NULL before the migration reads back as the sentinel after it, a real
	/// tenant's row is untouched, and the column ends up closed with its binary collation intact.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The legacy shape is reconstructed from the shipped schema rather than hand-written, so this test
	/// cannot drift from the product: it creates the current table and then re-opens the tenant column,
	/// which is exactly the state a database created before this wave is in.
	/// </para>
	/// <para>
	/// The collation assertion is not incidental. <c>ALTER COLUMN</c> does not preserve a column's
	/// collation — omitting the clause resets it to the DATABASE default, which is typically
	/// case-INSENSITIVE. A migration that dropped it would leave <c>'Acme'</c> and <c>'acme'</c> the same
	/// tenant, so a scoped read would return another tenant's events: the tenant predicate would fail
	/// OPEN, silently, with every other assertion here still green.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task BackfillALegacyNullTenantWhileLeavingARealTenantAlone()
	{
		RequireDocker();
		await CreateFreshShippedSchemaAsync().ConfigureAwait(false);
		await ReopenTenantColumnToLegacyShapeAsync().ConfigureAwait(false);

		(await IsTenantColumnNullableAsync().ConfigureAwait(false)).ShouldBeTrue(
			"the legacy shape must actually be re-established, or the migration below proves nothing");

		await ExecuteAsync(
			"""
			INSERT INTO [dbo].[EventStoreEvents]
				([EventId], [AggregateId], [AggregateType], [EventType], [EventData], [Version], [Timestamp], [TenantId])
			VALUES
				(N'legacy', N'agg-legacy', N'Order', N'Created', 0x01, 1, SYSDATETIMEOFFSET(), NULL),
				(N'tenanted', N'agg-tenanted', N'Order', N'Created', 0x01, 1, SYSDATETIMEOFFSET(), N'acme');
			""").ConfigureAwait(false);

		await RunShippedMigrationAsync().ConfigureAwait(false);

		var legacy = await ScalarAsync<string>(
			"SELECT [TenantId] FROM [dbo].[EventStoreEvents] WHERE [EventId] = N'legacy';").ConfigureAwait(false);
		legacy.ShouldBe(
			Sentinel,
			"a row written as NULL before the migration must read back as the sentinel after it");

		var tenanted = await ScalarAsync<string>(
			"SELECT [TenantId] FROM [dbo].[EventStoreEvents] WHERE [EventId] = N'tenanted';").ConfigureAwait(false);
		tenanted.ShouldBe(
			"acme",
			"the backfill must touch only genuinely untenanted rows — a real tenant's events are not rewritten");

		(await IsTenantColumnNullableAsync().ConfigureAwait(false)).ShouldBeFalse(
			"the migration must close the column, not merely rewrite the values in it");

		var collation = await ScalarAsync<string>(
			"""
			SELECT c.collation_name
			FROM sys.columns c
			WHERE c.object_id = OBJECT_ID(N'[dbo].[EventStoreEvents]') AND c.name = N'TenantId';
			""").ConfigureAwait(false);
		collation.ShouldBe(
			"Latin1_General_BIN2",
			"the ALTER must restate the binary collation; losing it makes the tenant predicate fail OPEN");
	}

	/// <summary>
	/// UPGRADE: running the migration twice is a no-op, and running it against an already-converged
	/// database changes nothing.
	/// </summary>
	/// <remarks>
	/// Deployment scripts get re-run — by a retried pipeline, or by an operator who cannot tell whether
	/// the first attempt finished. A migration that is only correct once is a migration that will be
	/// wrong in production.
	/// </remarks>
	[Fact]
	public async Task BeSafeToRunTwice()
	{
		RequireDocker();
		await CreateFreshShippedSchemaAsync().ConfigureAwait(false);
		await ReopenTenantColumnToLegacyShapeAsync().ConfigureAwait(false);

		await ExecuteAsync(
			"""
			INSERT INTO [dbo].[EventStoreEvents]
				([EventId], [AggregateId], [AggregateType], [EventType], [EventData], [Version], [Timestamp], [TenantId])
			VALUES (N'legacy', N'agg-legacy', N'Order', N'Created', 0x01, 1, SYSDATETIMEOFFSET(), NULL);
			""").ConfigureAwait(false);

		await RunShippedMigrationAsync().ConfigureAwait(false);
		await RunShippedMigrationAsync().ConfigureAwait(false);

		var stored = await ScalarAsync<string>(
			"SELECT [TenantId] FROM [dbo].[EventStoreEvents] WHERE [EventId] = N'legacy';").ConfigureAwait(false);
		stored.ShouldBe(Sentinel, "a second run must leave the converged data exactly as it was");

		var rows = await ScalarAsync<int>(
			"SELECT COUNT(*) FROM [dbo].[EventStoreEvents];").ConfigureAwait(false);
		rows.ShouldBe(1, "a second run must not duplicate or drop rows");
	}

	private void RequireDocker() =>
		_fixture.DockerAvailable.ShouldBeTrue(
			"this lock asserts a property of the SHIPPED SCHEMA and is deliberately never skipped — " +
			"a green run that never reached a database would certify nothing.");

	/// <summary>
	/// Drops and recreates the table from the DDL the package ships, so each test starts clean.
	/// </summary>
	private async Task CreateFreshShippedSchemaAsync()
	{
		await ExecuteAsync(
			"""
			IF OBJECT_ID(N'[dbo].[EventStoreEvents]', 'U') IS NOT NULL
				DROP TABLE [dbo].[EventStoreEvents];
			""").ConfigureAwait(false);

		await ShippedEventStoreSchema.EnsureCreatedAsync(_connectionString, CancellationToken.None)
			.ConfigureAwait(false);
	}

	/// <summary>
	/// Puts the tenant column back into its pre-wave shape: nullable, with no default.
	/// </summary>
	/// <remarks>
	/// The unique constraint and index are dropped first because SQL Server will not alter a column they
	/// depend on — the same reason the shipped migration drops and recreates them.
	/// </remarks>
	private async Task ReopenTenantColumnToLegacyShapeAsync() =>
		await ExecuteAsync(
			"""
			DECLARE @df SYSNAME = (
				SELECT name FROM sys.default_constraints
				WHERE parent_object_id = OBJECT_ID(N'[dbo].[EventStoreEvents]')
				  AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[dbo].[EventStoreEvents]'), 'TenantId', 'ColumnId'));
			IF @df IS NOT NULL
				EXEC('ALTER TABLE [dbo].[EventStoreEvents] DROP CONSTRAINT [' + @df + ']');

			IF EXISTS (SELECT * FROM sys.indexes
					   WHERE name = N'IX_EventStoreEvents_Stream'
						 AND object_id = OBJECT_ID(N'[dbo].[EventStoreEvents]'))
				DROP INDEX [IX_EventStoreEvents_Stream] ON [dbo].[EventStoreEvents];

			IF EXISTS (SELECT * FROM sys.key_constraints
					   WHERE parent_object_id = OBJECT_ID(N'[dbo].[EventStoreEvents]')
						 AND name = N'UQ_EventStoreEvents_Stream' AND type = N'UQ')
				ALTER TABLE [dbo].[EventStoreEvents] DROP CONSTRAINT [UQ_EventStoreEvents_Stream];

			ALTER TABLE [dbo].[EventStoreEvents]
				ALTER COLUMN [TenantId] NVARCHAR(255) COLLATE Latin1_General_BIN2 NULL;

			ALTER TABLE [dbo].[EventStoreEvents]
				ADD CONSTRAINT [UQ_EventStoreEvents_Stream]
					UNIQUE ([AggregateId], [AggregateType], [Version], [TenantId]);

			CREATE NONCLUSTERED INDEX [IX_EventStoreEvents_Stream]
				ON [dbo].[EventStoreEvents] ([AggregateId], [AggregateType], [TenantId], [Version]);
			""").ConfigureAwait(false);

	/// <summary>
	/// Runs the migration script the package ships, batch by batch, on ONE connection.
	/// </summary>
	/// <remarks>
	/// The single connection is load-bearing, not tidiness. The script opens a transaction in its
	/// first batch and commits it in its last, and a transaction belongs to a session — so a harness
	/// that reconnects between batches loses it at the first <c>GO</c> and is not applying the script
	/// the way the script requires. The migration detects that and refuses, which is the behaviour a
	/// consumer whose runner reconnects will get; this arm is here to exercise the migration, so it
	/// applies it correctly. <c>ShippedMigrationTableAbsenceShould</c> holds the same connection for
	/// the same reason.
	/// </remarks>
	private async Task RunShippedMigrationAsync()
	{
		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync().ConfigureAwait(false);

		foreach (var batch in SplitBatches(LoadShippedMigration()))
		{
			// CA2100: the batch is the package's own shipped migration, read from the embedded copy.
			// Executing it verbatim IS the thing under test.
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
			await using var command = new SqlCommand(batch, connection);
#pragma warning restore CA2100
			_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
		}
	}

	private async Task<bool> IsTenantColumnNullableAsync() =>
		await ScalarAsync<int>(
			"""
			SELECT CAST(c.is_nullable AS INT)
			FROM sys.columns c
			WHERE c.object_id = OBJECT_ID(N'[dbo].[EventStoreEvents]') AND c.name = N'TenantId';
			""").ConfigureAwait(false) == 1;

	private async Task ExecuteAsync(string sql)
	{
		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync().ConfigureAwait(false);

		// CA2100: every statement here is a constant in this file or the package's own shipped script.
		// None is reachable from user input, and DDL object definitions are not parameterisable.
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
		await using var command = new SqlCommand(sql, connection);
#pragma warning restore CA2100
		_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
	}

	private async Task<T> ScalarAsync<T>(string sql)
	{
		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync().ConfigureAwait(false);

#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
		await using var command = new SqlCommand(sql, connection);
#pragma warning restore CA2100
		var value = await command.ExecuteScalarAsync().ConfigureAwait(false);

		return value is null or DBNull
			? default!
			: (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
	}

	private static IEnumerable<string> SplitBatches(string script) =>
		script
			.Split(["\nGO\r\n", "\nGO\n", "\r\nGO\r\n"], StringSplitOptions.None)
			.Select(static batch => batch.Trim())
			.Where(static batch => batch.Length > 0 && !batch.Equals("GO", StringComparison.OrdinalIgnoreCase));

	/// <summary>
	/// Loads the shipped migration by resource-name suffix, for the reason given on
	/// <see cref="ShippedEventStoreSchema"/>: pinning the full manifest name would turn an unrelated
	/// project restructure into a null stream instead of a sentence.
	/// </summary>
	private static string LoadShippedMigration()
	{
		var assembly = Assembly.GetExecutingAssembly();

		var resourceName = Array.Find(
			assembly.GetManifestResourceNames(),
			name => name.EndsWith(MigrationScriptFileName, StringComparison.Ordinal))
			?? throw new InvalidOperationException(
				$"The shipped migration '{MigrationScriptFileName}' is not embedded in {assembly.GetName().Name}. " +
				"It is linked in by the test project's EmbeddedResource item; if that item was removed, this " +
				"lock would be asserting against a migration no consumer has.");

		using var stream = assembly.GetManifestResourceStream(resourceName)!;
		using var reader = new StreamReader(stream);

		return reader.ReadToEnd();
	}
}
