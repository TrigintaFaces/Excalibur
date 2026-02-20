// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;

namespace Excalibur.Data.Postgres.EventSourcing;

/// <summary>
/// Data request to insert a single event into the Postgres event store.
/// Returns the global position (sequence number) of the inserted event.
/// </summary>
/// <remarks>
/// Uses Postgres UNIQUE constraint on (aggregate_id, aggregate_type, version) for optimistic concurrency.
/// If a version conflict occurs, Postgres error code 23505 (unique_violation) will be thrown.
/// </remarks>
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
	/// <param name="schemaName">The database schema name.</param>
	/// <param name="tableName">The events table name.</param>
	/// <param name="transaction">The transaction to participate in.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public InsertEventRequest(
		string eventId,
		string aggregateId,
		string aggregateType,
		string eventType,
		byte[] eventData,
		byte[]? metadata,
		long version,
		DateTimeOffset timestamp,
		string schemaName,
		string tableName,
		IDbTransaction? transaction,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
		ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
		ArgumentNullException.ThrowIfNull(eventData);
		ArgumentException.ThrowIfNullOrWhiteSpace(schemaName);
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

		var sql = $"""
			INSERT INTO {schemaName}.{tableName} (event_id, aggregate_id, aggregate_type, event_type, event_data, metadata, version, timestamp, is_dispatched)
			VALUES (@EventId, @AggregateId, @AggregateType, @EventType, @EventData, @Metadata, @Version, @Timestamp, false)
			RETURNING global_sequence
			""";

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
