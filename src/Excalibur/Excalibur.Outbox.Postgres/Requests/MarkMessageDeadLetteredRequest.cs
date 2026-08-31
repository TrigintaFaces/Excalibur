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
[NoTenantTerm(
	TenantConfinement.IdentityAddressed,
	"the post-claim mutation path: the drain has already claimed this row cross-tenant and terminates it by its globally-unique message id. This store holds no tenant context to filter by - an outbox store reads no ambient tenant context and accepts a tenant only as an explicit argument - so the statement carries no tenant term, and a caller that supplies a message id it did not obtain from a claim reaches the row behind it. Isolation on this table is established where the row is written, by stamping tenant_id")]
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
    /// <remarks>
    /// <para>
    /// The copy-to-DLQ and delete target the globally-unique outbox <c>message_id</c>, which addresses exactly
    /// one row, so no tenant predicate is applied: the drain is cross-tenant infrastructure and must always be
    /// able to terminate the row it claimed, regardless of any ambient tenant context.
    /// </para>
    /// <para>
    /// The tenant is nonetheless COPIED, as provenance. The delete leaves the dead-letter row as the only
    /// remaining record of the message, so a term this statement does not carry across is destroyed rather
    /// than merely unqueryable: an operator could no longer attribute the entry, and a redrive could no
    /// longer re-enter the partition the message came from.
    /// </para>
    /// </remarks>
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
                INSERT INTO {deadLetterTableName} (message_id, tenant_id, message_type, message_metadata, message_body, occurred_on, attempts, error_message)
                        SELECT message_id, tenant_id, message_type, message_metadata, message_body, occurred_on, attempts + 1, @Reason
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
