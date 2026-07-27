// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Outbox.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

#pragma warning disable CA2100 // SQL strings use a compile-time const table name in a test fixture.

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// bd-b1c55r / bd-omn3c0 — independent (author≠impl, TestsDeveloper) NON-SKIPPED real-SQL-Server lock for the
/// outbox <b>drain</b> tenant seam. The claim (<c>GetUnsentMessagesAsync</c>) is GLOBAL by design (a serverless
/// <c>IOutboxProcessor</c> may run inside any tenant's request); the Id-keyed mark MUST therefore also be
/// tenant-agnostic — the outbox <c>Id</c> is the globally-unique PK, so a mark can only address exactly one row.
/// </summary>
/// <remarks>
/// This replaces the earlier "cross-tenant <c>MarkSent</c> throws" assertion, which certified the bug: an
/// ambient-tenant predicate on the mark meant a drain running under tenant A claimed tenant B's row globally,
/// sent it, then <c>MarkSentAsync WHERE TenantId=A</c> matched 0 rows → threw → the row stuck unsent → lease
/// expiry → re-claimed + re-sent unbounded (at-least-once liveness hole). Per the SA seam ruling the ambient
/// predicate is deleted from every Id-keyed drain mark, restoring claim/mark symmetry.
/// <para>
/// <b>RED on the pre-fix impl</b> (ambient <c>AND TenantId = @TenantId</c> on the mark): the drain-claimed
/// cross-ambient-tenant row's <c>MarkSentAsync</c> throws instead of marking it terminal. Tenant stamping on the
/// write path is PRESERVED (second fact) — the tenant dimension is still recorded, it just no longer scopes the
/// unique-Id drain mark.
/// </para>
/// </remarks>
[Collection(SqlServerOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Database", "SqlServer")]
[Trait("Component", "Core")]
public sealed class SqlServerOutboxStoreTenantIsolationShould : IClassFixture<SqlServerOutboxStoreContainerFixture>
{
	private const string DrainAmbientTenant = "tenant-A";
	private const string MessageTenant = "tenant-B";

	private readonly SqlServerOutboxStoreContainerFixture _fixture;

	public SqlServerOutboxStoreTenantIsolationShould(SqlServerOutboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task Mark_a_globally_claimed_row_terminal_even_under_a_different_ambient_tenant()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);

		// A message OWNED by tenant B is staged (StageMessageAsync stamps the message's own tenant).
		const string messageId = "outbox-drain-cross-ambient-tenant";
		await store.StageMessageAsync(
			new OutboundMessage { Id = messageId, MessageType = "T", Payload = [1], Destination = "dest", TenantId = MessageTenant },
			CancellationToken.None).ConfigureAwait(false);

		// The drain runs under ambient tenant A and GLOBALLY claims tenant B's row (no tenant predicate on claim).
		var claimed = await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
		claimed.ShouldContain(m => m.Id == messageId, "the global drain must claim the row regardless of its tenant");

		// The Id-keyed mark MUST succeed — the row is globally-unique, so a tenant predicate here only buys the
		// liveness hole. RED on the pre-fix ambient-scoped mark (WHERE TenantId='tenant-A' → 0 rows → throws).
		await Should.NotThrowAsync(async () =>
			await store.MarkSentAsync(messageId, CancellationToken.None).ConfigureAwait(false));

		// The row is terminal (Sent) — not stuck, so no lease-expiry re-send.
		(await ReadStatusAsync(messageId).ConfigureAwait(false))
			.ShouldBe((int)OutboxStatus.Sent, "the drained row must reach Sent so it is never re-claimed / re-sent");
	}

	[Fact]
	public async Task Preserve_the_staged_messages_own_tenant_stamp()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);

		// Tenant stamping on the write path is preserved (SA ruling: drain-mark de-scope only) — each message's
		// own TenantId is recorded on its row, independent of the drain's ambient tenant.
		await store.StageMessageAsync(
			new OutboundMessage { Id = "outbox-stamp-a", MessageType = "T", Payload = [1], Destination = "d", TenantId = DrainAmbientTenant },
			CancellationToken.None).ConfigureAwait(false);
		await store.StageMessageAsync(
			new OutboundMessage { Id = "outbox-stamp-b", MessageType = "T", Payload = [1], Destination = "d", TenantId = MessageTenant },
			CancellationToken.None).ConfigureAwait(false);

		(await ReadTenantAsync("outbox-stamp-a").ConfigureAwait(false)).ShouldBe(DrainAmbientTenant);
		(await ReadTenantAsync("outbox-stamp-b").ConfigureAwait(false)).ShouldBe(MessageTenant);
	}

	// ajui0a: SqlServerOutboxStore no longer threads an ambient ITenantContext — tenant isolation is the
	// per-message TenantId column (stamped on the write path) and the drain is deliberately cross-tenant, so the
	// old ambientTenantId plumbing + FixedTenantContext were vestigial and are removed.
	private async Task<SqlServerOutboxStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"SQL Server container must be available — real-infra outbox drain tenant lock is never skipped.");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var options = new SqlServerOutboxOptions { ConnectionString = _fixture.ConnectionString };
		return new SqlServerOutboxStore(
			() => new SqlConnection(_fixture.ConnectionString),
			options,
			payloadSerializer: null,
			NullLogger<SqlServerOutboxStore>.Instance);
	}

	private async Task<int> ReadStatusAsync(string messageId)
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);
		var sql = $"SELECT Status FROM [{_fixture.SchemaName}].[{_fixture.OutboxTableName}] WHERE Id = @Id";
		await using var command = new SqlCommand(sql, connection);
		_ = command.Parameters.AddWithValue("@Id", messageId);
		var result = await command.ExecuteScalarAsync().ConfigureAwait(false);
		return result is null or DBNull ? -1 : Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture);
	}

	private async Task<string?> ReadTenantAsync(string messageId)
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);
		var sql = $"SELECT TenantId FROM [{_fixture.SchemaName}].[{_fixture.OutboxTableName}] WHERE Id = @Id";
		await using var command = new SqlCommand(sql, connection);
		_ = command.Parameters.AddWithValue("@Id", messageId);
		var result = await command.ExecuteScalarAsync().ConfigureAwait(false);
		return result is null or DBNull ? null : (string)result;
	}
}
