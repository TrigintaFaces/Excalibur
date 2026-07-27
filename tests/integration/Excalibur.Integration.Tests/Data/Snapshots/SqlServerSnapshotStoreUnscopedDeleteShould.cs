// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Excalibur.Dispatch;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Xunit;

#pragma warning disable CA1812 // Internal record is never instantiated (constructed via CreateSnapshot)

namespace Excalibur.Integration.Tests.Data.Snapshots;

/// <summary>
/// Author≠implementer RED-first real-SQL-Server lock (bead <c>18c3el</c>): an UNSCOPED destructive delete on
/// <c>SqlServerSnapshotStore</c> must NOT span tenants. Sibling of the Postgres/Oracle destructive-delete
/// arms; the empty-branch predicate ships in every provider.
/// </summary>
/// <remarks>
/// <para>
/// <b>GREEN parity regression guard.</b> SQL Server's <c>DeleteSnapshotsRequest</c> /
/// <c>DeleteSnapshotsOlderThanRequest</c> predicate is ALREADY unconditional
/// (<c>AND TenantId = @TenantId</c>, with the term routed through <c>KeyedTenantPartition</c> so the
/// unscoped path binds the reserved <c>__untenanted__</c> sentinel rather than an empty string), so this
/// provider never had the empty-branch defect —
/// only the Postgres snapshot-delete did (the genuine RED-first lock). These arms guard against a regression
/// to the empty-branch form (<c>18c3el</c>), the same class as the GDPR erase.
/// </para>
/// <para>
/// <b>Property-based, not mechanism-based</b> (testing-patterns §3 corollary): the safety arm asserts the
/// owning tenant's snapshot SURVIVES an unscoped delete — true under whichever bounding the fix uses
/// (<c>TenantId IS NULL</c> or the <c>''</c> sentinel the snapshot table stores untenanted rows as), never
/// the SQL. <b>Both arms per operation (testing-patterns §3):</b> SAFETY (a tenant's snapshot survives an
/// unscoped delete) paired with LIVENESS (the owning tenant's own scoped delete still removes it).
/// </para>
/// <para>
/// <b>GREEN today (parity guard):</b> the safety arms pass at HEAD (the predicate is already unconditional);
/// they go RED only if the unscoped branch is ever regressed to the empty-branch form (<c>18c3el</c>).
/// <b>verify-against-real-infra-not-mock:</b> real SQL Server (TestContainers); survival is read back through
/// the store's own scoped read.
/// </para>
/// </remarks>
[Collection(SqlServerSnapshotStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerSnapshotStoreUnscopedDeleteShould
{
	private const string TenantB = "tenant-b";
	private const string AggregateType = "TestAggregate";

	private readonly SqlServerSnapshotStoreContainerFixture _fixture;

	public SqlServerSnapshotStoreUnscopedDeleteShould(SqlServerSnapshotStoreContainerFixture fixture) =>
		_fixture = fixture;

	private ISnapshotStore CreateStore(bool tenantScoped) =>
		new SqlServerSnapshotStore(
			() => _fixture.CreateConnection(),
			NullLogger<SqlServerSnapshotStore>.Instance,
			_fixture.SchemaName,
			_fixture.TableName,
			tenantScoped ? new AmbientHolderTenantContext() : null);

	private static ISnapshot CreateSnapshot(string aggregateId, long version, string data, string? tenantId) =>
		new SqlServerUnscopedDeleteSnapshot(
			Guid.NewGuid().ToString(),
			aggregateId,
			AggregateType,
			version,
			DateTimeOffset.UtcNow,
			Encoding.UTF8.GetBytes(data),
			null,
			tenantId);

	/// <summary>
	/// LIVENESS for the UNTENANTED partition: an unscoped delete must still remove the untenanted snapshot it
	/// owns.
	/// </summary>
	/// <remarks>
	/// Every other arm in this file asserts that something SURVIVES an unscoped delete, or that a SCOPED delete
	/// works. None of them asserts that the unscoped delete deletes anything at all — so a predicate regressed
	/// to a term that matches no row (binding NULL, or a sentinel the rows were never written with) would leave
	/// every existing arm GREEN while the single-tenant delete path silently stopped working. That is the exact
	/// shape this file's own header claims to guard: "an unscoped delete must not span tenants" is satisfied by
	/// one that deletes nothing, forever.
	/// <para>
	/// This is also the arm that binds the property the file previously only described in prose — the term the
	/// unscoped path actually uses. It is asserted by behaviour (the row this store owns is gone) rather than
	/// by SQL text, so it holds under whichever bounding the implementation chooses.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Still_Delete_Its_Own_Untenanted_Snapshot_On_An_Unscoped_DeleteSnapshots()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"the single-tenant delete path must keep working — this real-SQL-Server lock must never be skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);
		var aggregateId = Guid.NewGuid().ToString();

		// No ambient tenant: the single-tenant host shape, whose rows live in the untenanted partition.
		var unscopedStore = CreateStore(tenantScoped: false);
		await unscopedStore.SaveSnapshotAsync(
			CreateSnapshot(aggregateId, 1, "untenanted-data", tenantId: null), CancellationToken.None)
			.ConfigureAwait(false);

		(await unscopedStore.GetLatestSnapshotAsync(aggregateId, AggregateType, CancellationToken.None)
			.ConfigureAwait(false))
			.ShouldNotBeNull("the untenanted snapshot must exist before the delete, or this arm proves nothing.");

		await unscopedStore.DeleteSnapshotsAsync(aggregateId, AggregateType, CancellationToken.None)
			.ConfigureAwait(false);

		(await unscopedStore.GetLatestSnapshotAsync(aggregateId, AggregateType, CancellationToken.None)
			.ConfigureAwait(false))
			.ShouldBeNull(
				"an unscoped delete must remove the untenanted snapshot it owns — a tenant term that matches no "
				+ "row would satisfy every safety arm here while making the single-tenant delete path inert.");
	}

	/// <summary>SAFETY. An unscoped DeleteSnapshots must not remove a tenant's snapshot.</summary>
	[Fact]
	public async Task Not_Delete_A_Tenants_Snapshot_On_An_Unscoped_DeleteSnapshots()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"a cross-tenant destructive delete is a data-protection incident — this real-SQL-Server lock must "
			+ "never be skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);
		var aggregateId = Guid.NewGuid().ToString();

		var scopedStore = CreateStore(tenantScoped: true);
		using (TenantContextHolder.BeginScope(TenantB))
		{
			await scopedStore.SaveSnapshotAsync(
				CreateSnapshot(aggregateId, 1, "tenant-b-data", TenantB), CancellationToken.None).ConfigureAwait(false);
		}

		var unscopedStore = CreateStore(tenantScoped: false);
		await unscopedStore.DeleteSnapshotsAsync(aggregateId, AggregateType, CancellationToken.None).ConfigureAwait(false);

		ISnapshot? survivor;
		using (TenantContextHolder.BeginScope(TenantB))
		{
			survivor = await scopedStore.GetLatestSnapshotAsync(
				aggregateId, AggregateType, CancellationToken.None).ConfigureAwait(false);
		}

		_ = survivor.ShouldNotBeNull(
			"Regression guard (GREEN): SQL Server's unscoped DeleteSnapshots predicate is already unconditional "
			+ "(`AND TenantId = @TenantId`, unscoped binding the `__untenanted__` sentinel), so an unscoped "
			+ "delete never removed a tenant's snapshot. This arm goes RED if regressed to the empty-branch "
			+ "defect — the guarantee: an unscoped delete must NOT remove a tenant's snapshot");
	}

	/// <summary>LIVENESS. The owning tenant's own scoped DeleteSnapshots still removes its snapshot.</summary>
	[Fact]
	public async Task Still_Delete_The_Owning_Tenants_Snapshot_On_A_Scoped_DeleteSnapshots()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"the owning tenant's own delete must keep working — this real-SQL-Server lock must never be skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);
		var aggregateId = Guid.NewGuid().ToString();

		var scopedStore = CreateStore(tenantScoped: true);
		using (TenantContextHolder.BeginScope(TenantB))
		{
			await scopedStore.SaveSnapshotAsync(
				CreateSnapshot(aggregateId, 1, "tenant-b-data", TenantB), CancellationToken.None).ConfigureAwait(false);
			await scopedStore.DeleteSnapshotsAsync(aggregateId, AggregateType, CancellationToken.None).ConfigureAwait(false);

			var gone = await scopedStore.GetLatestSnapshotAsync(
				aggregateId, AggregateType, CancellationToken.None).ConfigureAwait(false);
			gone.ShouldBeNull("the owning tenant's scoped delete removes its own snapshot (delete is not a no-op)");
		}
	}

	/// <summary>SAFETY. An unscoped DeleteSnapshotsOlderThan must not remove a tenant's snapshot.</summary>
	[Fact]
	public async Task Not_Delete_A_Tenants_Snapshot_On_An_Unscoped_DeleteSnapshotsOlderThan()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"a cross-tenant destructive prune is a data-protection incident — this real-SQL-Server lock must "
			+ "never be skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);
		var aggregateId = Guid.NewGuid().ToString();

		var scopedStore = CreateStore(tenantScoped: true);
		using (TenantContextHolder.BeginScope(TenantB))
		{
			await scopedStore.SaveSnapshotAsync(
				CreateSnapshot(aggregateId, 3, "tenant-b-data", TenantB), CancellationToken.None).ConfigureAwait(false);
		}

		var unscopedStore = CreateStore(tenantScoped: false);
		await unscopedStore.DeleteSnapshotsOlderThanAsync(
			aggregateId, AggregateType, 10, CancellationToken.None).ConfigureAwait(false);

		ISnapshot? survivor;
		using (TenantContextHolder.BeginScope(TenantB))
		{
			survivor = await scopedStore.GetLatestSnapshotAsync(
				aggregateId, AggregateType, CancellationToken.None).ConfigureAwait(false);
		}

		_ = survivor.ShouldNotBeNull(
			"Regression guard (GREEN): SQL Server's unscoped DeleteSnapshotsOlderThan predicate is already "
			+ "unconditional (`AND TenantId = @TenantId`, unscoped binding the `__untenanted__` sentinel); this "
			+ "arm goes RED if regressed to the empty-branch defect — the guarantee: an unscoped prune must NOT "
			+ "remove a tenant's snapshot");
	}

	/// <summary>LIVENESS. The owning tenant's own scoped DeleteSnapshotsOlderThan still prunes its snapshot.</summary>
	[Fact]
	public async Task Still_Prune_The_Owning_Tenants_Snapshot_On_A_Scoped_DeleteSnapshotsOlderThan()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"the owning tenant's own prune must keep working — this real-SQL-Server lock must never be skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);
		var aggregateId = Guid.NewGuid().ToString();

		var scopedStore = CreateStore(tenantScoped: true);
		using (TenantContextHolder.BeginScope(TenantB))
		{
			await scopedStore.SaveSnapshotAsync(
				CreateSnapshot(aggregateId, 3, "tenant-b-data", TenantB), CancellationToken.None).ConfigureAwait(false);
			await scopedStore.DeleteSnapshotsOlderThanAsync(
				aggregateId, AggregateType, 10, CancellationToken.None).ConfigureAwait(false);

			var gone = await scopedStore.GetLatestSnapshotAsync(
				aggregateId, AggregateType, CancellationToken.None).ConfigureAwait(false);
			gone.ShouldBeNull("the owning tenant's scoped prune removes its own older snapshot (prune is not a no-op)");
		}
	}

	private sealed class AmbientHolderTenantContext : ITenantContext
	{
		public string? TenantId => TenantContextHolder.Current;

		public bool HasTenant => !string.IsNullOrEmpty(TenantContextHolder.Current);
	}

	private sealed record SqlServerUnscopedDeleteSnapshot(
		string SnapshotId,
		string AggregateId,
		string AggregateType,
		long Version,
		DateTimeOffset CreatedAt,
		ReadOnlyMemory<byte> Data,
		IDictionary<string, object>? Metadata,
		string? TenantId) : ISnapshot;
}
