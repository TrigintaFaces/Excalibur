// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Postgres;

using Microsoft.Extensions.Logging.Abstractions;

using Npgsql;

using Shouldly;

using Xunit;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Author≠implementer RED-first real-Postgres lock for the GDPR-erasure tenant-scope class: an erase (or
/// erasure existence check) MUST NOT reach a partition outside the one it operates in. Binds two paired
/// GDPR failures that share one empty-branch predicate (beads <c>18c3el</c> over-erasure + <c>chvpym</c>
/// under-erasure).
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect (one shape, two directions):</b> both <c>EraseEventsRequest</c> and
/// <c>IsErasedRequest</c> compute <c>scope.IsScoped ? " AND tenant_id = @TenantId" : string.Empty</c>. The
/// EMPTY (unscoped / <see cref="TenantScope.Untenanted"/>) branch drops the tenant boundary entirely, so an
/// unscoped operation matches rows across every partition rather than only its own (the untenanted one).
/// This is reachable in production, not contrived: erasure runs from a background service with no ambient
/// tenant (<c>scope.IsScoped == false</c>).
/// </para>
/// <para>
/// <b>Why one tenant, not two:</b> the events table's UNIQUE key is
/// <c>(aggregate_id, aggregate_type, version)</c> — TENANT-AGNOSTIC — so two tenants cannot hold the same
/// aggregate id through the store's write path (the second tenant's v0 collides). The reachable, testable
/// harm is an <b>unscoped</b> operation reaching a <b>tenanted</b> partition: one aggregate owned by a
/// tenant, operated on with an absent ambient tenant.
/// </para>
/// <para>
/// <b>Assertions are PROPERTY-based, never mechanism-based</b> (testing-patterns §3 corollary): the arms
/// assert the guarantee ("the tenant's payloads survive an unscoped erase"; "an unscoped IsErased returns
/// false"), so they hold under EITHER admissible fix — an unscoped branch bounded to the untenanted
/// partition, OR an unscoped erase that refuses at construction. They do NOT assert any particular SQL
/// predicate or throw.
/// </para>
/// <para>
/// <b>Both arms per direction (testing-patterns §3):</b> each SAFETY arm (the out-of-partition operation
/// is refused) is paired with a LIVENESS arm (the in-partition operation still works) — without liveness,
/// a store that erased nothing, or reported nothing erased, would satisfy safety alone.
/// </para>
/// <para>
/// <b>RED-first status</b> (independent of the fix; the lock lands before the fix and turns green when it
/// lands): the SAFETY arms are RED against committed HEAD. The over-erasure safety arm goes GREEN when
/// <c>EraseEventsRequest</c>'s unscoped branch stops reaching tenanted rows (<c>18c3el</c>); the
/// under-erasure safety arm goes GREEN when <c>IsErasedRequest</c>'s predicate becomes unconditional
/// (<c>chvpym</c>). The LIVENESS arms are GREEN now and stay GREEN — they fail only if a "fix" makes erase
/// a no-op.
/// </para>
/// <para>
/// <b>verify-against-real-infra-not-mock:</b> runs against real Postgres (TestContainers); the payload
/// survival is read straight from the engine, bypassing the store's own read path, so a defect in the
/// store's interpretation cannot hide the result. NON-SKIPPED (<c>DockerAvailable.ShouldBeTrue</c>) — a
/// skip-gated compliance lock is the gap that lets the erase path ship unmeasured.
/// </para>
/// </remarks>
[Collection(PostgresEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Postgres")]
public sealed class PostgresEventStoreErasureTenantScopeShould
{
	private const string AggregateType = "Order";
	private const string OwningTenant = "tenant-B";

	private readonly PostgresEventStoreContainerFixture _fixture;

	public PostgresEventStoreErasureTenantScopeShould(PostgresEventStoreContainerFixture fixture) =>
		_fixture = fixture;

	private sealed class FixedTenant(string? tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => TenantId is not null;
	}

	private PostgresEventStore StoreFor(string? tenantId) =>
		new(
			NpgsqlDataSource.Create(_fixture.ConnectionString),
			NullLogger<PostgresEventStore>.Instance,
			schema: "public",
			table: _fixture.TableName,
			tenantContext: tenantId is null ? UntenantedTestTenantContext.Instance : (ITenantContext)new FixedTenant(tenantId));

[MessageName("Test.PostgresEventStoreErasureTenantScope.OrderPlaced")]
private sealed record OrderPlaced(string AggregateId, long Version) : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public IDictionary<string, object>? Metadata { get; init; }
	}

	/// <summary>
	/// Counts the rows for this aggregate+tenant whose <c>event_data</c> is still present (not tombstoned),
	/// read straight from the engine so the store's own read path cannot mask an erasure that reached the
	/// wrong partition.
	/// </summary>
	private async Task<int> SurvivingPayloadCountAsync(string aggregateId, string tenantId)
	{
		await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync().ConfigureAwait(false);

		// CA2100: the only interpolated element is the fixture's constant table name; the aggregate id and
		// tenant id — the sole values that could carry input — are bound parameters.
#pragma warning disable CA2100
		await using var command = new NpgsqlCommand(
			$"SELECT COUNT(*) FROM public.{_fixture.TableName} "
			+ "WHERE aggregate_id = @aggId AND tenant_id = @tenant AND event_data IS NOT NULL",
			connection);
#pragma warning restore CA2100
		_ = command.Parameters.AddWithValue("aggId", aggregateId);
		_ = command.Parameters.AddWithValue("tenant", tenantId);

		return (int)(long)(await command.ExecuteScalarAsync().ConfigureAwait(false))!;
	}

	[Fact]
	public async Task NotTombstoneATenantsPayloads_WhenAnUnscopedEraseRuns()
	{
		// 18c3el over-erasure — SAFETY. RED against committed HEAD (the empty unscoped predicate matches the
		// owning tenant's rows and NULLs them). GREEN once the unscoped erase no longer reaches tenanted
		// rows. Property-based: holds whether the fix bounds the predicate to the untenanted partition or
		// refuses the unscoped erase outright.
		_fixture.DockerAvailable.ShouldBeTrue(
			"GDPR erasure is a legal obligation and cross-partition erasure is a data-protection incident — "
			+ "this real-Postgres lock must never be skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var aggId = "agg-" + Guid.NewGuid().ToString("N");
		var owner = StoreFor(OwningTenant);

		_ = await owner.AppendAsync(
			aggId, AggregateType,
			new IDomainEvent[] { new OrderPlaced(aggId, 0), new OrderPlaced(aggId, 1) },
			-1, CancellationToken.None).ConfigureAwait(false);

		(await SurvivingPayloadCountAsync(aggId, OwningTenant).ConfigureAwait(false))
			.ShouldBe(2, "precondition: the owning tenant's two payloads exist before the unscoped erase");

		// The out-of-partition operation. An unscoped erase that refuses at construction is an admissible
		// fix, so a throw here is not a failure of the guarantee — the guarantee is asserted on the data.
		try
		{
			_ = await StoreFor(null)
				.EraseEventsAsync(aggId, AggregateType, Guid.NewGuid(), CancellationToken.None)
				.ConfigureAwait(false);
		}
		catch (Exception)
		{
			// refuse-at-construction is a valid fix mechanism; the property is verified on the rows below.
		}

		// SAFETY (property, not mechanism): an unscoped erase must not reach the owning tenant's partition.
		(await SurvivingPayloadCountAsync(aggId, OwningTenant).ConfigureAwait(false))
			.ShouldBe(
				2,
				"EXPECTED RED until the unscoped erase no longer reaches tenanted rows (tracked: 18c3el). An "
				+ "erase with no ambient tenant operates on the untenanted partition; it must NOT tombstone a "
				+ "tenant's events — that is a GDPR erasure of one subject destroying another partition's data");
	}

	[Fact]
	public async Task StillTombstoneTheOwningTenantsPayloads_WhenTheOwningTenantErases()
	{
		// 18c3el over-erasure — LIVENESS. GREEN now and after the fix. Fails only if a "fix" turns the
		// in-partition erase into a no-op. Proves the safety arm above is not satisfied by an inert erase.
		_fixture.DockerAvailable.ShouldBeTrue(
			"the in-partition erase must keep working — this real-Postgres lock must never be skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var aggId = "agg-" + Guid.NewGuid().ToString("N");
		var owner = StoreFor(OwningTenant);

		_ = await owner.AppendAsync(
			aggId, AggregateType,
			new IDomainEvent[] { new OrderPlaced(aggId, 0), new OrderPlaced(aggId, 1) },
			-1, CancellationToken.None).ConfigureAwait(false);

		var count = await owner
			.EraseEventsAsync(aggId, AggregateType, Guid.NewGuid(), CancellationToken.None)
			.ConfigureAwait(false);

		count.ShouldBe(2, "the owning tenant's erase reports both of its own events erased");

		(await SurvivingPayloadCountAsync(aggId, OwningTenant).ConfigureAwait(false))
			.ShouldBe(0, "the owning tenant's own payloads are actually tombstoned (erasure is not a no-op)");
	}

	[Fact]
	public async Task NotReportErased_ForAnUnscopedIsErasedAgainstATenantsTombstone()
	{
		// chvpym under-erasure — SAFETY. RED against committed HEAD (the empty unscoped predicate matches
		// the owning tenant's tombstone, so an unscoped IsErased returns true and a required erasure is
		// skipped as "already done"). GREEN once IsErasedRequest's predicate is unconditional (tracked:
		// chvpym). Property-based: asserts the returned value, not the SQL.
		_fixture.DockerAvailable.ShouldBeTrue(
			"under-erasure reported as success is a silent GDPR failure — this real-Postgres lock must never "
			+ "be skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var aggId = "agg-" + Guid.NewGuid().ToString("N");
		var owner = StoreFor(OwningTenant);

		_ = await owner.AppendAsync(
			aggId, AggregateType,
			new IDomainEvent[] { new OrderPlaced(aggId, 0) },
			-1, CancellationToken.None).ConfigureAwait(false);

		// The owning tenant erases its own aggregate, creating ITS tombstone (event_type = erased marker,
		// tenant_id = owning tenant).
		_ = await owner
			.EraseEventsAsync(aggId, AggregateType, Guid.NewGuid(), CancellationToken.None)
			.ConfigureAwait(false);

		(await owner.IsErasedAsync(aggId, AggregateType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeTrue("precondition: the owning tenant's own tombstone exists");

		// SAFETY: an unscoped existence check must not match another partition's tombstone. If it does, the
		// caller reads "already erased" and skips a required erasure — under-erasure reported as success.
		(await StoreFor(null).IsErasedAsync(aggId, AggregateType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeFalse(
				"EXPECTED RED until IsErasedRequest's predicate is unconditional (tracked: chvpym). An "
				+ "unscoped IsErased operates on the untenanted partition; it must NOT match a tenant's "
				+ "tombstone, or a required erasure is skipped and logged as already-done");
	}

	[Fact]
	public async Task StillReportErased_ForTheOwningTenantsOwnTombstone()
	{
		// chvpym under-erasure — LIVENESS. GREEN now and after the fix. Fails only if a "fix" makes the
		// in-partition existence check stop finding the tenant's own tombstone. Proves the safety arm above
		// is not satisfied by an IsErased that always returns false.
		_fixture.DockerAvailable.ShouldBeTrue(
			"the in-partition erasure existence check must keep working — this real-Postgres lock must never "
			+ "be skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var aggId = "agg-" + Guid.NewGuid().ToString("N");
		var owner = StoreFor(OwningTenant);

		_ = await owner.AppendAsync(
			aggId, AggregateType,
			new IDomainEvent[] { new OrderPlaced(aggId, 0) },
			-1, CancellationToken.None).ConfigureAwait(false);

		_ = await owner
			.EraseEventsAsync(aggId, AggregateType, Guid.NewGuid(), CancellationToken.None)
			.ConfigureAwait(false);

		(await owner.IsErasedAsync(aggId, AggregateType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeTrue("the owning tenant still observes its own aggregate's erasure state");
	}
}
