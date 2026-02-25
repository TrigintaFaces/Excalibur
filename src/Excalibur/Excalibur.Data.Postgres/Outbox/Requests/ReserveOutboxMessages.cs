// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;
using Excalibur.Dispatch.Abstractions;

using OutboxMessage = Excalibur.Dispatch.Delivery.OutboxMessage;

namespace Excalibur.Data.Postgres.Outbox;

/// <summary>
/// Represents a data request to reserve outbox messages for processing in the Postgres database.
/// </summary>
public sealed class ReserveOutboxMessages : DataRequest<IEnumerable<IOutboxMessage>>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ReserveOutboxMessages" /> class.
	/// </summary>
	/// <param name="dispatcherId"> The unique identifier of the dispatcher reserving the messages. </param>
	/// <param name="batchSize"> The maximum number of messages to reserve in this batch. </param>
	/// <param name="reservationTimeout"> The timeout in milliseconds for the reservation. </param>
	/// <param name="outboxTableName"> The name of the outbox table. </param>
	/// <param name="sqlTimeOutSeconds"> The SQL command timeout in seconds. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	public ReserveOutboxMessages(
		string dispatcherId,
		int batchSize,
		int reservationTimeout,
		string outboxTableName,
		int sqlTimeOutSeconds,
		CancellationToken cancellationToken)
	{
		var sql = $"""
		           WITH cte_outbox AS (
		                   SELECT message_id
		                   FROM {outboxTableName}
		                   WHERE dispatcher_id IS NULL OR NOW() > dispatcher_timeout
		                   ORDER BY occurred_on
		                   LIMIT {batchSize}
		                   )
		                   UPDATE {outboxTableName}
		                   SET dispatcher_id = @DispatcherId,
		                   dispatcher_timeout = NOW() + (@ReservationTimeout || ' milliseconds')::interval
		                   WHERE message_id IN (SELECT message_id FROM cte_outbox)
		                   RETURNING message_id AS MessageId,
		                   message_type AS MessageType,
		                   message_metadata AS MessageMetadata,
		                   message_body AS MessageBody,
		                   occurred_on AS OccurredOn,
		                   attempts AS Attempts,
		                   dispatcher_id AS DispatcherId,
		                   dispatcher_timeout AS DispatcherTimeout;
		           """;

		var parameters = new DynamicParameters();
		parameters.Add("DispatcherId", dispatcherId, direction: ParameterDirection.Input);
		parameters.Add("ReservationTimeout", reservationTimeout, direction: ParameterDirection.Input);

		Command = CreateCommand(sql, (DynamicParameters?)parameters, commandTimeout: sqlTimeOutSeconds,
			cancellationToken: cancellationToken);
		ResolveAsync = async conn => await conn.QueryAsync<OutboxMessage>(Command).ConfigureAwait(false);
	}
}
