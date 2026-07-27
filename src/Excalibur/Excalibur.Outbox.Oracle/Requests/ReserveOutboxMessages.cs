// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;

using OutboxMessage = Excalibur.Outbox.OutboxMessage;

namespace Excalibur.Outbox.Oracle;

/// <summary>
/// Represents a data request to reserve outbox messages for processing in the Oracle database.
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
	/// <remarks>
	/// The claim stamps <c>dispatcher_id</c> with a value unique to this call (the supplied dispatcher id
	/// followed by a fresh GUID) and the select-back returns only the rows carrying that value, bounded to
	/// <paramref name="batchSize"/>. A reservation is therefore addressable by the exact claim; a request to
	/// release a dispatcher's reservations matches the dispatcher-id <em>prefix</em>.
	/// </remarks>
	public ReserveOutboxMessages(
		string dispatcherId,
		int batchSize,
		int reservationTimeout,
		string outboxTableName,
		int sqlTimeOutSeconds,
		CancellationToken cancellationToken)
	{
		// Oracle claim — PL/SQL cursor with TRUE skip-locked (SA ruling on A0 #4, bead 9wka92).
		// ORA-02014 forbids `FETCH FIRST n`/`ROWNUM` in the same statement as `FOR UPDATE SKIP LOCKED`,
		// so the row cap cannot live in the SELECT. Instead the cap lives on the CURSOR FETCH: Oracle
		// locks rows as they are fetched, so opening a `... FOR UPDATE SKIP LOCKED` cursor and
		// `FETCH BULK COLLECT INTO ... LIMIT n` locks exactly n eligible rows and SKIPS rows already
		// locked by a concurrent reserver's open cursor. Two concurrent reservers therefore claim
		// DISJOINT id sets and neither blocks to timeout on a contended row — the idiomatic Oracle
		// queue-table dequeue (Oracle AQ / NServiceBus Oracle). The FORALL then stamps dispatcher_id on
		// exactly those n rows under the locks; the select-back reads the rows this dispatcher owns.
		var claimSql = $"""
		           DECLARE
		               CURSOR eligible IS
		                   SELECT message_id
		                   FROM {outboxTableName}
		                   WHERE (dispatcher_id IS NULL OR SYSTIMESTAMP > dispatcher_timeout)
		                     AND (next_attempt_at IS NULL OR next_attempt_at <= SYSTIMESTAMP)
		                     AND (scheduled_at IS NULL OR scheduled_at <= SYSTIMESTAMP)
		                   ORDER BY partition_key, sequence_number, occurred_on
		                   FOR UPDATE SKIP LOCKED;
		               TYPE id_table IS TABLE OF {outboxTableName}.message_id%TYPE;
		               claimed_ids id_table;
		           BEGIN
		               OPEN eligible;
		               FETCH eligible BULK COLLECT INTO claimed_ids LIMIT :BatchSize;
		               CLOSE eligible;
		               FORALL i IN 1 .. claimed_ids.COUNT
		                   UPDATE {outboxTableName}
		                   SET dispatcher_id = :ClaimToken,
		                       dispatcher_timeout = SYSTIMESTAMP + NUMTODSINTERVAL(:ReservationTimeout, 'SECOND')
		                   WHERE message_id = claimed_ids(i);
		           END;
		           """;

		var selectSql = $"""
		           SELECT message_id AS MessageId,
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
		           FROM {outboxTableName}
		           WHERE dispatcher_id = :ClaimToken
		           ORDER BY partition_key, sequence_number, occurred_on
		           FETCH FIRST :BatchSize ROWS ONLY
		           """;

		// Per-call claim token: the claim stamps dispatcher_id with a value UNIQUE to THIS call
		// ("{dispatcherId}:{guid}") and the select-back reads exactly the rows it just stamped. Keying the
		// read on the dispatcher id alone returned every row this (process-stable) dispatcher had ever
		// reserved and not completed — unbounded by batchSize, re-dispatching rows a previous still-in-flight
		// poll is delivering. The dispatcher id stays as a prefix so a row still names its owner in a
		// diagnostic query; crash recovery is unaffected because eligibility keys on dispatcher_timeout
		// expiry, not the token. Not security material — it identifies a batch — so a Guid is the right
		// primitive, not a CSPRNG draw.
		var claimToken = FormattableString.Invariant($"{dispatcherId}:{Guid.NewGuid():N}");

		// Per-statement DynamicParameters — the discipline the rest of the Oracle suite follows. The Oracle
		// outbox path uses ODP.NET positional binding (BindByName is NOT set here), so each statement MUST
		// bind against a parameter set whose positional order matches its own placeholders. The claim binds
		// BatchSize → ClaimToken → ReservationTimeout in textual-appearance order; the select-back binds
		// ClaimToken → BatchSize (its :ClaimToken WHERE, then the :BatchSize FETCH FIRST cap) and therefore
		// needs its OWN param set — sharing the claim set would positionally mis-bind and stall the drain.
		var claimParameters = new DynamicParameters();
		claimParameters.Add("BatchSize", batchSize, direction: ParameterDirection.Input);
		claimParameters.Add("ClaimToken", claimToken, direction: ParameterDirection.Input);
		claimParameters.Add("ReservationTimeout", reservationTimeout, direction: ParameterDirection.Input);

		var selectParameters = new DynamicParameters();
		selectParameters.Add("ClaimToken", claimToken, direction: ParameterDirection.Input);
		selectParameters.Add("BatchSize", batchSize, direction: ParameterDirection.Input);

		// Command exposes the read (base telemetry references the resultset-producing statement).
		Command = CreateCommand(selectSql, (DynamicParameters?)selectParameters, commandTimeout: sqlTimeOutSeconds,
			cancellationToken: cancellationToken);
		ResolveAsync = async conn =>
		{
			var claimCommand = new CommandDefinition(claimSql, claimParameters, commandTimeout: sqlTimeOutSeconds,
				cancellationToken: cancellationToken);
			_ = await conn.ExecuteAsync(claimCommand).ConfigureAwait(false);
			return await conn.QueryAsync<OutboxMessage>(Command).ConfigureAwait(false);
		};
	}
}
