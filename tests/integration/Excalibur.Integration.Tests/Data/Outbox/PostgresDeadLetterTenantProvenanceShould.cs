// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Dapper;

using Excalibur.Data;
using Excalibur.Outbox.Postgres;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Locks the TENANT PROVENANCE of a dead-lettered message on Postgres, against a real database, for the
/// shipped schema and both move paths.
/// </summary>
/// <remarks>
/// <para>
/// The property under test is that a message which stops being deliverable still records which tenant
/// produced it. This matters more here than anywhere else in the outbox, because the move DELETEs the
/// outbox row: the dead-letter row is the ONLY surviving record of the message, so a column the move does
/// not copy is destroyed rather than merely unqueryable. Without it an operator cannot attribute a dead
/// letter, and a redrive cannot return the message to the partition it came from — on precisely the path
/// someone reaches for when something has already gone wrong.
/// </para>
/// <para>
/// It runs against a real database because the property is decided by the SCHEMA and the SQL together, not
/// by any C# type: whether the column exists, whether the INSERT…SELECT carries it, and whether the
/// read-back projects it are answered by the server and by nothing else. It provisions from the DDL the
/// package SHIPS and drives the request types the store
/// actually uses, rather than hand-written SQL — a hand-written INSERT here would pass regardless of what
/// the store does.
/// </para>
/// <para>
/// Both arms are present deliberately. "An untenanted message stores the reserved key" is satisfied by a
/// move that stamps EVERY entry with that key, which would destroy tenant identity completely while
/// looking perfectly correct; the arm that a real tenant survives verbatim is what makes that
/// inexpressible.
/// </para>
/// <para>
/// There is no upgrade path to lock. The package ships one CREATE script per provider, already at the
/// final shape, so the only database these arms can meaningfully describe is the one a consumer actually
/// provisions. Arms that asserted about converting a legacy database were removed with the migration
/// scripts rather than left asserting against a path that no longer exists.
/// </para>
/// <para>
/// NOT skip-gated. A Docker-unavailable run FAILS rather than passing vacuously — a provenance lock that
/// goes green by never executing is the failure mode it exists to prevent.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Database", "Postgres")]
[Trait("Component", "Core")]
public sealed class PostgresDeadLetterTenantProvenanceShould(PostgresOutboxStoreContainerFixture fixture)
	: IClassFixture<PostgresOutboxStoreContainerFixture>
{
	private const string Sentinel = "__untenanted__";

	private readonly PostgresOutboxStoreContainerFixture _fixture = fixture;

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private string ConnectionString => _fixture.ConnectionString;

	/// <summary>
	/// THE HEADLINE ARM: a message dead-letters, and the tenant survives both the move and the read-back.
	/// </summary>
	/// <remarks>
	/// Drives the shipped request types end to end — stage, move, read back — because every one of the
	/// three is a place the tenant can be dropped, and dropping it at any of them produces the same
	/// symptom: an entry nobody can attribute.
	/// </remarks>
	[Fact]
	public async Task CarryARealTenantThroughTheMoveAndTheReadBack()
	{
		RequireDocker();
		await ShippedPostgresOutboxSchema.CreateFreshAsync(ConnectionString, Ct);

		await StageAsync("m-tenanted", tenantId: "acme");
		await MoveToDeadLetterAsync("m-tenanted");

		(await StoredTenantOfAsync("m-tenanted")).ShouldBe(
			"acme",
			"the move must copy the originating tenant: it deletes the outbox row, so a tenant it does not "
			+ "carry across is destroyed rather than merely unqueryable");

		var record = await ReadBackAsync("m-tenanted");

		record.TenantId.ShouldBe(
			"acme",
			"the operator-facing read must project the tenant, or an operator still cannot attribute the "
			+ "entry even though the database holds it");
	}

	/// <summary>
	/// THE OTHER ARM: an untenanted message lands on the reserved key, not on NULL and not on a blank.
	/// </summary>
	/// <remarks>
	/// Paired with the arm above deliberately. A move that stamped EVERY entry with the reserved key would
	/// satisfy this arm perfectly while destroying tenant identity, and nothing else here would notice.
	/// </remarks>
	[Fact]
	public async Task StoreTheReservedKeyForAnUntenantedMessageRatherThanNullOrBlank()
	{
		RequireDocker();
		await ShippedPostgresOutboxSchema.CreateFreshAsync(ConnectionString, Ct);

		await StageAsync("m-untenanted", tenantId: null);
		await MoveToDeadLetterAsync("m-untenanted");

		(await StoredTenantOfAsync("m-untenanted")).ShouldBe(
			Sentinel,
			"an untenanted message must be recorded as the untenanted PARTITION, not as an absent value — "
			+ "there is exactly one way to say a message has no tenant");

		(await ReadBackAsync("m-untenanted")).TenantId.ShouldBe(Sentinel);
	}

	/// <summary>
	/// THE SECOND MOVE PATH: terminating a message through the store carries the tenant too.
	/// </summary>
	/// <remarks>
	/// There are two ways a message reaches this table — the retry-exhaustion move and the store's terminal
	/// dead-letter transition — and they are separate SQL statements in separate request types. Covering
	/// only the first would leave a path that silently drops the tenant behind a green suite.
	/// </remarks>
	[Fact]
	public async Task CarryTheTenantOnTheTerminalDeadLetterTransitionToo()
	{
		RequireDocker();
		await ShippedPostgresOutboxSchema.CreateFreshAsync(ConnectionString, Ct);

		await StageAsync("m-terminal", tenantId: "contoso");

		using var store = CreateStore();
		await store.MarkDeadLetteredAsync("m-terminal", "poison message", Ct);

		(await StoredTenantOfAsync("m-terminal")).ShouldBe(
			"contoso",
			"the terminal transition is a second write path into the same table and must carry the tenant "
			+ "for the same reason the retry-exhaustion move does");
	}

	/// <summary>
	/// THE STRUCTURAL ARM: the fresh-install column refuses NULL rather than merely looking closed.
	/// </summary>
	[Fact]
	public async Task RejectAnExplicitNullTenantOnTheDeadLetterTable()
	{
		RequireDocker();
		await ShippedPostgresOutboxSchema.CreateFreshAsync(ConnectionString, Ct);

		var write = async () => await ExecuteAsync(
			"""
			INSERT INTO public.outbox_dead_letters
			    (message_id, tenant_id, message_type, message_body, occurred_on, attempts)
			VALUES ('dl-null', NULL, 'T', '\x01'::bytea, NOW(), 1);
			""");

		_ = await write.ShouldThrowAsync<PostgresException>(
			"a provenance column that can hold NULL is one that can silently lose the provenance");
	}

	/// <summary>
	/// THE KEY ARM: the shipped dead-letter key carries the tenant term, not the message id alone.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Read from the LIVE CATALOGUE rather than from the script text. Parsing the shipped .sql would test
	/// our own file against itself and would stay green against a server that never applied the
	/// constraint — the database is the only thing that can answer what the key actually is.
	/// </para>
	/// <para>
	/// Both columns are asserted, and the message-id half is the liveness arm. "tenant_id is in this set"
	/// is satisfied vacuously by an EMPTY set, which is exactly what a renamed table, a wrong constraint
	/// type, or a catalogue query that matched nothing produces. Asserting the id too makes an empty
	/// result RED instead of green.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task CarryTheTenantTermInTheShippedDeadLetterKey()
	{
		RequireDocker();
		await ShippedPostgresOutboxSchema.CreateFreshAsync(ConnectionString, Ct);

		var keyColumns = await PrimaryKeyColumnsAsync();

		keyColumns.ShouldContain(
			"message_id",
			"the dead-letter key must address a message at all — an empty column set here would make the "
			+ "tenant assertion below vacuously true");

		keyColumns.ShouldContain(
			"tenant_id",
			"without the tenant term in the key, two tenants' dead letters carrying the same message id "
			+ "collide on ONE row: the second write overwrites the first, and the only surviving record of "
			+ "that message — the row an operator attributes and a redrive reads — is destroyed");
	}

	private PostgresOutboxStore CreateStore()
	{
		var db = A.Fake<IDb>();
		_ = A.CallTo(() => db.Connection).Returns(new NpgsqlConnection(ConnectionString));

		return new PostgresOutboxStore(
			db,
			Options.Create(new PostgresOutboxStoreOptions
			{
				SchemaName = "public",
				OutboxTableName = "outbox",
				DeadLetterTableName = "outbox_dead_letters",
			}),
			NullLogger<PostgresOutboxStore>.Instance);
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
			outboxTableName: "public.outbox",
			sqlTimeOutSeconds: 30,
			cancellationToken: Ct);

		await using var connection = new NpgsqlConnection(ConnectionString);
		await connection.OpenAsync(Ct);
		_ = await request.ResolveAsync(connection);
	}

	private async Task MoveToDeadLetterAsync(string messageId)
	{
		var request = new MoveOutboxMessageToDeadLetter(
			messageId: messageId,
			outboxTableName: "public.outbox",
			deadLetterTableName: "public.outbox_dead_letters",
			sqlTimeOutSeconds: 30,
			cancellationToken: Ct);

		await using var connection = new NpgsqlConnection(ConnectionString);
		await connection.OpenAsync(Ct);
		_ = await request.ResolveAsync(connection);
	}

	private async Task<DeadLetterRecord> ReadBackAsync(string messageId)
	{
		var request = new GetDeadLetterMessages(
			deadLetterTableName: "public.outbox_dead_letters",
			maxRetries: 0,
			olderThan: null,
			batchSize: 50,
			offset: 0,
			sqlTimeOutSeconds: 30,
			cancellationToken: Ct);

		await using var connection = new NpgsqlConnection(ConnectionString);
		await connection.OpenAsync(Ct);
		var records = await request.ResolveAsync(connection);

		return records.ShouldHaveSingleItem(
			$"the read-back must return the dead-lettered message '{messageId}'; an empty result would make "
			+ "every assertion below vacuously true");
	}

	private void RequireDocker() =>
		_fixture.DockerAvailable.ShouldBeTrue(
			"this lock asserts a property of the SHIPPED SCHEMA and SQL and is deliberately never skipped — "
			+ "a green run that never reached a database would certify nothing.");

	private Task<string?> StoredTenantOfAsync(string messageId) =>
		ScalarAsync<string>(
			$"SELECT tenant_id FROM public.outbox_dead_letters WHERE message_id = '{messageId}';");

	/// <summary>
	/// Reads the dead-letter table's PRIMARY KEY column set from the live catalogue.
	/// </summary>
	/// <remarks>
	/// Returns the COMPOSITION rather than a boolean, so the caller can assert both the presence of the
	/// tenant term and the presence of the message id. A boolean "contains tenant" helper cannot
	/// distinguish a key that is missing the term from a query that matched no key at all.
	/// </remarks>
	private async Task<IReadOnlyList<string>> PrimaryKeyColumnsAsync()
	{
		await using var connection = new NpgsqlConnection(ConnectionString);
		await connection.OpenAsync(Ct);

		var columns = await connection.QueryAsync<string>(
			"""
			SELECT a.attname
			  FROM pg_constraint c
			  JOIN unnest(c.conkey) AS k(attnum) ON TRUE
			  JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = k.attnum
			 WHERE c.conrelid = 'public.outbox_dead_letters'::regclass
			   AND c.contype = 'p';
			""");

		return columns.ToList();
	}

	private async Task ExecuteAsync(string sql)
	{
		await using var connection = new NpgsqlConnection(ConnectionString);
		await connection.OpenAsync(Ct);
		_ = await connection.ExecuteAsync(sql);
	}

	private async Task<T?> ScalarAsync<T>(string sql)
	{
		await using var connection = new NpgsqlConnection(ConnectionString);
		await connection.OpenAsync(Ct);
		return await connection.ExecuteScalarAsync<T>(sql);
	}
}
