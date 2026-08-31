// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Sqlite;
using Excalibur.Integration.Tests.Infrastructure;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Xunit;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Author≠impl real-infra lock for Excalibur_Dispatch-xay8ur: the SQLite <see cref="SqliteEventStore"/>
/// derives a tenant scope from the ambient <see cref="ITenantContext"/> and applies a row-level
/// <c>TenantId</c> discriminator in the same statement on every read/write, so one tenant can NEVER
/// observe another tenant's event streams — matching the shape already shipped for
/// <c>PostgresEventStoreTenantIsolationShould</c> / <c>SqlServerEventStoreTenantIsolationShould</c> /
/// <c>OracleEventStoreTenantIsolationShould</c>.
/// </summary>
/// <remarks>
/// <b>verify-against-real-infra-not-mock:</b> SQLite is an embedded, file-based database, so this runs
/// against the real engine with no Docker container and is inherently non-skipped — the same reasoning
/// <see cref="SqliteEventStoreFixture"/>'s own doc comment records. A mock cannot reproduce row-level
/// scoping, the real UNIQUE-constraint collision behaviour, or the untenanted-partition NOT-NULL hazard.
/// <para>
/// <b>Both arms (testing-patterns §3):</b> SAFETY — tenant B's scoped read must not see tenant A's rows,
/// and a duplicate version within one tenant (or within the untenanted partition) is still rejected;
/// LIVENESS — tenant A still reads its own stream, the non-MT store round-trips its events, and two
/// tenants may independently own a stream at the same natural aggregate id.
/// </para>
/// <para>
/// <b>RED-on-mutant:</b> drop the <c>TenantId</c> term from <c>SqliteEventStore</c>'s read/write SQL (or
/// widen the UNIQUE key without the tenant column) and the isolation and liveness facts below go RED —
/// this is exactly the pre-fix shape: <c>UNIQUE(AggregateId, AggregateType, Version)</c> with no tenant
/// term meant tenant B's version probe reported -1 while its INSERT collided with tenant A's row, a
/// conflict that could never converge on retry.
/// </para>
/// </remarks>
[Collection(SqliteEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Sqlite")]
public sealed class SqliteEventStoreTenantIsolationShould : IClassFixture<SqliteEventStoreFixture>
{
	private const string AggregateType = "Order";

	private readonly SqliteEventStoreFixture _fixture;

	public SqliteEventStoreTenantIsolationShould(SqliteEventStoreFixture fixture) => _fixture = fixture;

	private SqliteEventStore StoreFor(string? tenantId) =>
		new(
			_fixture.ConnectionString,
			NullLogger<SqliteEventStore>.Instance,
			tenantId is null ? UntenantedTestTenantContext.Instance : new FixedTestTenantContext(tenantId),
			// RequireTenant = true: these arms assert ISOLATION BETWEEN tenants, so the store must run in the
			// mode that keeps their rows apart. The single-tenant setting would select the initializer's
			// converge, which rewrites stored tenant identifiers onto one identity -- collapsing the very
			// partitions these arms exist to prove are separate.
			Microsoft.Extensions.Options.Options.Create(new TenantContextOptions { RequireTenant = true }));

	private sealed record OrderPlaced(string AggregateId, long Version) : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public string EventType { get; init; } = nameof(OrderPlaced);
		public IDictionary<string, object>? Metadata { get; init; }
	}

	[Fact]
	public async Task ScopeEveryStreamToItsTenant_OneTenantNeverSeesAnother()
	{
		await _fixture.CleanupAsync().ConfigureAwait(false);

		var aggId = "agg-" + Guid.NewGuid().ToString("N");
		var tenantA = StoreFor("tenant-A");
		var tenantB = StoreFor("tenant-B");

		// Tenant A writes a 2-event stream.
		_ = await tenantA.AppendAsync(
			aggId, AggregateType,
			[new OrderPlaced(aggId, 0), new OrderPlaced(aggId, 1)],
			-1, CancellationToken.None).ConfigureAwait(false);

		// SAFETY — tenant B cannot LOAD tenant A's events.
		(await tenantB.LoadAsync(aggId, AggregateType, -1, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeEmpty("tenant B must not load tenant A's event stream (row-level TenantId scoping)");

		// LIVENESS — tenant A still sees exactly its own 2 events (reads scoped to the writing tenant).
		(await tenantA.LoadAsync(aggId, AggregateType, -1, CancellationToken.None).ConfigureAwait(false))
			.Count.ShouldBe(2, "tenant A sees exactly its own 2 events (its own tenant-scoped stream)");
	}

	[Fact]
	public async Task RoundTripUnscoped_WhenUntenantedContext_WithTheReservedSentinel()
	{
		// LIVENESS (non-MT) — a store scoped to the reserved untenanted partition is the genuine
		// non-multi-tenant path: it must round-trip append -> load and not throw.
		await _fixture.CleanupAsync().ConfigureAwait(false);

		var aggId = "agg-" + Guid.NewGuid().ToString("N");
		var nonTenant = StoreFor(null);

		_ = await nonTenant.AppendAsync(
			aggId, AggregateType,
			[new OrderPlaced(aggId, 0), new OrderPlaced(aggId, 1)],
			-1, CancellationToken.None).ConfigureAwait(false);

		(await nonTenant.LoadAsync(aggId, AggregateType, -1, CancellationToken.None).ConfigureAwait(false))
			.Count.ShouldBe(2, "the non-multi-tenant store round-trips its own events via the untenanted partition");
	}

	[Fact]
	public async Task NotDiscloseATenantsEvents_ToAnUnscopedReader()
	{
		// SAFETY — the untenanted partition is a real, distinct partition; a store scoped to it must not
		// receive a real tenant's events. Property-based: asserts the disclosure, not the SQL.
		await _fixture.CleanupAsync().ConfigureAwait(false);

		var aggId = "agg-" + Guid.NewGuid().ToString("N");

		// Tenant B owns a 2-event stream.
		_ = await StoreFor("tenant-B").AppendAsync(
			aggId, AggregateType,
			[new OrderPlaced(aggId, 0), new OrderPlaced(aggId, 1)],
			-1, CancellationToken.None).ConfigureAwait(false);

		// An unscoped reader's partition is the untenanted one; it must NOT receive tenant B's events.
		(await StoreFor(null).LoadAsync(aggId, AggregateType, -1, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeEmpty(
				"an unscoped reader must not receive a tenant's events — the untenanted partition (the "
				+ "reserved '__untenanted__' sentinel) excludes tenant-scoped rows");
	}

	/// <summary>
	/// LIVENESS for the tenant-scoped stream key. Two tenants must be able to use the SAME natural
	/// aggregate id independently — the case a tenant-less uniqueness key makes impossible.
	/// </summary>
	/// <remarks>
	/// RED before the key carried the tenant term: the version probe is tenant-scoped, so tenant B probes
	/// its own (empty) partition and gets -1, then its INSERT collides with tenant A's row on
	/// <c>UNIQUE (AggregateId, AggregateType, Version)</c>. The store classifies a unique violation as a
	/// concurrency conflict, which is RETRYABLE — and the retry re-probes, gets -1 again, and collides
	/// again. So the pre-fix failure is not merely a rejected append but one that cannot converge, which is
	/// why this arm asserts success rather than merely a different error classification.
	/// </remarks>
	[Fact]
	public async Task AdmitTheSameAggregateId_ForTwoTenantsIndependently()
	{
		await _fixture.CleanupAsync().ConfigureAwait(false);

		// A natural key, which is the whole point: an order number or customer reference is chosen by the
		// consumer, so two tenants colliding on one is routine rather than exotic.
		var sharedAggId = "order-" + Guid.NewGuid().ToString("N");

		var tenantA = StoreFor("tenant-A");
		var tenantB = StoreFor("tenant-B");

		var appendA = await tenantA.AppendAsync(
			sharedAggId, AggregateType,
			[new OrderPlaced(sharedAggId, 0), new OrderPlaced(sharedAggId, 1)],
			-1, CancellationToken.None).ConfigureAwait(false);
		appendA.Success.ShouldBeTrue("tenant A opens the stream first; nothing contends with it");

		// The lock. Tenant B has never used this aggregate id, its probe returns -1, and its append must
		// therefore succeed. Under the tenant-less key this returned a concurrency conflict forever.
		var appendB = await tenantB.AppendAsync(
			sharedAggId, AggregateType,
			[new OrderPlaced(sharedAggId, 0)],
			-1, CancellationToken.None).ConfigureAwait(false);

		appendB.Success.ShouldBeTrue(
			"tenant B must be able to open its own stream at an aggregate id tenant A already used — the "
			+ "tenant participates in stream identity, so the two streams are distinct rows. Under a "
			+ "tenant-less key B's tenant-scoped probe reports -1 while its INSERT collides with A's row, "
			+ "and the resulting conflict is retryable but can never converge");

		// Each tenant reads back exactly its own stream, so the two did not merge into one.
		(await tenantA.LoadAsync(sharedAggId, AggregateType, -1, CancellationToken.None).ConfigureAwait(false))
			.Count.ShouldBe(2, "tenant A still holds its own two events");
		(await tenantB.LoadAsync(sharedAggId, AggregateType, -1, CancellationToken.None).ConfigureAwait(false))
			.Count.ShouldBe(1, "tenant B holds its own single event, not tenant A's stream");
	}

	/// <summary>
	/// SAFETY for the tenant-scoped stream key. Widening the key must not weaken optimistic concurrency
	/// WITHIN a tenant: a second append of the same version by the same tenant must still be rejected.
	/// </summary>
	/// <remarks>
	/// This is the arm that fails if the key is widened by DROPPING the constraint rather than replacing
	/// it — a change under which every isolation and liveness assertion in this file continues to pass.
	/// </remarks>
	[Fact]
	public async Task StillRejectADuplicateVersion_WithinOneTenant()
	{
		await _fixture.CleanupAsync().ConfigureAwait(false);

		var aggId = "agg-" + Guid.NewGuid().ToString("N");
		var tenantA = StoreFor("tenant-A");

		(await tenantA.AppendAsync(
			aggId, AggregateType,
			[new OrderPlaced(aggId, 0)],
			-1, CancellationToken.None).ConfigureAwait(false))
			.Success.ShouldBeTrue("the first append opens the stream at version 0");

		// Same tenant, same aggregate, same version. The tenant term is equal on both rows, so the key
		// must still reject this.
		var duplicate = await tenantA.AppendAsync(
			aggId, AggregateType,
			[new OrderPlaced(aggId, 0)],
			-1, CancellationToken.None).ConfigureAwait(false);

		duplicate.Success.ShouldBeFalse(
			"a second append of version 0 by the SAME tenant must be rejected — adding the tenant to the "
			+ "key makes concurrency per-tenant, it does not remove it");
		duplicate.IsConcurrencyConflict.ShouldBeTrue(
			"and it must be classified as a concurrency conflict so the caller's retry path engages");

		// The rejected append left nothing behind.
		(await tenantA.LoadAsync(aggId, AggregateType, -1, CancellationToken.None).ConfigureAwait(false))
			.Count.ShouldBe(1, "the rejected duplicate must not have been written");
	}

	/// <summary>
	/// SAFETY for the untenanted partition specifically — the rows a pre-tenancy database is made of.
	/// </summary>
	/// <remarks>
	/// Separate from the arm above because it exercises the sentinel rather than a real tenant, and the
	/// sentinel is the value the rebuild-with-staging reconciliation backfills legacy rows onto. Were the
	/// column left nullable, those rows would carry NULL; SQLite treats each NULL in a unique key as
	/// distinct from every other, so this duplicate would be ACCEPTED — optimistic concurrency silently
	/// gone for exactly the rows that predate tenancy, with no other assertion in this file able to
	/// observe it.
	/// </remarks>
	[Fact]
	public async Task StillRejectADuplicateVersion_InTheUntenantedPartition()
	{
		await _fixture.CleanupAsync().ConfigureAwait(false);

		var aggId = "agg-" + Guid.NewGuid().ToString("N");
		var unscoped = StoreFor(null);

		(await unscoped.AppendAsync(
			aggId, AggregateType,
			[new OrderPlaced(aggId, 0)],
			-1, CancellationToken.None).ConfigureAwait(false))
			.Success.ShouldBeTrue("the unscoped store opens its stream in the untenanted partition");

		var duplicate = await unscoped.AppendAsync(
			aggId, AggregateType,
			[new OrderPlaced(aggId, 0)],
			-1, CancellationToken.None).ConfigureAwait(false);

		duplicate.Success.ShouldBeFalse(
			"a duplicate version in the untenanted partition must be rejected — the sentinel is a concrete "
			+ "value precisely so it can participate in a unique key, which a NULL discriminator cannot");
		duplicate.IsConcurrencyConflict.ShouldBeTrue(
			"and it must be classified as a concurrency conflict");
	}
}
