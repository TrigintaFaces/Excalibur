// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Excalibur.Dispatch;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Sqlite;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Xunit;

#pragma warning disable CA1812 // Internal class is never instantiated

namespace Excalibur.Integration.Tests.Data.Snapshots;

/// <summary>
/// Binds the UNSCOPED read path of <see cref="SqliteSnapshotStore"/> — the half no conformance arm
/// can reach, and the half a real cross-tenant leak was found in.
/// </summary>
/// <remarks>
/// <para>
/// The snapshot conformance suite runs every arm inside an ambient tenant scope, so all of its
/// tenant-isolation arms exercise the SCOPED path. That is deliberate and it leaves the unscoped path
/// with no coverage — measured: the conformance suite returned 17 passed / 0 failed against a store
/// whose unscoped read matched any tenant's row. These arms exist because a green suite is not
/// evidence about a path the suite cannot execute.
/// </para>
/// <para>
/// The defect they bind: the write stores every row under <c>COALESCE(@TenantId, '')</c> and keys the
/// upsert <c>ON CONFLICT(AggregateId, AggregateType, TenantId)</c>, so a single-tenant row lives
/// <b>under</b> the empty-string sentinel rather than outside the key. A read whose tenant predicate is
/// conditional therefore emits <b>no</b> filter when unscoped, and matches whatever row exists for that
/// aggregate — including a different tenant's. Read and write must agree on the key; here that means
/// both are unconditional.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Sqlite")]
public sealed class SqliteSnapshotStoreUnscopedReadShould : IClassFixture<SqliteSnapshotStoreFixture>
{
	private const string TenantA = "tenant-a";
	private const string AggregateType = "TestAggregate";

	private readonly SqliteSnapshotStoreFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqliteSnapshotStoreUnscopedReadShould"/> class.
	/// </summary>
	/// <param name="fixture">The SQLite snapshot store fixture.</param>
	public SqliteSnapshotStoreUnscopedReadShould(SqliteSnapshotStoreFixture fixture)
	{
		_fixture = fixture;
	}

	/// <summary>
	/// SAFETY. A store with no tenant context must not read a row written under a tenant.
	/// </summary>
	/// <remarks>
	/// RED against a conditional read predicate (<c>scope.IsScoped ? " AND TenantId = @TenantId" :
	/// string.Empty</c>): unscoped emits no filter, so the tenant's row is returned to a caller that
	/// has no tenant at all. This is the arm the conformance suite structurally cannot provide.
	/// </remarks>
	[Fact]
	public async Task Not_Return_A_Tenants_Snapshot_To_An_Unscoped_Reader()
	{
		var aggregateId = Guid.NewGuid().ToString();

		var scopedStore = CreateStore(tenantScoped: true);
		using (TenantContextHolder.BeginScope(TenantA))
		{
			await scopedStore.SaveSnapshotAsync(
				CreateSnapshot(aggregateId, 1, "tenant-a-data", TenantA),
				CancellationToken.None).ConfigureAwait(false);
		}

		// A single-tenant deployment: no ambient context, no tenant on the request.
		var unscopedStore = CreateStore(tenantScoped: false);
		var leaked = await unscopedStore.GetLatestSnapshotAsync(
			aggregateId,
			AggregateType,
			CancellationToken.None).ConfigureAwait(false);

		leaked.ShouldBeNull(
			"an unscoped reader must not receive a row written under a tenant — the write puts the " +
			"tenant in the key, so the read must filter on it unconditionally or it matches any tenant");
	}

	/// <summary>
	/// LIVENESS. Without this, a store whose unscoped read returned nothing at all — single-tenant
	/// mode entirely broken — would satisfy the arm above and look correct.
	/// </summary>
	[Fact]
	public async Task Still_Read_Back_Its_Own_Row_When_Unscoped()
	{
		var aggregateId = Guid.NewGuid().ToString();
		var store = CreateStore(tenantScoped: false);

		await store.SaveSnapshotAsync(
			CreateSnapshot(aggregateId, 1, "unscoped-data", tenantId: null),
			CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.GetLatestSnapshotAsync(
			aggregateId,
			AggregateType,
			CancellationToken.None).ConfigureAwait(false);

		_ = loaded.ShouldNotBeNull(
			"the single-tenant path must still work; an unscoped read that returns nothing would " +
			"satisfy the isolation arm above while having removed the feature");
		Encoding.UTF8.GetString(loaded.Data.ToArray()).ShouldBe("unscoped-data");
	}

	/// <summary>
	/// SAFETY, the mirror direction. A tenant must not read a row written with no tenant.
	/// </summary>
	/// <remarks>
	/// The empty-string sentinel is a real key value, not an absence, so a scoped reader asking for
	/// <c>tenant-a</c> must not match it. Guards the fix from being "corrected" into a predicate that
	/// treats the sentinel as a wildcard.
	/// </remarks>
	[Fact]
	public async Task Not_Return_An_Unscoped_Row_To_A_Tenant_Reader()
	{
		var aggregateId = Guid.NewGuid().ToString();

		var unscopedStore = CreateStore(tenantScoped: false);
		await unscopedStore.SaveSnapshotAsync(
			CreateSnapshot(aggregateId, 1, "unscoped-data", tenantId: null),
			CancellationToken.None).ConfigureAwait(false);

		var scopedStore = CreateStore(tenantScoped: true);
		ISnapshot? leaked;
		using (TenantContextHolder.BeginScope(TenantA))
		{
			leaked = await scopedStore.GetLatestSnapshotAsync(
				aggregateId,
				AggregateType,
				CancellationToken.None).ConfigureAwait(false);
		}

		leaked.ShouldBeNull(
			"the empty-string tenant is a key value, not a wildcard — a scoped reader must not match it");
	}

	/// <summary>
	/// SAFETY, on the WRITE axis. One tenant's save must not overwrite another tenant's snapshot at the
	/// same aggregate id.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Every other arm in this file — and every tenant-isolation test in this repository — asserts what
	/// a READER receives. None asserts what a WRITER destroys. That gap was measured after a
	/// cross-tenant OVERWRITE was found in a sibling store: a save at another tenant's id point-read the
	/// victim's row for its version, the versions agreed, and the upsert replaced it.
	/// </para>
	/// <para>
	/// The two failures are not the same severity. A disclosure leaves the victim's data intact and is
	/// in principle detectable afterwards; an overwrite replaces the row, satisfies the concurrency
	/// check on the way past, and leaves no trace that it happened. <b>The read arms above cannot see
	/// this</b> — they would pass against a store that had already destroyed the data they are reading
	/// around.
	/// </para>
	/// <para>
	/// Expected GREEN here: this store keys its upsert <c>ON CONFLICT(AggregateId, AggregateType,
	/// TenantId)</c>, so the two tenants' rows are distinct by construction and neither save can reach
	/// the other. The arm exists so that stops being an accident of the current key.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Not_Let_One_Tenants_Save_Overwrite_Another_Tenants_Snapshot()
	{
		var sharedAggregateId = Guid.NewGuid().ToString();

		var store = CreateStore(tenantScoped: true);

		using (TenantContextHolder.BeginScope(TenantA))
		{
			await store.SaveSnapshotAsync(
				CreateSnapshot(sharedAggregateId, 1, "tenant-a-original", TenantA),
				CancellationToken.None).ConfigureAwait(false);
		}

		// A second tenant saves at the SAME aggregate id — the shape that destroyed data elsewhere.
		using (TenantContextHolder.BeginScope("tenant-b"))
		{
			await store.SaveSnapshotAsync(
				CreateSnapshot(sharedAggregateId, 1, "tenant-b-write", "tenant-b"),
				CancellationToken.None).ConfigureAwait(false);
		}

		ISnapshot? tenantAsRow;
		using (TenantContextHolder.BeginScope(TenantA))
		{
			tenantAsRow = await store.GetLatestSnapshotAsync(
				sharedAggregateId,
				AggregateType,
				CancellationToken.None).ConfigureAwait(false);
		}

		_ = tenantAsRow.ShouldNotBeNull(
			"tenant A's snapshot must still EXIST after tenant B saved at the same aggregate id");
		Encoding.UTF8.GetString(tenantAsRow.Data.ToArray()).ShouldBe(
			"tenant-a-original",
			"tenant A's snapshot must be UNCHANGED. If it now carries tenant B's payload, a foreign " +
			"write reached it — an overwrite, not a disclosure: A's data is gone, the concurrency check " +
			"was satisfied on the way past, and nothing recorded that it happened");
	}

	/// <summary>
	/// REACHABILITY. Proves the upsert path the overwrite arm depends on is actually taken by this
	/// fixture state — without mutating anything.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The overwrite arm asserts that a foreign write does not reach the victim's row. That assertion is
	/// worthless if <b>no write ever reaches any existing row</b> from this fixture — the arm would pass
	/// because the conflict path is never entered, not because the tenant discriminated.
	/// </para>
	/// <para>
	/// The liveness twin below does NOT establish this: with the tenant in the conflict key, the second
	/// tenant's save is a fresh INSERT and never touches the upsert branch at all. It proves both
	/// tenants can write, which is a different claim.
	/// </para>
	/// <para>
	/// This arm changes only the TENANT relative to the overwrite arm — same fixture, same aggregate id,
	/// same operation — so a second save by the SAME tenant must UPDATE rather than accumulate. If this
	/// fails, the conflict path is unreachable here and the overwrite arm proves nothing about
	/// isolation. Verified independently by mutation (collapsing the key made the overwrite arm RED at a
	/// real round-trip), but the point of this arm is that no future reader has to run that mutation to
	/// know the trap is live.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Take_The_Upsert_Path_When_The_Same_Tenant_Saves_Twice()
	{
		var aggregateId = Guid.NewGuid().ToString();
		var store = CreateStore(tenantScoped: true);

		using (TenantContextHolder.BeginScope(TenantA))
		{
			await store.SaveSnapshotAsync(
				CreateSnapshot(aggregateId, 1, "first", TenantA),
				CancellationToken.None).ConfigureAwait(false);
			await store.SaveSnapshotAsync(
				CreateSnapshot(aggregateId, 2, "second", TenantA),
				CancellationToken.None).ConfigureAwait(false);

			var loaded = await store.GetLatestSnapshotAsync(
				aggregateId,
				AggregateType,
				CancellationToken.None).ConfigureAwait(false);

			_ = loaded.ShouldNotBeNull();
			Encoding.UTF8.GetString(loaded.Data.ToArray()).ShouldBe(
				"second",
				"a second save by the SAME tenant must take the upsert path and replace the row. If it " +
				"does not, the conflict branch is unreachable from this fixture and the overwrite arm " +
				"above passes for the wrong reason — it never exercises the path it claims to guard");
		}
	}

	/// <summary>
	/// LIVENESS for the overwrite arm. A store that silently dropped every second write would satisfy it
	/// while having broken saving entirely.
	/// </summary>
	[Fact]
	public async Task Still_Let_The_Second_Tenant_Save_Its_Own_Row()
	{
		var sharedAggregateId = Guid.NewGuid().ToString();
		var store = CreateStore(tenantScoped: true);

		using (TenantContextHolder.BeginScope(TenantA))
		{
			await store.SaveSnapshotAsync(
				CreateSnapshot(sharedAggregateId, 1, "tenant-a-original", TenantA),
				CancellationToken.None).ConfigureAwait(false);
		}

		ISnapshot? tenantBsRow;
		using (TenantContextHolder.BeginScope("tenant-b"))
		{
			await store.SaveSnapshotAsync(
				CreateSnapshot(sharedAggregateId, 1, "tenant-b-write", "tenant-b"),
				CancellationToken.None).ConfigureAwait(false);

			tenantBsRow = await store.GetLatestSnapshotAsync(
				sharedAggregateId,
				AggregateType,
				CancellationToken.None).ConfigureAwait(false);
		}

		_ = tenantBsRow.ShouldNotBeNull(
			"tenant B's own save must succeed — a store that refused or dropped the second tenant's " +
			"write would pass the overwrite arm above while having removed multi-tenant saving");
		Encoding.UTF8.GetString(tenantBsRow.Data.ToArray()).ShouldBe("tenant-b-write");
	}

	private ISnapshotStore CreateStore(bool tenantScoped) =>
		new SqliteSnapshotStore(
			_fixture.ConnectionString,
			NullLogger<SqliteSnapshotStore>.Instance,
			tenantContext: tenantScoped ? new AmbientHolderTenantContext() : null);

	private static ISnapshot CreateSnapshot(string aggregateId, long version, string data, string? tenantId) =>
		new UnscopedReadSnapshot(
			Guid.NewGuid().ToString(),
			aggregateId,
			AggregateType,
			version,
			DateTimeOffset.UtcNow,
			Encoding.UTF8.GetBytes(data),
			null,
			tenantId);

	private sealed class AmbientHolderTenantContext : ITenantContext
	{
		public string? TenantId => TenantContextHolder.Current;

		public bool HasTenant => !string.IsNullOrEmpty(TenantContextHolder.Current);
	}

	private sealed record UnscopedReadSnapshot(
		string SnapshotId,
		string AggregateId,
		string AggregateType,
		long Version,
		DateTimeOffset CreatedAt,
		ReadOnlyMemory<byte> Data,
		IDictionary<string, object>? Metadata,
		string? TenantId) : ISnapshot;
}
