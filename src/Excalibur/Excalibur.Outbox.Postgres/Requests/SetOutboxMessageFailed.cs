// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data;

namespace Excalibur.Outbox.Postgres;

/// <summary>
/// Records a failed delivery attempt on a main-table outbox message: sets the non-decreasing retry count and
/// the last error on the row (so a sub-ceiling failure is trackable by <c>GetFailedMessages</c> /
/// <c>GetStatistics</c> without a status column — design-preserving, matching the Oracle outbox), frees the
/// reservation, and stamps a failure-anchored visibility floor (<c>next_attempt_at</c>) so the message is
/// re-claimable for retry only after the floor elapses — never in the same drain cycle (no zero-backoff
/// hot-loop) and never terminally (at-least-once). Distinct from dead-lettering, which is the terminal move
/// at the retry ceiling, and from <c>SetOutboxMessageBackoff</c>, which applies the fine-grained computed backoff.
/// </summary>
/// <remarks>
/// The Postgres outbox is delete-on-sent, so a row's mere presence plus a recorded <c>error_message</c> is
/// the "failed but still retryable" signal — a dedicated status column would only ever hold the pending or
/// failed values (Sent rows are deleted, dead-lettered rows are moved to the terminal table), so
/// <c>error_message IS NOT NULL</c> is the minimal discriminator that satisfies the failed-state contract.
/// </remarks>
internal sealed class SetOutboxMessageFailed : DataRequest<int>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="SetOutboxMessageFailed"/> class.
	/// </summary>
	/// <param name="messageId">The unique identifier of the failed message.</param>
	/// <param name="retryCount">
	/// The retry-attempt count reported by the caller. Applied as <c>GREATEST(attempts, @RetryCount)</c> so the
	/// persisted count is non-decreasing across re-claims and a stale lower report cannot weaken termination.
	/// </param>
	/// <param name="errorMessage">The error describing the failure, stored as the message's last error.</param>
	/// <param name="dispatcherId">
	/// The identifier of the dispatcher reporting the failure. The update applies only when the message is
	/// unreserved or reserved by this same dispatcher, so a caller can never clear a reservation it does not hold.
	/// </param>
	/// <param name="failureBackoffFloorSeconds">
	/// The failure-anchored visibility floor F, in seconds. The message becomes re-claimable only after
	/// <c>NOW() + F</c>; F must exceed the poll interval so the plain failure path cannot hot-loop the drain.
	/// </param>
	/// <param name="outboxTableName">The name of the outbox table.</param>
	/// <param name="sqlTimeOutSeconds">The SQL command timeout in seconds.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public SetOutboxMessageFailed(
		string messageId,
		int retryCount,
		string errorMessage,
		string dispatcherId,
		int failureBackoffFloorSeconds,
		string outboxTableName,
		int sqlTimeOutSeconds,
		CancellationToken cancellationToken)
	{
		// Canonical MarkFailedAsync re-claimability contract: a failed (sub-ceiling) message is neither
		// terminal nor immediately re-claimable. Stamp a failure-anchored visibility floor
		// next_attempt_at = NOW() + F (the SERVER clock — never a dispatcher-side timestamp), so the claim
		// predicate (next_attempt_at <= NOW()) defers re-delivery by at least F. F exceeds the poll interval,
		// so the plain (no-computed-backoff) path cannot hot-loop the drain in the same cycle; fine-grained
		// backoff remains SetOutboxMessageBackoff. The floor is applied on the reserved AND the
		// unreserved-input path (stage-then-fail-without-claim, dispatcher_timeout IS NULL) — closing the
		// zero-backoff hole where "let the reservation expire" would give floor = 0. Freeing the reservation
		// is safe because the floor, not the lease, now governs the next claim.
		//
		// attempts = GREATEST(attempts, @RetryCount): attempts are non-decreasing across re-claims, so a
		// stale late failure report with a lower count cannot lower the authoritative value and weaken the
		// processor's DLQ-ceiling (termination) guarantee.
		//
		// The reservation-ownership guard (dispatcher_id IN {NULL, caller}) makes reservation-theft
		// unrepresentable: a failure reported against a lease a DIFFERENT dispatcher now holds matches no row
		// and is a no-op (no double-concurrent-delivery). The IS NULL arm is load-bearing — staging a message
		// and reporting it failed without ever reserving it is a supported path, floored the same way.
		var sql = $"""
			UPDATE {outboxTableName}
			   SET attempts = GREATEST(attempts, @RetryCount),
			       error_message = @ErrorMessage,
			       dispatcher_id = NULL,
			       dispatcher_timeout = NULL,
			       next_attempt_at = NOW() + (@FailureBackoffFloorSeconds || ' seconds')::interval
			   WHERE message_id = @MessageId
			     AND (dispatcher_id IS NULL OR dispatcher_id = @DispatcherId);
			""";

		var parameters = new DynamicParameters();
		parameters.Add("RetryCount", retryCount, direction: ParameterDirection.Input);
		parameters.Add("ErrorMessage", errorMessage, direction: ParameterDirection.Input);
		parameters.Add("MessageId", messageId, direction: ParameterDirection.Input);
		parameters.Add("DispatcherId", dispatcherId, direction: ParameterDirection.Input);
		parameters.Add("FailureBackoffFloorSeconds", failureBackoffFloorSeconds, direction: ParameterDirection.Input);

		Command = CreateCommand(sql, (DynamicParameters?)parameters, commandTimeout: sqlTimeOutSeconds, cancellationToken: cancellationToken);
		ResolveAsync = async conn => await conn.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
