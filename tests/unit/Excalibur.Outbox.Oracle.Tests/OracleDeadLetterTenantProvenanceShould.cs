// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Dapper;

using Oracle.ManagedDataAccess.Client;

using Shouldly;

using Xunit;

namespace Excalibur.Outbox.Oracle.Tests;

/// <summary>
/// Locks the TENANT PROVENANCE of a dead-lettered message on Oracle, against a real database, for the
/// shipped schema and both move paths.
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
/// Oracle treats NULLs as DISTINCT in a unique index, which is why the shipped column is closed and the
/// key carries the tenant. That is server-checked and nothing else.
/// </description></item>
/// </list>
/// <para>
/// There is no upgrade path to lock. The package ships one CREATE script per provider, already at the
/// final shape, so the only database these arms can meaningfully describe is the one a consumer actually
/// provisions. Arms that asserted about converting a legacy database were removed with the migration
/// scripts rather than left asserting against a path that no longer exists.
/// </para>
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
[Collection(OracleOutboxCollection.Name)]
public sealed class OracleDeadLetterTenantProvenanceShould(OracleOutboxStoreContainerFixture fixture)
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
	/// THE KEY ARM: the shipped dead-letter unique key carries the tenant term, not the message id alone.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Read from the LIVE CATALOGUE rather than from the script text, and that matters more on this engine
	/// than on Postgres: the shipped script creates this table from inside a PL/SQL block via
	/// <c>EXECUTE IMMEDIATE</c>, so the constraint is assembled at run time and the file says only what we
	/// intended, never what the server ended up with.
	/// </para>
	/// <para>
	/// Both columns are asserted, and the message-id half is the liveness arm. "TENANT_ID is in this set"
	/// is satisfied vacuously by an EMPTY set, which is exactly what a renamed table or a constraint of a
	/// different type produces. Asserting the id too makes an empty result RED instead of green.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task CarryTheTenantTermInTheShippedDeadLetterKey()
	{
		await RequireDockerAndFreshSchemaAsync();

		var keyColumns = await UniqueKeyColumnsAsync();

		keyColumns.ShouldContain(
			"MESSAGE_ID",
			"the dead-letter key must address a message at all — an empty column set here would make the "
			+ "tenant assertion below vacuously true");

		keyColumns.ShouldContain(
			"TENANT_ID",
			"without the tenant term in the key, two tenants' dead letters carrying the same message id "
			+ "collide on ONE row: the second write overwrites the first, and the only surviving record of "
			+ "that message — the row an operator attributes and a redrive reads — is destroyed");
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

	private Task<string?> StoredTenantOfAsync(string messageId) =>
		ScalarAsync<string>(
			$"SELECT TENANT_ID FROM OUTBOX_DEAD_LETTERS WHERE MESSAGE_ID = '{messageId}'");

	/// <summary>
	/// Reads the dead-letter table's UNIQUE key column set from the live catalogue.
	/// </summary>
	/// <remarks>
	/// Selected by constraint TYPE rather than by name, so renaming the constraint does not silently turn
	/// this into a query that matches nothing. Returns the COMPOSITION rather than a boolean, so the
	/// caller can tell a key that is missing the tenant term from a query that found no key at all.
	/// Oracle folds unquoted identifiers to upper case, which is why the caller asserts upper-case names.
	/// </remarks>
	private async Task<IReadOnlyList<string>> UniqueKeyColumnsAsync()
	{
		await using var connection = new OracleConnection(ConnectionString);
		await connection.OpenAsync(Ct);

		var columns = await connection.QueryAsync<string>(
			"SELECT ucc.COLUMN_NAME FROM USER_CONS_COLUMNS ucc "
			+ "JOIN USER_CONSTRAINTS uc ON uc.CONSTRAINT_NAME = ucc.CONSTRAINT_NAME "
			+ "WHERE ucc.TABLE_NAME = 'OUTBOX_DEAD_LETTERS' AND uc.CONSTRAINT_TYPE = 'U'");

		return columns.ToList();
	}

	private async Task ExecuteAsync(string sql)
	{
		await using var connection = new OracleConnection(ConnectionString);
		await connection.OpenAsync(Ct);
		_ = await connection.ExecuteAsync(sql);
	}

	private async Task<T?> ScalarAsync<T>(string sql)
	{
		await using var connection = new OracleConnection(ConnectionString);
		await connection.OpenAsync(Ct);
		return await connection.ExecuteScalarAsync<T>(sql);
	}
}
