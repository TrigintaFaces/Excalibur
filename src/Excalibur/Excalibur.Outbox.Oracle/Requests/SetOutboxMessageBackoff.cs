// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data;

namespace Excalibur.Outbox.Oracle;

/// <summary>
/// Represents a data request that marks a failed outbox message with an exponential-backoff schedule in the
/// Oracle database: it increments the attempt count, records the absolute next-attempt time, and frees the
/// reservation so the message becomes re-claimable -- but only once <c>next_attempt_at</c> has elapsed.
/// </summary>
/// <remarks>
/// This is the Oracle counterpart of the SqlServer outbox's mark-failed-with-backoff path. Clearing
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
		//
		// Ownership is an EXACT-PREFIX match on the per-call claim token: the reserve path stamps
		// dispatcher_id = "{dispatcherId}:{guid}", so a bare "dispatcher_id = :DispatcherId" matched ZERO rows
		// and this backoff update was a silent no-op on Oracle. Match the dispatcher prefix exactly via
		// SUBSTR(dispatcher_id, 1, LEN) = "{dispatcherId}:", with the prefix + length computed in C# so each
		// parameter is referenced once (ODP.NET positional bind) and no LIKE wildcard can widen the guard.
		var sql = $"""
		   UPDATE {outboxTableName}
		           SET attempts = attempts + 1,
		               next_attempt_at = :NextAttemptAt,
		               dispatcher_id = NULL,
		               dispatcher_timeout = NULL
		           WHERE message_id = :MessageId
		             AND (dispatcher_id IS NULL OR SUBSTR(dispatcher_id, 1, :DispatcherPrefixLen) = :DispatcherPrefix)
		   """;

		// ODP.NET binds positionally (this store does not set BindByName), so parameters are added in the
		// exact textual order their placeholders appear: :NextAttemptAt (SET), then :MessageId (WHERE), then
		// :DispatcherPrefixLen then :DispatcherPrefix (WHERE). Adding a parameter out of this order would
		// silently bind it to the wrong placeholder — the order below mirrors the statement above and must stay that way.
		var parameters = new DynamicParameters();
		parameters.Add("NextAttemptAt", nextAttemptAt, direction: ParameterDirection.Input);
		parameters.Add("MessageId", messageId, direction: ParameterDirection.Input);
		parameters.Add("DispatcherPrefixLen", dispatcherId.Length + 1, direction: ParameterDirection.Input);
		parameters.Add("DispatcherPrefix", dispatcherId + ":", direction: ParameterDirection.Input);

		Command = CreateCommand(sql, (DynamicParameters?)parameters, commandTimeout: sqlTimeOutSeconds, cancellationToken: cancellationToken);
		ResolveAsync = async conn => await conn.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
