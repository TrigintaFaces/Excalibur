// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;
using System.Text;

using Oracle.ManagedDataAccess.Client;

using Testcontainers.Oracle;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.Data.Migrations;

/// <summary>
/// An Oracle container of this suite's own, because these arms reshape the real shipped tables.
/// </summary>
/// <remarks>
/// The shipped migrations address their tables by literal name, so an arm that puts a table back to its
/// prior shape has to reshape the real table rather than a copy beside it. Sharing a container with the
/// store suites would leave them running against whichever shape an arm here last left behind, so this
/// suite owns its container outright.
/// </remarks>
public sealed class OracleNarrowingUpgradeContainerFixture : ContainerFixtureBase
{
	private OracleContainer? _container;

	/// <summary>Gets the connection string for this suite's Oracle container.</summary>
	public string ConnectionString => _container?.GetConnectionString()
		?? throw new InvalidOperationException("Container not initialized");

	protected override TimeSpan ContainerStartTimeout => TimeSpan.FromMinutes(6);

	/// <summary>Creates a connection to this suite's container.</summary>
	public OracleConnection CreateConnection() => new(ConnectionString);

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new OracleBuilder()
			// Pinned to the 23 tag for the reason the sibling Oracle fixtures record: the floating
			// "slim-faststart" tag resolves to an image whose listener never registers the service
			// Testcontainers connects to, and every arm then fails with ORA-12514 before reaching a database.
			.WithImage("gvenzl/oracle-free:23-slim-faststart")
			.WithName($"oracle-narrowing-test-{Guid.NewGuid():N}")
			.WithUsername("DISPATCH")
			.WithPassword("Test_Pass123")
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	protected override async Task DisposeContainerAsync(CancellationToken cancellationToken)
	{
		try
		{
			if (_container is not null)
			{
				using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
				cts.CancelAfter(TimeSpan.FromSeconds(30));
				await _container.DisposeAsync().AsTask().WaitAsync(cts.Token).ConfigureAwait(false);
			}
		}
		catch (Exception)
		{
			// Suppress disposal errors and timeouts to prevent a test-host crash, as the sibling Oracle
			// fixtures do.
		}
	}
}

/// <summary>
/// Covers the shipped Oracle migrations that NARROW a tenant column to the portable maximum.
/// </summary>
/// <remarks>
/// <para>
/// These are migrations: they run once, against a consumer's real database, and they cannot be un-run.
/// The properties below are the ones an upgrade actually depends on — it converges a database at the
/// prior version, it is safe to run twice, and above all it REFUSES rather than shortening a tenant
/// identifier.
/// </para>
/// <para>
/// The refusal arm is the one that matters. Both columns are part of a key: the inbox dedup key
/// <c>(MessageId, HandlerType, TenantId)</c> and the snapshot uniqueness key
/// <c>(AGGREGATEID, AGGREGATETYPE, TENANTID)</c>. Two tenants whose identifiers agree in their first 64
/// characters would collapse onto ONE key, so one tenant's delivery would be seen as the other's
/// duplicate and skipped, and one tenant's snapshot would satisfy the other's upsert target.
/// </para>
/// <para>
/// The trailing <c>/</c> is removed before the block is sent, and only the trailing one. It is the
/// SQL*Plus block terminator, not SQL. Sending the block itself, verbatim, is what an ODP.NET-based
/// runner does.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Infrastructure", "Oracle")]
public sealed class OracleTenantNarrowingMigrationShould : IClassFixture<OracleNarrowingUpgradeContainerFixture>
{
	private readonly OracleNarrowingUpgradeContainerFixture _fixture;

	public OracleTenantNarrowingMigrationShould(OracleNarrowingUpgradeContainerFixture fixture) =>
		_fixture = fixture;

	/// <summary>
	/// Gets the two narrowing migrations, each with the table it narrows, the width the version before it
	/// declared, and a minimal insert for that table.
	/// </summary>
	/// <remarks>
	/// The prior width is reconstructed by applying the CURRENT create script and widening the column back
	/// to what the earlier one declared. That is deliberate: pinning a copy of the old create script into
	/// the test would freeze a second definition of the table that nothing keeps in step with the shipped
	/// one, and the arms would then drift into testing a table this project no longer ships.
	/// </remarks>
	public static TheoryData<string, string, string, string, int, string> Migrations =>
		new()
		{
			{
				"inbox 003",
				"src/Excalibur/Excalibur.Inbox.Oracle/Scripts/001_CreateInboxSchema.MultiTenant.sql",
				"src/Excalibur/Excalibur.Inbox.Oracle/Scripts/003_NarrowTenantIdToPortableMaximum.sql",
				"INBOX_MESSAGES",
				255,
				"INSERT INTO INBOX_MESSAGES (MessageId, HandlerType, ReceivedAt, TenantId) "
				+ "VALUES (:rowKey, 'Handler', SYSTIMESTAMP, :tenantId)"
			},
			{
				"snapshot 006",
				"src/Excalibur/Excalibur.EventSourcing.Oracle/Scripts/001_CreateSnapshotSchema.sql",
				"src/Excalibur/Excalibur.EventSourcing.Oracle/Scripts/006_NarrowSnapshotTenantIdToPortableMaximum.sql",
				"EVENTSTORESNAPSHOTS",
				255,
				"INSERT INTO EVENTSTORESNAPSHOTS (AGGREGATEID, AGGREGATETYPE, VERSION, DATA, CREATEDAT, TENANTID) "
				+ "VALUES (:rowKey, 'Agg', 1, HEXTORAW('00'), SYSTIMESTAMP, :tenantId)"
			},
		};

	[Theory]
	[MemberData(nameof(Migrations))]
	public async Task Narrow_TheTenantColumn_OnADatabaseAtThePriorVersion(
		string label,
		string createScript,
		string narrowScript,
		string table,
		int priorWidth,
		string insert)
	{
		_ = insert;

		await ArrangeAtPriorVersionAsync(label, createScript, table, priorWidth).ConfigureAwait(false);

		await ApplyAsync(narrowScript).ConfigureAwait(false);

		var width = await TenantColumnWidthAsync(table).ConfigureAwait(false);

		width.ShouldBe(
			64,
			$"{label} did not converge a table at the prior version. That table is the only reason the "
			+ "script exists: Oracle has no CREATE TABLE IF NOT EXISTS, so re-running the create script "
			+ "against an existing table raises ORA-00955 and changes nothing.");
	}

	[Theory]
	[MemberData(nameof(Migrations))]
	public async Task Change_Nothing_WhenRunAgainstAnAlreadyConvergedTable(
		string label,
		string createScript,
		string narrowScript,
		string table,
		int priorWidth,
		string insert)
	{
		_ = insert;

		await ArrangeAtPriorVersionAsync(label, createScript, table, priorWidth).ConfigureAwait(false);
		await ApplyAsync(narrowScript).ConfigureAwait(false);
		var afterFirstRun = await TenantColumnWidthAsync(table).ConfigureAwait(false);

		// An operator who cannot tell whether the first pass completed will run it again, and a table
		// provisioned by the CURRENT create script is already converged before anyone runs this at all.
		var failure = await Record.ExceptionAsync(() => ApplyAsync(narrowScript)).ConfigureAwait(false);

		failure.ShouldBeNull($"{label} failed on a second run against an already-converged table.");

		var afterSecondRun = await TenantColumnWidthAsync(table).ConfigureAwait(false);
		afterSecondRun.ShouldBe(afterFirstRun, $"{label} altered an already-converged column on re-run.");
	}

	[Theory]
	[MemberData(nameof(Migrations))]
	public async Task Refuse_RatherThanTruncate_WhenARowHoldsAnOverlongTenantIdentifier(
		string label,
		string createScript,
		string narrowScript,
		string table,
		int priorWidth,
		string insert)
	{
		await ArrangeAtPriorVersionAsync(label, createScript, table, priorWidth).ConfigureAwait(false);

		// Two tenants that agree in their first 64 characters and differ after. Shortening makes them one.
		var shared = new string('t', 64);
		var tenantA = shared + "-alpha";
		var tenantB = shared + "-beta";

		await InsertAsync(insert, tenantA).ConfigureAwait(false);
		await InsertAsync(insert, tenantB).ConfigureAwait(false);

		var failure = await Record.ExceptionAsync(() => ApplyAsync(narrowScript)).ConfigureAwait(false);

		var oracleFailure = failure.ShouldBeOfType<OracleException>(
			$"{label} did not refuse a row whose tenant identifier does not fit in 64 bytes. Narrowing it "
			+ "would have merged two tenants onto one key, silently, on live data.");

		oracleFailure.Message.ShouldContain(
			"REFUSED",
			Case.Sensitive,
			$"{label} must raise its own refusal rather than a bare ORA-01441, which names neither the "
			+ "consequence nor the remedy — and the remedy is not the obvious one: shortening the values "
			+ "in place is exactly what must not be done.");

		oracleFailure.Message.ShouldContain(
			"70",
			Case.Sensitive,
			$"{label} refused without naming how long the longest offending identifier is. The operator "
			+ "cannot act on a refusal that does not say what is in the way.");

		var width = await TenantColumnWidthAsync(table).ConfigureAwait(false);

		width.ShouldBe(
			priorWidth,
			$"{label} refused but the column was narrowed anyway, so the refusal is a message rather than "
			+ "a protection.");

		var survivors = await DistinctTenantsAsync(table).ConfigureAwait(false);

		survivors.ShouldBe(
			[tenantA, tenantB],
			ignoreOrder: true,
			$"{label} lost or shortened a tenant identifier. Both rows must survive byte-for-byte: the "
			+ "whole point of refusing is that no row is quietly re-filed under another tenant's identity.");
	}

	/// <summary>
	/// Provisions the table from the shipped create script, then widens its tenant column back to the
	/// width the previous version declared.
	/// </summary>
	private async Task ArrangeAtPriorVersionAsync(
		string label,
		string createScript,
		string table,
		int priorWidth)
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			_fixture.InitializationError
			?? "Oracle must be reachable: these arms cover migrations that run once against a consumer's "
			+ "real database, and a skip is indistinguishable from never having tested them.");

		await ExecuteIgnoringMissingObjectAsync($"DROP TABLE {table} CASCADE CONSTRAINTS")
			.ConfigureAwait(false);

		foreach (var statement in StatementsOf(ShippedScript.RawBytesOf(createScript)))
		{
			await ExecuteAsync(statement).ConfigureAwait(false);
		}

		await ExecuteAsync($"ALTER TABLE {table} MODIFY (TENANTID VARCHAR2({priorWidth.ToString(CultureInfo.InvariantCulture)}))")
			.ConfigureAwait(false);

		// LIVENESS on the arrangement. Every arm concludes something from what the script did to this
		// column; if the widen-back silently did not happen the table is already converged and each arm
		// passes having tested nothing.
		var arranged = await TenantColumnWidthAsync(table).ConfigureAwait(false);

		arranged.ShouldBe(
			priorWidth,
			$"the {label} fixture failed to put TENANTID back to its prior width, so these arms would run "
			+ "against an already-converged table and prove nothing.");
	}

	/// <summary>
	/// Sends a shipped migration as a single anonymous PL/SQL block, with only the trailing SQL*Plus
	/// terminator removed.
	/// </summary>
	private async Task ApplyAsync(string narrowScript)
	{
		var block = ShippedScript.WithoutClientDirectives(ShippedScript.RawBytesOf(narrowScript)).TrimEnd();

		block.ShouldEndWith(
			"/",
			Case.Sensitive,
			"the shipped migration no longer ends with the SQL*Plus block terminator. If that changed "
			+ "deliberately, this arm must change with it rather than quietly trimming something else.");

		await ExecuteAsync(block[..^1]).ConfigureAwait(false);
	}

	private async Task InsertAsync(string insert, string tenantId)
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
		await using var command = connection.CreateCommand();

		// CA2100: the text comes from this class's own const-backed theory data; the tenant identifier and
		// row id are bound as parameters.
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
		command.CommandText = insert;
#pragma warning restore CA2100
		command.BindByName = true;
		_ = command.Parameters.Add(":rowKey", Guid.NewGuid().ToString("N"));
		_ = command.Parameters.Add(":tenantId", tenantId);
		_ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
	}

	private async Task<IReadOnlyList<string>> DistinctTenantsAsync(string table)
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
		await using var command = connection.CreateCommand();

		// CA2100: the table name comes from this class's own const-backed theory data.
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
		command.CommandText = $"SELECT DISTINCT TENANTID FROM {table}";
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
	/// Returns the DECLARED width of the tenant column, or zero when the column is absent.
	/// </summary>
	/// <remarks>
	/// Reads <c>CHAR_LENGTH</c> when the column was declared in character semantics and
	/// <c>DATA_LENGTH</c> otherwise, so the answer is the declared length rather than a byte count a
	/// multibyte character set inflates — the same discrimination the shipped scripts make.
	/// </remarks>
	private async Task<int> TenantColumnWidthAsync(string table)
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
		await using var command = connection.CreateCommand();

		command.CommandText = """
			SELECT NVL(MAX(CASE WHEN CHAR_USED = 'C' THEN CHAR_LENGTH ELSE DATA_LENGTH END), 0)
			  FROM USER_TAB_COLUMNS
			 WHERE TABLE_NAME = :tableName AND COLUMN_NAME = 'TENANTID'
			""";

		command.BindByName = true;
		_ = command.Parameters.Add(":tableName", table);

		var value = await command
			.ExecuteScalarAsync(TestContext.Current.CancellationToken)
			.ConfigureAwait(false);

		return value is null or DBNull ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
	}

	/// <summary>
	/// Splits a shipped Oracle create script into statements, dropping whole-line comments first.
	/// </summary>
	private static IEnumerable<string> StatementsOf(string script)
	{
		var stripped = new StringBuilder();

		foreach (var line in script.Split('\n'))
		{
			var trimmed = line.Trim();

			if (!trimmed.StartsWith("--", StringComparison.Ordinal))
			{
				_ = stripped.Append(trimmed).Append('\n');
			}
		}

		return stripped
			.ToString()
			.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Where(static statement => statement.Length > 0);
	}

	private async Task ExecuteAsync(string sql)
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
		await using var command = connection.CreateCommand();

		// CA2100: the text is either this class's own const-backed fixture DDL or the package's shipped
		// script.
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
		command.CommandText = sql;
#pragma warning restore CA2100
		command.CommandTimeout = 180;
		_ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
	}

	/// <summary>Runs DDL that is expected to fail only because the object is not there yet.</summary>
	private async Task ExecuteIgnoringMissingObjectAsync(string sql)
	{
		try
		{
			await ExecuteAsync(sql).ConfigureAwait(false);
		}
		catch (OracleException ex) when (ex.Number is 942 or 4043)
		{
			// ORA-00942 / ORA-04043: the object does not exist. That is the state this call wants.
		}
	}
}
