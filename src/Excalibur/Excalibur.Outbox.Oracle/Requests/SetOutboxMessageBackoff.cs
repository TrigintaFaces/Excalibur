// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data;

namespace Excalibur.Outbox.Oracle;

/// <summary>
/// Represents a data request that marks a failed outbox message with an exponential-backoff schedule in the
/// Oracle database: it increments the attempt count, records the next-attempt gate as a delay measured from
/// the server clock, and frees the reservation so the message becomes re-claimable -- but only once
/// <c>next_attempt_at</c> has elapsed.
/// </summary>
/// <remarks>
/// This is the Oracle counterpart of the SqlServer outbox's mark-failed-with-backoff path. Clearing
/// <c>dispatcher_id</c>/<c>dispatcher_timeout</c> makes <c>next_attempt_at</c> the sole re-claim gate, so the
/// computed backoff delay genuinely throttles re-delivery (rather than the coarse reservation timeout).
/// The gate is written as <c>SYSTIMESTAMP + max(delay, F)</c>, so the value the claim predicate reads back
/// and the clock it is compared against come from one machine.
/// </remarks>
[NoTenantTerm(
	TenantConfinement.IdentityAddressed,
	"the post-claim mutation path: the drain has already claimed this row cross-tenant and reschedules it by its globally-unique message id. This store holds no tenant context to filter by - an outbox store reads no ambient tenant context and accepts a tenant only as an explicit argument - so the statement carries no tenant term, and a caller that supplies a message id it did not obtain from a claim reaches the row behind it. Isolation on this table is established where the row is written, by stamping tenant_id")]
internal sealed class SetOutboxMessageBackoff : DataRequest<int>
{

	/// <summary>
	/// Initializes a new instance of the <see cref="SetOutboxMessageBackoff"/> class.
	/// </summary>
	/// <param name="messageId">The unique identifier of the message that failed.</param>
	/// <param name="nextAttemptDelaySeconds">
	/// The caller's computed next-attempt schedule expressed as a DELAY in seconds -- deliberately a duration
	/// rather than an instant, so it carries no clock across the wire. Composed with
	/// <paramref name="failureBackoffFloorSeconds"/> rather than replacing it: the column receives the later
	/// of the two, re-anchored on the server clock. A schedule that has already elapsed arrives negative,
	/// which is what an elapsed schedule means; composed with the floor it simply loses to it.
	/// </param>
	/// <param name="failureBackoffFloorSeconds">
	/// The failure-anchored visibility floor F, in seconds. Both terms are measured from <c>SYSTIMESTAMP</c>,
	/// the same server clock the claim predicate compares against, so a skewed dispatcher can neither shorten
	/// F nor defer a message whose backoff has genuinely elapsed.
	/// </param>
	/// <param name="dispatcherId">
	/// The identifier of the dispatcher reporting the failure. The update applies only when the message is
	/// unreserved or reserved by this same dispatcher, so a caller can never clear a reservation it does not hold.
	/// </param>
	/// <param name="outboxTableName">The name of the outbox table.</param>
	/// <param name="sqlTimeOutSeconds">The SQL command timeout in seconds.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public SetOutboxMessageBackoff(
		string messageId,
		double nextAttemptDelaySeconds,
		string dispatcherId,
		int failureBackoffFloorSeconds,
		string outboxTableName,
		int sqlTimeOutSeconds,
		CancellationToken cancellationToken)
	{
		// The reservation guard is what makes clearing the lease SAFE. An unconditional unreserve lets a stalled
		// dispatcher whose lease already expired clear the LIVE reservation of the dispatcher that has since
		// claimed the message; next_attempt_at only DELAYS the third claim, it does not prevent the resulting
		// double delivery. Restricting the update to a message that is either unreserved (dispatcher_id IS NULL)
		// or still held by this caller makes that outcome unrepresentable. This mirrors SetOutboxMessageFailed —
		// the two paths reach the same contract and must guard it identically.
		//
		// Ownership is an EXACT-PREFIX match on the per-call claim token: the reserve path stamps
		// dispatcher_id = "{dispatcherId}:{guid}", so a bare "dispatcher_id = :DispatcherId" matched ZERO rows
		// and this backoff update was a silent no-op on Oracle. Match the dispatcher prefix exactly via
		// SUBSTR(dispatcher_id, 1, LEN) = "{dispatcherId}:", with the prefix + length computed in C# so each
		// parameter is referenced once (ODP.NET positional bind) and no LIKE wildcard can widen the guard.
		// next_attempt_at is the LATER of the caller's computed schedule and the configured floor F, never
		// whichever the caller supplied. Binding the caller's schedule alone was the defect: the backoff
		// calculator yields about a second at the first attempt, so a consumer who configured a floor of five
		// minutes was retried a second later — and this is the path the processor PREFERS whenever the store
		// advertises the backoff capability, so the same failure correctly waited F WITHOUT the capability and
		// ignored it WITH one. GREATEST can only push the attempt out, so relaxing the floor below F is not
		// something ordinary use can express; it takes changing GREATEST to LEAST, a single token.
		//
		// BOTH terms are DURATIONS, and the maximum is taken over the two durations before ONE SYSTIMESTAMP
		// anchors the result. That selects exactly what taking the maximum over two instants selected —
		// gate - now == max(delay, F) — but it leaves NO dispatcher clock in the persisted column. The earlier
		// form composed the caller's absolute instant with a server-anchored floor, so the claim predicate
		// (next_attempt_at <= SYSTIMESTAMP) compared a dispatcher-stamped value against the database's clock:
		// one comparison across two machines that need not agree. A dispatcher running ahead of the database
		// therefore kept a message invisible for the whole skew AFTER its backoff had genuinely elapsed.
		// Deferring a due message is not the harmless direction — it is a delivery stall bounded by nothing
		// but the size of the skew, and a store that never hands a due message back satisfies every safety
		// property while delivering nothing. The caller's instant is converted to the delay it represents on
		// the DISPATCHER's own clock, where both operands come from that one clock and the skew cancels, and
		// the delay is re-anchored on SYSTIMESTAMP here.
		var sql = $"""
		   UPDATE {outboxTableName}
		           SET attempts = attempts + 1,
		               next_attempt_at = SYSTIMESTAMP + NUMTODSINTERVAL(
		                   GREATEST(:NextAttemptDelaySeconds, :FailureBackoffFloorSeconds), 'SECOND'),
		               dispatcher_id = NULL,
		               dispatcher_timeout = NULL
		           WHERE message_id = :MessageId
		             AND (dispatcher_id IS NULL OR SUBSTR(dispatcher_id, 1, :DispatcherPrefixLen) = :DispatcherPrefix)
		   """;

		// ODP.NET binds positionally (this store does not set BindByName), so parameters are added in the
		// exact textual order their placeholders appear: :NextAttemptDelaySeconds then
		// :FailureBackoffFloorSeconds (both inside the SET's GREATEST), then :MessageId (WHERE), then
		// :DispatcherPrefixLen then :DispatcherPrefix (WHERE). Adding a parameter out of this order would
		// silently bind it to the wrong placeholder — the order below mirrors the statement above and must
		// stay that way. The floor parameter is second because it sits second in the statement text, INSIDE
		// the GREATEST; adding it at the end would bind the floor to the message id.
		//
		// Both GREATEST operands are bound as the same numeric kind, so the comparison needs no implicit
		// conversion and fractional seconds survive; NUMTODSINTERVAL converts whichever wins.
		var parameters = new DynamicParameters();
		parameters.Add("NextAttemptDelaySeconds", nextAttemptDelaySeconds, direction: ParameterDirection.Input);
		parameters.Add("FailureBackoffFloorSeconds", (double)failureBackoffFloorSeconds, direction: ParameterDirection.Input);
		parameters.Add("MessageId", messageId, direction: ParameterDirection.Input);
		parameters.Add("DispatcherPrefixLen", dispatcherId.Length + 1, direction: ParameterDirection.Input);
		parameters.Add("DispatcherPrefix", dispatcherId + ":", direction: ParameterDirection.Input);

		Command = CreateCommand(sql, (DynamicParameters?)parameters, commandTimeout: sqlTimeOutSeconds, cancellationToken: cancellationToken);
		ResolveAsync = async conn => await conn.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
