// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;

using OutboxMessage = Excalibur.Outbox.OutboxMessage;

namespace Excalibur.Outbox.Postgres;

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
		// Atomic claim: the CTE locks the eligible rows it selects (FOR UPDATE) and SKIP LOCKED steps
		// over rows already locked by a concurrent dispatcher's in-flight transaction, so each row is
		// claimed by exactly one dispatcher (no overlapping batches -> no double-dispatch). Without the
		// row lock, two concurrent dispatchers could SELECT overlapping rows before either UPDATE landed
		// and both would claim the same message. This mirrors the SqlServer claim's READPAST/UPDLOCK/
		// ROWLOCK hints (GetUnsentMessagesRequest.cs). The locking clause MUST follow LIMIT.
		//
		// The claim is wrapped in a CTE and re-ordered by an OUTER SELECT. That is load-bearing, not
		// redundant with the inner ORDER BY: the inner one decides WHICH rows are claimed (the oldest
		// per partition), and does not decide the order they are returned in. UPDATE ... RETURNING
		// emits rows in whatever order the executor produces, so reading the RETURNING directly handed
		// the dispatcher its batch in an arbitrary order and messages left the outbox out of sequence --
		// the very ordering this table carries partition_key and sequence_number to provide. Both
		// clauses are required: the inner for selection, the outer for delivery order.
		var sql = $"""
		           WITH cte_outbox AS (
		                   SELECT message_id
		                   FROM {outboxTableName}
		                   WHERE (dispatcher_id IS NULL OR NOW() > dispatcher_timeout)
		                     AND (next_attempt_at IS NULL OR next_attempt_at <= NOW())
		                     AND (scheduled_at IS NULL OR scheduled_at <= NOW())
		                   ORDER BY partition_key, sequence_number, occurred_on
		                   LIMIT {batchSize}
		                   FOR UPDATE SKIP LOCKED
		                   ),
		                   claimed AS (
		                   UPDATE {outboxTableName}
		                   SET dispatcher_id = @DispatcherId,
		                   dispatcher_timeout = NOW() + (@ReservationTimeout || ' seconds')::interval
		                   WHERE message_id IN (SELECT message_id FROM cte_outbox)
		                   RETURNING message_id AS MessageId,
		                   message_type AS MessageType,
		                   message_metadata AS MessageMetadata,
		                   message_body AS MessageBody,
		                   tenant_id AS TenantId,
		                   destination AS Destination,
		                   correlation_id AS CorrelationId,
		                   causation_id AS CausationId,
		                   priority AS Priority,
		                   scheduled_at AS ScheduledAt,
		                   partition_key AS PartitionKey,
		                   group_key AS GroupKey,
		                   sequence_number AS SequenceNumber,
		                   target_transports AS TargetTransports,
		                   is_multi_transport AS IsMultiTransport,
		                   occurred_on AS CreatedAt,
		                   attempts AS Attempts,
		                   dispatcher_id AS DispatcherId,
		                   dispatcher_timeout AS DispatcherTimeout
		                   )
		                   SELECT * FROM claimed
		                   ORDER BY PartitionKey, SequenceNumber, CreatedAt;
		           """;

		var parameters = new DynamicParameters();
		parameters.Add("DispatcherId", dispatcherId, direction: ParameterDirection.Input);
		parameters.Add("ReservationTimeout", reservationTimeout, direction: ParameterDirection.Input);

		Command = CreateCommand(sql, (DynamicParameters?)parameters, commandTimeout: sqlTimeOutSeconds,
			cancellationToken: cancellationToken);
		ResolveAsync = async conn => await conn.QueryAsync<OutboxMessage>(Command).ConfigureAwait(false);
	}
}
