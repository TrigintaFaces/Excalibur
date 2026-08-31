// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;

using OutboxMessage = Excalibur.Outbox.OutboxMessage;

namespace Excalibur.Outbox.Postgres;

/// <summary>
/// Represents a data request that atomically enforces the leadership-fencing high-water mark AND reserves a
/// batch of outbox messages in a <em>single</em> statement, so no fresher leader can advance the high-water
/// between the fence check and the claim.
/// </summary>
/// <remarks>
/// <para>
/// The statement is a writable common-table-expression (wCTE). The <c>fence</c> CTE runs the fence
/// compare-and-swap (<c>INSERT … ON CONFLICT DO UPDATE … RETURNING</c>): PostgreSQL takes the per-scope
/// fence row lock and advances the stored high-water to <c>GREATEST(existing, presented)</c> — strictly
/// monotonic. The <c>cte_outbox</c> claim CTE selects the eligible rows <c>FOR UPDATE SKIP LOCKED</c>
/// (each row claimed by exactly one dispatcher, no overlapping batches), gated on
/// <c>(SELECT high_water_token FROM fence) = @Token</c> — so rows are claimed <em>only</em> when the
/// freshly-advanced high-water equals the presented token (the token was accepted). A superseded (stale)
/// leader observes a returned high-water strictly greater than its token, the gate is false for every row,
/// and the claim yields the empty set — a set-based, non-throwing signal, exactly as the
/// <see cref="IFencedOutboxStore"/> claim contract requires.
/// </para>
/// <para>
/// Because the whole wCTE executes as one statement, it runs in a single (implicitly-committed) transaction:
/// the fence row lock taken by the fence CTE is held until the statement completes, so a concurrent fresher
/// leader's fence blocks on that lock until this claim commits — the check-then-act window the separate
/// fence-then-claim path exposed is closed by construction, without holding an explicit multi-statement
/// transaction on the shared connection.
/// </para>
/// <para>
/// The claim stamps <c>dispatcher_id</c>/<c>dispatcher_timeout</c> and the <c>RETURNING</c> clause returns
/// exactly the claimed rows, bounded to the requested batch size. The claim targets the outbox rows by their
/// eligibility predicate (no tenant filter): the drain is cross-tenant infrastructure and each returned row
/// carries its own persisted <c>tenant_id</c>.
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
		// Fence CAS + SKIP-LOCKED claim folded into ONE wCTE (Shape 1). The fence CTE takes the per-scope
		// fence row lock and advances the high-water; the claim CTE selects FOR UPDATE SKIP LOCKED gated on
		// the fence returning EXACTLY the presented token (a stale leader's gate is false -> empty claim, no
		// throw). The locking clause MUST follow LIMIT. Because it is one statement, the fence row lock is
		// held through the claim under the implicit transaction -- no explicit BeginTransaction on the shared
		// connection (which monopolizes/poisons it).
		var sql = $"""
		           WITH fence AS (
		                   INSERT INTO {fenceTableName} AS f (scope_key, high_water_token)
		                       VALUES (@ScopeKey, @Token)
		                       ON CONFLICT (scope_key) DO UPDATE
		                           SET high_water_token = GREATEST(f.high_water_token, @Token)
		                       RETURNING high_water_token
		                   ),
		                   cte_outbox AS (
		                   SELECT message_id
		                   FROM {outboxTableName}
		                   WHERE (dispatcher_id IS NULL OR NOW() > dispatcher_timeout)
		                     AND (next_attempt_at IS NULL OR next_attempt_at <= NOW())
		                     AND (scheduled_at IS NULL OR scheduled_at <= NOW())
		                     AND (SELECT high_water_token FROM fence) = @Token
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
		                   -- The inner ORDER BY chooses WHICH rows are claimed; it does not decide the
		                   -- order they come back in, because UPDATE ... RETURNING emits rows in whatever
		                   -- order the executor produces. Without this outer sort the fenced claim handed
		                   -- the dispatcher its batch unordered, which is the path that matters most: a
		                   -- fenced claim runs in the multi-instance deployments partition ordering exists
		                   -- for. Kept identical to the unfenced sibling so the two cannot drift.
		                   SELECT * FROM claimed
		                   ORDER BY PartitionKey, SequenceNumber, CreatedAt;
		           """;

		var parameters = new DynamicParameters();
		parameters.Add("ScopeKey", outboxTableName, direction: ParameterDirection.Input);
		parameters.Add("Token", fencingToken, direction: ParameterDirection.Input);
		parameters.Add("DispatcherId", dispatcherId, direction: ParameterDirection.Input);
		parameters.Add("ReservationTimeout", reservationTimeout, direction: ParameterDirection.Input);

		Command = CreateCommand(sql, (DynamicParameters?)parameters, commandTimeout: sqlTimeOutSeconds,
			cancellationToken: cancellationToken);
		ResolveAsync = async conn => await conn.QueryAsync<OutboxMessage>(Command).ConfigureAwait(false);
	}
}
