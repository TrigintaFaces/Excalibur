// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Data;

using Dapper;

using Excalibur.Data;

namespace Excalibur.Outbox.Postgres;

/// <summary>
/// The single-row result of a fenced, claim-scoped failure report: the effective high-water token after the
/// fence compare-and-swap, how many rows the guarded update applied to, and whether the row was there at all.
/// </summary>
/// <remarks>
/// <b>Three facts, because the outcome is four-valued and two of them are refusals with opposite
/// remedies.</b> A bare row count collapses "a newer tenure exists" (stop draining entirely), "another claim
/// holds this row" (skip this message, keep draining) and "there is no such row" (nothing was owed) into one
/// zero, and a caller cannot recover the distinction afterwards. All three fields come from the SAME
/// statement, so the classification describes the window the mutation actually ran in.
/// </remarks>
internal sealed class FencedClaimMutationResult
{
	/// <summary>
	/// Gets or sets the effective high-water token for the scope after the compare-and-swap. Equal to the
	/// presented token when it was accepted; strictly greater when a successor tenure has advanced past it.
	/// </summary>
	public long HighWaterToken { get; set; }

	/// <summary>Gets or sets the number of rows the guarded update applied to (0 or 1).</summary>
	public int UpdatedCount { get; set; }

	/// <summary>Gets or sets a value indicating whether the row was present when the statement ran.</summary>
	public bool RowExists { get; set; }
}

/// <summary>
/// Records a delivery failure only if the caller still holds BOTH the leadership tenure and the claim,
/// evaluating the fence, the ownership term and the mutation in a single statement.
/// </summary>
/// <remarks>
/// <para>
/// <b>The fence step WRITES, and the mutation is conditioned on that write accepting the token.</b> The
/// fence common-table-expression runs first: its upsert takes the per-scope row lock and advances the stored
/// high-water to the greater of the existing value and the presented one. The update is then gated on the
/// freshly-advanced high-water equalling the presented token, so a superseded tenure observes a returned
/// high-water strictly greater than its own and writes nothing. Both effects execute in one statement under
/// that row lock, so no fresher tenure can interleave between the check and the write. A scalar subquery
/// over the fence table inside the update's own WHERE clause would release its lock before the update began,
/// which is the same gap wearing a different shape.
/// </para>
/// <para>
/// <b>The mark is per-SCOPE, not per-row, and that is load-bearing rather than incidental.</b> One cell
/// keyed by the outbox table governs every row in it. A per-row mark would leave every row a newer leader
/// has not yet reached unguarded — which is most rows, on every handover — because the superseded tenure's
/// token still equals the untouched mark on those rows.
/// </para>
/// <para>
/// <b>The claim term is a separate predicate and neither term subsumes the other.</b> The fence answers "is
/// my tenure still current"; within one tenure a processor may claim a message, fail, reclaim it and fail
/// again, and the fencing token is identical across those cycles. The claim identity is what separates them.
/// A statement carrying only the fence admits a stale cycle of a live tenure; one carrying only the claim
/// admits a superseded tenure whose claim was never released.
/// </para>
/// <para>
/// <b>Two schedule shapes, composed explicitly rather than through an optional term.</b> With a computed
/// backoff the statement mirrors the scheduled completion — the attempt count increments and the visibility
/// floor is the greater of the caller's delay and the configured floor. Without one it mirrors the plain
/// completion — the attempt count is raised to the caller's absolute value and never lowered, and the floor
/// alone anchors re-claimability. These are two different writes and the fragment says so; an "optional"
/// schedule term folded into one shape would silently give one caller the other's semantics.
/// </para>
/// </remarks>
[NoTenantTerm(
	TenantConfinement.IdentityAddressed,
	"the post-claim completion path, fenced and claim-scoped: the drain has already claimed this row cross-tenant and completes it by its globally-unique message id, gated on the scope fence high-water and on the claim identity stamped at reservation. Those bound which dispatcher generation and which claim may act, not which tenant. This store holds no tenant context to filter by, so the statement carries no tenant term; isolation on this table is established where the row is written, by stamping tenant_id")]
internal sealed class FencedSetOutboxMessageFailed : DataRequest<FencedClaimMutationResult>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="FencedSetOutboxMessageFailed"/> class.
	/// </summary>
	/// <param name="messageId">The unique identifier of the message that failed.</param>
	/// <param name="errorMessage">The error describing the failure.</param>
	/// <param name="retryCount">The absolute attempt count, used only when no backoff schedule is supplied.</param>
	/// <param name="nextAttemptDelaySeconds">
	/// The caller's computed backoff as a delay in seconds, or <see langword="null"/> to record the failure
	/// with no computed schedule.
	/// </param>
	/// <param name="fencingToken">The caller's leadership tenure token.</param>
	/// <param name="claimIdentity">The identity stamped on the row when this claim handed out the message.</param>
	/// <param name="failureBackoffFloorSeconds">The minimum time before the message may be re-claimed.</param>
	/// <param name="outboxTableName">The qualified name of the outbox table.</param>
	/// <param name="fenceTableName">The qualified name of the fence control table.</param>
	/// <param name="sqlTimeOutSeconds">The SQL command timeout in seconds.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public FencedSetOutboxMessageFailed(
		string messageId,
		string errorMessage,
		int retryCount,
		double? nextAttemptDelaySeconds,
		long fencingToken,
		string claimIdentity,
		int failureBackoffFloorSeconds,
		string outboxTableName,
		string fenceTableName,
		int sqlTimeOutSeconds,
		CancellationToken cancellationToken)
	{
		// The two schedule shapes, written out rather than merged. See the remarks for why an "optional"
		// schedule term would hand one caller the other's attempt-count semantics.
		var schedule = nextAttemptDelaySeconds is null
			? """
			           attempts = GREATEST(attempts, @RetryCount),
			                   next_attempt_at = NOW() + make_interval(secs => @FloorSeconds),
			  """
			: """
			           attempts = attempts + 1,
			                   next_attempt_at = NOW() + GREATEST(make_interval(secs => @NextAttemptDelaySeconds), make_interval(secs => @FloorSeconds)),
			  """;

		var sql = $"""
		   WITH fence AS (
		           INSERT INTO {fenceTableName} AS f (scope_key, high_water_token)
		               VALUES (@ScopeKey, @Token)
		               ON CONFLICT (scope_key) DO UPDATE
		                   SET high_water_token = GREATEST(f.high_water_token, @Token)
		               RETURNING high_water_token
		           ),
		           upd AS (
		               UPDATE {outboxTableName}
		                   SET {schedule}
		                       error_message = @ErrorMessage,
		                       dispatcher_id = NULL,
		                       dispatcher_timeout = NULL
		               WHERE message_id = @MessageId
		                 AND dispatcher_id = @DispatcherId
		                 AND (SELECT high_water_token FROM fence) = @Token
		               RETURNING message_id
		           )
		           SELECT (SELECT high_water_token FROM fence) AS HighWaterToken,
		                  (SELECT COUNT(*) FROM upd)::int AS UpdatedCount,
		                  EXISTS (SELECT 1 FROM {outboxTableName} WHERE message_id = @MessageId) AS RowExists;
		   """;

		var parameters = new DynamicParameters();
		parameters.Add("ScopeKey", outboxTableName, direction: ParameterDirection.Input);
		parameters.Add("Token", fencingToken, direction: ParameterDirection.Input);
		parameters.Add("MessageId", messageId, direction: ParameterDirection.Input);
		parameters.Add("DispatcherId", claimIdentity, direction: ParameterDirection.Input);
		parameters.Add("ErrorMessage", errorMessage, direction: ParameterDirection.Input);
		parameters.Add("FloorSeconds", (double)failureBackoffFloorSeconds, direction: ParameterDirection.Input);

		if (nextAttemptDelaySeconds is { } delay)
		{
			parameters.Add("NextAttemptDelaySeconds", delay, direction: ParameterDirection.Input);
		}
		else
		{
			parameters.Add("RetryCount", retryCount, direction: ParameterDirection.Input);
		}

		Command = CreateCommand(sql, (DynamicParameters?)parameters, commandTimeout: sqlTimeOutSeconds,
			cancellationToken: cancellationToken);
		ResolveAsync = async conn => await conn.QuerySingleAsync<FencedClaimMutationResult>(Command).ConfigureAwait(false);
	}
}
