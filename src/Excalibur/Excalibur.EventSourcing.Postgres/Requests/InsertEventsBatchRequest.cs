// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;
using System.Globalization;
using System.Text;

using Dapper;

using Excalibur.Data;

namespace Excalibur.EventSourcing.Postgres.Requests;

/// <summary>
/// The position and version of a single event inserted by <see cref="InsertEventsBatchRequest"/>.
/// </summary>
/// <remarks>
/// The <see cref="Version"/> is carried back from the <c>RETURNING</c> clause so callers can restore the
/// per-event ordering — the order of rows returned by a multi-row <c>INSERT ... RETURNING</c> is not
/// guaranteed to match the order of the <c>VALUES</c> tuples, so positions must be matched by version,
/// not by row index.
/// </remarks>
internal readonly record struct EventInsertPosition(long Position, long Version);

/// <summary>
/// Data request that inserts a batch of events into the Postgres event store with a <strong>single</strong>
/// multi-row <c>INSERT ... VALUES ... RETURNING</c> statement, returning each inserted event's position and
/// version. Replaces the per-event insert loop so an append is one round-trip and one atomic statement.
/// </summary>
internal sealed class InsertEventsBatchRequest : DataRequestBase<IDbConnection, IReadOnlyList<EventInsertPosition>>
{
	/// <summary>
	/// The maximum number of events per statement. PostgreSQL caps a command at 65535 parameters; 256
	/// events (2048 parameters) is a conservative chunk that keeps statement text small. Larger appends
	/// are chunked by the caller within the same transaction.
	/// </summary>
	internal const int MaxEventsPerStatement = 256;

	/// <summary>
	/// Initializes a new instance of the <see cref="InsertEventsBatchRequest"/> class.
	/// </summary>
	/// <param name="rows">The events to insert (must be non-empty and within <see cref="MaxEventsPerStatement"/>).</param>
	/// <param name="transaction">The transaction to participate in.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="schema">The schema name for the event store table. Default: "public".</param>
	/// <param name="table">The event store table name. Default: "events".</param>
	public InsertEventsBatchRequest(
		IReadOnlyList<EventInsertRow> rows,
		IDbTransaction? transaction,
		CancellationToken cancellationToken,
		string schema = "public",
		string table = "events")
	{
		ArgumentNullException.ThrowIfNull(rows);
		if (rows.Count is 0 or > MaxEventsPerStatement)
		{
			throw new ArgumentOutOfRangeException(
				nameof(rows),
				rows.Count,
				$"Batch size must be between 1 and {MaxEventsPerStatement}.");
		}

		var qualifiedTable = PgTableName.Format(schema, table);

		var valuesBuilder = new StringBuilder();
		var parameters = new DynamicParameters();

		for (var i = 0; i < rows.Count; i++)
		{
			var row = rows[i];
			ArgumentException.ThrowIfNullOrWhiteSpace(row.EventId);
			ArgumentException.ThrowIfNullOrWhiteSpace(row.AggregateId);
			ArgumentException.ThrowIfNullOrWhiteSpace(row.AggregateType);
			ArgumentException.ThrowIfNullOrWhiteSpace(row.EventType);
			ArgumentNullException.ThrowIfNull(row.EventData);

			var p = i.ToString(CultureInfo.InvariantCulture);
			if (i > 0)
			{
				_ = valuesBuilder.Append(',');
			}

			_ = valuesBuilder
				.Append("(@EventId").Append(p)
				.Append(",@AggregateId").Append(p)
				.Append(",@AggregateType").Append(p)
				.Append(",@EventType").Append(p)
				.Append(",@EventData").Append(p)
				.Append(",@Metadata").Append(p)
				.Append(",@Version").Append(p)
				.Append(",@Timestamp").Append(p)
				.Append(')');

			parameters.Add("@EventId" + p, row.EventId);
			parameters.Add("@AggregateId" + p, row.AggregateId);
			parameters.Add("@AggregateType" + p, row.AggregateType);
			parameters.Add("@EventType" + p, row.EventType);
			parameters.Add("@EventData" + p, row.EventData, DbType.Binary);
			parameters.Add("@Metadata" + p, row.Metadata, DbType.Binary);
			parameters.Add("@Version" + p, row.Version);
			parameters.Add("@Timestamp" + p, row.Timestamp);
		}

#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in PgTableName.Format
		var sql = $"""
			INSERT INTO {qualifiedTable} (event_id, aggregate_id, aggregate_type, event_type, event_data, metadata, version, timestamp)
			VALUES {valuesBuilder}
			RETURNING position, version
			""";
#pragma warning restore CA2100

		Command = CreateCommand(sql, parameters, transaction, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
		{
			var result = await connection.QueryAsync<EventInsertPosition>(Command).ConfigureAwait(false);
			return result.AsList();
		};
	}
}

/// <summary>
/// A single event row supplied to <see cref="InsertEventsBatchRequest"/>.
/// </summary>
internal readonly record struct EventInsertRow(
	string EventId,
	string AggregateId,
	string AggregateType,
	string EventType,
	byte[] EventData,
	byte[]? Metadata,
	long Version,
	DateTimeOffset Timestamp);
