// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.SqlServer;
using Excalibur.MultiTenancy;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Xunit;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Author≠implementer real-SQL-Server lock for the GDPR-erasure tenant-isolation guarantee: an unscoped
/// erase (or erasure existence check) MUST NOT reach a partition outside the one it operates in.
/// </summary>
/// <remarks>
/// <para>
/// <b>Design A (the settled model — 2g734k flip):</b> event-store erase tenant-isolation is enforced
/// STRUCTURALLY by the fail-closed guard at the composition layer, NOT by a bare-store predicate. The
/// production erase entry point always resolves the GUARDED <c>"default"</c> store (the
/// <c>TenantScopedEventStore</c> decorator); an unscoped erase through it calls <c>RequireTenant()</c>,
/// which throws before any <c>UPDATE</c>, so no tenant's rows are reached. A bare
/// <see cref="SqlServerEventStore"/> with no tenant context is the NON-multi-tenant single-partition store,
/// which has no cross-tenant rows to protect. The old model (<c>scope.IsScoped ? "…" : string.Empty</c> with
/// isolation carried by an <c>IS NULL</c>/predicate on the unscoped branch) was reverted by s7yc33 — erase
/// isolation moved to the guard, so the unscoped bare-store predicate is intentionally empty.
/// </para>
/// <para>
/// <b>SAFETY arms (guarded path):</b> an unscoped erase / IsErased resolved through the real DI-composed
/// GUARDED store fails closed (<see cref="TenantRequiredException"/>), leaving a tenant's payloads intact —
/// the structural cross-tenant-isolation guarantee, strengthened to fail-closed. <b>LIVENESS arms
/// (scoped):</b> a scoped erase / IsErased still operates on its own tenant's partition. Each SAFETY arm is
/// paired with its LIVENESS arm (testing-patterns §3).
/// </para>
/// <para>
/// This is the store-level companion of <c>SqlServerEventStoreErasureMtFailClosedShould</c> (which resolves
/// the same guarded seam through the erasure contributor). <b>verify-against-real-infra-not-mock:</b> real
/// SQL Server (TestContainers); payload survival read straight from the engine. NON-SKIPPED
/// (<c>DockerAvailable.ShouldBeTrue</c>).
/// </para>
/// </remarks>
[Collection(SqlServerEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerEventStoreErasureTenantScopeShould
{
	private const string AggregateType = "Order";
	private const string OwningTenant = "tenant-B";

	private readonly SqlServerEventStoreContainerFixture _fixture;

	public SqlServerEventStoreErasureTenantScopeShould(SqlServerEventStoreContainerFixture fixture) =>
		_fixture = fixture;

	private sealed class FixedTenant(string? tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => TenantId is not null;
	}

	private SqlServerEventStore StoreFor(string? tenantId) =>
		new(
			() => _fixture.CreateConnection(),
			NullLogger<SqlServerEventStore>.Instance,
			schema: _fixture.SchemaName,
			table: _fixture.TableName,
			tenantContext: tenantId is null ? null : new FixedTenant(tenantId));

	// Resolves the GUARDED "default" event store through the real DI composition (row-discriminator MT) —
	// exactly the seam the erasure contributor resolves. TenantScopedEventStore wraps the store, so an
	// unscoped erase/IsErased fails closed via RequireTenant. This is the PRODUCTION erase entry point; a
	// bare direct-constructed SqlServerEventStore bypasses it (S873-vacuous), which is why the SAFETY arms
	// bind the guarded seam and not the bare store.
	private ServiceProvider BuildGuardedProvider()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSqlServerEventStore(
			() => _fixture.CreateConnection(), _fixture.SchemaName, _fixture.TableName);
		_ = services.AddMultiTenancy(o => o.Strategy = TenantIsolationStrategy.RowDiscriminator);
		return services.BuildServiceProvider();
	}

	private static IEventStoreErasure GuardedErasure(ServiceProvider provider) =>
		(IEventStoreErasure)provider.GetRequiredKeyedService<IEventStore>("default");

	private sealed record OrderPlaced(string AggregateId, long Version) : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public string EventType { get; init; } = nameof(OrderPlaced);
		public IDictionary<string, object>? Metadata { get; init; }
	}

	/// <summary>
	/// Counts the rows for this aggregate+tenant whose <c>EventData</c> is still present (not tombstoned),
	/// read straight from the engine so the store's own read path cannot mask an erasure that reached the
	/// wrong partition.
	/// </summary>
	private async Task<int> SurvivingPayloadCountAsync(string aggregateId, string tenantId)
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		// CA2100: the only interpolated elements are the fixture's constant schema/table names; the aggregate
		// id and tenant id — the sole values that could carry input — are bound parameters.
#pragma warning disable CA2100
		await using var command = new SqlCommand(
			$"SELECT COUNT(*) FROM [{_fixture.SchemaName}].[{_fixture.TableName}] "
			+ "WHERE AggregateId = @aggId AND TenantId = @tenant AND EventData IS NOT NULL",
			connection);
#pragma warning restore CA2100
		_ = command.Parameters.AddWithValue("@aggId", aggregateId);
		_ = command.Parameters.AddWithValue("@tenant", tenantId);

		return Convert.ToInt32(
			await command.ExecuteScalarAsync().ConfigureAwait(false), CultureInfo.InvariantCulture);
	}

	[Fact]
	public async Task RejectAnUnscopedErase_LeavingATenantsPayloadsIntact_ViaTheGuardedStore()
	{
		// 18c3el over-erase → Design A (2g734k, SA-ruled STALE). Cross-tenant over-erase is prevented
		// STRUCTURALLY by the fail-closed guard (TenantScopedEventStore.RequireTenant), NOT the reverted
		// bare-store predicate (s7yc33). The production erase entry point resolves the GUARDED "default"; an
		// unscoped erase through it throws BEFORE any UPDATE, so a tenant's payloads survive. Strengthened to
		// fail-closed — it NEVER certifies bare over-erase.
		_fixture.DockerAvailable.ShouldBeTrue(
			"GDPR erasure is a legal obligation and cross-partition erasure is a data-protection incident — "
			+ "this real-SQL-Server lock must never be skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var aggId = "agg-" + Guid.NewGuid().ToString("N");
		_ = await StoreFor(OwningTenant).AppendAsync(
			aggId, AggregateType,
			new IDomainEvent[] { new OrderPlaced(aggId, 0), new OrderPlaced(aggId, 1) },
			-1, CancellationToken.None).ConfigureAwait(false);

		(await SurvivingPayloadCountAsync(aggId, OwningTenant).ConfigureAwait(false))
			.ShouldBe(2, "precondition: the owning tenant's two payloads exist before the unscoped erase attempt");

		await using var provider = BuildGuardedProvider();
		var guarded = GuardedErasure(provider);

		// Unscoped erase through the GUARDED store: no ambient tenant → RequireTenant() throws before any UPDATE.
		_ = await Should.ThrowAsync<TenantRequiredException>(() =>
			guarded.EraseEventsAsync(aggId, AggregateType, Guid.NewGuid(), CancellationToken.None))
			.ConfigureAwait(false);

		(await SurvivingPayloadCountAsync(aggId, OwningTenant).ConfigureAwait(false))
			.ShouldBe(2, "the guarded unscoped erase failed closed → NO rows mutated on any tenant");
	}

	[Fact]
	public async Task StillTombstoneTheOwningTenantsPayloads_WhenTheOwningTenantErases()
	{
		// 18c3el over-erasure — LIVENESS. GREEN now and after the fix. Fails only if a "fix" turns the
		// in-partition erase into a no-op.
		_fixture.DockerAvailable.ShouldBeTrue(
			"the in-partition erase must keep working — this real-SQL-Server lock must never be skipped");
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
	public async Task RejectAnUnscopedIsErased_NotMatchingATenantsTombstone_ViaTheGuardedStore()
	{
		// chvpym under-erase → Design A (2g734k, SA-ruled STALE). An unscoped IsErased through the GUARDED
		// store fails closed (RequireTenant throws), so it can NEVER falsely report a tenant's aggregate as
		// already-erased and skip a required erasure. Strengthened to fail-closed.
		_fixture.DockerAvailable.ShouldBeTrue(
			"under-erasure reported as success is a silent GDPR failure — this real-SQL-Server lock must never "
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
			.ShouldBeTrue("precondition: the owning tenant's own tombstone exists");

		await using var provider = BuildGuardedProvider();
		var guarded = GuardedErasure(provider);

		// Unscoped IsErased through the GUARDED store fails closed (no ambient tenant → RequireTenant throws).
		_ = await Should.ThrowAsync<TenantRequiredException>(() =>
			guarded.IsErasedAsync(aggId, AggregateType, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task StillReportErased_ForTheOwningTenantsOwnTombstone()
	{
		// chvpym under-erasure — LIVENESS. GREEN now and after the fix. Fails only if a "fix" makes the
		// in-partition existence check stop finding the tenant's own tombstone.
		_fixture.DockerAvailable.ShouldBeTrue(
			"the in-partition erasure existence check must keep working — this real-SQL-Server lock must never "
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
