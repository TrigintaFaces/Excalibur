// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.IO;

using Microsoft.Data.Sqlite;

// SQL strings here are test-controlled constants and the shipped script read off disk — never user
// input. Parameterising them is not possible for DDL anyway, which is most of what this exercises.
#pragma warning disable CA2100

namespace Excalibur.EventSourcing.Tests.Sqlite;

/// <summary>
/// Binds the SHIPPED upgrade script <c>Scripts/002_MakeEventAndSnapshotIdentityTenantScoped.sql</c>
/// against a database shaped exactly as the previously shipped
/// <c>Scripts/001_CreateEventStoreSchema.sql</c> left it — no store involved in the seed, and the script
/// read off disk rather than reproduced here.
/// </summary>
/// <remarks>
/// <para>
/// This is the script-provisioned counterpart of <see cref="SqliteEventStoreTenantMigrationShould"/>,
/// which covers the same upgrade performed at runtime by the initializer. The two paths must land on the
/// same shape, and only this one is reachable by a consumer whose schema is owned by a migration tool
/// rather than by the application: for them 001 is a no-op against their existing database and the
/// runtime reconciliation never runs, so a broken 002 is a broken upgrade with no fallback.
/// </para>
/// <para>
/// The load-bearing property is the TENANT-SCOPED key, and it is asserted in both directions, because
/// each arm alone is satisfied by a wrong table. A table that rejects everything satisfies the safety
/// arm; a table with no uniqueness at all satisfies the liveness arm.
/// <see cref="Fail_the_tenant_scoped_concurrency_assertion_against_a_database_that_was_never_migrated"/>
/// is the non-vacuity proof: the identical assertion run against an unmigrated database fails.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Database", "Sqlite")]
public sealed class SqliteShippedTenantUpgradeScriptShould : IDisposable
{
	private const string UntenantedTerm = "__untenanted__";

	/// <summary>The pre-tenancy shape: no TenantId, and a stream key of three columns.</summary>
	private const string LegacyEventsDdl = """
		CREATE TABLE [Events] (
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
		CREATE INDEX IX_Events_AggregateId ON [Events] (AggregateId, AggregateType, Version);
		CREATE TABLE [Snapshots] (
		    Id INTEGER PRIMARY KEY AUTOINCREMENT,
		    SnapshotId TEXT NOT NULL,
		    AggregateId TEXT NOT NULL,
		    AggregateType TEXT NOT NULL,
		    Version INTEGER NOT NULL,
		    Data BLOB NOT NULL,
		    CreatedAt TEXT NOT NULL,
		    UNIQUE(AggregateId, AggregateType)
		);
		""";

	private static readonly string UpgradeScriptPath = ResolveRepoRelative(
		"src/Excalibur/Excalibur.EventSourcing.Sqlite/Scripts/002_MakeEventAndSnapshotIdentityTenantScoped.sql");

	private readonly string _databasePath;
	private readonly string _connectionString;

	public SqliteShippedTenantUpgradeScriptShould()
	{
		_databasePath = Path.Combine(Path.GetTempPath(), $"excalibur-sqlite-upgrade-{Guid.NewGuid():N}.db");
		_connectionString = $"Data Source={_databasePath}";
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		SqliteConnection.ClearAllPools();

		if (File.Exists(_databasePath))
		{
			File.Delete(_databasePath);
		}
	}

	/// <summary>
	/// LIVENESS: a pre-tenancy database reaches the current shape with every row, payload and global
	/// position intact.
	/// </summary>
	[Fact]
	public void Bring_a_pre_tenancy_database_onto_the_tenant_scoped_shape()
	{
		SeedLegacyDatabase();

		RunUpgradeScript();

		Columns("Events").ShouldContain("TenantId",
			"the upgrade must add the column every read and write this store issues names — without it "
			+ "the first append fails with 'no such column: TenantId'.");
		Columns("Snapshots").ShouldContain("TenantId");

		// Every pre-existing row belongs to the untenanted partition, and to the RESERVED spelling of it:
		// '' is not portable and the framework's tenant scope rejects the sentinel, so no real tenant can
		// claim that partition.
		Query("SELECT DISTINCT TenantId FROM [Events];").ShouldBe([UntenantedTerm]);
		Query("SELECT DISTINCT TenantId FROM [Snapshots];").ShouldBe([UntenantedTerm]);

		// The data survived, not merely the shape. Payload and metadata are BLOBs and the nullable one is
		// genuinely NULL for one seeded row, which a rebuild that defaulted it would silently change.
		Query("SELECT GlobalPosition || '|' || EventId || '|' || AggregateId || '|' || Version "
			+ "|| '|' || quote(EventData) || '|' || quote(coalesce(Metadata, 'NULL')) FROM [Events] ORDER BY GlobalPosition;")
			.ShouldBe([
				"1|e1|order-1|0|X'0102'|'NULL'",
				"2|e2|order-1|1|X'0304'|X'FF'",
				"3|e3|order-2|0|X'0506'|'NULL'",
			]);
		Query("SELECT SnapshotId || '|' || AggregateId || '|' || Version || '|' || quote(Data) FROM [Snapshots];")
			.ShouldBe(["s1|order-1|1|X'AABB'"]);

		// The index follows a renamed table keeping its NAME, so a rebuild that recreated it before
		// dropping the old table would silently leave the new table with none.
		Query("SELECT name FROM sqlite_master WHERE type = 'index' AND tbl_name = 'Events' AND name NOT LIKE 'sqlite_%';")
			.ShouldBe(["IX_Events_AggregateId"]);
	}

	/// <summary>
	/// SAFETY and LIVENESS on the one property the upgrade exists to establish: the tenant participates
	/// in stream IDENTITY, so optimistic concurrency is per-tenant rather than global.
	/// </summary>
	[Fact]
	public void Enforce_stream_identity_per_tenant_after_the_upgrade()
	{
		SeedLegacyDatabase();
		RunUpgradeScript();

		// LIVENESS — two tenants may hold the same aggregate id at the same version. Under the previous
		// three-column key they could not, and the failure did not present as a tenancy bug: the second
		// tenant's version probe reported "does not exist" while its append collided with the first
		// tenant's row, a conflict that never converges on retry.
		AppendEvent("shared-order", version: 0, tenant: "tenant-a");
		Should.NotThrow(() => AppendEvent("shared-order", version: 0, tenant: "tenant-b"),
			"the tenant is part of the key, so the same aggregate id at the same version in a DIFFERENT "
			+ "tenant is a different stream position and must be accepted.");

		// SAFETY — within ONE tenant the key still rejects a second writer at the same version, which is
		// how the store detects a concurrent modification.
		var duplicate = Should.Throw<SqliteException>(() => AppendEvent("shared-order", version: 0, tenant: "tenant-a"),
			"two writers appending the same version for the same aggregate AND tenant must not both "
			+ "succeed — that constraint violation IS the optimistic-concurrency check.");
		duplicate.Message.ShouldContain("UNIQUE", Case.Insensitive);

		// The same pair of properties for snapshots, whose key is the (aggregate, tenant) pair.
		SaveSnapshot("shared-order", tenant: "tenant-a");
		Should.NotThrow(() => SaveSnapshot("shared-order", tenant: "tenant-b"));
		_ = Should.Throw<SqliteException>(() => SaveSnapshot("shared-order", tenant: "tenant-a"));
	}

	/// <summary>
	/// NON-VACUITY: the assertion above is not satisfiable by a database that never ran the script.
	/// </summary>
	/// <remarks>
	/// Without this arm the test above proves nothing about the SCRIPT — a passing result would be
	/// consistent with the seed already being correct. Here the identical liveness call is made against an
	/// unmigrated database and must fail.
	/// </remarks>
	[Fact]
	public void Fail_the_tenant_scoped_concurrency_assertion_against_a_database_that_was_never_migrated()
	{
		SeedLegacyDatabase();

		// NOT running the upgrade script is the whole point of this arm.
		var failure = Should.Throw<SqliteException>(() => AppendEvent("shared-order", version: 0, tenant: "tenant-a"),
			"a pre-tenancy database cannot accept a tenant-scoped append at all, so the liveness arm above "
			+ "is impossible to pass without the upgrade — which is what makes it a real lock on the script.");
		failure.Message.ShouldContain("TenantId");
	}

	/// <summary>
	/// SAFETY: a second run refuses and leaves the database exactly as it found it — in particular it does
	/// not re-stamp the untenanted sentinel over tenant assignments made since the first run.
	/// </summary>
	/// <remarks>
	/// SQLite's SQL has no procedural branch, so the script cannot skip its own DDL; what it can do is
	/// refuse and roll back. That is the re-runnable contract available on this engine, and the property
	/// that matters — the database is unchanged — is what this asserts. It also asserts the SECOND,
	/// runner-independent refusal: the rename target left behind by the first run is occupied.
	/// </remarks>
	[Fact]
	public void Refuse_a_second_run_and_change_nothing()
	{
		SeedLegacyDatabase();
		RunUpgradeScript();

		// A tenant assignment made after the upgrade. A second run that re-executed the rebuild would
		// stamp the sentinel back over this, silently, with no constraint objecting.
		Execute("UPDATE [Events] SET TenantId = 'tenant-a' WHERE AggregateId = 'order-1';");
		var before = Query("SELECT GlobalPosition || '|' || AggregateId || '|' || TenantId FROM [Events] ORDER BY GlobalPosition;");

		_ = Should.Throw<SqliteException>(RunUpgradeScript,
			"the precondition must refuse a database that already carries the tenant column.");

		Query("SELECT GlobalPosition || '|' || AggregateId || '|' || TenantId FROM [Events] ORDER BY GlobalPosition;")
			.ShouldBe(before, "a refused run must leave every row, and every tenant assignment, untouched.");
		Query("SELECT DISTINCT TenantId FROM [Snapshots];").ShouldBe([UntenantedTerm]);

		// The backup left by the first run is what makes the refusal hold even for a runner that ignored
		// the error above: the rename that begins the rebuild has nowhere to go.
		Query("SELECT name FROM sqlite_master WHERE type = 'table' AND name = 'Events_before_tenant_upgrade';")
			.ShouldBe(["Events_before_tenant_upgrade"]);
	}

	/// <summary>
	/// SAFETY: a post-upgrade append lands strictly above every position carried over, so a reader that
	/// has consumed up to the old maximum never sees a new event appear below it.
	/// </summary>
	[Fact]
	public void Preserve_global_position_and_never_reuse_it()
	{
		SeedLegacyDatabase();
		RunUpgradeScript();

		// Copying GlobalPosition explicitly is what advances SQLite's AUTOINCREMENT high-water mark to the
		// largest value carried over. A rebuild that let the new table assign fresh values would leave the
		// mark tracking only the renumbered rows, and a later append could reuse a position a reader has
		// already passed — the exact hazard the AUTOINCREMENT choice exists to prevent.
		var carriedMax = long.Parse(Query("SELECT max(GlobalPosition) FROM [Events];")[0], provider: null);
		carriedMax.ShouldBe(3L, "the pre-existing positions must be preserved, not renumbered.");

		AppendEvent("order-3", version: 0, tenant: UntenantedTerm);

		var appended = long.Parse(
			Query("SELECT GlobalPosition FROM [Events] WHERE AggregateId = 'order-3';")[0], provider: null);
		appended.ShouldBeGreaterThan(carriedMax);
	}

	// ---- helpers -------------------------------------------------------------------------------

	private void SeedLegacyDatabase()
	{
		Execute(LegacyEventsDdl);
		Execute("""
			INSERT INTO [Events] (EventId, AggregateId, AggregateType, EventType, EventData, Metadata, Version, Timestamp)
			VALUES ('e1', 'order-1', 'Order', 'Created', x'0102', NULL,   0, '2026-01-01T00:00:00Z'),
			       ('e2', 'order-1', 'Order', 'Shipped', x'0304', x'ff',  1, '2026-01-02T00:00:00Z'),
			       ('e3', 'order-2', 'Order', 'Created', x'0506', NULL,   0, '2026-01-03T00:00:00Z');
			INSERT INTO [Snapshots] (SnapshotId, AggregateId, AggregateType, Version, Data, CreatedAt)
			VALUES ('s1', 'order-1', 'Order', 1, x'aabb', '2026-01-02T00:00:00Z');
			""");
	}

	/// <summary>Executes the shipped script as a migration runner would: one batch, aborting on error.</summary>
	private void RunUpgradeScript() => Execute(File.ReadAllText(UpgradeScriptPath));

	private void AppendEvent(string aggregateId, long version, string tenant) => Execute(
		"INSERT INTO [Events] (EventId, AggregateId, AggregateType, EventType, EventData, Version, Timestamp, TenantId) "
		+ $"VALUES ('{Guid.NewGuid():N}', '{aggregateId}', 'Order', 'Created', x'99', {version}, '2026-02-01T00:00:00Z', '{tenant}');");

	private void SaveSnapshot(string aggregateId, string tenant) => Execute(
		"INSERT INTO [Snapshots] (SnapshotId, AggregateId, AggregateType, Version, Data, CreatedAt, TenantId) "
		+ $"VALUES ('{Guid.NewGuid():N}', '{aggregateId}', 'Order', 0, x'99', '2026-02-01T00:00:00Z', '{tenant}');");

	/// <summary>
	/// A fresh connection per call. Disposing it is what rolls back the script's open transaction when a
	/// statement refuses, which is the behaviour a real runner produces and the reason a refused run
	/// leaves nothing behind.
	/// </summary>
	private void Execute(string sql)
	{
		using var connection = new SqliteConnection(_connectionString);
		connection.Open();
		using var command = connection.CreateCommand();
		command.CommandText = sql;
		_ = command.ExecuteNonQuery();
	}

	private List<string> Query(string sql)
	{
		using var connection = new SqliteConnection(_connectionString);
		connection.Open();
		using var command = connection.CreateCommand();
		command.CommandText = sql;
		using var reader = command.ExecuteReader();

		var results = new List<string>();
		while (reader.Read())
		{
			results.Add(reader.IsDBNull(0) ? "<null>" : reader.GetValue(0).ToString() ?? string.Empty);
		}

		return results;
	}

	private List<string> Columns(string table) =>
		Query($"SELECT name FROM pragma_table_info('{table}');");

	private static string ResolveRepoRelative(string relativePath)
	{
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Excalibur.sln")))
		{
			dir = dir.Parent;
		}

		dir.ShouldNotBeNull("could not locate the solution root (Excalibur.sln) above the test assembly.");
		var full = Path.Combine(dir.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
		File.Exists(full).ShouldBeTrue(
			$"the shipped upgrade script is a required deliverable but was not found at '{full}'.");
		return full;
	}
}
