// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Outbox.Postgres;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// bd-b1c55r / bd-omn3c0 — independent (author≠impl) NON-SKIPPED real-Postgres lock for the outbox <b>drain</b>
/// tenant seam (Postgres sibling of <see cref="SqlServerOutboxStoreTenantIsolationShould"/>). The claim
/// (<c>GetUnsentMessagesAsync</c>) is GLOBAL by design; the Id-keyed mark MUST therefore also be tenant-agnostic —
/// the outbox message id is the globally-unique key, so a mark addresses exactly one row.
/// </summary>
/// <remarks>
/// Replaces the earlier "cross-tenant <c>MarkSent</c> throws" assertion, which certified the bug: an
/// ambient-tenant predicate on the delete meant a drain under tenant A globally claimed tenant B's row, sent it,
/// then the tenant-scoped delete matched 0 rows → threw → the row stuck → re-claimed + re-sent unbounded. Per the
/// SA seam ruling the ambient predicate is deleted from the Id-keyed drain marks (the Postgres store deletes the
/// row on send). Tenant stamping on the write path is PRESERVED (second fact).
/// <para>
/// <b>RED on the pre-fix impl</b> (ambient <c>AND tenant_id = @TenantId</c> on the delete): the drain-claimed
/// cross-ambient-tenant row's <c>MarkSentAsync</c> throws instead of removing it.
/// </para>
/// </remarks>
[Collection(PostgresOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Database", "Postgres")]
[Trait("Component", "Core")]
public sealed class PostgresOutboxStoreTenantIsolationShould : IClassFixture<PostgresOutboxStoreContainerFixture>
{
	private const string DrainAmbientTenant = "tenant-A";
	private const string MessageTenant = "tenant-B";

	private readonly PostgresOutboxStoreContainerFixture _fixture;

	public PostgresOutboxStoreTenantIsolationShould(PostgresOutboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task Mark_a_globally_claimed_row_terminal_even_under_a_different_ambient_tenant()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);

		// A message OWNED by tenant B is staged (StageMessageAsync stamps the message's own tenant_id).
		const string messageId = "outbox-drain-cross-ambient-tenant";
		await store.StageMessageAsync(
			new OutboundMessage { Id = messageId, MessageType = "T", Payload = [1], Destination = "dest", TenantId = MessageTenant },
			CancellationToken.None).ConfigureAwait(false);

		// The drain runs under ambient tenant A and GLOBALLY claims tenant B's row (no tenant predicate on claim).
		var claimed = await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
		claimed.ShouldContain(m => m.Id == messageId, "the global drain must claim the row regardless of its tenant");

		// The Id-keyed mark MUST succeed. RED on the pre-fix ambient-scoped delete (WHERE tenant_id='tenant-A' →
		// 0 rows deleted → throws).
		await Should.NotThrowAsync(async () =>
			await store.MarkSentAsync(messageId, CancellationToken.None).ConfigureAwait(false));

		// The Postgres store deletes the row on send — terminal, so it is never re-claimed / re-sent.
		(await RowCountByIdAsync(messageId).ConfigureAwait(false))
			.ShouldBe(0, "the drained row must be removed so it is never re-claimed / re-sent");
	}

	[Fact]
	public async Task Preserve_the_staged_messages_own_tenant_stamp()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);

		// Tenant stamping on the write path is preserved (SA ruling: drain-mark de-scope only).
		await store.StageMessageAsync(
			new OutboundMessage { Id = "outbox-stamp-a", MessageType = "T", Payload = [1], Destination = "d", TenantId = DrainAmbientTenant },
			CancellationToken.None).ConfigureAwait(false);
		await store.StageMessageAsync(
			new OutboundMessage { Id = "outbox-stamp-b", MessageType = "T", Payload = [1], Destination = "d", TenantId = MessageTenant },
			CancellationToken.None).ConfigureAwait(false);

		(await ReadTenantAsync("outbox-stamp-a").ConfigureAwait(false)).ShouldBe(DrainAmbientTenant);
		(await ReadTenantAsync("outbox-stamp-b").ConfigureAwait(false)).ShouldBe(MessageTenant);
	}

	// ajui0a: PostgresOutboxStore no longer threads an ambient ITenantContext — tenant isolation is the
	// per-message tenant_id column (stamped on the write path) and the drain is deliberately cross-tenant, so the
	// old ambientTenantId plumbing + FixedTenantContext were vestigial and are removed.
	private async Task<PostgresOutboxStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Postgres container must be available — real-infra outbox drain tenant lock is never skipped.");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		// Consumer-default surface: an IDb whose Connection yields a fresh Npgsql connection per access.
		var db = A.Fake<IDb>();
		_ = A.CallTo(() => db.Connection).ReturnsLazily(() => _fixture.CreateConnection());

		var options = Options.Create(new PostgresOutboxStoreOptions
		{
			SchemaName = _fixture.SchemaName,
			OutboxTableName = _fixture.OutboxTableName,
			DeadLetterTableName = _fixture.DeadLetterTableName,
		});

		return new PostgresOutboxStore(
			db,
			options,
			NullLogger<PostgresOutboxStore>.Instance,
			metrics: null);
	}

	private async Task<long> RowCountByIdAsync(string messageId)
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);
		return await connection.ExecuteScalarAsync<long>(
			$"SELECT COUNT(*) FROM {_fixture.SchemaName}.{_fixture.OutboxTableName} WHERE message_id = @Id",
			new { Id = messageId }).ConfigureAwait(false);
	}

	private async Task<string?> ReadTenantAsync(string messageId)
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);
		return await connection.ExecuteScalarAsync<string?>(
			$"SELECT tenant_id FROM {_fixture.SchemaName}.{_fixture.OutboxTableName} WHERE message_id = @Id",
			new { Id = messageId }).ConfigureAwait(false);
	}
}
