// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;
using System.Text.RegularExpressions;

using Dapper;

using Microsoft.Data.SqlClient;

using Shouldly;

using Tests.Shared.Fixtures;

#pragma warning disable CA1812 // Instantiated by the xUnit test runner.

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.SqlServer;

/// <summary>
/// Runs the CDC idempotency schema we publish for consumers against a real SQL Server, exactly as
/// published, and binds the two properties it has to keep.
/// </summary>
/// <remarks>
/// <para>
/// The DDL is read from the documentation file rather than copied here. A copy would let the published
/// schema and the tested schema drift apart silently, and the published one is the artifact that matters:
/// it is not documentation, it is code a consumer runs against their own database. Nothing else in the
/// build executes it, so without this lock a defect in it surfaces first on their machine.
/// </para>
/// <para>
/// The dedupe key carries a consumer discriminator so two consumers of one source table keep separate
/// already-processed sets. Adding that column also pushed the clustered key from 532 to 1044 bytes, past
/// SQL Server's 900-byte cap. SQL Server accepts such a <c>CREATE TABLE</c> with only a warning and then
/// fails individual inserts with <c>Msg 1946</c> once a long value appears - and 1946 is not a duplicate
/// key violation, so the filter does not absorb it. The change would be processed and never recorded as
/// processed, then redelivered on every pass. Both arms below are therefore load-bearing, and neither is
/// reachable without a real server: the byte accounting is enforced by the engine, not by our code.
/// </para>
/// </remarks>
[Collection(ContainerCollections.SqlServer)]
[Trait("Category", "Integration")]
[Trait("Component", "Cdc")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerCdcProcessedEventsSchemaShould
{
	private const string PublishedDdlPath = "docs-site/docs/patterns/cdc.md";

	private readonly SqlServerFixture _fixture;
	private readonly string _schemaName = $"cdc_{Guid.NewGuid():N}";

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerCdcProcessedEventsSchemaShould"/> class.
	/// </summary>
	/// <param name="fixture">The shared SQL Server container fixture.</param>
	public SqlServerCdcProcessedEventsSchemaShould(SqlServerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <summary>
	/// SAFETY plus LIVENESS on the key width: the published key must accept its own widest legal entry.
	/// </summary>
	[Fact]
	public async Task AcceptAMaximumLengthEntryUnderTheClusteredKeyLimit()
	{
		var table = await CreatePublishedTableAsync();
		await using var connection = OpenConnection();

		// The probe widths are read from the published DDL, not hardcoded. Hardcoding them makes this arm
		// vacuous the moment someone widens a column: the engine only rejects a key whose ACTUAL bytes
		// exceed 900, so a fixed-size probe keeps passing against a schema that has already become unusable
		// for its own widest legal value.
		var ddl = ReadPublishedDdl();
		var maxTableName = new string('t', DeclaredWidth(ddl, "TableName"));
		var maxConsumerId = new string('c', DeclaredWidth(ddl, "ConsumerId"));

		await Should.NotThrowAsync(
			() => connection.ExecuteAsync(
				$"INSERT INTO {table} (TableName, Lsn, SeqVal, ConsumerId) VALUES (@t, @l, @s, @c)",
				new { t = maxTableName, l = new byte[] { 1 }, s = new byte[] { 1 }, c = maxConsumerId }),
			"the published schema must accept an entry at the widest value its own column widths allow. " +
			"SQL Server caps a clustered index key at 900 bytes and only warns at CREATE TABLE, so an " +
			"oversized key is not detectable until an insert fails with Msg 1946 at runtime. That error " +
			"is not a duplicate-key violation, so the filter does not absorb it: the change is processed " +
			"and never marked processed, and it is redelivered forever.");

		var stored = await connection.ExecuteScalarAsync<int>(
			$"SELECT COUNT(*) FROM {table} WHERE ConsumerId = @c", new { c = maxConsumerId });
		stored.ShouldBe(1, "the row must actually be readable back, not merely inserted without error.");
	}

	/// <summary>
	/// SAFETY: two consumers of one source table must be able to record the same change independently.
	/// </summary>
	[Fact]
	public async Task KeepASeparateProcessedSetPerConsumerForTheSameChange()
	{
		var table = await CreatePublishedTableAsync();
		await using var connection = OpenConnection();

		var lsn = new byte[] { 0, 0, 0, 1 };
		var seqVal = new byte[] { 0, 0, 0, 1 };

		await connection.ExecuteAsync(
			$"INSERT INTO {table} (TableName, Lsn, SeqVal, ConsumerId) VALUES (@t, @l, @s, @c)",
			new { t = "dbo.Orders", l = lsn, s = seqVal, c = "orders-projector" });

		// The second consumer records the SAME change. If the key omitted ConsumerId this is a primary key
		// violation, which the filter swallows as "already processed" -- so the second consumer would skip
		// a change it never saw, with no error anywhere.
		await Should.NotThrowAsync(
			() => connection.ExecuteAsync(
				$"INSERT INTO {table} (TableName, Lsn, SeqVal, ConsumerId) VALUES (@t, @l, @s, @c)",
				new { t = "dbo.Orders", l = lsn, s = seqVal, c = "audit-forwarder" }),
			"a second consumer of the same table must be able to mark the same change processed. " +
			"Sharing one set means whichever consumer marks first silently suppresses delivery for every " +
			"other, and a suppression loses the change permanently while a duplicate only reprocesses it.");

		var consumers = (await connection.QueryAsync<string>(
			$"SELECT ConsumerId FROM {table} WHERE TableName = 'dbo.Orders'")).ToList();
		consumers.Count.ShouldBe(2, "both consumers' marks must coexist as distinct rows.");
	}

	/// <summary>
	/// LIVENESS: the key must still reject a genuine duplicate for the same consumer, or dedupe does nothing.
	/// </summary>
	[Fact]
	public async Task StillRejectTheSameChangeTwiceForOneConsumer()
	{
		var table = await CreatePublishedTableAsync();
		await using var connection = OpenConnection();

		var row = new { t = "dbo.Orders", l = new byte[] { 0, 0, 0, 2 }, s = new byte[] { 0, 0, 0, 2 }, c = "orders-projector" };
		var insert = $"INSERT INTO {table} (TableName, Lsn, SeqVal, ConsumerId) VALUES (@t, @l, @s, @c)";

		await connection.ExecuteAsync(insert, row);

		var duplicate = await Should.ThrowAsync<SqlException>(
			() => connection.ExecuteAsync(insert, row),
			"adding a consumer discriminator must not disable deduplication itself. If the same consumer " +
			"can record one change twice, the key no longer identifies a change and the filter suppresses " +
			"nothing - which every other arm here would still pass.");

		duplicate.Number.ShouldBeOneOf(2627, 2601);
	}

	// ---- helpers ------------------------------------------------------------------------------------

	private SqlConnection OpenConnection()
	{
		var connection = new SqlConnection(_fixture.ConnectionString);
		connection.Open();
		return connection;
	}

	private async Task<string> CreatePublishedTableAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Docker/SQL Server must be available - the published schema is executable code a consumer runs, " +
			"and the index-key limit it has to satisfy is enforced by the engine, not by anything we can " +
			"assert in process. This lock must never be skipped.");

		// Run the published DDL under a per-test schema so parallel runs cannot collide, and so the
		// assertions describe this test's own table.
		var ddl = ReadPublishedDdl().Replace("[Cdc]", $"[{_schemaName}]", StringComparison.Ordinal);

		await using var connection = OpenConnection();
		await connection.ExecuteAsync($"IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = '{_schemaName}') EXEC('CREATE SCHEMA [{_schemaName}]')");
		await connection.ExecuteAsync(ddl);

		return $"[{_schemaName}].[CdcProcessedEvents]";
	}

	/// <summary>
	/// Extracts the published <c>CdcProcessedEvents</c> CREATE TABLE from the documentation, so this lock
	/// binds the artifact consumers copy rather than a duplicate of it that can drift.
	/// </summary>
	private static string ReadPublishedDdl()
	{
		var docPath = Path.Combine(FindRepositoryRoot(), PublishedDdlPath);
		File.Exists(docPath).ShouldBeTrue($"the published CDC documentation must exist at '{PublishedDdlPath}'.");

		var content = File.ReadAllText(docPath);
		var match = Regex.Match(
			content,
			@"CREATE TABLE \[Cdc\]\.\[CdcProcessedEvents\].*?\);",
			RegexOptions.Singleline,
			TimeSpan.FromSeconds(5));

		match.Success.ShouldBeTrue(
			"the published CDC documentation must contain a CdcProcessedEvents CREATE TABLE. If it was " +
			"renamed or removed, this lock is no longer binding the artifact consumers run and must be " +
			"repointed rather than deleted.");

		return match.Value;
	}

	/// <summary>
	/// Reads a column's declared <c>NVARCHAR</c> width out of the published DDL, so the probe values scale
	/// with the schema instead of being fixed against it.
	/// </summary>
	private static int DeclaredWidth(string ddl, string columnName)
	{
		var match = Regex.Match(
			ddl,
			columnName + @"\s+NVARCHAR\((?<width>\d+)\)",
			RegexOptions.IgnoreCase,
			TimeSpan.FromSeconds(5));

		match.Success.ShouldBeTrue(
			$"the published schema must declare an NVARCHAR width for '{columnName}'; without it this arm " +
			"cannot size its probe and would silently stop testing the key limit.");

		return int.Parse(match.Groups["width"].Value, CultureInfo.InvariantCulture);
	}

	private static string FindRepositoryRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Excalibur.sln")))
		{
			directory = directory.Parent;
		}

		directory.ShouldNotBeNull("the repository root (identified by Excalibur.sln) must be locatable from the test output directory.");
		return directory!.FullName;
	}
}
