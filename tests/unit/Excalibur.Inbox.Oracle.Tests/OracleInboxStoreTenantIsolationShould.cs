// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Inbox.Oracle;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Oracle.ManagedDataAccess.Client;

using Shouldly;

using Xunit;

#pragma warning disable CA2100 // SQL uses a compile-time const table name in a test fixture.

namespace Excalibur.Inbox.Oracle.Tests;

/// <summary>
/// ljbwh8 — independent (author≠impl, TestsDeveloper) NON-SKIPPED real-Oracle tenant-isolation lock for the
/// inbox dedup/claim CAS and keyed reads (Oracle sibling of <c>SqlServerInboxStoreTenantIsolationShould</c>).
/// The atomic first-writer claim (<c>TryMarkAsProcessedAsync(messageId, handlerType, ct)</c>) and the keyed
/// reads (<c>IsProcessedAsync</c>) key on the composite <b>(TenantId, MessageId, HandlerType)</b>: the ambient
/// <see cref="ITenantContext"/> is woven into the INSERT and every keyed predicate. Two different tenants with
/// the SAME <c>MessageId</c>+<c>HandlerType</c> MUST each win exactly once, and one tenant's scoped read MUST
/// NOT see another tenant's row — dedup and reads are per-tenant, never global.
/// </summary>
/// <remarks>
/// Proves the tenant dimension end-to-end against real Oracle (a mocked path cannot reproduce Oracle's
/// composite-PK ORA-00001 dedup or the server-side predicate — see <c>verify-against-real-infra-not-mock</c>).
/// It hand-rolls an isolated table whose PK is the full composite <c>(MessageId, HandlerType, TenantId)</c> so
/// two tenants' identical message ids are distinct rows and the tenant dimension is what is under test.
/// <para>
/// SAFETY + LIVENESS (<c>testing-patterns §3</c>): SAFETY — tenant B's scoped read/claim does NOT see or
/// collide with tenant A's row; LIVENESS — tenant A DOES see its own row (across a fresh store instance), and
/// dedup still fires within a single tenant. <b>RED on the mutant</b> that drops <c>TenantId</c> from the
/// keyed predicate/insert: tenant B's claim then matches tenant A's row on <c>MessageId</c>+<c>HandlerType</c>
/// alone (cross-tenant dedup collision), and B's scoped read sees A's row.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Database", "Oracle")]
[Trait("Component", "Inbox")]
public sealed class OracleInboxStoreTenantIsolationShould : IClassFixture<OracleInboxStoreContainerFixture>
{
	private const string TableName = "INBOX_TENANT_ISO_TEST";
	private const string HandlerType = "TestHandler";
	private const string TenantA = "tenant-A";
	private const string TenantB = "tenant-B";

	private readonly OracleInboxStoreContainerFixture _fixture;

	public OracleInboxStoreTenantIsolationShould(OracleInboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task Admit_the_same_message_id_once_per_tenant_without_cross_tenant_collision()
	{
		await EnsureTableAsync().ConfigureAwait(false);
		const string messageId = "msg-shared-across-tenants";

		// Tenant A is the first writer for its own (MessageId, HandlerType, TenantId) row.
		(await CreateStore(TenantA).TryMarkAsProcessedAsync(messageId, HandlerType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeTrue("tenant A must win the first claim for its own tenant-scoped row");

		// Tenant B claims the SAME MessageId+HandlerType — must ALSO win exactly once. RED on the drop-TenantId
		// mutant: B would match A's row on (MessageId, HandlerType) alone → no insert → false.
		(await CreateStore(TenantB).TryMarkAsProcessedAsync(messageId, HandlerType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeTrue(
				"tenant B must independently claim the same message id — dedup is PER-TENANT, not global "
				+ "(a false here is the cross-tenant dedup collision this lock guards against)");
	}

	[Fact]
	public async Task Deduplicate_within_a_single_tenant()
	{
		await EnsureTableAsync().ConfigureAwait(false);
		const string messageId = "msg-within-tenant";
		var storeA = CreateStore(TenantA);

		(await storeA.TryMarkAsProcessedAsync(messageId, HandlerType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeTrue("first claim within a tenant wins");

		// Same tenant, same message: dedup MUST still fire (isolation must not disable per-tenant exactly-once).
		(await storeA.TryMarkAsProcessedAsync(messageId, HandlerType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeFalse("a second claim within the SAME tenant must be deduplicated (exactly-once per tenant)");
	}

	[Fact]
	public async Task Isolate_keyed_reads_per_tenant_across_a_fresh_store_instance()
	{
		await EnsureTableAsync().ConfigureAwait(false);
		const string messageId = "msg-scoped-read";

		// Tenant A records the message as processed.
		(await CreateStore(TenantA).TryMarkAsProcessedAsync(messageId, HandlerType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeTrue("tenant A records the processed row");

		// FRESH store instances (durable round-trip, not a cached in-process value):
		// SAFETY — tenant B's scoped read must NOT see tenant A's row.
		(await CreateStore(TenantB).IsProcessedAsync(messageId, HandlerType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeFalse(
				"tenant B's scoped read must not see tenant A's processed row — the keyed read predicate binds "
				+ "TenantId, so this is false unless the tenant dimension was dropped (cross-tenant read leak)");

		// LIVENESS — tenant A's scoped read DOES see its own row (the isolation is not "safe by returning nothing").
		(await CreateStore(TenantA).IsProcessedAsync(messageId, HandlerType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeTrue("tenant A's scoped read must see its own processed row — isolation must not withhold a tenant's own data");
	}

	private OracleInboxStore CreateStore(string tenantId)
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Oracle container must be available — real-infra tenant-isolation lock is never skipped.");

		var options = Options.Create(new OracleInboxOptions
		{
			ConnectionString = _fixture.ConnectionString,
			SchemaName = _fixture.SchemaName,
			TableName = TableName,
		});
		return new OracleInboxStore(
			options,
			NullLogger<OracleInboxStore>.Instance,
			new FixedTenantContext(tenantId),
			Options.Create(new TenantContextOptions { RequireTenant = true }));
	}

	// Isolated table whose PK is the FULL composite (MessageId, HandlerType, TenantId), so two tenants' identical
	// message ids are distinct rows and the tenant dimension of the CAS/reads is under test. Columns mirror the
	// canonical INBOX_MESSAGES; TenantId is NOT NULL (both stores always supply a non-null ambient tenant).
	private async Task EnsureTableAsync()
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		// Oracle has no DROP TABLE IF EXISTS — swallow ORA-00942 (table or view does not exist).
		try
		{
			await using var drop = connection.CreateCommand();
			drop.CommandText = $"DROP TABLE {TableName} CASCADE CONSTRAINTS";
			_ = await drop.ExecuteNonQueryAsync().ConfigureAwait(false);
		}
		catch (OracleException ex) when (ex.Number == 942)
		{
			// ORA-00942: table or view does not exist — nothing to drop.
		}

		await using var create = connection.CreateCommand();
		create.CommandText = $"""
			CREATE TABLE {TableName} (
				MessageId          VARCHAR2(255)                   NOT NULL,
				HandlerType        VARCHAR2(500)                   NOT NULL,
				MessageType        VARCHAR2(500),
				Payload            BLOB,
				Metadata           CLOB,
				ReceivedAt         TIMESTAMP(7) WITH TIME ZONE     NOT NULL,
				ProcessedAt        TIMESTAMP(7) WITH TIME ZONE,
				Status             NUMBER(10)     DEFAULT 0        NOT NULL,
				LastError          VARCHAR2(4000),
				RetryCount         NUMBER(10)     DEFAULT 0        NOT NULL,
				LastAttemptAt      TIMESTAMP(7) WITH TIME ZONE,
				NextAttemptAt      TIMESTAMP(7) WITH TIME ZONE,
				LeaseExpiresAtUtc  TIMESTAMP(7) WITH TIME ZONE,
				CorrelationId      VARCHAR2(255),
				TenantId           VARCHAR2(255)                   NOT NULL,
				Source             VARCHAR2(255),
				CONSTRAINT PK_INBOX_TENANT_ISO PRIMARY KEY (MessageId, HandlerType, TenantId)
			)
			""";
		_ = await create.ExecuteNonQueryAsync().ConfigureAwait(false);
	}

	/// <summary>A minimal ambient tenant context pinned to a single tenant id.</summary>
	private sealed class FixedTenantContext(string? tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => !string.IsNullOrEmpty(TenantId);
	}
}
