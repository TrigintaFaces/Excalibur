// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data;

namespace Excalibur.Outbox.SqlServer.Requests;

/// <summary>
/// Data request to transition a message to the terminal <c>DeadLettered</c> status in the outbox after its
/// retry policy is exhausted.
/// </summary>
/// <remarks>
/// Sets <c>Status = 5</c> (<c>OutboxStatus.DeadLettered</c>) and clears the delivery lease so the message is
/// structurally excluded from every claim predicate and can never be re-claimed or re-dead-lettered.
/// </remarks>
public sealed class MarkMessageDeadLetteredRequest : DataRequestBase<IDbConnection, int>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MarkMessageDeadLetteredRequest"/> class.
	/// </summary>
	/// <param name="tableName">The qualified outbox table name.</param>
	/// <param name="messageId">The message ID to dead-letter.</param>
	/// <param name="reason">The reason the message was dead-lettered.</param>
	/// <param name="commandTimeout">Command timeout in seconds.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public MarkMessageDeadLetteredRequest(
		string tableName,
		string messageId,
		string reason,
		int commandTimeout,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentNullException.ThrowIfNull(reason);

		// Status 5 = DeadLettered (terminal). Clear the lease so a stale-lease sweep cannot resurrect it.
		var sql = $"""
			UPDATE {tableName}
			SET Status = 5, LastError = @Reason, LastAttemptAt = @LastAttemptAt, LeasedAt = NULL, LeasedBy = NULL
			WHERE Id = @MessageId
			""";

		var parameters = new DynamicParameters();
		parameters.Add("@MessageId", messageId);
		parameters.Add("@Reason", reason);
		parameters.Add("@LastAttemptAt", DateTimeOffset.UtcNow);

		Command = CreateCommand(sql, parameters, commandTimeout: commandTimeout, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
