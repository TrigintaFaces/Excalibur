// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Oracle;

using Microsoft.Extensions.Logging.Abstractions;

using Oracle.ManagedDataAccess.Client;

using Shouldly;

using Xunit;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Author≠implementer RED-first real-Oracle lock for the GDPR-erasure tenant-scope class: an erase (or
/// erasure existence check) MUST NOT reach a partition outside the one it operates in. Binds two paired
/// GDPR failures that share one empty-branch predicate (beads <c>18c3el</c> over-erasure + <c>chvpym</c>
/// under-erasure). Oracle sibling of the Postgres lock; the defect ships identically in every store.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect:</b> both <c>EraseEventsRequest</c> and <c>IsErasedRequest</c> compute
/// <c>scope.IsScoped ? " AND TENANTID = :TenantId" : string.Empty</c>. The EMPTY (unscoped /
/// <see cref="TenantScope.None"/>) branch drops the tenant boundary, so an unscoped operation matches rows
/// across every partition rather than only its own. Reachable in production: erasure runs from a background
/// service with no ambient tenant.
/// </para>
/// <para>
/// <b>One tenant, not two:</b> the events table's UNIQUE key is
/// <c>(AGGREGATEID, AGGREGATETYPE, VERSION)</c> — tenant-agnostic — so two tenants cannot hold the same
/// aggregate id through the store. The reachable, testable harm is an unscoped operation reaching a
/// tenanted partition.
/// </para>
/// <para>
/// <b>Property-based, not mechanism-based</b> (testing-patterns §3 corollary): the arms assert the
/// guarantee, so they hold under either admissible fix (bound the unscoped branch to the untenanted
/// partition, OR refuse the unscoped erase). <b>Both arms per direction:</b> each SAFETY arm is paired with
/// a LIVENESS arm proving the in-partition operation still works.
/// </para>
/// <para>
/// <b>RED-first status:</b> the over-erasure safety arm goes GREEN when <c>EraseEventsRequest</c>'s unscoped
/// branch stops reaching tenanted rows (<c>18c3el</c>); the under-erasure safety arm goes GREEN when
/// <c>IsErasedRequest</c>'s predicate becomes unconditional (<c>chvpym</c>). The liveness arms are GREEN
/// now.
/// </para>
/// <para>
/// <b>verify-against-real-infra-not-mock:</b> real Oracle (TestContainers); payload survival read straight
/// from the engine, bypassing the store's read path. The raw reader binds by name
/// (<c>BindByName = true</c>) — ODP.NET binds positionally by default. NON-SKIPPED
/// (<c>DockerAvailable.ShouldBeTrue</c>).
/// </para>
/// </remarks>
[Collection(OracleEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Oracle")]
public sealed class OracleEventStoreErasureTenantScopeShould
{
	private const string AggregateType = "Order";
	private const string OwningTenant = "tenant-B";

	private readonly OracleEventStoreContainerFixture _fixture;

	public OracleEventStoreErasureTenantScopeShould(OracleEventStoreContainerFixture fixture) =>
		_fixture = fixture;

	private sealed class FixedTenant(string? tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => TenantId is not null;
	}

	private OracleEventStore StoreFor(string? tenantId) =>
		new(
			() => new OracleConnection(_fixture.ConnectionString),
			NullLogger<OracleEventStore>.Instance,
			schema: _fixture.Schema,
			table: _fixture.TableName,
			tenantContext: tenantId is null ? null : new FixedTenant(tenantId));

	private sealed record OrderPlaced(string AggregateId, long Version) : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public string EventType { get; init; } = nameof(OrderPlaced);
		public IDictionary<string, object>? Metadata { get; init; }
	}

	/// <summary>
	/// Counts the rows for this aggregate+tenant whose <c>EVENTDATA</c> is still present (not tombstoned),
	/// read straight from the engine so the store's own read path cannot mask an erasure that reached the
	/// wrong partition. Binds by name — ODP.NET is positional by default.
	/// </summary>
	private async Task<int> SurvivingPayloadCountAsync(string aggregateId, string tenantId)
	{
		await using var connection = new OracleConnection(_fixture.ConnectionString);
		await connection.OpenAsync().ConfigureAwait(false);

		// CA2100: the only interpolated element is the fixture's constant table name; the aggregate id and
		// tenant id — the sole values that could carry input — are bound parameters.
#pragma warning disable CA2100
		await using var command = new OracleCommand(
			$"SELECT COUNT(*) FROM {_fixture.TableName} "
			+ "WHERE AGGREGATEID = :aggId AND TENANTID = :tenant AND EVENTDATA IS NOT NULL",
			connection)
		{
			BindByName = true,
		};
#pragma warning restore CA2100
		_ = command.Parameters.Add(":aggId", aggregateId);
		_ = command.Parameters.Add(":tenant", tenantId);

		return Convert.ToInt32(
			await command.ExecuteScalarAsync().ConfigureAwait(false), CultureInfo.InvariantCulture);
	}

	[Fact]
	public async Task NotTombstoneATenantsPayloads_WhenAnUnscopedEraseRuns()
	{
		// 18c3el over-erasure — SAFETY. RED against committed HEAD (the empty unscoped predicate matches the
		// owning tenant's rows and NULLs them). GREEN once the unscoped erase no longer reaches tenanted rows.
		_fixture.DockerAvailable.ShouldBeTrue(
			"GDPR erasure is a legal obligation and cross-partition erasure is a data-protection incident — "
			+ "this real-Oracle lock must never be skipped");
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

		(await SurvivingPayloadCountAsync(aggId, OwningTenant).ConfigureAwait(false))
			.ShouldBe(
				2,
				"EXPECTED RED until the unscoped erase no longer reaches tenanted rows (tracked: 18c3el). An "
				+ "erase with no ambient tenant operates on the untenanted partition; it must NOT tombstone a "
				+ "tenant's events");
	}

	[Fact]
	public async Task StillTombstoneTheOwningTenantsPayloads_WhenTheOwningTenantErases()
	{
		// 18c3el over-erasure — LIVENESS. GREEN now and after the fix. Fails only if a "fix" turns the
		// in-partition erase into a no-op.
		_fixture.DockerAvailable.ShouldBeTrue(
			"the in-partition erase must keep working — this real-Oracle lock must never be skipped");
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
		// chvpym under-erasure — SAFETY. RED against committed HEAD (an unscoped IsErased matches the owning
		// tenant's tombstone → a required erasure is skipped as "already done"). GREEN once IsErasedRequest's
		// predicate is unconditional (tracked: chvpym).
		_fixture.DockerAvailable.ShouldBeTrue(
			"under-erasure reported as success is a silent GDPR failure — this real-Oracle lock must never be "
			+ "skipped");
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
			.ShouldBeTrue("precondition: the owning tenant's own tombstone exists");

		(await StoreFor(null).IsErasedAsync(aggId, AggregateType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeFalse(
				"EXPECTED RED until IsErasedRequest's predicate is unconditional (tracked: chvpym). An "
				+ "unscoped IsErased must NOT match a tenant's tombstone, or a required erasure is skipped and "
				+ "logged as already-done");
	}

	[Fact]
	public async Task StillReportErased_ForTheOwningTenantsOwnTombstone()
	{
		// chvpym under-erasure — LIVENESS. GREEN now and after the fix. Fails only if a "fix" makes the
		// in-partition existence check stop finding the tenant's own tombstone.
		_fixture.DockerAvailable.ShouldBeTrue(
			"the in-partition erasure existence check must keep working — this real-Oracle lock must never be "
			+ "skipped");
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
