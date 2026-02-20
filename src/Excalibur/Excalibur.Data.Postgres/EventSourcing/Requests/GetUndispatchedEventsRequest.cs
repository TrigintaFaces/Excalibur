// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;
using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.Data.Postgres.EventSourcing;

/// <summary>
/// Data request to get undispatched events from the Postgres event store.
/// </summary>
public sealed class GetUndispatchedEventsRequest : DataRequestBase<IDbConnection, IReadOnlyList<StoredEvent>>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="GetUndispatchedEventsRequest"/> class.
	/// </summary>
	/// <param name="batchSize">Maximum number of events to retrieve.</param>
	/// <param name="schemaName">The database schema name.</param>
	/// <param name="tableName">The events table name.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public GetUndispatchedEventsRequest(
		int batchSize,
		string schemaName,
		string tableName,
		CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(batchSize, 0);
		ArgumentException.ThrowIfNullOrWhiteSpace(schemaName);
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

		var sql = $"""
			SELECT event_id AS EventId, aggregate_id AS AggregateId, aggregate_type AS AggregateType,
			       event_type AS EventType, event_data AS EventData, metadata AS Metadata,
			       version AS Version, timestamp AS Timestamp, is_dispatched AS IsDispatched
			FROM {schemaName}.{tableName}
			WHERE is_dispatched = false
			ORDER BY global_sequence ASC
			LIMIT @BatchSize
			""";

		var parameters = new DynamicParameters();
		parameters.Add("@BatchSize", batchSize);

		Command = CreateCommand(sql, parameters, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
		{
			var dtos = await connection.QueryAsync<StoredEventDto>(Command).ConfigureAwait(false);
			return dtos.Select(static dto => dto.ToStoredEvent()).ToList();
		};
	}
}
