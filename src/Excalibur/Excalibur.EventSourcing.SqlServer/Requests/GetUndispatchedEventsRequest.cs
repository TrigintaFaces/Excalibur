// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;
using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.EventSourcing.SqlServer.Requests;

/// <summary>
/// Data request to get undispatched events from the event store.
/// </summary>
public sealed class GetUndispatchedEventsRequest : DataRequestBase<IDbConnection, IReadOnlyList<StoredEvent>>
{
	private const string Sql = """
		SELECT TOP (@BatchSize) EventId, AggregateId, AggregateType, EventType, EventData, Metadata, Version, Timestamp, IsDispatched
		FROM EventStoreEvents
		WHERE IsDispatched = 0
		ORDER BY Position ASC
		""";

	/// <summary>
	/// Initializes a new instance of the <see cref="GetUndispatchedEventsRequest"/> class.
	/// </summary>
	/// <param name="batchSize">Maximum number of events to retrieve.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public GetUndispatchedEventsRequest(
		int batchSize,
		CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(batchSize, 0);

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
