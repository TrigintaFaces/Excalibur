// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Runtime.CompilerServices;

using Dapper;

using Excalibur.EventSourcing.ParallelCatchUp;
using Excalibur.EventSourcing.SqlServer.Requests;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Excalibur.EventSourcing.SqlServer.ParallelCatchUp;

/// <summary>
/// SQL Server implementation of <see cref="IRangeQueryableEventStore"/> for parallel catch-up.
/// </summary>
/// <remarks>
/// Executes indexed range queries on the <c>Position</c> column (the global stream ordinal) to
/// support parallel stream partitioning. Each query reads a batch of events within a position range.
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

			// The global-ordinal column is named Position (see SqlServerGlobalStreamQuery); the prior
			// SELECT/WHERE/ORDER BY referenced a non-existent GlobalPosition column and threw at runtime
			// on parallel catch-up (masked by in-memory-only tests). ao97rb / FR-7.
			var sql = $"""
				SELECT EventId, AggregateId, AggregateType, EventType,
				       EventData, Metadata, Version, Timestamp, Position
				FROM {qualifiedTable}
				WHERE Position >= @FromPosition AND Position <= @ToPosition
				ORDER BY Position
				""";

			var events = await connection.QueryAsync<SqlServerStoredEventRow>(
				new CommandDefinition(
					sql,
					new { FromPosition = currentPosition, ToPosition = batchEnd },
					cancellationToken: cancellationToken)).ConfigureAwait(false);

			foreach (var row in events)
			{
				// Carry the global ordinal onto the StoredEvent so downstream consumers (e.g. the
				// projection hosts) read the correct GlobalPosition — the column is Position, the
				// property is GlobalPosition.
				yield return new StoredEvent(
					row.EventId,
					row.AggregateId,
					row.AggregateType,
					row.EventType,
					row.EventData,
					row.Metadata,
					row.Version,
					row.Timestamp)
				{
					GlobalPosition = row.Position,
				};
			}

			// Gap-tolerant paging (778kpz): do NOT break on an empty batch. A gap in the
			// Position IDENTITY sequence (reseed / identity-cache jump / skip) narrower than
			// [from,to] must not stop enumeration early; advance past it and continue to
			// toPosition. Termination is bounded by the while-condition (currentPosition <=
			// toPosition), so there is no unbounded scan at the true tail (FR-1a).
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
		long Position);
}
