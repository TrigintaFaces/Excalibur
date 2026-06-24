// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data;

namespace Excalibur.Outbox.Postgres;

/// <summary>
/// Data request to transition a message to the terminal dead-lettered state in the Postgres outbox.
/// </summary>
/// <remarks>
/// The Postgres outbox schema uses a separate dead-letter table (not a status column) as the terminal
/// state. This request inserts the message into the dead-letter table with the supplied reason as
/// <c>error_message</c>, then deletes it from the main outbox table. Because the row is removed from
/// the main table, it is structurally excluded from every claim predicate and can never be re-claimed.
/// </remarks>
internal sealed class MarkMessageDeadLetteredRequest : DataRequest<int>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MarkMessageDeadLetteredRequest"/> class.
    /// </summary>
    /// <param name="messageId">The unique identifier of the message to dead-letter.</param>
    /// <param name="reason">The reason the message was dead-lettered.</param>
    /// <param name="outboxTableName">The fully qualified outbox table name.</param>
    /// <param name="deadLetterTableName">The fully qualified dead-letter table name.</param>
    /// <param name="commandTimeout">Command timeout in seconds.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public MarkMessageDeadLetteredRequest(
        string messageId,
        string reason,
        string outboxTableName,
        string deadLetterTableName,
        int commandTimeout,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        ArgumentNullException.ThrowIfNull(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(outboxTableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(deadLetterTableName);

        // Insert into dead-letter table with the supplied reason, then delete from main outbox.
        // Deleting the row is the Postgres idiom for a terminal state — the row is structurally
        // absent from all claim predicates without needing a status column.
        var sql = $"""
                INSERT INTO {deadLetterTableName} (message_id, message_type, message_metadata, message_body, occurred_on, attempts, error_message)
                        SELECT message_id, message_type, message_metadata, message_body, occurred_on, attempts + 1, @Reason
                        FROM {outboxTableName}
                        WHERE message_id = @MessageId;

                        DELETE FROM {outboxTableName}
                        WHERE message_id = @MessageId;
                """;

        var parameters = new DynamicParameters();
        parameters.Add("MessageId", messageId, direction: ParameterDirection.Input);
        parameters.Add("Reason", reason, direction: ParameterDirection.Input);

        Command = CreateCommand(sql, parameters, commandTimeout: commandTimeout, cancellationToken: cancellationToken);
        ResolveAsync = async connection =>
            await connection.ExecuteAsync(Command).ConfigureAwait(false);
    }
}
