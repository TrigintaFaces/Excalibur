// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.Dispatch;
using Excalibur.Domain.Model;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

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
	private readonly ITenantContext? _tenantContext;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqliteSnapshotStore"/> class.
	/// </summary>
	/// <param name="connectionString">The SQLite connection string.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="table">The snapshot table name. Default: "Snapshots".</param>
	/// <param name="tenantContext">
	/// The ambient tenant context, or <see langword="null"/> in a single-tenant host. When supplied, every
	/// read, save, and delete is restricted to the resolved tenant's own rows.
	/// </param>
	public SqliteSnapshotStore(
		string connectionString,
		ILogger<SqliteSnapshotStore> logger,
		string table = "Snapshots",
		ITenantContext? tenantContext = null)
	{
		_tenantContext = tenantContext;
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
		await SqliteTableInitializer.EnsureSnapshotsTableAsync(connection, _table, cancellationToken)
			.ConfigureAwait(false);

		var scope = TenantScope.FromContext(_tenantContext);
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
		await SqliteTableInitializer.EnsureSnapshotsTableAsync(connection, _table, cancellationToken)
			.ConfigureAwait(false);

		var scope = TenantScope.FromContext(_tenantContext);
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
		await SqliteTableInitializer.EnsureSnapshotsTableAsync(connection, _table, cancellationToken)
			.ConfigureAwait(false);

		var scope = TenantScope.FromContext(_tenantContext);
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
		await SqliteTableInitializer.EnsureSnapshotsTableAsync(connection, _table, cancellationToken)
			.ConfigureAwait(false);

		var scope = TenantScope.FromContext(_tenantContext);
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
