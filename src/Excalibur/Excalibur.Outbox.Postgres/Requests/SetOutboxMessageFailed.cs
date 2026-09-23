// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Data;

using Dapper;

using Excalibur.Data;

namespace Excalibur.Outbox.Postgres;

/// <summary>
/// What one claim-scoped failure statement observed, in the single action that decided the mutation.
/// </summary>
/// <remarks>
/// Two fields rather than a rowcount, because a bare zero conflates a lost claim with an absent row and
/// those map to opposite outcomes. Both are read in one statement, so they share its snapshot.
/// </remarks>
internal sealed class ClaimMutationResult
{
	/// <summary>Gets or sets the number of rows the guarded update applied to.</summary>
	public int UpdatedCount { get; set; }

	/// <summary>Gets or sets a value indicating whether the row was present when the statement ran.</summary>
	public bool RowExists { get; set; }
}

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
[NoTenantTerm(
	TenantConfinement.IdentityAddressed,
	"the post-claim mutation path: the drain has already claimed this row cross-tenant and marks failed it by its globally-unique message id. This store holds no tenant context to filter by - an outbox store reads no ambient tenant context and accepts a tenant only as an explicit argument - so the statement carries no tenant term, and a caller that supplies a message id it did not obtain from a claim reaches the row behind it. Isolation on this table is established where the row is written, by stamping tenant_id")]
internal sealed class SetOutboxMessageFailed : DataRequest<ClaimMutationResult>
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
	/// <param name="claimIdentity">
	/// The claim under which the caller received this message, or <see langword="null"/> when the caller
	/// holds no claim. Supplying it adds an EXACT ownership term so the update applies only to that claim;
	/// omitting it produces a statement WITHOUT the term rather than one whose term is satisfiable by
	/// absence. Two cycles of one process share a process identity, so only the claim half discriminates
	/// the case this guard exists for.
	/// </param>
	/// <param name="processIdentityPrefix">
	/// The PROCESS half of this store's own claim identity, with its separator -- presented when the
	/// caller holds no claim. It cannot name a specific claim, but it refuses a failure report from every
	/// OTHER process, conceding only two cycles of this one. Pass <see langword="null"/> to guard on
	/// nothing, which lets any process release any claim and is never correct for a live store.
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
		string? claimIdentity,
		string? processIdentityPrefix,
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
		// The CLAIM-ownership guard: the caller presents the identity stamped on the row when this exact
		// claim handed it the message, and the mutation applies only to that claim. An exact match, not a
		// prefix: the prefix is the PROCESS half, and two cycles of one process share it -- which is the
		// case this guard exists for.
		//
		// There is no "dispatcher_id IS NULL" arm. It used to admit a stage-then-fail-without-claiming path,
		// and that path is not reachable: every caller of this member in the framework is a drain or a
		// pass-through decorator, and none reports a failure against a row it never claimed. The arm's only
		// reachable effect was admitting a caller whose claim had already been released by a competitor's
		// success -- which is exactly the write it was supposed to exclude.
		// TWO STATEMENTS, not one predicate with a hole in it. A caller that holds a claim gets the
		// ownership term; a caller that does not gets a statement WITHOUT it. An "optional" term would be
		// the removed IS NULL arm rebuilt under a new name -- a guard you can satisfy by omitting the thing
		// it guards is not a guard.
		// TWO GRAINS, chosen by what the caller can present -- never nothing.
		//
		//   holds a claim   AND dispatcher_id = @DispatcherId        exact, refuses every other claim
		//   holds none      AND (dispatcher_id IS NULL OR left(dispatcher_id, n) = @Prefix)
		//                  -- the IS NULL arm is NOT optional: left(NULL, n) = @Prefix is NULL, not
		//                  -- true, so without it a message that was never claimed can never be failed.
		//
		// The prefix term is weaker than the exact one, and weaker is not absent. It refuses every FOREIGN
		// process -- the whole safety property -- and concedes only two cycles of one process, which the
		// exact term already covers for any caller that holds a claim. The previous form reasoned from
		// 'the prefix is weaker' to 'carry no term', and an empty predicate let any process release any
		// claim: the row is matched on message_id alone, dispatcher_id is set NULL and next_attempt_at
		// re-stamped, so a second dispatcher takes a message the first is still delivering.
		//
		// left() with a C#-computed length, never LIKE: a % or _ in a consumer's identity would widen the
		// guard. Same shape SQL Server landed for this defect and Oracle already shipped.
		var ownership = claimIdentity is not null
			? " AND dispatcher_id = @DispatcherId"
			: processIdentityPrefix is { Length: > 0 }
				? " AND (dispatcher_id IS NULL OR left(dispatcher_id, @ProcessPrefixLength) = @ProcessPrefix)"
				: string.Empty;

		var sql = $"""
			WITH upd AS (
			    UPDATE {outboxTableName}
			       SET attempts = GREATEST(attempts, @RetryCount),
			           error_message = @ErrorMessage,
			           dispatcher_id = NULL,
			           dispatcher_timeout = NULL,
			           next_attempt_at = NOW() + (@FailureBackoffFloorSeconds || ' seconds')::interval
			       WHERE message_id = @MessageId{ownership}
			    RETURNING message_id
			)
			SELECT (SELECT COUNT(*) FROM upd)::int AS UpdatedCount,
			       EXISTS (SELECT 1 FROM {outboxTableName} WHERE message_id = @MessageId) AS RowExists;
			""";

		var parameters = new DynamicParameters();
		parameters.Add("RetryCount", retryCount, direction: ParameterDirection.Input);
		parameters.Add("ErrorMessage", errorMessage, direction: ParameterDirection.Input);
		parameters.Add("MessageId", messageId, direction: ParameterDirection.Input);
		if (claimIdentity is not null)
		{
			parameters.Add("DispatcherId", claimIdentity, direction: ParameterDirection.Input);
		}
		if (claimIdentity is null && processIdentityPrefix is { Length: > 0 })
		{
			parameters.Add("ProcessPrefix", processIdentityPrefix, direction: ParameterDirection.Input);
			parameters.Add("ProcessPrefixLength", processIdentityPrefix.Length, direction: ParameterDirection.Input);
		}

		parameters.Add("FailureBackoffFloorSeconds", failureBackoffFloorSeconds, direction: ParameterDirection.Input);

		Command = CreateCommand(sql, (DynamicParameters?)parameters, commandTimeout: sqlTimeOutSeconds, cancellationToken: cancellationToken);
		ResolveAsync = async conn => await conn.QuerySingleAsync<ClaimMutationResult>(Command).ConfigureAwait(false);
	}
}
