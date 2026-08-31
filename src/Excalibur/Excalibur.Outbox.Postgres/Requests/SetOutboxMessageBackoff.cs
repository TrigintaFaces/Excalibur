// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data;

namespace Excalibur.Outbox.Postgres;

/// <summary>
/// Represents a data request that marks a failed outbox message with an exponential-backoff schedule in the
/// Postgres database: it increments the attempt count, records the absolute next-attempt time, and frees the
/// reservation so the message becomes re-claimable -- but only once <c>next_attempt_at</c> has elapsed.
/// </summary>
/// <remarks>
/// This is the Postgres counterpart of the SqlServer outbox's mark-failed-with-backoff path. Clearing
/// <c>dispatcher_id</c>/<c>dispatcher_timeout</c> makes <c>next_attempt_at</c> the sole re-claim gate, so the
/// computed backoff delay genuinely throttles re-delivery (rather than the coarse reservation timeout).
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
	/// <param name="nextAttemptAt">
	/// The caller's computed next-attempt time. Composed with <paramref name="floorSeconds"/> rather than
	/// replacing it: the column receives the later of the two, so this schedule can only defer the next
	/// attempt beyond F, never bring it forward.
	/// </param>
	/// <param name="errorMessage">
	/// The error describing the failure, stored as the message's last error. Written here for the same
	/// reason <c>SetOutboxMessageFailed</c> writes it: on a delete-on-sent table <c>error_message IS NOT
	/// NULL</c> IS the failed-state signal, so a backoff write that omits it schedules the retry correctly
	/// but leaves the failure invisible to the failed-message queries and statistics.
	/// </param>
	/// <param name="dispatcherId">
	/// The identifier of the dispatcher reporting the failure. The update applies only when the message is
	/// unreserved or reserved by this same dispatcher, so a caller can never clear a reservation it does not hold.
	/// </param>
	/// <param name="floorSeconds">
	/// The failure-anchored visibility floor F, in seconds, evaluated against the server clock so a skewed
	/// dispatcher cannot shorten it.
	/// </param>
	/// <param name="outboxTableName">The name of the outbox table.</param>
	/// <param name="sqlTimeOutSeconds">The SQL command timeout in seconds.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <remarks>
	/// The update targets the globally-unique outbox <c>message_id</c>, which addresses exactly one row, so no
	/// tenant predicate is applied: the drain is cross-tenant infrastructure and must always be able to update
	/// the row it claimed, regardless of any ambient tenant context.
	/// </remarks>
	public SetOutboxMessageBackoff(
		string messageId,
		DateTimeOffset nextAttemptAt,
		string errorMessage,
		string dispatcherId,
		int floorSeconds,
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
		// next_attempt_at is the LATER of the caller's computed schedule and the configured floor F, never
		// whichever the caller supplied. Binding the caller's instant alone was the defect: the backoff
		// calculator yields on the order of a second at the first attempt, so a consumer who set a floor of
		// five minutes — as the outbox guarantee contract instructs — was retried a second later, and this is
		// the path the processor PREFERS whenever the store advertises the backoff capability. The same
		// failure WITHOUT the capability correctly waited F, so the capability made the guarantee weaker.
		//
		// GREATEST can only push the next attempt OUT, so relaxing the floor below F is not something ordinary
		// use can express — it takes changing GREATEST to LEAST, a single token.
		//
		// BOTH terms are measured from now(), the same server clock the claim predicate (next_attempt_at <=
		// NOW()) compares against. The caller's schedule arrives as an absolute instant computed from the
		// DISPATCHER's clock; binding it here would straddle two clocks, and where the dispatcher runs ahead
		// of the database the message stays invisible for the whole skew AFTER its backoff has genuinely
		// elapsed. Deferring a due message is not the safe direction — it is a stall bounded by nothing but
		// the skew. The caller's instant is therefore converted to the DELAY it represents before it leaves
		// the dispatcher and re-anchored to now() here, which preserves the caller's intent exactly while
		// leaving one clock on both sides of the comparison.
		//
		// error_message is written here for the same reason the plain mark-failed path writes it: this table is
		// delete-on-sent, so a present row with a non-null error_message IS the "failed but still retryable"
		// signal that GetFailedMessages and GetStatistics select on. Omitting it scheduled the retry correctly
		// while recording no failure at all -- and because the processor PREFERS this path wherever the store
		// advertises the backoff capability, an operator asking why a message had not arrived saw nothing until
		// it dead-lettered.
		var sql = $"""
		   UPDATE {outboxTableName}
		           SET attempts = attempts + 1,
		               error_message = @ErrorMessage,
		               next_attempt_at = now() + GREATEST(make_interval(secs => @NextAttemptDelaySeconds), make_interval(secs => @FloorSeconds)),
		               dispatcher_id = NULL,
		               dispatcher_timeout = NULL
		           WHERE message_id = @MessageId
		             AND (dispatcher_id IS NULL OR dispatcher_id = @DispatcherId);
		   """;

		var parameters = new DynamicParameters();
		parameters.Add("MessageId", messageId, direction: ParameterDirection.Input);
		parameters.Add("ErrorMessage", errorMessage, direction: ParameterDirection.Input);
		// The caller's schedule travels as a DELAY in seconds rather than as an instant, so the statement can
		// re-anchor it on the server clock (see the note above). A schedule that has already elapsed yields a
		// NEGATIVE delay, which is what an elapsed schedule means: composed with the floor it simply loses to
		// it, and with no floor it makes the message due now. Fractional seconds are preserved because
		// make_interval's secs argument is double precision.
		parameters.Add(
			"NextAttemptDelaySeconds",
			(nextAttemptAt - DateTimeOffset.UtcNow).TotalSeconds,
			direction: ParameterDirection.Input);
		parameters.Add("DispatcherId", dispatcherId, direction: ParameterDirection.Input);
		parameters.Add("FloorSeconds", (double)floorSeconds, direction: ParameterDirection.Input);

		Command = CreateCommand(sql, (DynamicParameters?)parameters, commandTimeout: sqlTimeOutSeconds, cancellationToken: cancellationToken);
		ResolveAsync = async conn => await conn.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
