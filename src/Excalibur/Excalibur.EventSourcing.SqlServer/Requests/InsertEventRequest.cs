// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;

namespace Excalibur.EventSourcing.SqlServer.Requests;

/// <summary>
/// Data request to insert a single event into the event store.
/// Returns the position (sequence number) of the inserted event.
/// </summary>
public sealed class InsertEventRequest : DataRequestBase<IDbConnection, long>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="InsertEventRequest"/> class.
	/// </summary>
	/// <param name="eventId">The unique event identifier.</param>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="eventType">The event type name.</param>
	/// <param name="eventData">The serialized event data.</param>
	/// <param name="metadata">The serialized event metadata (optional).</param>
	/// <param name="version">The version number for this event.</param>
	/// <param name="timestamp">The event timestamp.</param>
	/// <param name="transaction">The transaction to participate in.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="schema">The schema name for the event store table. Default: "dbo".</param>
	/// <param name="table">The event store table name. Default: "EventStoreEvents".</param>
	public InsertEventRequest(
		string eventId,
		string aggregateId,
		string aggregateType,
		string eventType,
		byte[] eventData,
		byte[]? metadata,
		long version,
		DateTimeOffset timestamp,
		IDbTransaction? transaction,
		CancellationToken cancellationToken,
		string schema = "dbo",
		string table = "EventStoreEvents")
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
		ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
		ArgumentNullException.ThrowIfNull(eventData);

		var qualifiedTable = SqlTableName.Format(schema, table);

#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in SqlTableName.Format
		var sql = $"""
			INSERT INTO {qualifiedTable} (EventId, AggregateId, AggregateType, EventType, EventData, Metadata, Version, Timestamp)
			OUTPUT INSERTED.Position
			VALUES (@EventId, @AggregateId, @AggregateType, @EventType, @EventData, @Metadata, @Version, @Timestamp)
			""";
#pragma warning restore CA2100

		var parameters = new DynamicParameters();
		parameters.Add("@EventId", eventId);
		parameters.Add("@AggregateId", aggregateId);
		parameters.Add("@AggregateType", aggregateType);
		parameters.Add("@EventType", eventType);
		parameters.Add("@EventData", eventData, DbType.Binary);
		parameters.Add("@Metadata", metadata, DbType.Binary);
		parameters.Add("@Version", version);
		parameters.Add("@Timestamp", timestamp);

		Command = CreateCommand(sql, parameters, transaction, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.ExecuteScalarAsync<long>(Command).ConfigureAwait(false);
	}
}
