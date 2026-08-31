// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Oracle.ManagedDataAccess.Client;

using Shouldly;

using Xunit;

namespace Excalibur.Outbox.Oracle.Tests;

/// <summary>
/// Locks the TENANT PROVENANCE of a dead-lettered message on Oracle, against a real database, for the
/// fresh-install schema, the upgrade script, and both move paths.
/// </summary>
/// <remarks>
/// <para>
/// The property under test is that a message which stops being deliverable still records which tenant
/// produced it. This matters more here than anywhere else in the outbox, because the move DELETEs the
/// outbox row: the dead-letter row is the ONLY surviving record of the message, so a column the move does
/// not copy is destroyed rather than merely unqueryable. Without it an operator cannot attribute a dead
/// letter, and a redrive cannot return the message to the partition it came from.
/// </para>
/// <para>
/// Oracle earns its own suite rather than being assumed symmetric with Postgres, for reasons that are
/// dialect-specific and that no compiler in this repository can check:
/// </para>
/// <list type="bullet">
/// <item><description>
/// The move is a PL/SQL anonymous block, and ODP.NET binds positionally here. Adding a column to the
/// INSERT list changes the statement's shape, and a mis-bound block is discovered by running it against a
/// real server — or by a consumer.
/// </description></item>
/// <item><description>
/// Oracle folds the empty string to <c>NULL</c>, so an empty tenant would violate the very NOT NULL
/// constraint that is meant to make the term total. Only a real Oracle confirms the round trip.
/// </description></item>
/// <item><description>
/// The upgrade script is PL/SQL, and Oracle treats NULLs as DISTINCT in a unique index — the reason the
/// column is closed before the key is widened. Both are server-checked and nothing else.
/// </description></item>
/// </list>
/// <para>
/// Both arms are present deliberately. "An untenanted message stores the reserved key" is satisfied by a
/// move that stamps EVERY entry with that key, which would destroy tenant identity completely while
/// looking correct; the arm that a real tenant survives verbatim is what makes that inexpressible.
/// </para>
/// <para>
/// NOT skip-gated. A Docker-unavailable run FAILS rather than passing vacuously.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Oracle")]
public sealed class OracleDeadLetterTenantProvenanceShould(OracleOutboxStoreContainerFixture fixture)
	: IClassFixture<OracleOutboxStoreContainerFixture>
{
	private const string Sentinel = "__untenanted__";

	private readonly OracleOutboxStoreContainerFixture _fixture = fixture;

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private string ConnectionString => _fixture.ConnectionString;

	/// <summary>
	/// THE HEADLINE ARM: a message dead-letters, and the tenant survives both the move and the read-back.
	/// </summary>
	[Fact]
	public async Task CarryARealTenantThroughTheMoveAndTheReadBack()
	{
		await RequireDockerAndFreshSchemaAsync();

		await StageAsync("m-tenanted", tenantId: "acme");
		await MoveToDeadLetterAsync("m-tenanted");

		(await StoredTenantOfAsync("m-tenanted")).ShouldBe(
			"acme",
			"the move must copy the originating tenant: it deletes the outbox row, so a tenant it does not "
			+ "carry across is destroyed rather than merely unqueryable");

		(await ReadBackAsync("m-tenanted")).TenantId.ShouldBe(
			"acme",
			"the operator-facing read must project the tenant, or an operator still cannot attribute the "
			+ "entry even though the database holds it");
	}

	/// <summary>
	/// THE OTHER ARM: an untenanted message lands on the reserved key, not on NULL.
	/// </summary>
	/// <remarks>
	/// Paired with the arm above deliberately. A move that stamped EVERY entry with the reserved key would
	/// satisfy this arm perfectly while destroying tenant identity, and nothing else here would notice.
	/// </remarks>
	[Fact]
	public async Task StoreTheReservedKeyForAnUntenantedMessageRatherThanNull()
	{
		await RequireDockerAndFreshSchemaAsync();

		await StageAsync("m-untenanted", tenantId: null);
		await MoveToDeadLetterAsync("m-untenanted");

		(await StoredTenantOfAsync("m-untenanted")).ShouldBe(
			Sentinel,
			"an untenanted message must be recorded as the untenanted PARTITION, not as an absent value — "
			+ "and on Oracle the reserved value must be non-empty, since '' would be stored as NULL and "
			+ "could not satisfy the constraint at all");

		(await ReadBackAsync("m-untenanted")).TenantId.ShouldBe(Sentinel);
	}

	/// <summary>
	/// THE STRUCTURAL ARM: the fresh-install column refuses NULL rather than merely looking closed.
	/// </summary>
	[Fact]
	public async Task RejectAnExplicitNullTenantOnTheDeadLetterTable()
	{
		await RequireDockerAndFreshSchemaAsync();

		var write = async () => await ExecuteAsync(
			"""
			INSERT INTO OUTBOX_DEAD_LETTERS (message_id, tenant_id, message_type, occurred_on, attempts)
			VALUES ('dl-null', NULL, 'T', SYSTIMESTAMP, 1)
			""");

		_ = await write.ShouldThrowAsync<OracleException>(
			"a provenance column that can hold NULL is one that can silently lose the provenance");
	}

	/// <summary>
	/// UPGRADE: an entry written before the column existed converges onto the reserved key, the column ends
	/// up closed, and the key ends up carrying the tenant.
	/// </summary>
	/// <remarks>
	/// The reserved key on such a row records "no tenant was captured", which is NOT the claim that the
	/// message had no tenant — its real tenant was never written anywhere and the outbox row it could have
	/// been read from is gone. This arm locks that the convergence happens at all, since a column left
	/// nullable would preserve the very ambiguity the reserved key exists to remove.
	/// </remarks>
	[Fact]
	public async Task ConvergeAPreExistingDeadLetterOntoTheReservedKey()
	{
		await RequireDockerAndFreshSchemaAsync();
		await ShippedOracleOutboxSchema.RemoveDeadLetterTenantColumnToLegacyShapeAsync(ConnectionString, Ct);

		(await HasDeadLetterTenantColumnAsync()).ShouldBeFalse(
			"the legacy shape must actually be re-established, or the migration below proves nothing");

		await ExecuteAsync(
			"""
			INSERT INTO OUTBOX_DEAD_LETTERS (message_id, message_type, occurred_on, attempts)
			VALUES ('legacy-dl', 'T', SYSTIMESTAMP, 4)
			""");

		await ShippedOracleOutboxSchema.RunDeadLetterMigrationAsync(ConnectionString, Ct);

		(await StoredTenantOfAsync("legacy-dl")).ShouldBe(
			Sentinel,
			"an entry that predates the column must hold a value after the upgrade, because the column is "
			+ "now total — on that row the value records 'not captured', not 'known untenanted'");

		(await IsDeadLetterTenantNullableAsync()).ShouldBeFalse(
			"the upgrade must close the column, not merely put a value in it");

		(await UniqueKeyIncludesTenantAsync()).ShouldBeTrue(
			"the upgrade must widen the key, or a later write could drop the tenant and still satisfy the "
			+ "constraint");
	}

	/// <summary>
	/// UPGRADE, liveness: after the upgrade the move works and a real tenant is stored verbatim.
	/// </summary>
	/// <remarks>
	/// This is the arm that fails if the upgrade produces a schema the shipped PL/SQL cannot write to — a
	/// column of the wrong width, a key the INSERT violates, a bind that no longer lines up. An upgrade
	/// that converges the data and then rejects every subsequent write is worse than no upgrade, and the
	/// convergence arm above cannot see it.
	/// </remarks>
	[Fact]
	public async Task AcceptRealTraffic_AfterTheUpgrade()
	{
		await RequireDockerAndFreshSchemaAsync();
		await ShippedOracleOutboxSchema.RemoveDeadLetterTenantColumnToLegacyShapeAsync(ConnectionString, Ct);
		await ShippedOracleOutboxSchema.RunDeadLetterMigrationAsync(ConnectionString, Ct);

		await StageAsync("m-post-upgrade", tenantId: "fabrikam");
		await MoveToDeadLetterAsync("m-post-upgrade");

		(await StoredTenantOfAsync("m-post-upgrade")).ShouldBe(
			"fabrikam",
			"an upgraded database must accept the same traffic a fresh one does, with the tenant intact");
	}

	/// <summary>
	/// UPGRADE: the script is safe to run twice, and against an already-converged database.
	/// </summary>
	/// <remarks>
	/// Deployment scripts get re-run — by a retried pipeline, or by an operator who cannot tell whether the
	/// first attempt finished. A migration that is only correct once is one that will be wrong in production.
	/// </remarks>
	[Fact]
	public async Task BeSafeToRunTheUpgradeTwiceAndOnAFreshInstall()
	{
		await RequireDockerAndFreshSchemaAsync();

		await StageAsync("m-already", tenantId: "acme");
		await MoveToDeadLetterAsync("m-already");

		await ShippedOracleOutboxSchema.RunDeadLetterMigrationAsync(ConnectionString, Ct);
		await ShippedOracleOutboxSchema.RunDeadLetterMigrationAsync(ConnectionString, Ct);

		(await StoredTenantOfAsync("m-already")).ShouldBe(
			"acme",
			"running the upgrade against a converged database must not rewrite a real tenant to the "
			+ "reserved key — the backfill touches only rows that hold no value");

		(await ScalarAsync<decimal>("SELECT COUNT(*) FROM OUTBOX_DEAD_LETTERS")).ShouldBe(
			1m, "a second run must not duplicate or drop rows");

		(await IsDeadLetterTenantNullableAsync()).ShouldBeFalse("the column must remain closed");
		(await UniqueKeyIncludesTenantAsync()).ShouldBeTrue("the key must remain widened");
	}

	private async Task StageAsync(string messageId, string? tenantId)
	{
		var request = new InsertOutboxMessage(
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
			cancellationToken: Ct);

		await using var connection = new OracleConnection(ConnectionString);
		await connection.OpenAsync(Ct);
		_ = await request.ResolveAsync(connection);
	}

	private async Task MoveToDeadLetterAsync(string messageId)
	{
		var request = new MoveOutboxMessageToDeadLetter(
			messageId: messageId,
			outboxTableName: "OUTBOX",
			deadLetterTableName: "OUTBOX_DEAD_LETTERS",
			sqlTimeOutSeconds: 30,
			cancellationToken: Ct);

		await using var connection = new OracleConnection(ConnectionString);
		await connection.OpenAsync(Ct);
		_ = await request.ResolveAsync(connection);
	}

	private async Task<DeadLetterRecord> ReadBackAsync(string messageId)
	{
		var request = new GetDeadLetterMessages(
			deadLetterTableName: "OUTBOX_DEAD_LETTERS",
			maxRetries: 0,
			olderThan: null,
			batchSize: 50,
			offset: 0,
			sqlTimeOutSeconds: 30,
			cancellationToken: Ct);

		await using var connection = new OracleConnection(ConnectionString);
		await connection.OpenAsync(Ct);
		var records = await request.ResolveAsync(connection);

		return records.ShouldHaveSingleItem(
			$"the read-back must return the dead-lettered message '{messageId}'; an empty result would make "
			+ "every assertion below vacuously true");
	}

	private async Task RequireDockerAndFreshSchemaAsync()
	{
		await _fixture.EnsureInitializedAsync();

		_fixture.DockerAvailable.ShouldBeTrue(
			"this lock asserts a property of the SHIPPED SCHEMA and SQL and of Oracle's own dialect rules, "
			+ "and is deliberately never skipped — a green run that never reached a database would certify "
			+ "nothing.");

		await ShippedOracleOutboxSchema.CreateFreshAsync(ConnectionString, Ct);
	}

	private Task<string> StoredTenantOfAsync(string messageId) =>
		ScalarAsync<string>(
			$"SELECT TENANT_ID FROM OUTBOX_DEAD_LETTERS WHERE MESSAGE_ID = '{messageId}'");

	private async Task<bool> HasDeadLetterTenantColumnAsync() =>
		await ScalarAsync<decimal>(
			"SELECT COUNT(*) FROM USER_TAB_COLUMNS "
			+ "WHERE TABLE_NAME = 'OUTBOX_DEAD_LETTERS' AND COLUMN_NAME = 'TENANT_ID'") > 0m;

	private async Task<bool> IsDeadLetterTenantNullableAsync() =>
		await ScalarAsync<string>(
			"SELECT NULLABLE FROM USER_TAB_COLUMNS "
			+ "WHERE TABLE_NAME = 'OUTBOX_DEAD_LETTERS' AND COLUMN_NAME = 'TENANT_ID'") == "Y";

	private async Task<bool> UniqueKeyIncludesTenantAsync() =>
		await ScalarAsync<decimal>(
			"SELECT COUNT(*) FROM USER_CONS_COLUMNS "
			+ "WHERE TABLE_NAME = 'OUTBOX_DEAD_LETTERS' "
			+ "  AND CONSTRAINT_NAME = 'UQ_OUTBOX_DLQ_MESSAGE_ID' AND COLUMN_NAME = 'TENANT_ID'") > 0m;

	private async Task ExecuteAsync(string sql)
	{
		await using var connection = new OracleConnection(ConnectionString);
		await connection.OpenAsync(Ct);
		_ = await connection.ExecuteAsync(sql);
	}

	private async Task<T> ScalarAsync<T>(string sql)
	{
		await using var connection = new OracleConnection(ConnectionString);
		await connection.OpenAsync(Ct);
		return await connection.ExecuteScalarAsync<T>(sql);
	}
}
