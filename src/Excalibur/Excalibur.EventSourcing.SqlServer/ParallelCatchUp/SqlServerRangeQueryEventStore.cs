// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Runtime.CompilerServices;

using Dapper;

using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.ParallelCatchUp;
using Excalibur.EventSourcing.SqlServer.Requests;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Excalibur.EventSourcing.SqlServer.ParallelCatchUp;

/// <summary>
/// SQL Server implementation of <see cref="IRangeQueryableEventStore"/> for parallel catch-up.
/// </summary>
/// <remarks>
/// Executes indexed range queries on the <c>GlobalPosition</c> column to support
/// parallel stream partitioning. Each query reads a batch of events within a position range.
/// </remarks>
internal sealed class SqlServerRangeQueryEventStore : IRangeQueryableEventStore
{
	private readonly Func<SqlConnection> _connectionFactory;
	private readonly string _schema;
	private readonly string _table;
	private readonly ILogger<SqlServerRangeQueryEventStore> _logger;

	internal SqlServerRangeQueryEventStore(
		Func<SqlConnection> connectionFactory,
		string schema,
		string table,
		ILogger<SqlServerRangeQueryEventStore> logger)
	{
		ArgumentNullException.ThrowIfNull(connectionFactory);
		ArgumentNullException.ThrowIfNull(logger);

		_connectionFactory = connectionFactory;
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
		var qualifiedTable = SqlTableName.Format(_schema, _table);
		var currentPosition = fromPosition;

		while (currentPosition <= toPosition)
		{
			cancellationToken.ThrowIfCancellationRequested();

			var batchEnd = Math.Min(currentPosition + batchSize - 1, toPosition);

			await using var connection = _connectionFactory();
			await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

			var sql = $"""
				SELECT EventId, AggregateId, AggregateType, EventType,
				       EventData, Metadata, Version, Timestamp, GlobalPosition
				FROM {qualifiedTable}
				WHERE GlobalPosition >= @FromPosition AND GlobalPosition <= @ToPosition
				ORDER BY GlobalPosition
				""";

			var events = await connection.QueryAsync<SqlServerStoredEventRow>(
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
	/// Row mapping for Dapper query results.
	/// </summary>
	private sealed record SqlServerStoredEventRow(
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
