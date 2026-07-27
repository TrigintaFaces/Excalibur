// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Inbox;
using Excalibur.Inbox.Postgres;
using Excalibur.Inbox.SqlServer;

using Microsoft.Data.SqlClient;

using Npgsql;

#pragma warning disable CA2100 // SQL strings use compile-time-const schema/table names in a test fixture.

namespace Excalibur.Integration.Tests.Data.Inbox;

// bd-l9c3cv (S887) — independent (author≠impl, TestsDeveloper) NON-SKIPPED real-infra regression locks proving
// the ambient-tenant row-scoping of the SQL Server + Postgres inbox stores end-to-end. The store adopted the
// canonical TenantScope.FromContext(_tenantContext) pattern: the write stamps the AMBIENT tenant (never the
// row's own value) and every keyed read/claim/mark emits the tenant predicate ONLY from the ambient scope, so a
// scoped store can never emit a predicate-less read while a tenant is active. These locks assert the EMITTED
// behaviour against the real database (a mocked client cannot reproduce the server-side MERGE/UPDATE match
// semantics — see verify-against-real-infra-not-mock; the cross-tenant leak class is rw2ull/S886).
//
// CRITICAL — real DI, NOT a hand-injected ITenantContext (the S873 vacuity trap): the existing
// SqlServerInboxStoreTenantIsolationShould hand-injects a FixedTenantContext fake straight into the ctor, which
// only proves "the store works when handed a context." These locks instead build a real ServiceProvider through
// the PRODUCTION registration path (AddSqlServerInboxStore / AddExcaliburInbox().UsePostgres()) + AddTenantContext(),
// which Replace()s the fail-closed single-tenant default with the production ambient AmbientTenantContext.
// GetRequiredService resolves the store with DI supplying that context, and the ambient tenant is switched between
// writes and reads via the production TenantContextHolder.BeginScope — exactly how the tenant-resolver middleware
// sets it in production. Nothing is hand-injected.
//
// safety∧liveness (testing-patterns §3):
//   SAFETY  — with ambient tenant A's entry present, tenant B's scoped read/mark sees & mutates NOTHING of A's row
//             (as if A's entry does not exist to B), and B's dedup CAS wins independently (dedup is PER-TENANT).
//   LIVENESS — switch the ambient tenant back to A and A's scoped read STILL returns A's own entry (the store
//             isolates; it does not hide everything), and within-tenant dedup still deduplicates.
//
// RED on the leak mutant that drops the ambient tenant predicate from the read (a predicate-less read while a
// tenant is active) and from the CAS ON/conflict-target: tenant B then sees / collides with tenant A's row — the
// cross-tenant data-leak this invariant forbids.

/// <summary>
/// Real-SQL-Server ambient-tenant isolation lock for <see cref="SqlServerInboxStore"/> (bd-l9c3cv, S887).
/// </summary>
[Collection(SqlServerInboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Database", "SqlServer")]
[Trait("Component", "Inbox")]
public sealed class SqlServerInboxStoreAmbientTenantLeakShould : IClassFixture<SqlServerInboxStoreContainerFixture>
{
	private const string SchemaName = "dbo";
	private const string TableName = "inbox_ambient_tenant_leak_ss";
	private const string TenantA = "tenant-A";
	private const string TenantB = "tenant-B";
	private const string HandlerType = "TestHandler";

	private readonly SqlServerInboxStoreContainerFixture _fixture;

	public SqlServerInboxStoreAmbientTenantLeakShould(SqlServerInboxStoreContainerFixture fixture) => _fixture = fixture;

	[Fact]
	public async Task Isolate_a_scoped_read_and_mark_from_another_tenant_then_still_serve_the_owner()
	{
		await EnsureTableAsync().ConfigureAwait(false);
		var store = CreateStore();
		const string messageId = "msg-read-isolation";

		// Ambient tenant A writes an entry — stamped with A (the ambient tenant), never a row-supplied value.
		using (TenantContextHolder.BeginScope(TenantA))
		{
			_ = await store.CreateEntryAsync(
				messageId, HandlerType, "TestMessage", [1, 2, 3],
				new Dictionary<string, object>(), CancellationToken.None).ConfigureAwait(false);
		}

		// SAFETY — switch the ambient tenant to B. B's scoped reads/mark must behave as if A's entry does not exist.
		using (TenantContextHolder.BeginScope(TenantB))
		{
			(await store.GetEntryAsync(messageId, HandlerType, CancellationToken.None).ConfigureAwait(false))
				.ShouldBeNull("tenant B's scoped read must NOT see tenant A's entry (predicate-less-read leak guard)");

			(await store.IsProcessedAsync(messageId, HandlerType, CancellationToken.None).ConfigureAwait(false))
				.ShouldBeFalse("tenant B must not observe tenant A's entry as processed");

			// B's mark is scoped to B, so it matches 0 rows and throws — it must never mutate A's row.
			_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
				await store.MarkProcessedAsync(messageId, HandlerType, CancellationToken.None).ConfigureAwait(false))
				.ConfigureAwait(false);
		}

		// LIVENESS — switch back to A. A still sees its OWN entry, unmutated by B's mark (status still non-Processed).
		using (TenantContextHolder.BeginScope(TenantA))
		{
			var ownEntry = await store.GetEntryAsync(messageId, HandlerType, CancellationToken.None).ConfigureAwait(false);
			ownEntry.ShouldNotBeNull("tenant A's scoped read MUST return its own entry — isolation must not hide everything");
			ownEntry.TenantId.ShouldBe(TenantA);
			ownEntry.Status.ShouldNotBe(InboxStatus.Processed, "tenant B's cross-tenant mark must not have processed tenant A's entry");
		}
	}

	[Fact]
	public async Task Deduplicate_the_atomic_claim_per_tenant_not_globally()
	{
		await EnsureTableAsync().ConfigureAwait(false);
		var store = CreateStore();
		const string messageId = "msg-shared-cas";

		// Ambient A is the first writer for its own (MessageId, HandlerType, TenantId) row.
		using (TenantContextHolder.BeginScope(TenantA))
		{
			(await store.TryMarkAsProcessedAsync(messageId, HandlerType, CancellationToken.None).ConfigureAwait(false))
				.ShouldBeTrue("tenant A is the first writer for its own tenant-keyed row");
		}

		// SAFETY — ambient B claims the SAME MessageId+HandlerType and must ALSO win exactly once. The CAS is keyed
		// on the ambient tenant, so tenant A's row must not shadow tenant B (RED on the drop-TenantId-from-ON mutant).
		using (TenantContextHolder.BeginScope(TenantB))
		{
			(await store.TryMarkAsProcessedAsync(messageId, HandlerType, CancellationToken.None).ConfigureAwait(false))
				.ShouldBeTrue("tenant B must independently win the CAS — dedup is PER-TENANT, not global (cross-tenant dedup collision guard)");

			// LIVENESS — the SECOND claim within tenant B must still deduplicate (isolation must not disable dedup).
			(await store.TryMarkAsProcessedAsync(messageId, HandlerType, CancellationToken.None).ConfigureAwait(false))
				.ShouldBeFalse("a second claim within the SAME tenant must be deduplicated (exactly-once per tenant)");
		}
	}

	private SqlServerInboxStore CreateStore()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"SQL Server container must be available — real-infra ambient-tenant leak lock is never skipped.");

		var services = new ServiceCollection();
		_ = services.AddLogging();
		// PRODUCTION registration path — DI owns store construction and supplies the tenant context.
		_ = services.AddSqlServerInboxStore(o =>
		{
			o.ConnectionString = _fixture.ConnectionString;
			o.SchemaName = SchemaName;
			o.TableName = TableName;
		});
		// PRODUCTION multi-tenancy composition — Replace()s the fail-closed single-tenant default with the ambient
		// AmbientTenantContext that reads TenantContextHolder.Current. Real DI, not a hand-injected fake (S873 trap).
		_ = services.AddTenantContext();
		// Design A: multi-tenant mode is RequireTenant (AddMultiTenancy sets it in production); set it so the
		// DI-resolved store runs the multi-tenant path against the triple schema.
		_ = services.Configure<TenantContextOptions>(o => o.RequireTenant = true);

		var provider = services.BuildServiceProvider();
		return provider.GetRequiredService<SqlServerInboxStore>();
	}

	private async Task EnsureTableAsync()
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		// Isolated table whose PK is the FULL composite (MessageId, HandlerType, TenantId) — so two tenants' identical
		// message ids are distinct rows and the ambient-tenant dimension is what is under test. Fresh per test.
		var sql = $"""
			IF OBJECT_ID('[{SchemaName}].[{TableName}]', 'U') IS NOT NULL DROP TABLE [{SchemaName}].[{TableName}];
			CREATE TABLE [{SchemaName}].[{TableName}] (
				[MessageId]     NVARCHAR(255)  NOT NULL,
				[HandlerType]   NVARCHAR(500)  NOT NULL,
				[MessageType]   NVARCHAR(500)  NOT NULL,
				[Payload]       VARBINARY(MAX) NOT NULL,
				[Metadata]      NVARCHAR(MAX)  NULL,
				[ReceivedAt]    DATETIMEOFFSET NOT NULL,
				[ProcessedAt]   DATETIMEOFFSET NULL,
				[Status]        INT            NOT NULL DEFAULT 0,
				[LastError]     NVARCHAR(MAX)  NULL,
				[RetryCount]    INT            NOT NULL DEFAULT 0,
				[LastAttemptAt] DATETIMEOFFSET NULL,
				[NextAttemptAt] DATETIMEOFFSET NULL,
				[CorrelationId] NVARCHAR(255)  NULL,
				[TenantId]      NVARCHAR(255)  NOT NULL,
				[Source]        NVARCHAR(255)  NULL,
				CONSTRAINT [PK_{TableName}] PRIMARY KEY CLUSTERED ([MessageId], [HandlerType], [TenantId])
			);
			""";

		await using var command = new SqlCommand(sql, connection);
		_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
	}
}

/// <summary>
/// Real-Postgres ambient-tenant isolation lock for <see cref="PostgresInboxStore"/> (bd-l9c3cv, S887).
/// </summary>
[Collection(PostgresInboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Database", "Postgres")]
[Trait("Component", "Inbox")]
public sealed class PostgresInboxStoreAmbientTenantLeakShould : IClassFixture<PostgresInboxStoreContainerFixture>
{
	private const string SchemaName = "public";
	private const string TableName = "inbox_ambient_tenant_leak_pg";
	private const string TenantA = "tenant-A";
	private const string TenantB = "tenant-B";
	private const string HandlerType = "TestHandler";

	private readonly PostgresInboxStoreContainerFixture _fixture;

	public PostgresInboxStoreAmbientTenantLeakShould(PostgresInboxStoreContainerFixture fixture) => _fixture = fixture;

	[Fact]
	public async Task Isolate_a_scoped_read_and_mark_from_another_tenant_then_still_serve_the_owner()
	{
		await EnsureTableAsync().ConfigureAwait(false);
		var store = CreateStore();
		const string messageId = "msg-read-isolation";

		using (TenantContextHolder.BeginScope(TenantA))
		{
			_ = await store.CreateEntryAsync(
				messageId, HandlerType, "TestMessage", [1, 2, 3],
				new Dictionary<string, object>(), CancellationToken.None).ConfigureAwait(false);
		}

		// SAFETY — tenant B's scoped reads/mark must behave as if tenant A's entry does not exist.
		using (TenantContextHolder.BeginScope(TenantB))
		{
			(await store.GetEntryAsync(messageId, HandlerType, CancellationToken.None).ConfigureAwait(false))
				.ShouldBeNull("tenant B's scoped read must NOT see tenant A's entry (predicate-less-read leak guard)");

			(await store.IsProcessedAsync(messageId, HandlerType, CancellationToken.None).ConfigureAwait(false))
				.ShouldBeFalse("tenant B must not observe tenant A's entry as processed");

			_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
				await store.MarkProcessedAsync(messageId, HandlerType, CancellationToken.None).ConfigureAwait(false))
				.ConfigureAwait(false);
		}

		// LIVENESS — tenant A still sees its own entry, unmutated by tenant B's mark.
		using (TenantContextHolder.BeginScope(TenantA))
		{
			var ownEntry = await store.GetEntryAsync(messageId, HandlerType, CancellationToken.None).ConfigureAwait(false);
			ownEntry.ShouldNotBeNull("tenant A's scoped read MUST return its own entry — isolation must not hide everything");
			ownEntry.TenantId.ShouldBe(TenantA);
			ownEntry.Status.ShouldNotBe(InboxStatus.Processed, "tenant B's cross-tenant mark must not have processed tenant A's entry");
		}
	}

	[Fact]
	public async Task Deduplicate_the_atomic_claim_per_tenant_not_globally()
	{
		await EnsureTableAsync().ConfigureAwait(false);
		var store = CreateStore();
		const string messageId = "msg-shared-cas";

		using (TenantContextHolder.BeginScope(TenantA))
		{
			(await store.TryMarkAsProcessedAsync(messageId, HandlerType, CancellationToken.None).ConfigureAwait(false))
				.ShouldBeTrue("tenant A is the first writer for its own tenant-keyed row");
		}

		// SAFETY — tenant B claims the SAME MessageId+HandlerType and must ALSO win exactly once.
		using (TenantContextHolder.BeginScope(TenantB))
		{
			(await store.TryMarkAsProcessedAsync(messageId, HandlerType, CancellationToken.None).ConfigureAwait(false))
				.ShouldBeTrue("tenant B must independently win the CAS — dedup is PER-TENANT, not global (cross-tenant dedup collision guard)");

			// LIVENESS — within-tenant dedup still deduplicates.
			(await store.TryMarkAsProcessedAsync(messageId, HandlerType, CancellationToken.None).ConfigureAwait(false))
				.ShouldBeFalse("a second claim within the SAME tenant must be deduplicated (exactly-once per tenant)");
		}
	}

	private PostgresInboxStore CreateStore()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Postgres container must be available — real-infra ambient-tenant leak lock is never skipped.");

		var services = new ServiceCollection();
		_ = services.AddLogging();
		// PRODUCTION registration path — DI owns store construction and supplies the tenant context.
		_ = services.AddExcaliburInbox(inbox =>
			inbox.UsePostgres(pg => pg
				.ConnectionString(_fixture.ConnectionString)
				.SchemaName(SchemaName)
				.TableName(TableName)));
		// PRODUCTION multi-tenancy composition — real ambient context (not a hand-injected fake; S873 trap).
		_ = services.AddTenantContext();
		// Design A: multi-tenant mode is RequireTenant (AddMultiTenancy sets it in production); set it so the
		// DI-resolved store runs the multi-tenant path against the triple schema.
		_ = services.Configure<TenantContextOptions>(o => o.RequireTenant = true);

		var provider = services.BuildServiceProvider();
		return provider.GetRequiredService<PostgresInboxStore>();
	}

	private async Task EnsureTableAsync()
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		// Isolated table; PK is the FULL composite (message_id, handler_type, tenant_id). Fresh per test.
		var sql = $"""
			DROP TABLE IF EXISTS "{SchemaName}"."{TableName}";
			CREATE TABLE "{SchemaName}"."{TableName}" (
				message_id      VARCHAR(255)  NOT NULL,
				handler_type    VARCHAR(500)  NOT NULL,
				message_type    VARCHAR(500)  NOT NULL,
				payload         BYTEA         NOT NULL,
				metadata        JSONB         NULL,
				received_at     TIMESTAMPTZ   NOT NULL,
				processed_at    TIMESTAMPTZ   NULL,
				status          INT           NOT NULL DEFAULT 0,
				last_error      TEXT          NULL,
				retry_count     INT           NOT NULL DEFAULT 0,
				last_attempt_at TIMESTAMPTZ   NULL,
				lease_expires_at TIMESTAMPTZ  NULL,
				correlation_id  VARCHAR(255)  NULL,
				tenant_id       VARCHAR(255)  NOT NULL,
				source          VARCHAR(255)  NULL,
				CONSTRAINT pk_{TableName} PRIMARY KEY (message_id, handler_type, tenant_id)
			);
			""";

		await using var command = new NpgsqlCommand(sql, connection);
		_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
	}
}
