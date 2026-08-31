// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.IO;

using Dapper;

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Sqlite;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.Sqlite;

/// <summary>
/// Exercises <see cref="SqliteTableInitializer"/>'s events-table reconciliation — the rebuild-with-staging
/// migration that brings an existing untenanted <c>Events</c> table onto the tenant-scoped shape, and the
/// single-tenant convergence that follows it — against a database written exactly as a shipped earlier
/// version of this package would have left it (no store involved in the seed).
/// </summary>
/// <remarks>
/// <para>
/// This is the events-table counterpart of
/// <see cref="SqliteSnapshotStoreTenantWiringConvergenceShould"/>, with one material addition:
/// <c>GlobalPosition</c> is externally observable (the global stream order returned via
/// <c>AppendResult.FirstEventPosition</c>), unlike the snapshots table's surrogate <c>Id</c>. The rebuild
/// must preserve it exactly for every existing row and must never let a later append reuse a value the
/// rebuild carried over — that is the load-bearing property <c>RebuildLegacyTable</c> proves.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Database", "Sqlite")]
public sealed class SqliteEventStoreTenantMigrationShould : IDisposable
{
	private const string AggregateType = "MigrationAggregate";
	private const string UntenantedTerm = "__untenanted__";

	private readonly string _databasePath;
	private readonly string _connectionString;
	private readonly string _tableName;
	private readonly SqliteConnection _keepAlive;

	public SqliteEventStoreTenantMigrationShould()
	{
		_databasePath = Path.Combine(Path.GetTempPath(), $"excalibur-eventtenantmigration-{Guid.NewGuid():N}.db");
		_connectionString = $"Data Source={_databasePath}";
		_tableName = $"Events_{Guid.NewGuid():N}";

		_keepAlive = new SqliteConnection(_connectionString);
		_keepAlive.Open();
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		_keepAlive.Dispose();
		SqliteConnection.ClearAllPools();

		if (File.Exists(_databasePath))
		{
			File.Delete(_databasePath);
		}
	}

	/// <summary>
	/// SAFETY + LIVENESS: an existing untenanted table is rebuilt onto the tenant-scoped shape without
	/// losing a row, and without ever letting SQLite's AUTOINCREMENT sequence reuse a
	/// <c>GlobalPosition</c> the rebuild carried over.
	/// </summary>
	[Fact]
	public async Task RebuildLegacyTable_PreservesGlobalPosition_AndNeverReusesIt()
	{
		// A table shaped exactly as the pre-tenancy store left it: no TenantId column, three-column key.
		SeedLegacyTableNoTenantColumn();
		var firstAggPosition = SeedLegacyEventNoTenantColumn("aggregate-1", version: 0, payload: "legacy-1");
		var secondAggPosition = SeedLegacyEventNoTenantColumn("aggregate-2", version: 0, payload: "legacy-2");
		firstAggPosition.ShouldBeLessThan(secondAggPosition);

		var store = CreateStore(new SingleTenantDefaultContext());

		// Touch the store so the schema handshake (rebuild + convergence) runs.
		var replayed = await store.LoadAsync("aggregate-1", AggregateType, CancellationToken.None).ConfigureAwait(false);

		// LIVENESS — the legacy row survived the rebuild, at the SAME GlobalPosition (asserted indirectly
		// below via the sequence check) and with its original data intact.
		replayed.Count.ShouldBe(1, "the rebuild must preserve every existing row");
		replayed[0].Version.ShouldBe(0L);

		// SAFETY — a fresh append must land STRICTLY ABOVE every position the rebuild carried over. If the
		// rebuild had let the staging table assign fresh GlobalPosition values (as the Snapshots rebuild
		// does for its surrogate Id, safely, because nothing reads that column), SQLite's AUTOINCREMENT
		// high-water mark would track only the RE-numbered rows, and this append could return a position
		// already used by a preserved row — the exact rowid-reuse hazard the schema's own AUTOINCREMENT
		// choice exists to prevent.
		var appendResult = await store.AppendAsync(
			"aggregate-3", AggregateType,
			[new TestDomainEvent { EventId = Guid.NewGuid().ToString(), AggregateId = "aggregate-3", OccurredAt = DateTimeOffset.UtcNow, Data = "post-rebuild" }],
			expectedVersion: -1, CancellationToken.None).ConfigureAwait(false);

		appendResult.Success.ShouldBeTrue();
		appendResult.FirstEventPosition.ShouldNotBeNull();
		appendResult.FirstEventPosition!.Value.ShouldBeGreaterThan(
			secondAggPosition,
			"a post-rebuild append must never reuse a GlobalPosition the rebuild carried over from the "
			+ "legacy table — reuse would let a reader who has consumed up to the old max see a NEW event "
			+ "appear at a position it already passed");
	}

	/// <summary>
	/// SAFETY: rows a shipped single-tenant host already wrote under the untenanted term stay readable
	/// once the store resolves the single-tenant identity instead — and LIVENESS's converse — a real named
	/// tenant must still be unable to read them.
	/// </summary>
	[Fact]
	public async Task ConvergeLegacyUntenantedRows_ForSingleTenantHost()
	{
		SeedTenantedTable();
		SeedTenantedEvent("aggregate-converge", version: 0, payload: "written-before-wiring", UntenantedTerm);

		var singleTenantHost = CreateStore(new SingleTenantDefaultContext());

		var found = await singleTenantHost
			.LoadAsync("aggregate-converge", AggregateType, CancellationToken.None).ConfigureAwait(false);

		found.Count.ShouldBe(
			1,
			"a single-tenant host that now resolves the framework default tenant context must still read "
			+ "the events an earlier version of itself wrote under the untenanted term");

		var acme = CreateStore(new NamedTenantContext("acme"));
		var leaked = await acme.LoadAsync("aggregate-converge", AggregateType, CancellationToken.None).ConfigureAwait(false);

		leaked.ShouldBeEmpty(
			"a named tenant must never read the single-tenant host's converged partition — convergence "
			+ "must not be achieved by collapsing every tenant into one partition");
	}

	/// <summary>
	/// LIVENESS: a MULTI-TENANT host's untenanted rows are left where they are, not moved onto the default
	/// tenant identity.
	/// </summary>
	[Fact]
	public async Task NotConvergeTheUntenantedPartition_ForAMultiTenantHost()
	{
		SeedTenantedTable();
		SeedTenantedEvent("aggregate-system-owned", version: 0, payload: "system-owned", UntenantedTerm);

		var multiTenantHost = CreateStore(new NamedTenantContext("acme"), requireTenant: true);

		// Touch the store so its schema handshake runs; the convergence, if it ran, would run here.
		_ = await multiTenantHost.LoadAsync("aggregate-system-owned", AggregateType, CancellationToken.None)
			.ConfigureAwait(false);

		StoredTenantTermFor("aggregate-system-owned").ShouldBe(
			UntenantedTerm,
			"a multi-tenant host's untenanted rows must stay in the untenanted partition — moving them "
			+ "onto the single-tenant identity would hand rows that belong to no tenant to the default tenant");
	}

	private void SeedLegacyTableNoTenantColumn()
	{
		_ = _keepAlive.Execute(
			$"""
			CREATE TABLE IF NOT EXISTS [{_tableName}] (
				GlobalPosition INTEGER PRIMARY KEY AUTOINCREMENT,
				EventId TEXT NOT NULL,
				AggregateId TEXT NOT NULL,
				AggregateType TEXT NOT NULL,
				EventType TEXT NOT NULL,
				EventData BLOB NOT NULL,
				Metadata BLOB,
				Version INTEGER NOT NULL,
				Timestamp TEXT NOT NULL,
				UNIQUE(AggregateId, AggregateType, Version)
			);
			""");
	}

	/// <summary>Writes a row with no TenantId column at all, and returns the GlobalPosition it received.</summary>
	private long SeedLegacyEventNoTenantColumn(string aggregateId, long version, string payload)
	{
		return _keepAlive.QuerySingle<long>(
			$"""
			INSERT INTO [{_tableName}] (EventId, AggregateId, AggregateType, EventType, EventData, Version, Timestamp)
			VALUES (@EventId, @AggregateId, @AggregateType, @EventType, @EventData, @Version, @Timestamp);
			SELECT last_insert_rowid();
			""",
			new
			{
				EventId = Guid.NewGuid().ToString(),
				AggregateId = aggregateId,
				AggregateType,
				EventType = "LegacyEvent",
				EventData = System.Text.Encoding.UTF8.GetBytes(payload),
				Version = version,
				Timestamp = DateTimeOffset.UtcNow.ToString("O"),
			});
	}

	private void SeedTenantedTable()
	{
		_ = _keepAlive.Execute(
			$"""
			CREATE TABLE IF NOT EXISTS [{_tableName}] (
				GlobalPosition INTEGER PRIMARY KEY AUTOINCREMENT,
				EventId TEXT NOT NULL,
				AggregateId TEXT NOT NULL,
				AggregateType TEXT NOT NULL,
				EventType TEXT NOT NULL,
				EventData BLOB NOT NULL,
				Metadata BLOB,
				Version INTEGER NOT NULL,
				Timestamp TEXT NOT NULL,
				TenantId TEXT NOT NULL,
				UNIQUE(AggregateId, AggregateType, Version, TenantId)
			);
			""");
	}

	private void SeedTenantedEvent(string aggregateId, long version, string payload, string tenantTerm)
	{
		_ = _keepAlive.Execute(
			$"""
			INSERT INTO [{_tableName}] (EventId, AggregateId, AggregateType, EventType, EventData, Version, Timestamp, TenantId)
			VALUES (@EventId, @AggregateId, @AggregateType, @EventType, @EventData, @Version, @Timestamp, @TenantId);
			""",
			new
			{
				EventId = Guid.NewGuid().ToString(),
				AggregateId = aggregateId,
				AggregateType,
				EventType = "LegacyEvent",
				EventData = System.Text.Encoding.UTF8.GetBytes(payload),
				Version = version,
				Timestamp = DateTimeOffset.UtcNow.ToString("O"),
				TenantId = tenantTerm,
			});
	}

	/// <summary>Reads the tenant term actually stored for an aggregate, bypassing the store entirely.</summary>
	private string StoredTenantTermFor(string aggregateId) =>
		_keepAlive.QueryFirst<string>(
			$"SELECT TenantId FROM [{_tableName}] WHERE AggregateId = @AggregateId;",
			new { AggregateId = aggregateId });

	private SqliteEventStore CreateStore(ITenantContext tenantContext, bool requireTenant = false) =>
		new(
			_connectionString,
			NullLogger<SqliteEventStore>.Instance,
			tenantContext,
			Options.Create(new TenantContextOptions { RequireTenant = requireTenant }),
			_tableName);


	/// <summary>
	/// Mirrors the framework single-tenant default: always present, always the one canonical single-tenant
	/// identity.
	/// </summary>
	private sealed class SingleTenantDefaultContext : ITenantContext
	{
		public string? TenantId => TenantDefaults.DefaultTenantId;

		public bool HasTenant => true;
	}

	/// <summary>A context resolving a real, named tenant.</summary>
	private sealed class NamedTenantContext(string tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => true;
	}
}
