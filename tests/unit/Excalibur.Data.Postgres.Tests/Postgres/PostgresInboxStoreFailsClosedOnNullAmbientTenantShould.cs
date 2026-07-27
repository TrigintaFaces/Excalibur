// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Inbox.Postgres;

using Microsoft.Extensions.Logging.Abstractions;

using Npgsql;

using Xunit;

namespace Excalibur.Data.Tests.Postgres;

// Independent regression lock (author != implementer) for REVIEW_CODE BLOCKING B3, SA-ruled (a) fail-closed,
// symmetric with fdepwq (msg 28229).
//
// THE DEFECT. Every tenant-facing inbox method reads `var tenantId = _tenantContext?.TenantId;` then branches
// `tenantId is null ? <bare cross-tenant> : <tenant-scoped>` (PostgresInboxStore TryMarkAsProcessedAsync:342-345,
// and 20 sibling sites). That is fail-OPEN: when the store IS configured for multi-tenancy (an ITenantContext is
// injected) but the ambient tenant is unset (`.TenantId` is null), the dedup runs with the BARE
// `(message_id, handler_type)` conflict target across ALL tenants — so tenant B's message is silently suppressed
// as a "duplicate" because tenant A already processed the same id (cross-tenant message LOSS, SA's sharpest
// framing), and keyed reads leak across tenants. Yet ff06rr registered TenantScopingCapabilityMarker<IInboxStore>,
// telling AddMultiTenancy's fail-closed gate the inbox is tenant-safe — converting a startup rejection into a
// runtime leak. This is the exact widening ternary fdepwq DELETED from the event store this same sprint.
//
// THE FIX (SA (a), symmetric with fdepwq's ArgumentException.ThrowIfNullOrWhiteSpace(tenantId)). Every
// tenant-facing keyed/dedup op fails closed on a null/whitespace ambient tenant (12 guards). The store is always
// composed with a NON-NULL ITenantContext — the builder registers AddDefaultTenantContext() so a single-tenant
// host resolves the "__default__" context and a multi-tenant host resolves the ambient one — so a null tenant id
// under a present context means the ambient scope was not established, and that is the leak the guard closes. The
// ADMIN / re-admission drain methods (GetAllEntries / GetFailedEntries / GetStatistics / Cleanup) stay UNGUARDED
// and cross-tenant, per SA's ruling.
//
// SEAM. The lock binds the observable PROPERTY, not a mechanism (pin-interface-seam / testing-patterns §3
// corollary): a tenant-facing op throws BEFORE it touches SQL when the ambient tenant is absent; a resolved
// tenant reaches SQL; and an ADMIN drain op reaches SQL regardless (unguarded, cross-tenant). Construction is
// connection-FACTORY based, so a factory that throws a sentinel the instant it is invoked lets us assert
// "reached SQL" vs "failed closed first" deterministically, with no live database.
//
// SAFETY + LIVENESS (testing-patterns §3):
//   SAFETY  — a tenant-facing dedup op with a null ambient tenant → throws (ArgumentException) BEFORE the
//     connection factory is ever invoked. RED against the pre-fix widening ternary (which reaches the factory →
//     sentinel, not ArgumentException). This is the arm the cross-tenant-loss leak fails.
//   LIVENESS (admin drain unchanged) — an ADMIN drain op (GetAllEntries) reaches SQL even with a null ambient
//     tenant: the guard must NOT have spread to the drain, or the background re-admission path breaks (SA
//     criterion 2). Without this arm, a fix that guarded everything would pass the safety arm while killing the
//     drain.
//   LIVENESS (resolved) — a tenant-facing op with a RESOLVED tenant reaches SQL (guard passes). Proves the guard
//     fires ONLY on the null-ambient path, not on every op — the non-vacuity partner that stops the safety arm
//     passing via a blanket throw.
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PostgresInboxStoreFailsClosedOnNullAmbientTenantShould
{
	[Fact]
	public async Task FailClosed_BeforeTouchingSql_WhenMultiTenantAndAmbientTenantIsNull()
	{
		// SAFETY — the regression arm. RED against the fail-open ternary (reaches the sentinel factory → throws
		// SentinelConnectionReached, NOT ArgumentException). GREEN once the op guards fail-closed pre-SQL.
		var store = CreateStore(tenantContext: new AmbientTenantContext(tenantId: null));

		// The store now derives scope via the canonical TenantScope.FromContext: a present context that resolves
		// a null/whitespace tenant fails closed by construction with TenantRequiredException (an
		// InvalidOperationException) — the same canonical fail-closed signal the EventStore/Outbox/Saga emit —
		// rather than the ad-hoc ArgumentException the pre-canonical guard threw. Strengthened to the specific type.
		await Should.ThrowAsync<TenantRequiredException>(
			async () => await store.TryMarkAsProcessedAsync("message-1", "handler-1", CancellationToken.None),
			"A multi-tenant inbox (ITenantContext injected) with a NULL ambient tenant must FAIL CLOSED on the dedup " +
			"path — not widen to the bare (message_id, handler_type) conflict target across all tenants. Fail-open " +
			"here silently discards tenant B's message as a duplicate of tenant A's same id (cross-tenant loss) and " +
			"leaks keyed reads. The op must throw before it ever opens a connection, symmetric with the event " +
			"store's fdepwq guard.");
	}

	[Fact]
	public async Task ReachSql_OnTheAdminDrain_EvenWhenAmbientTenantIsNull()
	{
		// LIVENESS (admin drain unchanged — SA criterion 2). The admin / re-admission drain methods
		// (GetAllEntries / GetFailedEntries / GetStatistics / Cleanup) are UNGUARDED and cross-tenant by design.
		// GetAllEntries with a present-but-null ambient tenant MUST still reach SQL (sentinel), NOT fail closed —
		// or the fail-closed guard has spread from the tenant-facing dedup ops to the drain and the background
		// re-admission path is broken. GREEN before AND after the fix; goes RED only if a future edit guards the
		// drain.
		var store = CreateStore(tenantContext: new AmbientTenantContext(tenantId: null));

		await Should.ThrowAsync<SentinelConnectionReached>(
			async () => await store.GetAllEntriesAsync(CancellationToken.None),
			"The admin drain (GetAllEntries) must reach SQL even with a null ambient tenant — it is deliberately " +
			"cross-tenant and unguarded (SA criterion 2). If it throws ArgumentException, the fail-closed guard has " +
			"leaked from the tenant-facing dedup ops onto the drain, breaking background re-admission.");
	}

	[Fact]
	public async Task ReachSql_WhenMultiTenantAndAmbientTenantIsResolved()
	{
		// LIVENESS (resolved) + non-vacuity partner of the safety arm. A resolved tenant must pass the guard and
		// reach SQL. If a fix satisfied the safety arm by throwing whenever an ITenantContext is present, THIS arm
		// goes RED — so the pair pins the guard to the null-ambient path precisely.
		var store = CreateStore(tenantContext: new AmbientTenantContext(tenantId: "tenant-a"));

		await Should.ThrowAsync<SentinelConnectionReached>(
			async () => await store.TryMarkAsProcessedAsync("message-1", "handler-1", CancellationToken.None),
			"A multi-tenant inbox with a RESOLVED ambient tenant must pass the fail-closed guard and reach SQL " +
			"(sentinel). If this throws ArgumentException, the guard is firing on every multi-tenant op rather than " +
			"only the null-ambient path — an over-correction that would reject correct multi-tenant hosts.");
	}

	private static PostgresInboxStore CreateStore(ITenantContext? tenantContext) =>
		new(
			connectionFactory: () => throw new SentinelConnectionReached(),
			options: new PostgresInboxOptions(),
			logger: NullLogger<PostgresInboxStore>.Instance,
			tenantContext: tenantContext);

	// Thrown by the connection factory the instant it is invoked. Its appearance means the op reached the SQL/
	// connection stage; its ABSENCE (when ArgumentException is thrown instead) means the op failed closed first.
	private sealed class SentinelConnectionReached() : Exception("The inbox op reached the connection factory.")
	{
	}

	// A tenant context that is PRESENT (so the store is multi-tenant) but whose ambient tenant may be null — the
	// exact "multi-tenancy configured, ambient tenant unset" state the fail-open ternary mishandles.
	private sealed class AmbientTenantContext(string? tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => !string.IsNullOrEmpty(TenantId);
	}
}
