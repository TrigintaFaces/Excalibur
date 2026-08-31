// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Dapper;

using Excalibur.Dispatch;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Sqlite;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.Sqlite;

/// <summary>
/// Binds the tenant partition a SINGLE-TENANT host reads, so that a consumer who never asked for
/// multi-tenancy keeps their data when the store's <see cref="ITenantContext"/> stops being optional.
/// </summary>
/// <remarks>
/// <para>
/// A store used to resolve its partition differently depending on whether an ambient
/// <see cref="ITenantContext"/> happened to be registered: absent, it wrote the reserved untenanted term;
/// present, it wrote the single-tenant identity. Resolving that default was never an opt-in to tenancy - a
/// dependency's own registration helper could supply it - so the state could flip underneath a consumer on
/// a version bump, and rows written in one state stopped being returned in the other. Nothing errored: the
/// aggregate replayed from scratch and the snapshot was silently unreachable.
/// </para>
/// <para>
/// That divergence is now closed at the seam - the context is a required dependency, so the absent state
/// is not expressible and the two terms cannot disagree. What remains is the data already on disk. A
/// shipped consumer's database holds rows stamped with the untenanted term, written by the version they
/// installed, and those rows must stay readable. So these arms no longer construct the removed state: they
/// seed the bytes that state produced, which is the thing that actually has to keep working.
/// </para>
/// <para>
/// The liveness arm is what stops convergence being achieved by collapsing every partition into one: a real
/// named tenant must still be unable to read the single-tenant host's rows. The multi-tenant arm is what
/// stops it being achieved by converging indiscriminately: a host that genuinely uses the untenanted
/// partition must keep it.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Database", "Sqlite")]
public sealed class SqliteSnapshotStoreTenantWiringConvergenceShould : IDisposable
{
	private const string AggregateType = "WiringConvergenceAggregate";
	private const string UntenantedTerm = "__untenanted__";

	private readonly string _databasePath;
	private readonly string _connectionString;
	private readonly string _tableName;
	private readonly SqliteConnection _keepAlive;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqliteSnapshotStoreTenantWiringConvergenceShould"/>
	/// class over a private temporary database file.
	/// </summary>
	public SqliteSnapshotStoreTenantWiringConvergenceShould()
	{
		_databasePath = Path.Combine(Path.GetTempPath(), $"excalibur-tenantwiring-{Guid.NewGuid():N}.db");
		_connectionString = $"Data Source={_databasePath}";
		_tableName = $"Snapshots_{Guid.NewGuid():N}";

		_keepAlive = new SqliteConnection(_connectionString);
		_keepAlive.Open();
	}

	/// <summary>Releases the database file used by this test.</summary>
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
	/// SAFETY: rows a shipped single-tenant host already wrote under the untenanted term stay readable once
	/// the store resolves the single-tenant identity instead.
	/// </summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task ReadRowsAnEarlierVersionWroteUnderTheUntenantedTerm()
	{
		SeedLegacyRow("aggregate-written-before-wiring", 3L, "written-before-wiring", UntenantedTerm);

		var singleTenantHost = CreateStore(new SingleTenantDefaultContext());

		var found = await singleTenantHost
			.GetLatestSnapshotAsync("aggregate-written-before-wiring", AggregateType, CancellationToken.None)
			.ConfigureAwait(false);

		_ = found.ShouldNotBeNull(
			"a single-tenant host that now resolves the framework default tenant context must still read the "
			+ "rows an earlier version of itself wrote under the untenanted term - same host, same data, no "
			+ "tenancy opted into");
		found.Version.ShouldBe(3L);
		Encoding.UTF8.GetString(found.Data.ToArray()).ShouldBe("written-before-wiring");
	}

	/// <summary>
	/// SAFETY: the converse - what the store writes today is what the store reads back, so the two terms
	/// cannot drift apart again.
	/// </summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task ReadRowsWrittenWhileTheDefaultTenantContextWasResolved()
	{
		var writer = CreateStore(new SingleTenantDefaultContext());

		await writer.SaveSnapshotAsync(
			SnapshotFor("aggregate-written-after-wiring", 5L, "written-after-wiring"),
			CancellationToken.None).ConfigureAwait(false);

		// A separate instance over the same file: the partition must be a property of the deployment, not
		// of one object's lifetime.
		var reader = CreateStore(new SingleTenantDefaultContext());

		var found = await reader
			.GetLatestSnapshotAsync("aggregate-written-after-wiring", AggregateType, CancellationToken.None)
			.ConfigureAwait(false);

		_ = found.ShouldNotBeNull(
			"the partition a single-tenant host reads must be the one it writes, in either direction");
		found.Version.ShouldBe(5L);
		Encoding.UTF8.GetString(found.Data.ToArray()).ShouldBe("written-after-wiring");

		StoredTenantTermFor("aggregate-written-after-wiring").ShouldBe(
			TenantDefaults.DefaultTenantId,
			"a single-tenant host writes the single-tenant identity, so there is one term on disk and not two");
	}

	/// <summary>
	/// LIVENESS: convergence must not be achieved by collapsing every tenant into one partition - a real
	/// named tenant still cannot read the single-tenant host's rows.
	/// </summary>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task NotLetARealTenantReadTheSingleTenantHostRows()
	{
		var singleTenantHost = CreateStore(new SingleTenantDefaultContext());

		await singleTenantHost.SaveSnapshotAsync(
			SnapshotFor("aggregate-shared-id", 7L, "single-tenant-host-data"),
			CancellationToken.None).ConfigureAwait(false);

		var acme = CreateStore(new NamedTenantContext("acme"));

		var leaked = await acme
			.GetLatestSnapshotAsync("aggregate-shared-id", AggregateType, CancellationToken.None)
			.ConfigureAwait(false);

		leaked.ShouldBeNull(
			"a named tenant must never read the single-tenant host's partition - converging the two "
			+ "unwired states must not be done by making every tenant share one partition");
	}

	/// <summary>
	/// LIVENESS: a MULTI-TENANT host's untenanted rows are left where they are.
	/// </summary>
	/// <remarks>
	/// In a multi-tenant deployment the untenanted partition is a live partition holding rows that genuinely
	/// belong to no tenant. Converging it would move those rows into the default tenant's data - a silent
	/// cross-tenant write, and a worse fault than the one being fixed. The convergence must therefore key on
	/// the deployment mode and not merely on what a context resolves.
	/// </remarks>
	/// <returns>A task that represents the asynchronous test.</returns>
	[Fact]
	public async Task NotConvergeTheUntenantedPartitionOfAMultiTenantHost()
	{
		SeedLegacyRow("aggregate-system-owned", 11L, "system-owned", UntenantedTerm);

		var multiTenantHost = CreateStore(new NamedTenantContext("acme"), requireTenant: true);

		// Touch the store so its schema handshake runs; the convergence, if it ran, would run here.
		_ = await multiTenantHost
			.GetLatestSnapshotAsync("aggregate-system-owned", AggregateType, CancellationToken.None)
			.ConfigureAwait(false);

		StoredTenantTermFor("aggregate-system-owned").ShouldBe(
			UntenantedTerm,
			"a multi-tenant host's untenanted rows must stay in the untenanted partition - moving them onto "
			+ "the single-tenant identity would hand rows that belong to no tenant to the default tenant");
	}

	/// <summary>
	/// Writes a row exactly as a shipped earlier version left it: the tenant term stamped directly, with no
	/// store involved. This is the state a consumer's database is actually in.
	/// </summary>
	private void SeedLegacyRow(string aggregateId, long version, string payload, string tenantTerm)
	{
		using var connection = new SqliteConnection(_connectionString);
		connection.Open();

		_ = connection.Execute(
			$"""
			CREATE TABLE IF NOT EXISTS [{_tableName}] (
				Id INTEGER PRIMARY KEY AUTOINCREMENT,
				SnapshotId TEXT NOT NULL,
				AggregateId TEXT NOT NULL,
				AggregateType TEXT NOT NULL,
				Version INTEGER NOT NULL,
				Data BLOB NOT NULL,
				CreatedAt TEXT NOT NULL,
				TenantId TEXT NOT NULL,
				UNIQUE(AggregateId, AggregateType, TenantId)
			);
			""");

		_ = connection.Execute(
			$"""
			INSERT INTO [{_tableName}]
				(SnapshotId, AggregateId, AggregateType, Version, Data, CreatedAt, TenantId)
			VALUES (@SnapshotId, @AggregateId, @AggregateType, @Version, @Data, @CreatedAt, @TenantId);
			""",
			new
			{
				SnapshotId = Guid.NewGuid().ToString(),
				AggregateId = aggregateId,
				AggregateType,
				Version = version,
				Data = Encoding.UTF8.GetBytes(payload),
				CreatedAt = DateTimeOffset.UtcNow.ToString("O"),
				TenantId = tenantTerm,
			});
	}

	/// <summary>Reads the tenant term actually stored for an aggregate, bypassing the store entirely.</summary>
	private string StoredTenantTermFor(string aggregateId)
	{
		using var connection = new SqliteConnection(_connectionString);
		connection.Open();

		return connection.QueryFirst<string>(
			$"SELECT TenantId FROM [{_tableName}] WHERE AggregateId = @AggregateId;",
			new { AggregateId = aggregateId });
	}

	private static ISnapshot SnapshotFor(string aggregateId, long version, string payload) =>
		new WiringSnapshot(
			Guid.NewGuid().ToString(),
			aggregateId,
			AggregateType,
			version,
			DateTimeOffset.UtcNow,
			Encoding.UTF8.GetBytes(payload),
			null,
			null);

	private SqliteSnapshotStore CreateStore(ITenantContext tenantContext, bool requireTenant = false) =>
		new(
			_connectionString,
			NullLogger<SqliteSnapshotStore>.Instance,
			tenantContext,
			Options.Create(new TenantContextOptions { RequireTenant = requireTenant }),
			_tableName);


	/// <summary>
	/// Mirrors the framework single-tenant default: always present, always the one canonical
	/// single-tenant identity.
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

	private sealed record WiringSnapshot(
		string SnapshotId,
		string AggregateId,
		string AggregateType,
		long Version,
		DateTimeOffset CreatedAt,
		ReadOnlyMemory<byte> Data,
		IDictionary<string, object>? Metadata,
		string? TenantId) : ISnapshot;
}
