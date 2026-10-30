// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.EventSourcing.Postgres.Requests;
using Npgsql;

namespace Excalibur.EventSourcing.Postgres;

public sealed partial class PostgresEventStore
{
    /// <inheritdoc />
    public async ValueTask<AppendResult> AppendWithProjectionsAsync(string aggregateId, string aggregateType,
        IReadOnlyList<IDomainEvent> events, long expectedVersion,
        Func<ProjectionTransaction, CancellationToken, ValueTask> apply, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(apply);
        var applied = false;
        AppendResult result;
        try
        {
            result = await AppendWithOutboxStagingAsync(aggregateId, aggregateType, events, expectedVersion,
                async (transaction, token) =>
                {
                    var postgresTransaction = (NpgsqlTransaction)transaction;
                    var connection = postgresTransaction.Connection!;
                    var tenant = KeyedTenantPartition.FromScope(CurrentTenantScope).TenantId;
                    var table = PgTableName.Format(_schema, _table);
                    var history = await PostgresTransactionalHistory.LoadAsync(postgresTransaction,
                        table, aggregateId, aggregateType, tenant, token).ConfigureAwait(false);

                    // The initial version probe preceded the lock acquisition. A writer may
                    // have committed while we waited. Absent streams remain protected by the
                    // tenant-qualified version unique key; a losing append rolls back its model.
                    var current = await connection.ResolveAsync(new GetCurrentVersionRequest(
                        aggregateId, aggregateType, transaction, CurrentTenantScope, token, _schema, _table)).ConfigureAwait(false);
                    if (current != expectedVersion) { throw new ProjectionHistoryConflict(current); }
                    await apply(new ProjectionTransaction(transaction, history, tenant, table), token).ConfigureAwait(false);
                    applied = true;
                }, cancellationToken).ConfigureAwait(false);
        }
        catch (ProjectionHistoryConflict conflict)
        {
            return AppendResult.CreateConcurrencyConflict(expectedVersion, conflict.Version);
        }

        return result.Success && events.Count != 0 && (!applied || result.Outcome == AppendOutcome.AlreadyCommitted)
            ? AppendResult.CreateUnknown("Events were previously committed, but participation of the current transactional projections is unconfirmed. Reconcile the original operation; do not repeat the business command.")
            : result;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1064:Exceptions should be public", Justification = "Private control signal caught by the owning append method; never crosses the provider boundary.")]
    private sealed class ProjectionHistoryConflict(long version) : Exception
    {
        internal long Version { get; } = version;
    }
}
