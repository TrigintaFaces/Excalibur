// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Abstractions;

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

	/// <summary>
	/// Initializes a new instance of the <see cref="SqliteSnapshotStore"/> class.
	/// </summary>
	/// <param name="connectionString">The SQLite connection string.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="table">The snapshot table name. Default: "Snapshots".</param>
	public SqliteSnapshotStore(
		string connectionString,
		ILogger<SqliteSnapshotStore> logger,
		string table = "Snapshots")
	{
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

		var sql = $"""
			SELECT SnapshotId, AggregateId, AggregateType, Version, Data, CreatedAt
			FROM [{_table}]
			WHERE AggregateId = @AggregateId AND AggregateType = @AggregateType
			""";

		var row = await connection.QuerySingleOrDefaultAsync<SnapshotRow?>(
			new CommandDefinition(
				sql,
				new { AggregateId = aggregateId, AggregateType = aggregateType },
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

		var sql = $"""
			INSERT INTO [{_table}] (SnapshotId, AggregateId, AggregateType, Version, Data, CreatedAt)
			VALUES (@SnapshotId, @AggregateId, @AggregateType, @Version, @Data, @CreatedAt)
			ON CONFLICT(AggregateId, AggregateType) DO UPDATE SET
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
					snapshot.Data,
					CreatedAt = snapshot.CreatedAt.ToString("O"),
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

		await connection.ExecuteAsync(
			new CommandDefinition(
				$"DELETE FROM [{_table}] WHERE AggregateId = @AggregateId AND AggregateType = @AggregateType",
				new { AggregateId = aggregateId, AggregateType = aggregateType },
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

		await connection.ExecuteAsync(
			new CommandDefinition(
				$"DELETE FROM [{_table}] WHERE AggregateId = @AggregateId AND AggregateType = @AggregateType AND Version < @OlderThanVersion",
				new { AggregateId = aggregateId, AggregateType = aggregateType, OlderThanVersion = olderThanVersion },
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
