// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;

using OutboxMessage = Excalibur.Outbox.OutboxMessage;

namespace Excalibur.Outbox.Oracle;

/// <summary>
/// Represents a data request that atomically enforces the leadership-fencing high-water mark AND reserves a
/// batch of outbox messages, holding the fence row lock <em>through</em> the claim so no fresher leader can
/// interleave between the fence check and the reservation.
/// </summary>
/// <remarks>
/// <para>
/// The fence advance (<c>MERGE</c> — strictly monotonic <c>GREATEST</c>) and the SKIP-LOCKED cursor claim
/// run inside one server-side PL/SQL block: the MERGE takes the per-scope fence row lock, the effective
/// high-water is read, and the rows are stamped with the dispatcher claim token <em>only when</em> that
/// high-water equals the presented token. A superseded (stale) leader observes a strictly greater high-water
/// and stamps nothing — a set-based, non-throwing empty claim (the <see cref="IFencedOutboxStore"/> claim
/// contract). Because the fence advance and the reservation execute in the same block under the fence row
/// lock, the check-then-act window the separate fence-then-claim path exposed is closed by construction.
/// </para>
/// <para>
/// The claim stamps <c>dispatcher_id</c> with a value unique to this call ("{dispatcherId}:{guid}") and the
/// select-back returns only the rows carrying that value, bounded to the requested batch size. The claim
/// block and the select-back run on the same connection; the block auto-commits as one unit, so the rows are
/// durably reserved before the select-back reads them.
/// </para>
/// <para>
/// Both statements bind <b>by name</b> (<see cref="OracleDynamicParameters"/> sets
/// <c>OracleCommand.BindByName = true</c>), so each named value is supplied exactly once even where
/// <c>:ScopeKey</c> / <c>:Token</c> recur across the MERGE, the DUP_VAL_ON_INDEX fallback UPDATE, the
/// high-water read, and the fence comparison. The invariant rests on the language, not on a hand-maintained
/// positional insertion order.
/// </para>
/// </remarks>
[NoTenantTerm(
	TenantConfinement.EstateWide,
	"the fenced drain is cross-tenant by design, exactly as the unfenced claim is: one dispatcher serves every tenant and each claimed row carries its own tenant_id. The fence token bounds which dispatcher generation may claim, not which tenant; scoping the claim by tenant would stall delivery for every other tenant")]
internal sealed class FencedReserveOutboxMessages : DataRequest<IEnumerable<IOutboxMessage>>
{

	/// <summary>
	/// Initializes a new instance of the <see cref="FencedReserveOutboxMessages"/> class.
	/// </summary>
	/// <param name="dispatcherId">The unique identifier of the dispatcher reserving the messages.</param>
	/// <param name="batchSize">The maximum number of messages to reserve in this batch.</param>
	/// <param name="reservationTimeout">The reservation window, in SECONDS. A reserved message is not re-claimable by another dispatcher until this many seconds have elapsed.</param>
	/// <param name="fencingToken">The fencing token for the caller's current leadership tenure.</param>
	/// <param name="outboxTableName">The (qualified) name of the outbox table.</param>
	/// <param name="fenceTableName">The (qualified) name of the fence control table.</param>
	/// <param name="sqlTimeOutSeconds">The SQL command timeout in seconds.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public FencedReserveOutboxMessages(
		string dispatcherId,
		int batchSize,
		int reservationTimeout,
		long fencingToken,
		string outboxTableName,
		string fenceTableName,
		int sqlTimeOutSeconds,
		CancellationToken cancellationToken)
	{
		// Fence CAS + SKIP-LOCKED cursor claim in ONE block. The MERGE holds the per-scope fence row lock;
		// the effective high-water is read; the FORALL stamps dispatcher_id on exactly the fetched rows ONLY
		// when the presented token is the high-water (a stale leader stamps nothing -> empty claim). The
		// cursor cap lives on the FETCH (ORA-02014 forbids FETCH FIRST / ROWNUM alongside FOR UPDATE SKIP
		// LOCKED), mirroring ReserveOutboxMessages.
		var claimSql = $"""
		           DECLARE
		               v_high NUMBER;
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
		               BEGIN
		                   MERGE INTO {fenceTableName} f
		                   USING (SELECT :ScopeKey AS scope_key, :Token AS tok FROM dual) src
		                   ON (f.scope_key = src.scope_key)
		                   WHEN MATCHED THEN UPDATE SET f.high_water_token = GREATEST(f.high_water_token, src.tok)
		                   WHEN NOT MATCHED THEN INSERT (scope_key, high_water_token) VALUES (src.scope_key, src.tok);
		               EXCEPTION
		                   WHEN DUP_VAL_ON_INDEX THEN
		                       UPDATE {fenceTableName} SET high_water_token = GREATEST(high_water_token, :Token) WHERE scope_key = :ScopeKey;
		               END;
		               SELECT high_water_token INTO v_high FROM {fenceTableName} WHERE scope_key = :ScopeKey;
		               IF v_high = :Token THEN
		                   OPEN eligible;
		                   FETCH eligible BULK COLLECT INTO claimed_ids LIMIT :BatchSize;
		                   CLOSE eligible;
		                   FORALL i IN 1 .. claimed_ids.COUNT
		                       UPDATE {outboxTableName}
		                       SET dispatcher_id = :ClaimToken,
		                           dispatcher_timeout = SYSTIMESTAMP + NUMTODSINTERVAL(:ReservationTimeout, 'SECOND')
		                       WHERE message_id = claimed_ids(i);
		               END IF;
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

		// Per-call claim token (identifies THIS batch, not security material -> Guid, not a CSPRNG draw),
		// exactly as ReserveOutboxMessages: the select-back reads only the rows this call stamped.
		var claimToken = FormattableString.Invariant($"{dispatcherId}:{Guid.NewGuid():N}");

		// BindByName=true (OracleDynamicParameters): each named value is supplied once even though the claim
		// block references :ScopeKey / :Token across the MERGE, the DUP_VAL_ON_INDEX fallback, and the fence
		// comparison. Insertion order no longer carries meaning.
		var claimParameters = new DynamicParameters();
		claimParameters.Add("ScopeKey", outboxTableName, direction: ParameterDirection.Input);
		claimParameters.Add("Token", fencingToken, direction: ParameterDirection.Input);
		claimParameters.Add("BatchSize", batchSize, direction: ParameterDirection.Input);
		claimParameters.Add("ClaimToken", claimToken, direction: ParameterDirection.Input);
		claimParameters.Add("ReservationTimeout", reservationTimeout, direction: ParameterDirection.Input);

		var selectParameters = new DynamicParameters();
		selectParameters.Add("ClaimToken", claimToken, direction: ParameterDirection.Input);
		selectParameters.Add("BatchSize", batchSize, direction: ParameterDirection.Input);

		// Command exposes the read (base telemetry references the resultset-producing statement).
		Command = new CommandDefinition(selectSql, new OracleDynamicParameters(selectParameters),
			commandTimeout: sqlTimeOutSeconds, cancellationToken: cancellationToken);
		ResolveAsync = async conn =>
		{
			var claimCommand = new CommandDefinition(claimSql, new OracleDynamicParameters(claimParameters),
				commandTimeout: sqlTimeOutSeconds, cancellationToken: cancellationToken);
			_ = await conn.ExecuteAsync(claimCommand).ConfigureAwait(false);
			return await conn.QueryAsync<OutboxMessage>(Command).ConfigureAwait(false);
		};
	}
}
