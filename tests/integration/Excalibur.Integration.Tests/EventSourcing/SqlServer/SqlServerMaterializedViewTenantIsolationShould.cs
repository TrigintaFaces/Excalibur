// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.SqlServer;
using Excalibur.Integration.Tests.Data.Outbox;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Xunit;

namespace Excalibur.Integration.Tests.EventSourcing.SqlServer;

/// <summary>
/// The SQL Server sibling of the Postgres materialized-view tenant-isolation lock: two tenants projecting
/// the same named view keep separate view rows and separate checkpoints.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two distinct defects, and the second is the worse one.</b> Keyed on (ViewName, ViewId) alone, two
/// tenants projecting the same named view addressed ONE row: the MERGE matched on that pair, so the later
/// writer silently replaced the earlier one's data and a read returned whichever tenant wrote last. That is
/// a disclosure. The position table was keyed on ViewName alone, holding ONE checkpoint for every tenant,
/// so one tenant's progress advanced another's and that tenant's projector SKIPPED every event in between —
/// silently, with no error raised. That is data loss, and the monotonic MERGE guard makes it permanent.
/// </para>
/// <para>
/// <b>The MERGE match is the seam here, and it is why this cannot be a unit test.</b> Whether
/// <c>ON target.TenantId = source.TenantId AND ...</c> selects one row or another is decided by the engine.
/// A mocked connection would certify this store isolated while the real one merged across tenants.
/// NON-SKIPPED for that reason.
/// </para>
/// <para>
/// <b>This lock also covers the schema.</b> It calls the store's own <c>EnsureSchemaAsync</c> rather than
/// creating tables itself, so a DDL that drifted from the statements — a tenant column the emitted MERGE
/// binds but the table lacks — fails here rather than in a consumer's database.
/// </para>
/// <para>
/// <b>Liveness is not optional here.</b> A store that returns nothing to anybody satisfies every safety arm
/// in this file. Each safety assertion is therefore paired with the corresponding liveness assertion.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerMaterializedViewTenantIsolationShould : IClassFixture<SqlServerOutboxStoreContainerFixture>
{
	private readonly SqlServerOutboxStoreContainerFixture _fixture;

	public SqlServerMaterializedViewTenantIsolationShould(SqlServerOutboxStoreContainerFixture fixture) => _fixture = fixture;

	private sealed record CounterView(int Count);

	private SqlServerMaterializedViewStore StoreFor(string tenantId) =>
		new(
			() => new SqlConnection(_fixture.ConnectionString),
			NullLogger<SqlServerMaterializedViewStore>.Instance,
			new FixedTenantContext(tenantId));

	private async Task<(SqlServerMaterializedViewStore TenantA, SqlServerMaterializedViewStore TenantB)> ReadyStoresAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"cross-tenant read-model overwrite and silent projection gaps are data-integrity safety controls — "
			+ "this real-SqlServer lock must never be skipped");

		var tenantA = StoreFor("tenant-a");
		await tenantA.EnsureSchemaAsync(CancellationToken.None).ConfigureAwait(false);
		return (tenantA, StoreFor("tenant-b"));
	}

	/// <summary>
	/// SAFETY: the MERGE must not match another tenant's row. LIVENESS: each tenant still reads its own.
	/// </summary>
	[Fact]
	public async Task NotLetOneTenantOverwriteOrReadAnotherTenantsView()
	{
		var (tenantA, tenantB) = await ReadyStoresAsync().ConfigureAwait(false);

		var viewName = "tenancy_" + Guid.NewGuid().ToString("N");
		const string ViewId = "agg-1";

		await tenantA.SaveAsync(viewName, ViewId, new CounterView(11), CancellationToken.None).ConfigureAwait(false);
		await tenantB.SaveAsync(viewName, ViewId, new CounterView(22), CancellationToken.None).ConfigureAwait(false);

		var readByA = await tenantA.GetAsync<CounterView>(viewName, ViewId, CancellationToken.None).ConfigureAwait(false);
		readByA!.Count.ShouldBe(
			11,
			"tenant A must read its own view; reading 22 means tenant B's MERGE matched a row it does not own");

		var readByB = await tenantB.GetAsync<CounterView>(viewName, ViewId, CancellationToken.None).ConfigureAwait(false);
		readByB!.Count.ShouldBe(22, "tenant B must read its own view rather than nothing at all");

		var readByC = await StoreFor("tenant-c")
			.GetAsync<CounterView>(viewName, ViewId, CancellationToken.None).ConfigureAwait(false);
		readByC.ShouldBeNull("tenant C wrote no view under this name and must not be shown another tenant's");
	}

	/// <summary>
	/// SAFETY: one tenant's checkpoint must not advance another's — the silent-projection-gap defect.
	/// LIVENESS: each tenant's own checkpoint is readable and still advances.
	/// </summary>
	[Fact]
	public async Task NotLetOneTenantsProgressAdvanceAnotherTenantsCheckpoint()
	{
		var (tenantA, tenantB) = await ReadyStoresAsync().ConfigureAwait(false);

		var viewName = "tenancy_pos_" + Guid.NewGuid().ToString("N");

		await tenantA.SavePositionAsync(viewName, 500, CancellationToken.None).ConfigureAwait(false);

		var positionOfB = await tenantB.GetPositionAsync(viewName, CancellationToken.None).ConfigureAwait(false);
		positionOfB.ShouldBeNull(
			"tenant B has projected nothing; inheriting tenant A's position of 500 makes B's projector skip "
			+ "every event below 500, permanently and with no error raised");

		(await tenantA.GetPositionAsync(viewName, CancellationToken.None).ConfigureAwait(false))
			.ShouldBe(500, "tenant A must still read the checkpoint it wrote");

		await tenantB.SavePositionAsync(viewName, 7, CancellationToken.None).ConfigureAwait(false);
		(await tenantB.GetPositionAsync(viewName, CancellationToken.None).ConfigureAwait(false))
			.ShouldBe(7, "tenant B must keep its own checkpoint independently of tenant A's");

		(await tenantA.GetPositionAsync(viewName, CancellationToken.None).ConfigureAwait(false))
			.ShouldBe(500, "tenant B writing a lower position must not rewind tenant A's checkpoint");
	}

	/// <summary>
	/// SAFETY: a delete removes only the caller's row. LIVENESS: the caller's own row really is gone.
	/// </summary>
	[Fact]
	public async Task DeleteOnlyTheCallersOwnViewRow()
	{
		var (tenantA, tenantB) = await ReadyStoresAsync().ConfigureAwait(false);

		var viewName = "tenancy_del_" + Guid.NewGuid().ToString("N");
		const string ViewId = "agg-1";

		await tenantA.SaveAsync(viewName, ViewId, new CounterView(11), CancellationToken.None).ConfigureAwait(false);
		await tenantB.SaveAsync(viewName, ViewId, new CounterView(22), CancellationToken.None).ConfigureAwait(false);

		await tenantB.DeleteAsync(viewName, ViewId, CancellationToken.None).ConfigureAwait(false);

		(await tenantB.GetAsync<CounterView>(viewName, ViewId, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeNull("tenant B deleted its own view and must no longer read it");

		var survivingA = await tenantA.GetAsync<CounterView>(viewName, ViewId, CancellationToken.None).ConfigureAwait(false);
		survivingA!.Count.ShouldBe(11, "tenant B's delete must not remove tenant A's row");
	}

	/// <summary>
	/// SAFETY + LIVENESS across the atomic write path, which is a second writer of both rows and therefore
	/// must carry the same partition. A guarantee held by only one of a state's writers is not held.
	/// </summary>
	[Fact]
	public async Task PartitionTheAtomicViewAndPositionWriteToo()
	{
		var (tenantA, tenantB) = await ReadyStoresAsync().ConfigureAwait(false);

		var viewName = "tenancy_atomic_" + Guid.NewGuid().ToString("N");
		const string ViewId = "agg-1";

		await tenantA.SaveViewAndPositionAsync(viewName, ViewId, new CounterView(11), 500, CancellationToken.None)
			.ConfigureAwait(false);
		await tenantB.SaveViewAndPositionAsync(viewName, ViewId, new CounterView(22), 7, CancellationToken.None)
			.ConfigureAwait(false);

		(await tenantA.GetAsync<CounterView>(viewName, ViewId, CancellationToken.None).ConfigureAwait(false))!
			.Count.ShouldBe(11, "tenant A must read the view its atomic write persisted");
		(await tenantA.GetPositionAsync(viewName, CancellationToken.None).ConfigureAwait(false))
			.ShouldBe(500, "tenant A must read the checkpoint its atomic write persisted");

		(await tenantB.GetAsync<CounterView>(viewName, ViewId, CancellationToken.None).ConfigureAwait(false))!
			.Count.ShouldBe(22, "tenant B must read its own view rather than tenant A's");
		(await tenantB.GetPositionAsync(viewName, CancellationToken.None).ConfigureAwait(false))
			.ShouldBe(7, "tenant B must keep its own checkpoint rather than inheriting tenant A's 500");
	}

	/// <summary>
	/// A tenant context fixed to one identity. Two stores differing ONLY in this are what make the tenant
	/// partition the sole variable across the arms above.
	/// </summary>
	private sealed class FixedTenantContext(string tenantId) : ITenantContext
	{
		public string? TenantId => tenantId;

		public bool HasTenant => true;
	}
}
