// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;
using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.EventSourcing.Postgres.Requests;

/// <summary>
/// Data request to load events for an aggregate from the Postgres event store.
/// </summary>
public sealed class LoadEventsRequest : DataRequestBase<IDbConnection, IReadOnlyList<StoredEvent>>
{
	private const string Sql = """
		SELECT event_id AS EventId, aggregate_id AS AggregateId, aggregate_type AS AggregateType,
		       event_type AS EventType, event_data AS EventData, metadata AS Metadata,
		       version AS Version, timestamp AS Timestamp, is_dispatched AS IsDispatched
		FROM events
		WHERE aggregate_id = @AggregateId AND aggregate_type = @AggregateType AND version > @FromVersion
		ORDER BY version ASC
		""";

	/// <summary>
	/// Initializes a new instance of the <see cref="LoadEventsRequest"/> class.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="fromVersion">Load events after this version (-1 for all events).</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public LoadEventsRequest(
		string aggregateId,
		string aggregateType,
		long fromVersion,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);

		var parameters = new DynamicParameters();
		parameters.Add("@AggregateId", aggregateId);
		parameters.Add("@AggregateType", aggregateType);
		parameters.Add("@FromVersion", fromVersion);

		Command = CreateCommand(Sql, parameters, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
		{
			var events = await connection.QueryAsync<StoredEvent>(Command).ConfigureAwait(false);
			return events.AsList();
		};
	}
}
