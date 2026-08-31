// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;

using Excalibur.Integration.Tests.Data.Migrations;

using Oracle.ManagedDataAccess.Client;

using Testcontainers.Oracle;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// An Oracle container of this suite's own, because these arms rebuild <c>DISPATCH.SAGAS</c>.
/// </summary>
/// <remarks>
/// The shipped upgrade script addresses <c>DISPATCH.SAGAS</c> by literal name, so an arm that puts the
/// table back to its prior shape has to reshape the real table rather than a copy beside it. Sharing a
/// container with the saga-store suites would leave them running against whichever shape an arm here
/// last left behind, so this suite owns its container outright.
/// </remarks>
public sealed class OracleSagaUpgradeContainerFixture : ContainerFixtureBase
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
			// Pinned to the 23 tag for the reason the saga-store fixture records: the floating
			// "slim-faststart" tag resolves to an image whose listener never registers the service
			// Testcontainers connects to, and every arm then fails with ORA-12514 before reaching a database.
			.WithImage("gvenzl/oracle-free:23-slim-faststart")
			.WithName($"oracle-saga-upgrade-test-{Guid.NewGuid():N}")
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
/// Covers the shipped Oracle saga upgrade, which narrows <c>DISPATCH.SAGAS.TenantId</c> to the portable
/// maximum.
/// </summary>
/// <remarks>
/// <para>
/// This script shipped with no test. It is a migration — it runs once against a consumer's real database
/// and cannot be un-run — and the column it narrows LEADS <c>PK_SAGAS (TenantId, SagaId)</c>. Truncating
/// it therefore does not mislabel a saga, it merges two tenants onto one key: one tenant's saga state
/// starts satisfying another tenant's, and a saga can resume from state that belongs to someone else.
/// The arm that matters most is the one proving it refuses instead.
/// </para>
/// <para>
/// The trailing <c>/</c> is removed before the block is sent, and only the trailing one. It is the
/// SQL*Plus block terminator, not SQL: the script's own header says to run this file with a tool that
/// honours it. That is a real client construct, and it is a different thing from the psql meta-command
/// this suite's Postgres sibling exists to keep out — every Oracle migration tool understands <c>/</c>,
/// and the script says so in its header rather than leaving a consumer to discover it. Sending the block
/// itself, verbatim, is what an ODP.NET-based runner does.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Infrastructure", "Oracle")]
public sealed class OracleSagaTenantNarrowingShould : IClassFixture<OracleSagaUpgradeContainerFixture>
{
	private const string CreateScript = "src/Excalibur/Excalibur.Saga.Oracle/Scripts/01-SagaSchema.sql";

	private const string UpgradeScript =
		"src/Excalibur/Excalibur.Saga.Oracle/Scripts/01-SagaSchema.Upgrade.sql";

	private readonly OracleSagaUpgradeContainerFixture _fixture;

	public OracleSagaTenantNarrowingShould(OracleSagaUpgradeContainerFixture fixture) => _fixture = fixture;

	[Fact]
	public async Task Narrow_TheTenantColumn_OnADatabaseAtThePriorVersion()
	{
		await ArrangeAtPriorVersionAsync().ConfigureAwait(false);

		await ApplyUpgradeAsync().ConfigureAwait(false);

		var width = await TenantColumnWidthAsync().ConfigureAwait(false);

		width.ShouldBe(
			64,
			"the upgrade did not converge a table at the prior version. That table is the only reason the "
			+ "script exists: Oracle has no CREATE TABLE IF NOT EXISTS, so re-running 01-SagaSchema.sql "
			+ "against an existing table raises ORA-00955 and changes nothing.");
	}

	[Fact]
	public async Task Change_Nothing_WhenRunAgainstAnAlreadyConvergedTable()
	{
		await ArrangeAtPriorVersionAsync().ConfigureAwait(false);
		await ApplyUpgradeAsync().ConfigureAwait(false);
		var afterFirstRun = await TenantColumnWidthAsync().ConfigureAwait(false);

		var failure = await Record.ExceptionAsync(ApplyUpgradeAsync).ConfigureAwait(false);

		failure.ShouldBeNull(
			"the upgrade failed on a second run. A table provisioned by the current 01-SagaSchema.sql is "
			+ "already converged before anyone runs this, so the no-op path is the common one, not the "
			+ "exception.");

		var afterSecondRun = await TenantColumnWidthAsync().ConfigureAwait(false);
		afterSecondRun.ShouldBe(afterFirstRun, "the upgrade altered an already-converged column on re-run.");
	}

	[Fact]
	public async Task Report_ThatItDidNothing_WhenTheTenantColumnIsAbsent()
	{
		await ArrangeAtPriorVersionAsync().ConfigureAwait(false);

		// The primary key has to go first, and the reason is the same one that makes truncation dangerous:
		// TenantId LEADS PK_SAGAS, so Oracle refuses to drop it while the constraint stands (ORA-12991).
		await ExecuteAsync("ALTER TABLE DISPATCH.SAGAS DROP CONSTRAINT PK_SAGAS").ConfigureAwait(false);
		await ExecuteAsync("ALTER TABLE DISPATCH.SAGAS DROP COLUMN TenantId").ConfigureAwait(false);

		// The script's stated precondition is that the table came from some version of 01-SagaSchema.sql.
		// A table with no TenantId did not, and the script's own answer is to say so and change nothing
		// rather than to guess at what it is looking at.
		var failure = await Record.ExceptionAsync(ApplyUpgradeAsync).ConfigureAwait(false);

		failure.ShouldBeNull(
			"the upgrade raised on a table with no TenantId column. It is written to report and return "
			+ "there, and turning that into a hard error would change what an operator sees.");

		var width = await TenantColumnWidthAsync().ConfigureAwait(false);

		width.ShouldBe(
			0,
			"the upgrade created or altered a column on a table it does not recognise. It must leave a "
			+ "table it cannot have been meant for exactly as it found it.");
	}

	[Fact]
	public async Task Refuse_RatherThanTruncate_WhenASagaHoldsAnOverlongTenantIdentifier()
	{
		await ArrangeAtPriorVersionAsync().ConfigureAwait(false);

		// Two tenants agreeing in their first 64 characters. TenantId LEADS PK_SAGAS, so shortening these
		// does not merely relabel two sagas -- it collapses them onto one key.
		var shared = new string('t', 64);
		var tenantA = shared + "-alpha";
		var tenantB = shared + "-beta";

		await InsertSagaAsync(tenantA).ConfigureAwait(false);
		await InsertSagaAsync(tenantB).ConfigureAwait(false);

		var failure = await Record.ExceptionAsync(ApplyUpgradeAsync).ConfigureAwait(false);

		var oracleFailure = failure.ShouldBeOfType<OracleException>(
			"the upgrade did not refuse a saga whose tenant identifier does not fit VARCHAR2(64). "
			+ "Narrowing it would have merged two tenants onto one primary key, and a saga could then "
			+ "resume from another tenant's state with nothing reporting a problem.");

		oracleFailure.Message.ShouldContain(
			"REFUSED",
			Case.Sensitive,
			"the upgrade must re-raise ORA-01441 as its own refusal. The bare Oracle error names neither "
			+ "the consequence nor the remedy, and the remedy is not the obvious one — widening the column "
			+ "back or shortening the values in place is exactly what must not be done.");

		var width = await TenantColumnWidthAsync().ConfigureAwait(false);

		width.ShouldBe(
			200,
			"the upgrade refused but the column was narrowed anyway, so the refusal is a message rather "
			+ "than a protection.");

		var survivors = await DistinctTenantsAsync().ConfigureAwait(false);

		survivors.ShouldBe(
			[tenantA, tenantB],
			ignoreOrder: true,
			"a tenant identifier was lost or shortened. Both sagas must survive byte-for-byte: the whole "
			+ "point of refusing is that no saga is quietly re-filed under another tenant's identity.");
	}

	/// <summary>
	/// Provisions <c>DISPATCH.SAGAS</c> from the shipped create script, then widens <c>TenantId</c> back
	/// to the <c>VARCHAR2(200)</c> the previous version declared.
	/// </summary>
	/// <remarks>
	/// The prior shape is reconstructed from the CURRENT create script rather than from a pinned copy of
	/// the old one. A pinned copy is a second definition of the table that nothing keeps in step with the
	/// shipped one, and these arms would drift into testing a table this project no longer ships.
	/// </remarks>
	private async Task ArrangeAtPriorVersionAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			_fixture.InitializationError
			?? "Oracle must be reachable: this covers a migration that runs once against a consumer's real "
			+ "database, and a skip is indistinguishable from never having tested it.");

		await ExecuteIgnoringMissingObjectAsync("DROP TABLE DISPATCH.SAGAS CASCADE CONSTRAINTS")
			.ConfigureAwait(false);

		foreach (var statement in ShippedScript.RawBytesOf(CreateScript)
			.Split('\n')
			.Select(static line => line.Trim())
			.Where(static line => !line.StartsWith("--", StringComparison.Ordinal))
			.Aggregate(new System.Text.StringBuilder(), static (sb, line) => sb.Append(line).Append('\n'))
			.ToString()
			.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			await ExecuteAsync(statement).ConfigureAwait(false);
		}

		await ExecuteAsync("ALTER TABLE DISPATCH.SAGAS MODIFY (TenantId VARCHAR2(200))").ConfigureAwait(false);

		// LIVENESS on the arrangement. Every arm concludes something from what the script did to this
		// column; if the widen-back silently did not happen the table is already converged and each arm
		// passes having tested nothing.
		var arranged = await TenantColumnWidthAsync().ConfigureAwait(false);

		arranged.ShouldBe(
			200,
			"the fixture failed to put TenantId back to its prior width, so these arms would run against an "
			+ "already-converged table and prove nothing.");
	}

	/// <summary>
	/// Sends the shipped upgrade as a single anonymous PL/SQL block, with only the trailing SQL*Plus
	/// terminator removed.
	/// </summary>
	private async Task ApplyUpgradeAsync()
	{
		var block = ShippedScript.WithoutClientDirectives(ShippedScript.RawBytesOf(UpgradeScript)).TrimEnd();

		block.ShouldEndWith(
			"/",
			Case.Sensitive,
			"the shipped upgrade no longer ends with the SQL*Plus block terminator its header tells "
			+ "consumers to use. If that changed deliberately, this arm must change with it rather than "
			+ "quietly trimming something else.");

		await ExecuteAsync(block[..^1]).ConfigureAwait(false);
	}

	private async Task InsertSagaAsync(string tenantId)
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
		await using var command = connection.CreateCommand();

		// Only the NOT NULL columns without a default are supplied; the rest take theirs. Column names are
		// the shipped ones, so a rename in 01-SagaSchema.sql surfaces here rather than silently passing.
		command.CommandText = """
			INSERT INTO DISPATCH.SAGAS (SagaId, SagaType, StateJson, TenantId)
			VALUES (:sagaId, 'TestSaga', '{}', :tenantId)
			""";

		command.BindByName = true;
		_ = command.Parameters.Add(":sagaId", Guid.NewGuid().ToByteArray());
		_ = command.Parameters.Add(":tenantId", tenantId);
		_ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
	}

	private async Task<IReadOnlyList<string>> DistinctTenantsAsync()
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
		await using var command = connection.CreateCommand();
		command.CommandText = "SELECT DISTINCT TenantId FROM DISPATCH.SAGAS";

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
	/// Returns the DECLARED width of <c>TenantId</c>, or zero when the column is absent.
	/// </summary>
	/// <remarks>
	/// Reads <c>CHAR_LENGTH</c> when the column was declared in character semantics and
	/// <c>DATA_LENGTH</c> otherwise, so the answer is the declared length rather than a byte count a
	/// multibyte character set inflates — the same discrimination the shipped script makes.
	/// </remarks>
	private async Task<int> TenantColumnWidthAsync()
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
		await using var command = connection.CreateCommand();

		command.CommandText = """
			SELECT NVL(MAX(CASE WHEN CHAR_USED = 'C' THEN CHAR_LENGTH ELSE DATA_LENGTH END), 0)
			  FROM ALL_TAB_COLUMNS
			 WHERE OWNER = 'DISPATCH' AND TABLE_NAME = 'SAGAS' AND COLUMN_NAME = 'TENANTID'
			""";

		var value = await command
			.ExecuteScalarAsync(TestContext.Current.CancellationToken)
			.ConfigureAwait(false);

		return value is null or DBNull ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
	}

	private async Task ExecuteAsync(string sql)
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
		await using var command = connection.CreateCommand();

		// CA2100: the text is either this class's own const DDL or the package's shipped script.
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
