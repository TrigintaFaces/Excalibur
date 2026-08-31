// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Postgres;
using Excalibur.Integration.Tests.Data.Outbox;

using Microsoft.Extensions.Logging.Abstractions;

using Npgsql;

using Shouldly;

using Xunit;

namespace Excalibur.Integration.Tests.EventSourcing.Postgres;

/// <summary>
/// Binds the requirement that two tenants projecting the same named view keep separate view rows and
/// separate checkpoints, so neither can overwrite, read, or advance the other's.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two distinct defects, and the second is the worse one.</b> Keyed on (view_name, view_id) alone, two
/// tenants projecting the same named view addressed ONE row: the upsert has no guard on the view arm, so
/// the later writer silently replaced the earlier one's data and a read returned whichever tenant wrote
/// last. That is a disclosure. The position table was keyed on view_name alone, holding ONE checkpoint for
/// every tenant, so one tenant's progress advanced another's and that tenant's projector SKIPPED every
/// event in between — silently, with no error raised. That is data loss, and the monotonic guard makes it
/// permanent: the guard exists to stop the checkpoint moving backwards, so the skipped range can never be
/// re-read.
/// </para>
/// <para>
/// <b>verify-against-real-infra-not-mock:</b> real Postgres (TestContainers), NON-SKIPPED. The property
/// under test is enforced by the primary key and the ON CONFLICT target, both of which are evaluated by
/// the engine and not by any code a mock could stand in for. A mocked connection would certify this store
/// isolated while the real one shared a row.
/// </para>
/// <para>
/// <b>The tenant term is AMBIENT, not a parameter.</b> <c>IMaterializedViewStore</c> is unchanged: the
/// identity is injected at construction and resolved per call, so a caller can neither widen a lookup by
/// omitting a tenant nor redirect it by naming another. The two stores below differ ONLY in their tenant
/// context; every other argument is identical, which is what makes the partition the sole variable.
/// </para>
/// <para>
/// <b>Liveness is not optional here.</b> A store that returns nothing to anybody satisfies every safety
/// arm in this file. Each safety assertion is therefore paired with the corresponding liveness assertion —
/// that tenant A still reads back its own view, and its own checkpoint, unchanged.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Postgres")]
public sealed class PostgresMaterializedViewTenantIsolationShould : IClassFixture<PostgresOutboxStoreContainerFixture>, IDisposable
{
	private readonly PostgresOutboxStoreContainerFixture _fixture;
	private NpgsqlDataSource? _dataSource;

	public PostgresMaterializedViewTenantIsolationShould(PostgresOutboxStoreContainerFixture fixture) => _fixture = fixture;

	private sealed record CounterView(int Count);

	private NpgsqlDataSource DataSource => _dataSource ??= NpgsqlDataSource.Create(_fixture.ConnectionString);

	private PostgresMaterializedViewStore StoreFor(string tenantId) =>
		new(DataSource, NullLogger<PostgresMaterializedViewStore>.Instance, new FixedTenantContext(tenantId));

	private async Task ReadySchemaAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"cross-tenant read-model overwrite and silent projection gaps are data-integrity safety controls — "
			+ "this real-Postgres lock must never be skipped");

		await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync().ConfigureAwait(false);
		await using var cmd = connection.CreateCommand();

		// The tenant term leads both primary keys. This DDL mirrors the shape the shipped migration
		// produces; if the two drift, the store's ON CONFLICT target no longer matches a real constraint
		// and every save throws rather than silently misbehaving.
		cmd.CommandText = """
			CREATE TABLE IF NOT EXISTS materialized_views (
				tenant_id  TEXT NOT NULL,
				view_name  TEXT NOT NULL,
				view_id    TEXT NOT NULL,
				data       JSONB NOT NULL,
				created_at TIMESTAMPTZ NOT NULL,
				updated_at TIMESTAMPTZ NOT NULL,
				PRIMARY KEY (tenant_id, view_name, view_id)
			);
			CREATE TABLE IF NOT EXISTS materialized_view_positions (
				tenant_id  TEXT NOT NULL,
				view_name  TEXT NOT NULL,
				position   BIGINT NOT NULL,
				created_at TIMESTAMPTZ NOT NULL,
				updated_at TIMESTAMPTZ NOT NULL,
				PRIMARY KEY (tenant_id, view_name)
			);
			""";
		_ = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// SAFETY: one tenant's view must not be readable by, or overwritable by, another tenant of the same
	/// named view. LIVENESS, in the same test: tenant A must still read back its own view unchanged after
	/// tenant B has written to the same (view_name, view_id).
	/// </summary>
	[Fact]
	public async Task NotLetOneTenantOverwriteOrReadAnotherTenantsView()
	{
		await ReadySchemaAsync().ConfigureAwait(false);

		var viewName = "tenancy_" + Guid.NewGuid().ToString("N");
		const string ViewId = "agg-1";

		var tenantA = StoreFor("tenant-a");
		var tenantB = StoreFor("tenant-b");

		await tenantA.SaveAsync(viewName, ViewId, new CounterView(11), CancellationToken.None).ConfigureAwait(false);
		await tenantB.SaveAsync(viewName, ViewId, new CounterView(22), CancellationToken.None).ConfigureAwait(false);

		// SAFETY: B's write did not land on A's row.
		var readByA = await tenantA.GetAsync<CounterView>(viewName, ViewId, CancellationToken.None).ConfigureAwait(false);
		readByA!.Count.ShouldBe(
			11,
			"tenant A must read its own view; reading 22 means tenant B's save overwrote a row it does not own");

		// LIVENESS: both tenants can read, and each reads its own. A store that returned null to everyone
		// would pass the assertion above and be useless.
		var readByB = await tenantB.GetAsync<CounterView>(viewName, ViewId, CancellationToken.None).ConfigureAwait(false);
		readByB!.Count.ShouldBe(22, "tenant B must read its own view rather than nothing at all");

		// SAFETY: a tenant that has written nothing under this name sees nothing, rather than inheriting a row.
		var tenantC = StoreFor("tenant-c");
		var readByC = await tenantC.GetAsync<CounterView>(viewName, ViewId, CancellationToken.None).ConfigureAwait(false);
		readByC.ShouldBeNull("tenant C wrote no view under this name and must not be shown another tenant's");
	}

	/// <summary>
	/// SAFETY: one tenant's checkpoint must not advance another's — the silent-projection-gap defect.
	/// LIVENESS, in the same test: each tenant's own checkpoint is readable and still advances.
	/// </summary>
	/// <remarks>
	/// This is the arm that distinguishes a disclosure from data loss. Tenant B has projected nothing. If it
	/// reads tenant A's position of 500, its projector resumes from 500 and never processes events 1..500 —
	/// no exception, no log, just a read model that is permanently missing rows. The monotonic guard on the
	/// checkpoint means B cannot rewind to recover them either.
	/// </remarks>
	[Fact]
	public async Task NotLetOneTenantsProgressAdvanceAnotherTenantsCheckpoint()
	{
		await ReadySchemaAsync().ConfigureAwait(false);

		var viewName = "tenancy_pos_" + Guid.NewGuid().ToString("N");

		var tenantA = StoreFor("tenant-a");
		var tenantB = StoreFor("tenant-b");

		await tenantA.SavePositionAsync(viewName, 500, CancellationToken.None).ConfigureAwait(false);

		// SAFETY: B has projected nothing, so it must have no checkpoint at all.
		var positionOfB = await tenantB.GetPositionAsync(viewName, CancellationToken.None).ConfigureAwait(false);
		positionOfB.ShouldBeNull(
			"tenant B has projected nothing; inheriting tenant A's position of 500 makes B's projector skip "
			+ "every event below 500, permanently and with no error raised");

		// LIVENESS: A's own checkpoint is readable, so the isolation is not achieved by hiding everything.
		var positionOfA = await tenantA.GetPositionAsync(viewName, CancellationToken.None).ConfigureAwait(false);
		positionOfA.ShouldBe(500, "tenant A must still read the checkpoint it wrote");

		// LIVENESS: B can hold its own, lower checkpoint at the same time. This is the assertion that fails
		// if a fix were attempted by dropping the view name from the key instead of adding the tenant.
		await tenantB.SavePositionAsync(viewName, 7, CancellationToken.None).ConfigureAwait(false);
		(await tenantB.GetPositionAsync(viewName, CancellationToken.None).ConfigureAwait(false))
			.ShouldBe(7, "tenant B must keep its own checkpoint independently of tenant A's");

		// SAFETY: and B's lower checkpoint did not drag A's backwards.
		(await tenantA.GetPositionAsync(viewName, CancellationToken.None).ConfigureAwait(false))
			.ShouldBe(500, "tenant B writing a lower position must not rewind tenant A's checkpoint");
	}

	/// <summary>
	/// SAFETY: a delete removes only the caller's row. LIVENESS: the caller's own row really is gone, so the
	/// isolation is not achieved by making delete a no-op.
	/// </summary>
	[Fact]
	public async Task DeleteOnlyTheCallersOwnViewRow()
	{
		await ReadySchemaAsync().ConfigureAwait(false);

		var viewName = "tenancy_del_" + Guid.NewGuid().ToString("N");
		const string ViewId = "agg-1";

		var tenantA = StoreFor("tenant-a");
		var tenantB = StoreFor("tenant-b");

		await tenantA.SaveAsync(viewName, ViewId, new CounterView(11), CancellationToken.None).ConfigureAwait(false);
		await tenantB.SaveAsync(viewName, ViewId, new CounterView(22), CancellationToken.None).ConfigureAwait(false);

		await tenantB.DeleteAsync(viewName, ViewId, CancellationToken.None).ConfigureAwait(false);

		// LIVENESS: the delete did something.
		(await tenantB.GetAsync<CounterView>(viewName, ViewId, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeNull("tenant B deleted its own view and must no longer read it");

		// SAFETY: and it did not do it to anyone else.
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
		await ReadySchemaAsync().ConfigureAwait(false);

		var viewName = "tenancy_atomic_" + Guid.NewGuid().ToString("N");
		const string ViewId = "agg-1";

		var tenantA = StoreFor("tenant-a");
		var tenantB = StoreFor("tenant-b");

		await tenantA.SaveViewAndPositionAsync(viewName, ViewId, new CounterView(11), 500, CancellationToken.None)
			.ConfigureAwait(false);
		await tenantB.SaveViewAndPositionAsync(viewName, ViewId, new CounterView(22), 7, CancellationToken.None)
			.ConfigureAwait(false);

		// LIVENESS: each tenant reads back exactly what it wrote, through both halves of the atomic write.
		(await tenantA.GetAsync<CounterView>(viewName, ViewId, CancellationToken.None).ConfigureAwait(false))!
			.Count.ShouldBe(11, "tenant A must read the view its atomic write persisted");
		(await tenantA.GetPositionAsync(viewName, CancellationToken.None).ConfigureAwait(false))
			.ShouldBe(500, "tenant A must read the checkpoint its atomic write persisted");

		// SAFETY: B's lower position did not overwrite A's higher one, and B's view did not replace A's.
		(await tenantB.GetAsync<CounterView>(viewName, ViewId, CancellationToken.None).ConfigureAwait(false))!
			.Count.ShouldBe(22, "tenant B must read its own view rather than tenant A's");
		(await tenantB.GetPositionAsync(viewName, CancellationToken.None).ConfigureAwait(false))
			.ShouldBe(7, "tenant B must keep its own checkpoint rather than inheriting tenant A's 500");
	}

	public void Dispose() => _dataSource?.Dispose();

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
