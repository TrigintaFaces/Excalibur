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
internal sealed class SetOutboxMessageBackoff : DataRequest<int>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="SetOutboxMessageBackoff"/> class.
	/// </summary>
	/// <param name="messageId">The unique identifier of the message that failed.</param>
	/// <param name="nextAttemptAt">The absolute time before which the message must not be re-claimed.</param>
	/// <param name="dispatcherId">
	/// The identifier of the dispatcher reporting the failure. The update applies only when the message is
	/// unreserved or reserved by this same dispatcher, so a caller can never clear a reservation it does not hold.
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
		string dispatcherId,
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
		var sql = $"""
		   UPDATE {outboxTableName}
		           SET attempts = attempts + 1,
		               next_attempt_at = @NextAttemptAt,
		               dispatcher_id = NULL,
		               dispatcher_timeout = NULL
		           WHERE message_id = @MessageId
		             AND (dispatcher_id IS NULL OR dispatcher_id = @DispatcherId);
		   """;

		var parameters = new DynamicParameters();
		parameters.Add("MessageId", messageId, direction: ParameterDirection.Input);
		// next_attempt_at is timestamptz, and Npgsql writes a DateTimeOffset to that type only at offset 0 —
		// a raw bind rejects any other offset outright ("only offset 0 (UTC) is supported") rather than
		// converting, so a caller passing a local-offset instant would throw and the backoff would never be
		// recorded. TimestampTzParameter normalises to UTC, preserving the instant. scheduled_at already binds
		// through it; this path is the sibling that did not, despite the same column type and the same CLR type.
		parameters.Add("NextAttemptAt", new TimestampTzParameter(nextAttemptAt));
		parameters.Add("DispatcherId", dispatcherId, direction: ParameterDirection.Input);

		Command = CreateCommand(sql, (DynamicParameters?)parameters, commandTimeout: sqlTimeOutSeconds, cancellationToken: cancellationToken);
		ResolveAsync = async conn => await conn.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
