// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;
using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.Data.Postgres.EventSourcing;

/// <summary>
/// Data request to load events for an aggregate from the Postgres event store.
/// </summary>
public sealed class LoadEventsRequest : DataRequestBase<IDbConnection, IReadOnlyList<StoredEvent>>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="LoadEventsRequest"/> class.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="schemaName">The database schema name.</param>
	/// <param name="tableName">The events table name.</param>
	/// <param name="fromVersion">Load events after this version (-1 for all events).</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public LoadEventsRequest(
		string aggregateId,
		string aggregateType,
		string schemaName,
		string tableName,
		long fromVersion,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
		ArgumentException.ThrowIfNullOrWhiteSpace(schemaName);
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

		var sql = $"""
			SELECT event_id AS EventId, aggregate_id AS AggregateId, aggregate_type AS AggregateType,
			       event_type AS EventType, event_data AS EventData, metadata AS Metadata,
			       version AS Version, timestamp AS Timestamp, is_dispatched AS IsDispatched
			FROM {schemaName}.{tableName}
			WHERE aggregate_id = @AggregateId AND aggregate_type = @AggregateType AND version > @FromVersion
			ORDER BY version ASC
			""";

		var parameters = new DynamicParameters();
		parameters.Add("@AggregateId", aggregateId);
		parameters.Add("@AggregateType", aggregateType);
		parameters.Add("@FromVersion", fromVersion);

		Command = CreateCommand(sql, parameters, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
		{
			var dtos = await connection.QueryAsync<StoredEventDto>(Command).ConfigureAwait(false);
			return dtos.Select(static dto => dto.ToStoredEvent()).ToList();
		};
	}
}
