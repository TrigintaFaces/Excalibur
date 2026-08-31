// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;

using Microsoft.Data.SqlClient;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.Data.Migrations;

/// <summary>
/// Covers the shipped SQL Server migrations that NARROW a tenant column to the portable maximum.
/// </summary>
/// <remarks>
/// <para>
/// These are migrations: they run once, against a consumer's real database, and they cannot be un-run.
/// The properties below are the ones an upgrade actually depends on — it converges a database at the
/// prior version, it is safe to run twice, and above all it REFUSES rather than truncates.
/// </para>
/// <para>
/// The refusal arm is the one that matters and the reason these are tested together. Every column here
/// holds a tenant identifier. Shortening one to 64 characters does not produce a slightly wrong label —
/// it merges two tenants whose identifiers agree in their first 64 characters into one. On
/// <c>DeadLetterQueue</c>, <c>inbox_messages</c> and <c>dispatch.sagas</c> the column is part of the
/// primary key, so the merge is a KEY collision: one tenant's row starts satisfying another tenant's.
/// A happy-path-only suite would pass just as happily against a script that truncated.
/// </para>
/// <para>
/// Every arm applies the script exactly as shipped, split only on the <c>GO</c> batch separator, which
/// is a client construct rather than T-SQL and which every SQL Server runner honours.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Infrastructure", "SqlServer")]
[Collection(SqlServerTestCollection.CollectionName)]
public sealed class SqlServerTenantNarrowingMigrationShould
{
	private static readonly Dictionary<string, Subject> Subjects = new(StringComparer.Ordinal)
	{
		["outbox 002"] = new(
			[
				"src/Excalibur/Excalibur.Outbox.SqlServer/Scripts/001_CreateOutboxSchema.sql",
			],
			"src/Excalibur/Excalibur.Outbox.SqlServer/Scripts/002_NarrowTenantIdToPortableMaximum.sql",
			[
				new(
					"[dbo].[OutboxMessages]",
					255,
					"ALTER TABLE [dbo].[OutboxMessages] ALTER COLUMN TenantId NVARCHAR(255) COLLATE Latin1_General_BIN2 NOT NULL",
					"""
					INSERT INTO [dbo].[OutboxMessages] (Id, MessageType, Payload, Destination, TenantId)
					VALUES (CONVERT(NVARCHAR(255), NEWID()), 'T', 0x00, 'dest', @tenant)
					"""),
				new(
					"[dbo].[OutboxMessageTransports]",
					255,
					"ALTER TABLE [dbo].[OutboxMessageTransports] ALTER COLUMN TenantId NVARCHAR(255) COLLATE Latin1_General_BIN2 NOT NULL",
					// The transports row is a child of OutboxMessages by foreign key, so the parent is
					// created in the same statement rather than assumed to be there.
					"""
					DECLARE @parent NVARCHAR(255) = CONVERT(NVARCHAR(255), NEWID());
					INSERT INTO [dbo].[OutboxMessages] (Id, MessageType, Payload, Destination, TenantId)
					VALUES (@parent, 'T', 0x00, 'dest', '__untenanted__');
					INSERT INTO [dbo].[OutboxMessageTransports] (Id, MessageId, TransportName, TenantId)
					VALUES (CONVERT(NVARCHAR(255), NEWID()), @parent, 'transport', @tenant);
					"""),
				new(
					"[dbo].[DeadLetterQueue]",
					255,
					"""
					ALTER TABLE [dbo].[DeadLetterQueue] DROP CONSTRAINT PK_DeadLetterQueue;
					ALTER TABLE [dbo].[DeadLetterQueue] ALTER COLUMN TenantId NVARCHAR(255) COLLATE Latin1_General_BIN2 NOT NULL;
					ALTER TABLE [dbo].[DeadLetterQueue] ADD CONSTRAINT PK_DeadLetterQueue PRIMARY KEY (Id, TenantId);
					""",
					"""
					INSERT INTO [dbo].[DeadLetterQueue] (Id, TenantId, MessageType, Payload, Reason)
					VALUES (NEWID(), @tenant, 'T', 0x00, 0)
					"""),
			]),
		["inbox 003"] = new(
			[
				"src/Excalibur/Excalibur.Inbox.SqlServer/Scripts/001_CreateInboxSchema.MultiTenant.sql",
			],
			"src/Excalibur/Excalibur.Inbox.SqlServer/Scripts/003_NarrowTenantIdToPortableMaximum.sql",
			[
				new(
					"[dbo].[inbox_messages]",
					255,
					"""
					ALTER TABLE [dbo].[inbox_messages] DROP CONSTRAINT PK_inbox_messages;
					ALTER TABLE [dbo].[inbox_messages] ALTER COLUMN TenantId NVARCHAR(255) COLLATE Latin1_General_BIN2 NOT NULL;
					ALTER TABLE [dbo].[inbox_messages] ADD CONSTRAINT PK_inbox_messages PRIMARY KEY (MessageId, HandlerType, TenantId);
					""",
					"""
					INSERT INTO [dbo].[inbox_messages] (MessageId, HandlerType, MessageType, Payload, ReceivedAt, TenantId)
					VALUES (CONVERT(NVARCHAR(255), NEWID()), 'H', 'T', 0x00, SYSDATETIMEOFFSET(), @tenant)
					"""),
			]),
		["saga 03"] = new(
			[
				"src/Excalibur/Excalibur.Saga.SqlServer/Scripts/01-SagaSchema.sql",
				"src/Excalibur/Excalibur.Saga.SqlServer/Scripts/SagaTimeouts.sql",
			],
			"src/Excalibur/Excalibur.Saga.SqlServer/Scripts/03-NarrowTenantIdToPortableMaximum.sql",
			[
				new(
					"dispatch.sagas",
					200,
					"""
					ALTER TABLE dispatch.sagas DROP CONSTRAINT PK_dispatch_sagas;
					ALTER TABLE dispatch.sagas ALTER COLUMN TenantId NVARCHAR(200) COLLATE Latin1_General_BIN2 NOT NULL;
					ALTER TABLE dispatch.sagas ADD CONSTRAINT PK_dispatch_sagas PRIMARY KEY CLUSTERED (TenantId, SagaId);
					""",
					"""
					INSERT INTO dispatch.sagas (SagaId, SagaType, StateJson, TenantId)
					VALUES (NEWID(), 'TestSaga', '{}', @tenant)
					"""),
				new(
					"SagaTimeouts",
					200,
					"""
					DROP INDEX IX_SagaTimeouts_TenantId_SagaId ON SagaTimeouts;
					DROP INDEX IX_SagaTimeouts_TenantId_SagaId_TimeoutId ON SagaTimeouts;
					ALTER TABLE SagaTimeouts ALTER COLUMN TenantId NVARCHAR(200) COLLATE Latin1_General_BIN2 NOT NULL;
					CREATE INDEX IX_SagaTimeouts_TenantId_SagaId ON SagaTimeouts (TenantId, SagaId);
					CREATE INDEX IX_SagaTimeouts_TenantId_SagaId_TimeoutId ON SagaTimeouts (TenantId, SagaId, TimeoutId);
					""",
					"""
					INSERT INTO SagaTimeouts (TimeoutId, SagaId, SagaType, TimeoutType, DueAt, ScheduledAt, TenantId)
					VALUES (CONVERT(NVARCHAR(450), NEWID()), CONVERT(NVARCHAR(450), NEWID()), 'S', 'T', SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(), @tenant)
					"""),
			]),

		// The dead-letter schema carries its own upgrade arm rather than shipping a separate migration,
		// so the script under test and the create script are the same file. Re-running it IS the upgrade
		// path, which is exactly what an operator does, so the arms exercise it that way.
		["deadletter 001"] = new(
			[
				"src/Excalibur/Excalibur.Data.SqlServer/Scripts/001_CreateDeadLetterSchema.sql",
			],
			"src/Excalibur/Excalibur.Data.SqlServer/Scripts/001_CreateDeadLetterSchema.sql",
			[
				new(
					"[dbo].[DeadLetterMessages]",
					255,
					"""
					DROP INDEX [IX_DeadLetterMessages_TenantId_MovedToDeadLetterAt] ON [dbo].[DeadLetterMessages];
					ALTER TABLE [dbo].[DeadLetterMessages] ALTER COLUMN [TenantId] [nvarchar](255) COLLATE Latin1_General_BIN2 NOT NULL;
					CREATE NONCLUSTERED INDEX [IX_DeadLetterMessages_TenantId_MovedToDeadLetterAt]
					    ON [dbo].[DeadLetterMessages] ([TenantId], [MovedToDeadLetterAt])
					    INCLUDE ([MessageType], [Reason]);
					""",
					"""
					INSERT INTO [dbo].[DeadLetterMessages]
					    ([Id], [TenantId], [MessageId], [MessageType], [MessageBody], [MessageMetadata], [Reason], [MovedToDeadLetterAt])
					VALUES (REPLACE(CONVERT(NVARCHAR(36), NEWID()), '-', ''), @tenant, 'M', 'T', '{}', '{}', 'because', SYSDATETIMEOFFSET())
					"""),
			]),
	};

	private readonly SqlServerContainerFixture _fixture;

	public SqlServerTenantNarrowingMigrationShould(SqlServerContainerFixture fixture) => _fixture = fixture;

	/// <summary>Gets the label of each shipped narrowing migration under test.</summary>
	public static TheoryData<string> Migrations
	{
		get
		{
			var data = new TheoryData<string>();

			foreach (var label in Subjects.Keys)
			{
				data.Add(label);
			}

			return data;
		}
	}

	/// <summary>Gets the migrations that narrow more than one table, so a refusal has to be atomic.</summary>
	public static TheoryData<string> MultiTableMigrations
	{
		get
		{
			var data = new TheoryData<string>();

			foreach (var (label, subject) in Subjects)
			{
				if (subject.Tables.Length > 1)
				{
					data.Add(label);
				}
			}

			return data;
		}
	}

	/// <summary>Gets each (migration, table) pair whose refusal arm is covered.</summary>
	public static TheoryData<string, string> NarrowedTables
	{
		get
		{
			var data = new TheoryData<string, string>();

			foreach (var (label, subject) in Subjects)
			{
				foreach (var table in subject.Tables)
				{
					data.Add(label, table.Name);
				}
			}

			return data;
		}
	}

	[Theory]
	[MemberData(nameof(Migrations))]
	public async Task Narrow_EveryTenantColumn_OnADatabaseAtThePriorVersion(string label)
	{
		var subject = Subjects[label];

		await using var db = await ArrangeAtPriorVersionAsync(label, subject).ConfigureAwait(false);

		await ApplyAsync(db.ConnectionString, subject.NarrowScript).ConfigureAwait(false);

		foreach (var table in subject.Tables)
		{
			var width = await TenantColumnWidthAsync(db.ConnectionString, table.Name).ConfigureAwait(false);

			width.ShouldBe(
				128,
				$"{label} did not converge {table.Name} on a database at the prior version. That database is "
				+ "the only reason the script exists — the create script guards on the table's existence, so "
				+ "re-running the CREATE is not an upgrade path. (max_length is in bytes; 128 is "
				+ "NVARCHAR(64).)");
		}
	}

	[Theory]
	[MemberData(nameof(Migrations))]
	public async Task Change_Nothing_WhenRunAgainstAnAlreadyConvergedDatabase(string label)
	{
		var subject = Subjects[label];

		await using var db = await ArrangeAtPriorVersionAsync(label, subject).ConfigureAwait(false);

		await ApplyAsync(db.ConnectionString, subject.NarrowScript).ConfigureAwait(false);
		var afterFirstRun = await WidthsAsync(db.ConnectionString, subject).ConfigureAwait(false);

		// An operator who cannot tell whether the first pass completed will run it again, and a database
		// provisioned by the CURRENT create script is already converged before anyone runs this at all.
		// Both are ordinary, and neither may fail or alter anything.
		var failure = await Record
			.ExceptionAsync(() => ApplyAsync(db.ConnectionString, subject.NarrowScript))
			.ConfigureAwait(false);

		failure.ShouldBeNull($"{label} failed on a second run against an already-converged database.");

		var afterSecondRun = await WidthsAsync(db.ConnectionString, subject).ConfigureAwait(false);
		afterSecondRun.ShouldBe(afterFirstRun, $"{label} altered an already-converged column on re-run.");
	}

	[Theory]
	[MemberData(nameof(NarrowedTables))]
	public async Task Refuse_RatherThanTruncate_WhenARowHoldsAnOverlongTenantIdentifier(
		string label,
		string tableName)
	{
		var subject = Subjects[label];
		var table = subject.Tables.Single(t => string.Equals(t.Name, tableName, StringComparison.Ordinal));

		await using var db = await ArrangeAtPriorVersionAsync($"{label} {tableName}", subject)
			.ConfigureAwait(false);

		// Two tenants that agree in their first 64 characters and differ after. Truncation makes them one.
		var shared = new string('t', 64);
		var tenantA = shared + "-alpha";
		var tenantB = shared + "-beta";

		await InsertAsync(db.ConnectionString, table, tenantA).ConfigureAwait(false);
		await InsertAsync(db.ConnectionString, table, tenantB).ConfigureAwait(false);

		var failure = await Record
			.ExceptionAsync(() => ApplyAsync(db.ConnectionString, subject.NarrowScript))
			.ConfigureAwait(false);

		var sqlFailure = failure.ShouldBeOfType<SqlException>(
			$"{label} did not refuse a row in {tableName} whose tenant identifier is longer than 64 "
			+ "characters. Narrowing it would have merged two tenants into one scope, silently, on live "
			+ "data.");

		sqlFailure.Message.ShouldContain(
			"REFUSED",
			Case.Sensitive,
			$"{label} failed, but not with its own refusal — so the operator gets an error that names "
			+ "neither the cause nor the remedy, and the remedy is not the obvious one: shortening the "
			+ "values in place is exactly what must not be done.");

		sqlFailure.Message.ShouldContain(
			"70",
			Case.Sensitive,
			$"{label} refused without naming how long the longest offending identifier is. The operator "
			+ "cannot act on a refusal that does not say what is in the way.");

		var widthAfter = await TenantColumnWidthAsync(db.ConnectionString, tableName).ConfigureAwait(false);

		widthAfter.ShouldBe(
			table.PriorChars * 2,
			$"{label} refused but {tableName} was narrowed anyway, so the refusal is a message rather than "
			+ "a protection.");

		var survivors = await DistinctTenantsAsync(db.ConnectionString, tableName).ConfigureAwait(false);

		survivors.ShouldContain(
			tenantA,
			$"{label} lost or shortened a tenant identifier in {tableName}. Every row must survive "
			+ "byte-for-byte: the whole point of refusing is that no row is quietly re-filed under another "
			+ "tenant's identity.");

		survivors.ShouldContain(
			tenantB,
			$"{label} lost or shortened a tenant identifier in {tableName}.");
	}

	[Theory]
	[MemberData(nameof(MultiTableMigrations))]
	public async Task Refuse_WithoutNarrowingAnyTable_WhenALaterTableHoldsAnOverlongIdentifier(string label)
	{
		var subject = Subjects[label];
		var offending = subject.Tables[^1];

		await using var db = await ArrangeAtPriorVersionAsync($"{label} atomic", subject)
			.ConfigureAwait(false);

		// The offender is in the LAST table the script touches, so every earlier table is clean and would
		// narrow successfully if the script checked tables one at a time. That is the state this arm
		// exists to forbid: these migrations exist to remove a database where some tables carry the narrow
		// column and some carry the wide one, and a refusal that narrows part-way MANUFACTURES exactly
		// that — a third shape, produced by the safety path. It also makes the word REFUSED a lie to the
		// operator who reads it and believes their database is untouched.
		await InsertAsync(db.ConnectionString, offending, new string('t', 64) + "-alpha").ConfigureAwait(false);

		var failure = await Record
			.ExceptionAsync(() => ApplyAsync(db.ConnectionString, subject.NarrowScript))
			.ConfigureAwait(false);

		var sqlFailure = failure.ShouldBeOfType<SqlException>(
			$"{label} did not refuse at all when {offending.Name} held an overlong tenant identifier.");

		sqlFailure.Message.ShouldContain(
			offending.Name,
			Case.Sensitive,
			$"{label} refused without naming which table holds the offenders. With more than one candidate "
			+ "the operator cannot act on a refusal that does not say where to look.");

		foreach (var table in subject.Tables)
		{
			var width = await TenantColumnWidthAsync(db.ConnectionString, table.Name).ConfigureAwait(false);

			width.ShouldBe(
				table.PriorChars * 2,
				$"{label} refused, but {table.Name} was narrowed before the refusal. The database is now a "
				+ "shape neither version ships — some tables narrow, some wide — produced by the very path "
				+ "that was supposed to leave it alone.");
		}
	}

	private async Task<SqlServerScratchDatabase> ArrangeAtPriorVersionAsync(string label, Subject subject)
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			_fixture.InitializationError
			?? "SQL Server must be reachable: these arms cover migrations that run once against a "
			+ "consumer's real database, and a skip is indistinguishable from never having tested them.");

		var db = await SqlServerScratchDatabase
			.CreateAsync(_fixture.ConnectionString, label, TestContext.Current.CancellationToken)
			.ConfigureAwait(false);

		try
		{
			foreach (var script in subject.CreateScripts)
			{
				await ApplyAsync(db.ConnectionString, script).ConfigureAwait(false);
			}

			foreach (var table in subject.Tables)
			{
				await ExecuteAsync(db.ConnectionString, table.WidenBack).ConfigureAwait(false);

				// LIVENESS on the arrangement itself. Every arm below concludes something from what the
				// script did to this column, and if the widen-back silently did not happen, the database is
				// already converged and every arm passes while testing nothing.
				var arranged = await TenantColumnWidthAsync(db.ConnectionString, table.Name)
					.ConfigureAwait(false);

				arranged.ShouldBe(
					table.PriorChars * 2,
					$"the {label} fixture failed to put {table.Name} back to its prior width, so these arms "
					+ "would run against an already-converged database and prove nothing.");
			}

			return db;
		}
		catch
		{
			await db.DisposeAsync().ConfigureAwait(false);
			throw;
		}
	}

	private static async Task<string> WidthsAsync(string connectionString, Subject subject)
	{
		var widths = new List<string>();

		foreach (var table in subject.Tables)
		{
			var width = await TenantColumnWidthAsync(connectionString, table.Name).ConfigureAwait(false);
			widths.Add($"{table.Name}={width.ToString(CultureInfo.InvariantCulture)}");
		}

		return string.Join(", ", widths);
	}

	private static async Task InsertAsync(string connectionString, NarrowedTable table, string tenantId)
	{
		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

		// CA2100: the command text is this class's own const-shaped fixture DDL; the tenant identifier is
		// bound as a parameter.
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
		await using var command = new SqlCommand(table.Insert, connection);
#pragma warning restore CA2100
		_ = command.Parameters.AddWithValue("@tenant", tenantId);
		_ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
	}

	private static async Task<IReadOnlyList<string>> DistinctTenantsAsync(string connectionString, string table)
	{
		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

		// CA2100: the table name comes from this class's own const-backed subject table.
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
		await using var command = new SqlCommand($"SELECT DISTINCT TenantId FROM {table}", connection);
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

	/// <summary>
	/// Returns <c>sys.columns.max_length</c> for the tenant column, or zero when it is absent.
	/// </summary>
	/// <remarks>
	/// The value is in BYTES and nvarchar stores two per character, so NVARCHAR(64) reads back as 128.
	/// That is the same property the shipped scripts guard on, deliberately: a test that measured
	/// something else would not be checking the thing the migration decides on.
	/// </remarks>
	private static async Task<int> TenantColumnWidthAsync(string connectionString, string table)
	{
		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

		await using var command = new SqlCommand(
			"""
			SELECT ISNULL(MAX(c.max_length), 0)
			  FROM sys.columns c
			 WHERE c.object_id = OBJECT_ID(@table) AND c.name = N'TenantId'
			""",
			connection);

		_ = command.Parameters.AddWithValue("@table", table);

		var value = await command
			.ExecuteScalarAsync(TestContext.Current.CancellationToken)
			.ConfigureAwait(false);

		return value is null or DBNull ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
	}

	/// <summary>
	/// Sends a shipped script exactly as it ships, split only on the <c>GO</c> batch separator.
	/// </summary>
	private static async Task ApplyAsync(string connectionString, string script)
	{
		foreach (var batch in SplitBatches(ShippedScript.RawBytesOf(script)))
		{
			await ExecuteAsync(connectionString, batch).ConfigureAwait(false);
		}
	}

	private static async Task ExecuteAsync(string connectionString, string sql)
	{
		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

		// CA2100: either the package's own shipped DDL or fixture DDL built from this class's own
		// const-backed subject table.
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
		await using var command = new SqlCommand(sql, connection);
#pragma warning restore CA2100
		command.CommandTimeout = 180;
		_ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Splits a script on the <c>GO</c> batch separator, which is a client construct rather than T-SQL.
	/// </summary>
	private static IEnumerable<string> SplitBatches(string script)
	{
		var batch = new System.Text.StringBuilder();

		foreach (var line in script.Split('\n'))
		{
			if (string.Equals(line.Trim(), "GO", StringComparison.OrdinalIgnoreCase))
			{
				var text = batch.ToString();
				batch.Clear();

				if (!string.IsNullOrWhiteSpace(text))
				{
					yield return text;
				}

				continue;
			}

			_ = batch.Append(line).Append('\n');
		}

		var tail = batch.ToString();

		if (!string.IsNullOrWhiteSpace(tail))
		{
			yield return tail;
		}
	}

	private sealed record Subject(
		string[] CreateScripts,
		string NarrowScript,
		NarrowedTable[] Tables);

	private sealed record NarrowedTable(string Name, int PriorChars, string WidenBack, string Insert);
}
