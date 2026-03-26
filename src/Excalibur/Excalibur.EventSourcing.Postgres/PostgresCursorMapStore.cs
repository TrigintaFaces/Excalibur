// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;
using Excalibur.EventSourcing.Abstractions;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Excalibur.EventSourcing.Postgres;

/// <summary>
/// PostgreSQL implementation of <see cref="ICursorMapStore"/> using a key-value table.
/// </summary>
/// <remarks>
/// <para>
/// Uses a <c>projection_cursor_maps</c> table with columns
/// <c>(projection_name, stream_id, position)</c>. Saves are atomic via
/// <c>INSERT ... ON CONFLICT ... DO UPDATE</c>.
/// </para>
/// <para>
/// Table DDL:
/// <code>
/// CREATE TABLE projection_cursor_maps (
///     projection_name VARCHAR(256) NOT NULL,
///     stream_id VARCHAR(256) NOT NULL,
///     position BIGINT NOT NULL,
///     CONSTRAINT pk_projection_cursor_maps PRIMARY KEY (projection_name, stream_id)
/// );
/// </code>
/// </para>
/// </remarks>
public sealed class PostgresCursorMapStore : ICursorMapStore
{
	private readonly NpgsqlDataSource _dataSource;
	private readonly ILogger<PostgresCursorMapStore> _logger;

	/// <summary>
	/// Initializes a new instance with a connection string.
	/// </summary>
	/// <param name="connectionString">The PostgreSQL connection string.</param>
	/// <param name="logger">The logger instance.</param>
	public PostgresCursorMapStore(string connectionString, ILogger<PostgresCursorMapStore> logger)
		: this(NpgsqlDataSource.Create(connectionString), logger)
	{
	}

	/// <summary>
	/// Initializes a new instance with an <see cref="NpgsqlDataSource"/>.
	/// </summary>
	/// <param name="dataSource">The Npgsql data source for connection management.</param>
	/// <param name="logger">The logger instance.</param>
	public PostgresCursorMapStore(NpgsqlDataSource dataSource, ILogger<PostgresCursorMapStore> logger)
	{
		ArgumentNullException.ThrowIfNull(dataSource);
		ArgumentNullException.ThrowIfNull(logger);

		_dataSource = dataSource;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyDictionary<string, long>> GetCursorMapAsync(
		string projectionName,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(projectionName);

		await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		var rows = await connection.QueryAsync<CursorMapRow>(
			"SELECT stream_id, position FROM projection_cursor_maps WHERE projection_name = @ProjectionName",
			new { ProjectionName = projectionName }).ConfigureAwait(false);

		var result = new Dictionary<string, long>(StringComparer.Ordinal);
		foreach (var row in rows)
		{
			result[row.stream_id] = row.position;
		}

		return result;
	}

	/// <inheritdoc />
	public async Task SaveCursorMapAsync(
		string projectionName,
		IReadOnlyDictionary<string, long> cursorMap,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(projectionName);
		ArgumentNullException.ThrowIfNull(cursorMap);

		if (cursorMap.Count == 0)
		{
			return;
		}

		await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
		await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

		foreach (var entry in cursorMap)
		{
			await connection.ExecuteAsync(
				"""
				INSERT INTO projection_cursor_maps (projection_name, stream_id, position)
				VALUES (@ProjectionName, @StreamId, @Position)
				ON CONFLICT (projection_name, stream_id)
				DO UPDATE SET position = @Position
				""",
				new { ProjectionName = projectionName, StreamId = entry.Key, Position = entry.Value },
				transaction).ConfigureAwait(false);
		}

		await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task ResetCursorMapAsync(
		string projectionName,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(projectionName);

		await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		await connection.ExecuteAsync(
			"DELETE FROM projection_cursor_maps WHERE projection_name = @ProjectionName",
			new { ProjectionName = projectionName }).ConfigureAwait(false);
	}

	private sealed record CursorMapRow(string stream_id, long position);
}
