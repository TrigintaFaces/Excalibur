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
/// This is the Postgres counterpart of the SqlServer outbox's mark-failed-with-backoff path (q29qfg). Clearing
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
	/// <param name="outboxTableName">The name of the outbox table.</param>
	/// <param name="sqlTimeOutSeconds">The SQL command timeout in seconds.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public SetOutboxMessageBackoff(
		string messageId,
		DateTimeOffset nextAttemptAt,
		string outboxTableName,
		int sqlTimeOutSeconds,
		CancellationToken cancellationToken)
	{
		var sql = $"""
		   UPDATE {outboxTableName}
		           SET attempts = attempts + 1,
		               next_attempt_at = @NextAttemptAt,
		               dispatcher_id = NULL,
		               dispatcher_timeout = NULL
		           WHERE message_id = @MessageId;
		   """;

		var parameters = new DynamicParameters();
		parameters.Add("MessageId", messageId, direction: ParameterDirection.Input);
		parameters.Add("NextAttemptAt", nextAttemptAt, direction: ParameterDirection.Input);

		Command = CreateCommand(sql, (DynamicParameters?)parameters, commandTimeout: sqlTimeOutSeconds, cancellationToken: cancellationToken);
		ResolveAsync = async conn => await conn.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
