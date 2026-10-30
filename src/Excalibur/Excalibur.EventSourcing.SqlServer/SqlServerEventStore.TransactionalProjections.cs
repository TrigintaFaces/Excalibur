// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Data;
using Excalibur.EventSourcing.SqlServer.Requests;
using Microsoft.Data.SqlClient;

namespace Excalibur.EventSourcing.SqlServer;

public sealed partial class SqlServerEventStore
{
    /// <inheritdoc />
    public async ValueTask<AppendResult> AppendWithProjectionsAsync(string aggregateId, string aggregateType,
        IReadOnlyList<IDomainEvent> events, long expectedVersion,
        Func<ProjectionTransaction, CancellationToken, ValueTask> apply, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(apply);
        ArgumentNullException.ThrowIfNull(events);
        var applied = false;
        var result = await AppendWithOutboxStagingAsync(aggregateId, aggregateType, events, expectedVersion,
            async (transaction, token) =>
            {
                var sqlTransaction = (SqlTransaction)transaction;
                var connection = sqlTransaction.Connection!;
                var tenant = KeyedTenantPartition.FromScope(CurrentTenantScope).TenantId;
                var table = SqlTableName.Format(_schema, _table);
                using var command = connection.CreateCommand();
                command.Transaction = sqlTransaction;
                // Keep history stable against erasure until the model and events commit together.
#pragma warning disable CA2100 // Table identifiers are validated by SqlTableName.Format; values are parameters.
                command.CommandText = $"""
                    SELECT EventId, AggregateId, AggregateType, EventType, EventData, Metadata, Version, Timestamp
                    FROM {table} WITH (HOLDLOCK)
                    WHERE AggregateId = @Id AND AggregateType = @Type
                      AND COALESCE(TenantId, @Untenanted) = @Tenant
                    ORDER BY Version
                    """;
#pragma warning restore CA2100
                command.Parameters.AddWithValue("@Id", aggregateId);
                command.Parameters.AddWithValue("@Type", aggregateType);
                command.Parameters.AddWithValue("@Tenant", tenant);
                command.Parameters.AddWithValue("@Untenanted", KeyedTenantPartition.Untenanted.TenantId);
                var history = new List<StoredEvent>();
                await using (var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        history.Add(new StoredEvent(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                            await reader.IsDBNullAsync(4, token).ConfigureAwait(false) ? null : (byte[])reader.GetValue(4), await reader.IsDBNullAsync(5, token).ConfigureAwait(false) ? null : (byte[])reader.GetValue(5),
                            reader.GetInt64(6), await reader.GetFieldValueAsync<DateTimeOffset>(7, token).ConfigureAwait(false)) { TenantId = tenant });
                    }
                }

                await apply(new ProjectionTransaction(transaction, history, tenant, table), token).ConfigureAwait(false);
                applied = true;
            }, cancellationToken).ConfigureAwait(false);

        // A previous plain append could have written identical events without any projection work.
        // Never promote event identity recognition into an attestation of a different unit of work.
        return result.Success && events.Count != 0 && (!applied || result.Outcome == AppendOutcome.AlreadyCommitted)
            ? AppendResult.CreateUnknown("Events were previously committed, but participation of the current transactional projections is unconfirmed. Reconcile the original operation; do not repeat the business command.")
            : result;
    }
}
