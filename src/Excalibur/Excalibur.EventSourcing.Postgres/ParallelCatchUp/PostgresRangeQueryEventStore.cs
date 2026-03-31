// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Runtime.CompilerServices;

using Dapper;

using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.ParallelCatchUp;
using Excalibur.EventSourcing.Postgres.Requests;

using Microsoft.Extensions.Logging;

using Npgsql;

namespace Excalibur.EventSourcing.Postgres.ParallelCatchUp;

/// <summary>
/// Postgres implementation of <see cref="IRangeQueryableEventStore"/> for parallel catch-up.
/// </summary>
/// <remarks>
/// Executes indexed range queries on the <c>global_position</c> column to support
/// parallel stream partitioning. Each query reads a batch of events within a position range.
/// </remarks>
internal sealed class PostgresRangeQueryEventStore : IRangeQueryableEventStore
{
	private readonly NpgsqlDataSource _dataSource;
	private readonly string _schema;
	private readonly string _table;
	private readonly ILogger<PostgresRangeQueryEventStore> _logger;

	internal PostgresRangeQueryEventStore(
		NpgsqlDataSource dataSource,
		string schema,
		string table,
		ILogger<PostgresRangeQueryEventStore> logger)
	{
		ArgumentNullException.ThrowIfNull(dataSource);
		ArgumentNullException.ThrowIfNull(logger);

		_dataSource = dataSource;
		_schema = schema;
		_table = table;
		_logger = logger;
	}

	/// <inheritdoc />
	public async IAsyncEnumerable<StoredEvent> ReadRangeAsync(
		long fromPosition,
		long toPosition,
		int batchSize,
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var qualifiedTable = PgTableName.Format(_schema, _table);
		var currentPosition = fromPosition;

		while (currentPosition <= toPosition)
		{
			cancellationToken.ThrowIfCancellationRequested();

			var batchEnd = Math.Min(currentPosition + batchSize - 1, toPosition);

			await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

			var sql = $"""
				SELECT event_id AS EventId, aggregate_id AS AggregateId,
				       aggregate_type AS AggregateType, event_type AS EventType,
				       event_data AS EventData, metadata AS Metadata,
				       version AS Version, timestamp AS Timestamp,
				       global_position AS GlobalPosition
				FROM {qualifiedTable}
				WHERE global_position >= @FromPosition AND global_position <= @ToPosition
				ORDER BY global_position
				""";

			var events = await connection.QueryAsync<PostgresStoredEventRow>(
				new CommandDefinition(
					sql,
					new { FromPosition = currentPosition, ToPosition = batchEnd },
					cancellationToken: cancellationToken)).ConfigureAwait(false);

			var count = 0;
			foreach (var row in events)
			{
				yield return new StoredEvent(
					row.EventId,
					row.AggregateId,
					row.AggregateType,
					row.EventType,
					row.EventData,
					row.Metadata,
					row.Version,
					row.Timestamp);
				count++;
			}

			if (count == 0)
			{
				// No events in this range; skip ahead
				break;
			}

			currentPosition = batchEnd + 1;
		}
	}

	/// <summary>
	/// Row mapping for Dapper query results (aliased to PascalCase in SQL).
	/// </summary>
	private sealed record PostgresStoredEventRow(
		string EventId,
		string AggregateId,
		string AggregateType,
		string EventType,
		byte[] EventData,
		byte[]? Metadata,
		long Version,
		DateTimeOffset Timestamp,
		long GlobalPosition);
}
