// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.Dispatch;
using Excalibur.Domain.Model;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Sqlite;

/// <summary>
/// SQLite implementation of <see cref="ISnapshotStore"/>.
/// </summary>
/// <remarks>
/// Stores snapshots as binary blobs in SQLite. Auto-creates the table on first use.
/// Uses UPSERT (INSERT OR REPLACE) to maintain one snapshot per aggregate.
/// </remarks>
public sealed class SqliteSnapshotStore : ISnapshotStore
{
	private readonly string _connectionString;
	private readonly string _table;
	private readonly ILogger<SqliteSnapshotStore> _logger;
	private readonly ITenantContext _tenantContext;

	// The DEPLOYMENT MODE (TenantContextOptions.RequireTenant, set by AddMultiTenancy()) -- NOT "is a
	// context present", which is now always true. Only a single-tenant deployment may have its legacy
	// untenanted rows converged onto the single-tenant identity; doing that in a multi-tenant deployment
	// would move that host's genuinely-untenanted system rows into the default tenant's partition.
	private readonly bool _requireTenant;
	/// <summary>
	/// Gets the tenant term this store runs under, resolved in one place so every statement it builds binds
	/// the same value. The context is a required dependency, so the term is decided identically on every
	/// path: the store cannot resolve one partition on write and a different one on read.
	/// </summary>
	private TenantScope CurrentTenantScope =>
		TenantScope.FromContext(_tenantContext);


	/// <summary>
	/// Initializes a new instance of the <see cref="SqliteSnapshotStore"/> class.
	/// </summary>
	/// <param name="connectionString">The SQLite connection string.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="table">The snapshot table name. Default: "Snapshots".</param>
	/// <param name="tenantContextOptions">
	/// The tenant-context options; its <see cref="TenantContextOptions.RequireTenant"/> (set by
	/// <c>AddMultiTenancy()</c>) selects the deployment mode for the startup schema handshake, which decides
	/// whether legacy untenanted rows may be converged onto the single-tenant identity.
	/// </param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	public SqliteSnapshotStore(
		string connectionString,
		ILogger<SqliteSnapshotStore> logger,
		ITenantContext tenantContext,
		IOptions<TenantContextOptions> tenantContextOptions,
		string table = "Snapshots")
	{
		ArgumentNullException.ThrowIfNull(tenantContext);
		_tenantContext = tenantContext;
		ArgumentNullException.ThrowIfNull(tenantContextOptions);
		_requireTenant = tenantContextOptions.Value.RequireTenant;
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		ArgumentNullException.ThrowIfNull(logger);

		_connectionString = connectionString;
		_logger = logger;
		_table = table;
	}

	/// <inheritdoc/>
	public async ValueTask<ISnapshot?> GetLatestSnapshotAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		await using var connection = CreateConnection();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		await SqliteTableInitializer.EnsureSnapshotsTableAsync(connection, _table, _requireTenant, cancellationToken)
			.ConfigureAwait(false);

		var scope = CurrentTenantScope;
		// UNCONDITIONAL, and it must stay that way. The write stores every row under
		// the reserved '__untenanted__' sentinel and keys ON CONFLICT(AggregateId, AggregateType, TenantId),
		// so a single-tenant row lives under that sentinel rather than outside the key. A conditional
		// predicate would make an UNSCOPED read emit no tenant filter at all and match ANY tenant's
		// row for that aggregate — returning another tenant's snapshot to a single-tenant caller.
		// Read and write must agree on the key; here that means both are unconditional.
		const string tenantPredicate = " AND TenantId = @TenantId";

		var sql = $"""
			SELECT SnapshotId, AggregateId, AggregateType, Version, Data, CreatedAt
			FROM [{_table}]
			WHERE AggregateId = @AggregateId AND AggregateType = @AggregateType{tenantPredicate}
			""";

		var row = await connection.QuerySingleOrDefaultAsync<SnapshotRow?>(
			new CommandDefinition(
				sql,
				new { AggregateId = aggregateId, AggregateType = aggregateType, TenantId = KeyedTenantPartition.FromScope(scope).TenantId },
				cancellationToken: cancellationToken)).ConfigureAwait(false);

		if (row is null)
		{
			return null;
		}

		return new Snapshot
		{
			SnapshotId = row.SnapshotId,
			AggregateId = row.AggregateId,
			AggregateType = row.AggregateType,
			Version = row.Version,
			Data = row.Data,
			CreatedAt = DateTimeOffset.Parse(row.CreatedAt),
		};
	}

	/// <inheritdoc/>
	public async ValueTask SaveSnapshotAsync(
		ISnapshot snapshot,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(snapshot);

		await using var connection = CreateConnection();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		await SqliteTableInitializer.EnsureSnapshotsTableAsync(connection, _table, _requireTenant, cancellationToken)
			.ConfigureAwait(false);

		var scope = CurrentTenantScope;
		// The write needs no predicate: the tenant is written into the row and IS part of the
		// ON CONFLICT key below, so the upsert already discriminates by tenant unconditionally.
		// The read and delete paths carry the matching unconditional predicate — see them for why.

		var sql = $"""
			INSERT INTO [{_table}] (SnapshotId, AggregateId, AggregateType, Version, Data, CreatedAt, TenantId)
			VALUES (@SnapshotId, @AggregateId, @AggregateType, @Version, @Data, @CreatedAt, @TenantId)
			ON CONFLICT(AggregateId, AggregateType, TenantId) DO UPDATE SET
				SnapshotId = @SnapshotId,
				Version = @Version,
				Data = @Data,
				CreatedAt = @CreatedAt
			-- Only ever move the snapshot FORWARD. Without this the upsert was last-writer-wins, so a
			-- slower write carrying an older version overwrote a newer snapshot and GetLatestSnapshot
			-- then returned the older one. Concurrent saves are ordinary here: several instances can
			-- snapshot the same aggregate at once, and their writes land in no guaranteed order.
			-- Replaying from a stale snapshot is not corruption, but "latest" that goes backwards is a
			-- broken contract, and the rest of the family already enforces it -- SQL Server guards
			-- WHEN MATCHED AND source.Version > target.Version, Postgres guards
			-- WHERE existing.version < EXCLUDED.version, and Oracle guards its MERGE the same way.
			-- SQLite was the last outlier.
			-- A losing write updates no row, which the caller already tolerates: it discards the count.
			WHERE [{_table}].Version < excluded.Version
			""";

		await connection.ExecuteAsync(
			new CommandDefinition(
				sql,
				new
				{
					snapshot.SnapshotId,
					snapshot.AggregateId,
					snapshot.AggregateType,
					snapshot.Version,
					Data = snapshot.Data.ToArray(),
					CreatedAt = snapshot.CreatedAt.ToString("O"),
					TenantId = KeyedTenantPartition.FromScope(scope).TenantId,
				},
				cancellationToken: cancellationToken)).ConfigureAwait(false);

		_logger.LogDebug(
			"Saved snapshot for {AggregateType}/{AggregateId} at version {Version}",
			snapshot.AggregateType, snapshot.AggregateId, snapshot.Version);
	}

	/// <inheritdoc/>
	public async ValueTask DeleteSnapshotsAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		await using var connection = CreateConnection();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		await SqliteTableInitializer.EnsureSnapshotsTableAsync(connection, _table, _requireTenant, cancellationToken)
			.ConfigureAwait(false);

		var scope = CurrentTenantScope;
		// UNCONDITIONAL, and it must stay that way. The write stores every row under
		// the reserved '__untenanted__' sentinel and keys ON CONFLICT(AggregateId, AggregateType, TenantId),
		// so a single-tenant row lives under that sentinel rather than outside the key. A conditional
		// predicate would make an UNSCOPED read emit no tenant filter at all and match ANY tenant's
		// row for that aggregate — returning another tenant's snapshot to a single-tenant caller.
		// Read and write must agree on the key; here that means both are unconditional.
		const string tenantPredicate = " AND TenantId = @TenantId";

		await connection.ExecuteAsync(
			new CommandDefinition(
				$"DELETE FROM [{_table}] WHERE AggregateId = @AggregateId AND AggregateType = @AggregateType{tenantPredicate}",
				new { AggregateId = aggregateId, AggregateType = aggregateType, TenantId = KeyedTenantPartition.FromScope(scope).TenantId },
				cancellationToken: cancellationToken)).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async ValueTask DeleteSnapshotsOlderThanAsync(
		string aggregateId,
		string aggregateType,
		long olderThanVersion,
		CancellationToken cancellationToken)
	{
		await using var connection = CreateConnection();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		await SqliteTableInitializer.EnsureSnapshotsTableAsync(connection, _table, _requireTenant, cancellationToken)
			.ConfigureAwait(false);

		var scope = CurrentTenantScope;
		// UNCONDITIONAL, and it must stay that way. The write stores every row under
		// the reserved '__untenanted__' sentinel and keys ON CONFLICT(AggregateId, AggregateType, TenantId),
		// so a single-tenant row lives under that sentinel rather than outside the key. A conditional
		// predicate would make an UNSCOPED read emit no tenant filter at all and match ANY tenant's
		// row for that aggregate — returning another tenant's snapshot to a single-tenant caller.
		// Read and write must agree on the key; here that means both are unconditional.
		const string tenantPredicate = " AND TenantId = @TenantId";

		await connection.ExecuteAsync(
			new CommandDefinition(
				$"DELETE FROM [{_table}] WHERE AggregateId = @AggregateId AND AggregateType = @AggregateType{tenantPredicate} AND Version < @OlderThanVersion",
				new { AggregateId = aggregateId, AggregateType = aggregateType, OlderThanVersion = olderThanVersion, TenantId = KeyedTenantPartition.FromScope(scope).TenantId },
				cancellationToken: cancellationToken)).ConfigureAwait(false);
	}

	private SqliteConnection CreateConnection() => new(_connectionString);

	private sealed record SnapshotRow(
		string SnapshotId,
		string AggregateId,
		string AggregateType,
		long Version,
		byte[] Data,
		string CreatedAt);
}
