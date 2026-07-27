// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Inbox.Postgres;

using Microsoft.Extensions.Logging.Abstractions;

using Npgsql;

#pragma warning disable CA2100 // SQL strings use compile-time-const schema/table names in a test fixture.

namespace Excalibur.Integration.Tests.Data.Inbox;

// bd-bh40cy-sibling (S887 REVIEW_CODE BLOCKING 2) — independent (author != implementer, TestsDeveloper) NON-SKIPPED
// real-Postgres regression lock for the NON-MULTI-TENANT dedup/claim path. This is the gap the reviewer named: no
// test covered a Postgres inbox store constructed WITHOUT an ITenantContext (single-tenant / non-MT deployment).
//
// THE DEFECT (committed HEAD 31d016950, PostgresInboxStore.cs:238-240, 350-352, 402-404). The INSERT tenant column
// and the ON CONFLICT target were emitted UNCONDITIONALLY:
//     insertTenantCol = ", tenant_id";
//     conflictTarget  = "(message_id, handler_type, tenant_id)";
// A non-MT store (no ITenantContext → TenantScope.FromContext(null) → None → scope.TenantId == null) then ran
//     INSERT ... (message_id, handler_type, ..., tenant_id) VALUES (..., NULL)
//     ON CONFLICT (message_id, handler_type, tenant_id) DO NOTHING
// against the canonical NON-MT pair-key schema (PK (message_id, handler_type), no unique index covering
// (message_id, handler_type, tenant_id)). Postgres rejects the statement with SQLSTATE 42P10
// ("there is no unique or exclusion constraint matching the ON CONFLICT specification") on EVERY
// TryMarkAsProcessed / TryClaim / TryProcessTransactionally — the non-MT dedup path is simply broken. (The sibling
// silent variant — a triple index with a NULLABLE tenant_id run non-MT, where pre-PG15 NULLs are distinct so
// ON CONFLICT never fires and duplicates slip through — is avoided by the same fix: degrade to the pair target.)
//
// THE FIX (working tree). The three fragments become conditional on scope.IsScoped, degrading to
//     conflictTarget = "(message_id, handler_type)"   (no tenant column in the INSERT)
// when the store is non-MT — so the statement resolves against the pair-key schema and dedup works.
//
// SEAM / non-vacuity. The lock binds the OBSERVABLE property (a non-MT claim/dedup op succeeds and deduplicates
// against a pair-key table), NOT a mechanism — per pin-interface-seam + verify-against-real-infra-not-mock. A
// mocked connection cannot reproduce the server-side ON CONFLICT / 42P10 semantics, so this MUST run real Postgres.
//   RED  on committed HEAD 31d016950 — the unconditional triple ON CONFLICT target raises 42P10, the ShouldBeTrue
//        assertion never lands (the op throws first). This is the arm the non-MT break fails.
//   GREEN on the working-tree fix — the pair target resolves, first-writer wins (true), a redelivery deduplicates
//        (false). The dedup-still-fires assertions are the testing-patterns §3 liveness partner: they stop the
//        safety arm passing via an impl that simply never conflicts (e.g. inserts duplicate rows).
//
// The MULTI-TENANT per-tenant isolation of the same CAS is covered separately by PostgresInboxStoreAmbientTenantLeak
// Should (bd-l9c3cv), which builds its own isolated triple-key table — this lock owns the non-MT dimension only.

/// <summary>
/// Real-Postgres non-multi-tenant conflict-target regression lock for <see cref="PostgresInboxStore"/>
/// (S887 REVIEW_CODE BLOCKING 2).
/// </summary>
[Collection(PostgresInboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Database", "Postgres")]
[Trait("Component", "Inbox")]
public sealed class PostgresInboxStoreNonMultiTenantConflictTargetShould
	: IClassFixture<PostgresInboxStoreContainerFixture>
{
	private const string SchemaName = "public";
	private const string TableName = "inbox_nonmt_conflict_target_pg";
	private const string HandlerType = "TestHandler";

	private readonly PostgresInboxStoreContainerFixture _fixture;

	public PostgresInboxStoreNonMultiTenantConflictTargetShould(PostgresInboxStoreContainerFixture fixture) =>
		_fixture = fixture;

	[Fact]
	public async Task TryMarkAsProcessed_nonMultiTenant_wins_once_then_deduplicates_without_42P10()
	{
		await EnsureTableAsync().ConfigureAwait(false);
		var store = CreateNonMultiTenantStore();
		const string messageId = "msg-mark";

		// SAFETY (the regression arm). On committed HEAD the unconditional ON CONFLICT (…, tenant_id) target raises
		// 42P10 against the pair-key table and this throws before returning — RED. GREEN once the target degrades to
		// (message_id, handler_type) for the non-MT scope.
		(await store.TryMarkAsProcessedAsync(messageId, HandlerType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeTrue(
				"a non-MT inbox (no ITenantContext) must be the first writer and must NOT raise 42P10 — the ON " +
				"CONFLICT target must degrade to (message_id, handler_type) when scope is None, matching the pair-key " +
				"schema. A thrown 42P10 here is the non-MT break this lock guards.");

		// LIVENESS (dedup still fires). A redelivery of the SAME (message_id, handler_type) must deduplicate — the fix
		// must not "avoid 42P10" by inserting a second row / never conflicting. This is the non-vacuity partner.
		(await store.TryMarkAsProcessedAsync(messageId, HandlerType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeFalse(
				"a second non-MT claim of the same (message_id, handler_type) must be deduplicated (exactly-once) — " +
				"the pair-key ON CONFLICT must still fire, not silently insert a duplicate.");
	}

	[Fact]
	public async Task TryClaim_nonMultiTenant_claims_once_then_deduplicates_without_42P10()
	{
		await EnsureTableAsync().ConfigureAwait(false);
		var store = CreateNonMultiTenantStore();
		const string messageId = "msg-claim";

		(await store.TryClaimAsync(messageId, HandlerType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeTrue(
				"a non-MT claim must acquire the row and must NOT raise 42P10 — the claim path's ON CONFLICT target " +
				"must degrade to the pair key when scope is None.");

		(await store.TryClaimAsync(messageId, HandlerType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeFalse(
				"a second non-MT claim of the same key must fail (already claimed) — the pair-key ON CONFLICT must fire.");
	}

	[Fact]
	public async Task TryProcessTransactionally_nonMultiTenant_processes_once_then_deduplicates_without_42P10()
	{
		await EnsureTableAsync().ConfigureAwait(false);
		var store = CreateNonMultiTenantStore();
		const string messageId = "msg-txn";

		Func<IDbTransaction, CancellationToken, ValueTask> noopHandler = (_, _) => ValueTask.CompletedTask;

		(await store.TryProcessTransactionallyAsync(messageId, HandlerType, noopHandler, CancellationToken.None)
			.ConfigureAwait(false))
			.ShouldBeTrue(
				"a non-MT transactional process must claim + run the handler + mark processed and must NOT raise 42P10 " +
				"— the first-writer INSERT's ON CONFLICT target must degrade to the pair key when scope is None.");

		(await store.TryProcessTransactionallyAsync(messageId, HandlerType, noopHandler, CancellationToken.None)
			.ConfigureAwait(false))
			.ShouldBeFalse(
				"a redelivery must be recognised as already-processed (handler NOT re-invoked) — exactly-once must hold " +
				"on the non-MT pair-key path.");
	}

	// Non-MT store: NO ITenantContext (tenantContext defaults to null) → TenantScope.FromContext(null) → None → the
	// store must emit the pair-key conflict target. Direct connection-factory ctor against the real container — the
	// faithful shape of a single-tenant host that never called AddTenantContext().
	private PostgresInboxStore CreateNonMultiTenantStore()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Postgres container must be available — the non-MT conflict-target lock is never skipped.");

		var options = new PostgresInboxOptions
		{
			SchemaName = SchemaName,
			TableName = TableName,
		};

		return new PostgresInboxStore(
			connectionFactory: _fixture.CreateConnection,
			options: options,
			logger: NullLogger<PostgresInboxStore>.Instance);
	}

	// Canonical NON-MT schema: the composite PRIMARY KEY is the PAIR (message_id, handler_type) — there is NO unique
	// index covering tenant_id, so a triple ON CONFLICT target cannot resolve here (that is exactly the 42P10 the
	// committed impl trips). Columns mirror the store's Insert/Update/Select references. Fresh per test.
	private async Task EnsureTableAsync()
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		var sql = $"""
			DROP TABLE IF EXISTS "{SchemaName}"."{TableName}";
			CREATE TABLE "{SchemaName}"."{TableName}" (
				message_id       VARCHAR(255)  NOT NULL,
				handler_type     VARCHAR(500)  NOT NULL,
				message_type     VARCHAR(500)  NOT NULL,
				payload          BYTEA         NOT NULL,
				metadata         JSONB         NULL,
				received_at      TIMESTAMPTZ   NOT NULL,
				processed_at     TIMESTAMPTZ   NULL,
				status           INT           NOT NULL DEFAULT 0,
				last_error       TEXT          NULL,
				retry_count      INT           NOT NULL DEFAULT 0,
				last_attempt_at  TIMESTAMPTZ   NULL,
				lease_expires_at TIMESTAMPTZ   NULL,
				correlation_id   VARCHAR(255)  NULL,
				source           VARCHAR(255)  NULL,
				CONSTRAINT pk_{TableName} PRIMARY KEY (message_id, handler_type)
			);
			""";

		await using var command = new NpgsqlCommand(sql, connection);
		_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
	}
}
