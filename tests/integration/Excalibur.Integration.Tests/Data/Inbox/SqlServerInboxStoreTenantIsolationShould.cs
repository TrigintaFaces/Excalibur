// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Inbox.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

#pragma warning disable CA2100 // SQL strings use a compile-time const table name in a test fixture.

namespace Excalibur.Integration.Tests.Data.Inbox;

/// <summary>
/// bd-b1g4o0 — independent (author≠impl, TestsDeveloper) NON-SKIPPED real-SQL-Server tenant-isolation lock
/// for the row-discriminator dedup CAS. The atomic first-writer claim
/// (<c>TryMarkAsProcessedAsync(messageId, handlerType, ct)</c>) is a <c>MERGE … WITH (UPDLOCK, HOLDLOCK)</c> whose
/// composite key is <b>(MessageId, HandlerType, TenantId)</b>: the ambient <see cref="ITenantContext"/> is
/// woven into the MERGE source, the <c>ON</c> conflict-target, and the <c>INSERT</c>. Two different tenants
/// claiming the SAME <c>MessageId</c>+<c>HandlerType</c> MUST each win exactly once — dedup is per-tenant,
/// never global.
/// </summary>
/// <remarks>
/// <para>
/// This proves the tenant dimension of the CAS end-to-end against real SQL Server (the mocked/unit path
/// cannot reproduce the server-side MERGE match semantics — see <c>verify-against-real-infra-not-mock</c>).
/// It hand-rolls an isolated table so the shared conformance fixture is untouched; the isolated PK is the
/// full composite <c>(MessageId, HandlerType, TenantId)</c> so two tenants' identical message ids are
/// distinct rows.
/// </para>
/// <para>
/// <b>RED on the mutant</b> that drops <c>TenantId</c> from the CAS <c>ON</c> conflict-target (i.e. forces
/// <c>mergeOnTenant</c> to empty): tenant B's MERGE then MATCHES tenant A's already-Processed row on
/// <c>MessageId</c>+<c>HandlerType</c> alone → no-op UPDATE → <c>action != "INSERT"</c> → <c>false</c> —
/// a cross-tenant dedup collision (tenant B silently treated as a duplicate of tenant A).
/// </para>
/// </remarks>
[Collection(SqlServerInboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Database", "SqlServer")]
[Trait("Component", "Inbox")]
public sealed class SqlServerInboxStoreTenantIsolationShould : IClassFixture<SqlServerInboxStoreContainerFixture>
{
	private const string SchemaName = "dbo";
	private const string TableName = "inbox_tenant_isolation_test";
	private const string TenantA = "tenant-A";
	private const string TenantB = "tenant-B";

	private readonly SqlServerInboxStoreContainerFixture _fixture;

	public SqlServerInboxStoreTenantIsolationShould(SqlServerInboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task Admit_the_same_message_id_once_per_tenant_without_cross_tenant_collision()
	{
		await EnsureTableAsync().ConfigureAwait(false);

		var storeA = CreateStore(TenantA);
		var storeB = CreateStore(TenantB);

		const string messageId = "msg-shared-across-tenants";
		const string handlerType = "TestHandler";

		// Tenant A claims the message first.
		(await storeA.TryMarkAsProcessedAsync(messageId, handlerType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeTrue("tenant A is the first writer for its own (MessageId, HandlerType, TenantId) row");

		// Tenant B claims the SAME MessageId+HandlerType — must ALSO win exactly once. The CAS is keyed on the
		// tenant, so tenant A's row must not shadow tenant B. RED on the drop-TenantId-from-ON mutant.
		(await storeB.TryMarkAsProcessedAsync(messageId, handlerType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeTrue(
				"tenant B must independently claim the same message id — dedup is PER-TENANT, not global "
				+ "(a false here is the cross-tenant dedup collision this lock guards against)");
	}

	[Fact]
	public async Task Deduplicate_within_a_single_tenant()
	{
		await EnsureTableAsync().ConfigureAwait(false);

		var storeA = CreateStore(TenantA);
		const string messageId = "msg-within-tenant";
		const string handlerType = "TestHandler";

		(await storeA.TryMarkAsProcessedAsync(messageId, handlerType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeTrue("first claim within a tenant wins");

		// Same tenant, same message: the CAS MUST still deduplicate (isolation must not disable dedup).
		(await storeA.TryMarkAsProcessedAsync(messageId, handlerType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeFalse("a second claim within the SAME tenant must be deduplicated (exactly-once per tenant)");
	}

	private SqlServerInboxStore CreateStore(string tenantId)
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"SQL Server container must be available — real-infra tenant-isolation lock is never skipped.");

		var options = Options.Create(new SqlServerInboxOptions
		{
			ConnectionString = _fixture.ConnectionString,
			SchemaName = SchemaName,
			TableName = TableName,
		});
		return new SqlServerInboxStore(
			options,
			NullLogger<SqlServerInboxStore>.Instance,
			new FixedTenantContext(tenantId),
			Options.Create(new TenantContextOptions { RequireTenant = true }));
	}

	private async Task EnsureTableAsync()
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		// Isolated table whose PK is the FULL composite (MessageId, HandlerType, TenantId) — so two tenants'
		// identical message ids are distinct rows and the tenant dimension of the CAS is what is under test
		// (not a bare (MessageId, HandlerType) PK collision). Mirrors the canonical inbox_messages columns.
		// TenantId is NOT NULL here because both stores always supply a non-null ambient tenant.
		var sql = $"""
			IF OBJECT_ID('[{SchemaName}].[{TableName}]', 'U') IS NOT NULL DROP TABLE [{SchemaName}].[{TableName}];
			CREATE TABLE [{SchemaName}].[{TableName}] (
				[MessageId]        NVARCHAR(255)  NOT NULL,
				[HandlerType]      NVARCHAR(500)  NOT NULL,
				[MessageType]      NVARCHAR(500)  NOT NULL,
				[Payload]          VARBINARY(MAX) NOT NULL,
				[Metadata]         NVARCHAR(MAX)  NULL,
				[ReceivedAt]       DATETIMEOFFSET NOT NULL,
				[ProcessedAt]      DATETIMEOFFSET NULL,
				[Status]           INT            NOT NULL DEFAULT 0,
				[LastError]        NVARCHAR(MAX)  NULL,
				[RetryCount]       INT            NOT NULL DEFAULT 0,
				[LastAttemptAt]    DATETIMEOFFSET NULL,
				[NextAttemptAt]    DATETIMEOFFSET NULL,
				[CorrelationId]    NVARCHAR(255)  NULL,
				[TenantId]         NVARCHAR(255)  NOT NULL,
				[Source]           NVARCHAR(255)  NULL,
				CONSTRAINT [PK_{TableName}] PRIMARY KEY CLUSTERED ([MessageId], [HandlerType], [TenantId])
			);
			""";

		await using var command = new SqlCommand(sql, connection);
		_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
	}

	/// <summary>A minimal ambient tenant context pinned to a single tenant id.</summary>
	private sealed class FixedTenantContext(string? tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => !string.IsNullOrEmpty(TenantId);
	}
}
