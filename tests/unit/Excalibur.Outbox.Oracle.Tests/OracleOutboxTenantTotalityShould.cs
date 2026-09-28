// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Dapper;

using Oracle.ManagedDataAccess.Client;

using Shouldly;

using Xunit;

namespace Excalibur.Outbox.Oracle.Tests;

/// <summary>
/// Locks the TOTALITY of <c>OUTBOX.TENANT_ID</c> against a real Oracle, for the shipped schema and the
/// staging path that has to agree with it.
/// </summary>
/// <remarks>
/// <para>
/// The property under test is that there is exactly ONE way to say "this message has no tenant" — the
/// reserved <c>__untenanted__</c> sentinel — rather than two (the sentinel and SQL <c>NULL</c>).
/// </para>
/// <para>
/// Oracle earns its own suite rather than being assumed symmetric with Postgres, for two reasons that
/// are dialect-specific and each of which has exactly one correct answer:
/// </para>
/// <list type="bullet">
/// <item><description>
/// Oracle folds the empty string to <c>NULL</c>. A sentinel that was empty would be stored as NULL, so
/// the constraint would hold while the split it exists to remove survived. The reserved value is
/// non-empty for that reason, and only a real Oracle can confirm the round trip.
/// </description></item>
/// <item><description>
/// <c>DEFAULT</c> must precede <c>NOT NULL</c> in a column constraint. The reverse order is a syntax
/// error, and no compiler in this repository would catch it.
/// </description></item>
/// </list>
/// <para>
/// There is no upgrade path to lock. The package ships one CREATE script per provider, already at the
/// final shape, so the only database these arms can meaningfully describe is the one a consumer
/// actually provisions. Arms that asserted about converting a legacy database were removed with the
/// migration scripts rather than left asserting against a path that no longer exists.
/// </para>
/// <para>
/// It provisions from the DDL the package actually SHIPS rather than from the container fixture's
/// hand-written copy — see <see cref="ShippedOracleOutboxSchema"/>.
/// </para>
/// <para>
/// NOT skip-gated. A Docker-unavailable run FAILS rather than passing vacuously.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Oracle")]
[Collection(OracleOutboxCollection.Name)]
public sealed class OracleOutboxTenantTotalityShould(OracleOutboxStoreContainerFixture fixture)
{
	private const string Sentinel = "__untenanted__";

	private readonly OracleOutboxStoreContainerFixture _fixture = fixture;

	private string ConnectionString => _fixture.ConnectionString;

	/// <summary>
	/// FRESH INSTALL: a writer that omits the tenant column entirely gets the sentinel, not NULL.
	/// </summary>
	[Fact]
	public async Task DefaultAnOmittedTenantToTheSentinelOnAFreshInstall()
	{
		await RequireDockerAndFreshSchemaAsync();

		await ExecuteAsync(
			"INSERT INTO OUTBOX (MESSAGE_ID, MESSAGE_TYPE) VALUES ('m-1', 'T')");

		(await TenantOfAsync("m-1")).ShouldBe(
			Sentinel,
			"a message staged without a tenant must be stored as the untenanted PARTITION, not as an absent value");

		(await IsTenantColumnNullableAsync()).ShouldBeFalse(
			"the fresh-install schema must not be able to represent a NULL tenant at all");
	}

	/// <summary>
	/// FRESH INSTALL, liveness: a real tenant is stored verbatim and is not absorbed by the default.
	/// </summary>
	[Fact]
	public async Task StoreARealTenantVerbatimRatherThanApplyingTheDefault()
	{
		await RequireDockerAndFreshSchemaAsync();

		await ExecuteAsync(
			"INSERT INTO OUTBOX (MESSAGE_ID, MESSAGE_TYPE, TENANT_ID) VALUES ('m-1', 'T', 'acme')");

		(await TenantOfAsync("m-1")).ShouldBe("acme", "a real tenant must survive the write unchanged");
	}

	/// <summary>
	/// FRESH INSTALL: the closed column actually refuses NULL, rather than merely looking closed.
	/// </summary>
	[Fact]
	public async Task RejectAnExplicitNullTenantOnceTheColumnIsTotal()
	{
		await RequireDockerAndFreshSchemaAsync();

		var write = async () => await ExecuteAsync(
			"INSERT INTO OUTBOX (MESSAGE_ID, MESSAGE_TYPE, TENANT_ID) VALUES ('m-null', 'T', NULL)");

		var ex = await write.ShouldThrowAsync<OracleException>(
			"an explicit NULL tenant must be refused by the database, not silently accepted");

		ex.Number.ShouldBe(1400, "ORA-01400 is 'cannot insert NULL into' — the constraint, not some other failure");
	}

	/// <summary>
	/// FRESH INSTALL: an EMPTY tenant is refused too, because Oracle folds it to NULL.
	/// </summary>
	/// <remarks>
	/// This arm is Oracle-only and is the reason the sentinel is non-empty. On a dialect that stored
	/// <c>''</c> as a distinct value, an empty tenant would satisfy NOT NULL and quietly become a third
	/// spelling of "absent". Here it must fail, which is what proves the empty string never becomes one.
	/// </remarks>
	[Fact]
	public async Task RejectAnEmptyTenantBecauseOracleFoldsItToNull()
	{
		await RequireDockerAndFreshSchemaAsync();

		var write = async () => await ExecuteAsync(
			"INSERT INTO OUTBOX (MESSAGE_ID, MESSAGE_TYPE, TENANT_ID) VALUES ('m-empty', 'T', '')");

		var ex = await write.ShouldThrowAsync<OracleException>(
			"Oracle folds '' to NULL, so an empty tenant must hit the same constraint as an explicit NULL");

		ex.Number.ShouldBe(1400, "the empty string must fail as a NULL, which is exactly what Oracle treats it as");
	}

	/// <summary>
	/// THE COUPLING ARM: the real staging request writes an UNTENANTED message into the total column.
	/// </summary>
	/// <remarks>
	/// This is the arm that fails if the schema is made total without the staging path being changed to
	/// match. The request used to bind the caller's raw <c>tenantId</c> argument, which is null for an
	/// untenanted message; against a NOT NULL column that is ORA-01400 on every untenanted stage.
	/// </remarks>
	[Fact]
	public async Task StageAnUntenantedMessageThroughTheRealRequestAndStoreTheSentinel()
	{
		await RequireDockerAndFreshSchemaAsync();

		var request = NewInsertRequest("m-staged", tenantId: null);

		await using var connection = new OracleConnection(ConnectionString);
		await connection.OpenAsync(TestContext.Current.CancellationToken);
		_ = await request.ResolveAsync(connection);

		(await TenantOfAsync("m-staged")).ShouldBe(
			Sentinel,
			"the staging path must bind the untenanted PARTITION term, not the caller's raw null — binding "
			+ "the raw argument would fail with ORA-01400 on every untenanted stage");
	}

	/// <summary>
	/// THE COUPLING ARM, liveness: the same request preserves a real tenant.
	/// </summary>
	[Fact]
	public async Task StageATenantedMessageThroughTheRealRequestAndPreserveTheTenant()
	{
		await RequireDockerAndFreshSchemaAsync();

		var request = NewInsertRequest("m-tenanted", tenantId: "acme");

		await using var connection = new OracleConnection(ConnectionString);
		await connection.OpenAsync(TestContext.Current.CancellationToken);
		_ = await request.ResolveAsync(connection);

		(await TenantOfAsync("m-tenanted")).ShouldBe("acme", "normalising the untenanted case must not touch a real tenant");
	}

	/// <summary>
	/// The splitter keeps a PL/SQL block WHOLE rather than truncating it at its first internal semicolon.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Asserted directly because the consequence of getting this wrong is not a clear failure: a truncated
	/// block still reaches the driver as a plausible fragment and dies with an Oracle syntax error far
	/// from its cause. Every <see cref="ShippedOracleOutboxSchema.CreateFreshAsync"/> exercises the
	/// splitter incidentally, but nothing else asserts this property of it.
	/// </para>
	/// <para>
	/// The input is inline rather than a shipped file on purpose. It is the SPLITTER under test, not the
	/// DDL, so this arm stays meaningful whatever the shipped scripts happen to contain — and it cannot
	/// go quietly green the way a file-driven arm does when the file stops containing a block.
	/// </para>
	/// </remarks>
	[Fact]
	public void KeepAPlSqlBlockWholeWhenSplittingAScript()
	{
		const string Script = """
			ALTER TABLE OUTBOX MODIFY (TENANT_ID NULL);

			DECLARE
			    columnCount NUMBER;
			BEGIN
			    SELECT COUNT(*) INTO columnCount FROM USER_TAB_COLUMNS WHERE TABLE_NAME = 'OUTBOX';
			    IF columnCount > 0 THEN
			        NULL;
			    END IF;
			END;
			/
			""";

		var statements = ShippedOracleOutboxSchema.SplitStatements(Script).ToList();

		statements.Count.ShouldBe(
			2,
			"one plain statement and one PL/SQL block — a splitter that honoured the block's internal "
			+ "semicolons would yield more than two fragments");

		statements[0].ShouldBe(
			"ALTER TABLE OUTBOX MODIFY (TENANT_ID NULL)",
			"a plain statement still terminates at its semicolon, and the terminator is not passed on");

		// Asserted with an explicit comparison rather than a containment matcher: `string` is also
		// `IEnumerable<char>`, so the matcher's element overload is a candidate and the assertion can bind
		// to a predicate over characters instead of the substring intended.
		statements[1].Contains("END IF;", StringComparison.Ordinal).ShouldBeTrue(
			"the block must arrive whole: a splitter that cut at the first internal semicolon would hand "
			+ "the driver a fragment ending mid-block, which fails with a syntax error far from its cause");
	}

	private static InsertOutboxMessage NewInsertRequest(string messageId, string? tenantId) =>
		new(
			messageId: messageId,
			messageType: "T",
			messageMetadata: "{}",
			messageBody: [1],
			createdAt: DateTimeOffset.UtcNow,
			tenantId: tenantId,
			destination: null,
			correlationId: null,
			causationId: null,
			priority: 0,
			scheduledAt: null,
			partitionKey: null,
			groupKey: null,
			sequenceNumber: 0,
			targetTransports: null,
			isMultiTransport: false,
			outboxTableName: "OUTBOX",
			sqlTimeOutSeconds: 30,
			cancellationToken: TestContext.Current.CancellationToken);

	private async Task RequireDockerAndFreshSchemaAsync()
	{
		await _fixture.EnsureInitializedAsync();

		_fixture.DockerAvailable.ShouldBeTrue(
			"this lock asserts a property of the SHIPPED SCHEMA and of Oracle's own dialect rules, and is "
			+ "deliberately never skipped — a green run that never reached a database would certify nothing.");

		await ShippedOracleOutboxSchema.CreateFreshAsync(ConnectionString, TestContext.Current.CancellationToken);
	}

	private Task<string?> TenantOfAsync(string messageId) =>
		ScalarAsync<string>($"SELECT TENANT_ID FROM OUTBOX WHERE MESSAGE_ID = '{messageId}'");

	private async Task<bool> IsTenantColumnNullableAsync() =>
		await ScalarAsync<string>(
			"SELECT NULLABLE FROM USER_TAB_COLUMNS WHERE TABLE_NAME = 'OUTBOX' AND COLUMN_NAME = 'TENANT_ID'")
			== "Y";

	private async Task ExecuteAsync(string sql)
	{
		await using var connection = new OracleConnection(ConnectionString);
		await connection.OpenAsync(TestContext.Current.CancellationToken);
		_ = await connection.ExecuteAsync(sql);
	}

	private async Task<T?> ScalarAsync<T>(string sql)
	{
		await using var connection = new OracleConnection(ConnectionString);
		await connection.OpenAsync(TestContext.Current.CancellationToken);
		return await connection.ExecuteScalarAsync<T>(sql);
	}
}
