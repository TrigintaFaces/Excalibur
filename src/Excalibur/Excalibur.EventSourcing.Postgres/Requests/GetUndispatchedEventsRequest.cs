// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;
using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.EventSourcing.Postgres.Requests;

/// <summary>
/// Data request to get undispatched events from the Postgres event store.
/// </summary>
public sealed class GetUndispatchedEventsRequest : DataRequestBase<IDbConnection, IReadOnlyList<StoredEvent>>
{
	private const string Sql = """
		SELECT event_id AS EventId, aggregate_id AS AggregateId, aggregate_type AS AggregateType,
		       event_type AS EventType, event_data AS EventData, metadata AS Metadata,
		       version AS Version, timestamp AS Timestamp, is_dispatched AS IsDispatched
		FROM events
		WHERE is_dispatched = false
		ORDER BY position ASC
		LIMIT @BatchSize
		""";

	/// <summary>
	/// Initializes a new instance of the <see cref="GetUndispatchedEventsRequest"/> class.
	/// </summary>
	/// <param name="batchSize">The maximum number of events to return.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public GetUndispatchedEventsRequest(
		int batchSize,
		CancellationToken cancellationToken)
	{
		var parameters = new DynamicParameters();
		parameters.Add("@BatchSize", batchSize);

		Command = CreateCommand(Sql, parameters, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
		{
			var events = await connection.QueryAsync<StoredEvent>(Command).ConfigureAwait(false);
			return events.AsList();
		};
	}
}
