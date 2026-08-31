// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Threading.Tasks;

using Excalibur.Dispatch.Tests.Conformance.Snapshot;

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Sqlite;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

#pragma warning disable CA1812 // Internal class is never instantiated

namespace Excalibur.Integration.Tests.Data.Snapshots;

/// <summary>
/// Real-infrastructure conformance tests for <see cref="SqliteSnapshotStore"/> using the
/// Snapshot Conformance Test Kit against an embedded SQLite database.
/// </summary>
/// <remarks>
/// SQLite is a local, file-based database - it is itself the real infrastructure, so these tests
/// require no Docker container and are never skipped. The fixture provisions a unique temporary
/// database file; the store auto-creates its schema on first use.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Sqlite")]
public sealed class SqliteSnapshotStoreConformanceShould : SnapshotConformanceTestBase, IClassFixture<SqliteSnapshotStoreFixture>
{
	private readonly SqliteSnapshotStoreFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqliteSnapshotStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The SQLite snapshot store fixture.</param>
	public SqliteSnapshotStoreConformanceShould(SqliteSnapshotStoreFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	/// <remarks>
	/// The tenant context is required, not decoration. Without it the store's scope accessor falls back to
	/// <c>None</c>, the row key omits the tenant, and every tenant collides on one row — the untenanted
	/// path, exercised silently by a suite meant to prove the tenanted one. Supplying it also makes
	/// multi-tenancy active, so every call must resolve a tenant or the store fails closed by design;
	/// <c>TenantScopedConformance</c> on the base supplies that ambient tenant, which is why this needs
	/// no change to any arm. Passed by name because <c>tenantContext</c> follows the optional
	/// <c>table</c> parameter.
	/// </remarks>
	protected override Task<ISnapshotStore> CreateSnapshotStoreAsync()
	{
		var logger = NullLogger<SqliteSnapshotStore>.Instance;

		// Bind the default connection-string constructor (the surface most consumers use).
		// The store auto-creates its table on first use, so no DDL bootstrap is required.
		return Task.FromResult<ISnapshotStore>(
			new SqliteSnapshotStore(
				_fixture.ConnectionString,
				logger,
				tenantContext: CreateAmbientTenantContext(),
				tenantContextOptions: Options.Create(new TenantContextOptions())));
	}

	/// <inheritdoc/>
	protected override async Task DisposeSnapshotStoreAsync()
	{
		await _fixture.CleanupAsync().ConfigureAwait(false);
	}
	/// <summary>
	/// A save carrying an older version MUST NOT replace a newer stored snapshot.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The kit's concurrency arm fires ten saves at versions 10 through 100 and asserts the store then
	/// reports version 100. That arm can only catch this defect when the scheduler happens to land the
	/// older write last, which is why it passed on most runs and reported version 80 on one. This arm
	/// removes the scheduler from the question: save 100, then save 80, then read.
	/// </para>
	/// <para>
	/// Every other SQL provider already enforces this in its upsert - SQL Server with
	/// <c>WHEN MATCHED AND source.Version &gt; target.Version</c>, Postgres with
	/// <c>WHERE existing.version &lt; EXCLUDED.version</c>, Oracle with the same guard on its MERGE.
	/// SQLite's <c>DO UPDATE SET</c> carried no condition, so the write was last-writer-wins and
	/// "latest" could move backwards.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task NeverLetAnOlderSaveReplaceANewerSnapshot()
	{
		var aggregateId = Guid.NewGuid().ToString();
		const string aggregateType = "MonotonicAggregate";

		var newer = CreateTestSnapshot(aggregateId, aggregateType, 100, [100]);
		await SnapshotStore!.SaveSnapshotAsync(newer, TestContext.Current.CancellationToken);

		var stale = CreateTestSnapshot(aggregateId, aggregateType, 80, [80]);
		await SnapshotStore.SaveSnapshotAsync(stale, TestContext.Current.CancellationToken);

		var retrieved = await SnapshotStore.GetLatestSnapshotAsync(
			aggregateId,
			aggregateType,
			TestContext.Current.CancellationToken);

		_ = retrieved.ShouldNotBeNull();
		retrieved.Version.ShouldBe(
			100,
			"a stale save must not move the stored snapshot backwards - GetLatestSnapshotAsync would then "
			+ "return a snapshot that is not the latest, which is the contract this store advertises");

		// The payload too, not just the version: an implementation that guarded the version column while
		// still overwriting the data would satisfy a version-only assertion and hand back a snapshot whose
		// body and version disagree.
		retrieved.Data.ToArray().ShouldBe(new byte[] { 100 });
	}

	/// <summary>
	/// A save carrying a NEWER version MUST still be accepted, payload and all.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The liveness half of the arm above, and it is not redundant. The guard added to the upsert is a
	/// predicate on a write: a predicate that never matches refuses every save after the first, which
	/// satisfies the stale-save arm perfectly while storing nothing. So would a guard written the wrong
	/// way round. The safety arm cannot tell those apart from a correct one - it only ever asks that a
	/// write be refused.
	/// </para>
	/// <para>
	/// It advances the version twice so the second save is compared against an already-guarded row rather
	/// than only against an absent one: the INSERT path and the DO UPDATE path are different branches of
	/// the upsert, and only the second exercises the predicate.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task StillAcceptASaveThatCarriesANewerVersion()
	{
		var aggregateId = Guid.NewGuid().ToString();
		const string aggregateType = "MonotonicAggregate";

		await SnapshotStore!.SaveSnapshotAsync(
			CreateTestSnapshot(aggregateId, aggregateType, 10, [10]),
			TestContext.Current.CancellationToken);

		await SnapshotStore.SaveSnapshotAsync(
			CreateTestSnapshot(aggregateId, aggregateType, 20, [20]),
			TestContext.Current.CancellationToken);

		await SnapshotStore.SaveSnapshotAsync(
			CreateTestSnapshot(aggregateId, aggregateType, 30, [30]),
			TestContext.Current.CancellationToken);

		var retrieved = await SnapshotStore.GetLatestSnapshotAsync(
			aggregateId,
			aggregateType,
			TestContext.Current.CancellationToken);

		_ = retrieved.ShouldNotBeNull();
		retrieved.Version.ShouldBe(
			30,
			"the guard must refuse only saves that would move the snapshot backwards - a predicate that "
			+ "never matches refuses everything and satisfies the stale-save arm while storing nothing");

		// The body must advance with the version. A guard that updated the version column while leaving
		// the previous payload in place would pass a version-only assertion and return a snapshot whose
		// body belongs to an earlier version.
		retrieved.Data.ToArray().ShouldBe(new byte[] { 30 });
	}
}
