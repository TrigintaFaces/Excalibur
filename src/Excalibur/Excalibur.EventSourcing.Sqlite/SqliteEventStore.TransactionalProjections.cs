// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data.Common;
using Dapper;
using Excalibur.Dispatch;
using Microsoft.Data.Sqlite;

namespace Excalibur.EventSourcing.Sqlite;

public sealed partial class SqliteEventStore
{
    /// <inheritdoc />
    public async ValueTask<AppendResult> AppendWithProjectionsAsync(string aggregateId, string aggregateType,
        IReadOnlyList<IDomainEvent> events, long expectedVersion,
        Func<ProjectionTransaction, CancellationToken, ValueTask> apply, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(apply);
        var result = await AppendCommitBoundary.ExecuteAsync(state =>
            AppendCoreAsync(aggregateId, aggregateType, events, expectedVersion, apply, state, cancellationToken)).ConfigureAwait(false);
        return result.Outcome == AppendOutcome.AlreadyCommitted && events.Count != 0
            ? AppendResult.CreateUnknown("Events were previously committed, but participation of the current transactional projections is unconfirmed. Reconcile the original operation.")
            : result;
    }

    // Fault injection remains below the dispatch boundary, so a lost acknowledgement can be exercised.
    internal Func<DbTransaction, CancellationToken, Task> CommitTransactionAsync { get; init; } =
        static (transaction, token) => transaction.CommitAsync(token);

    private async Task<IReadOnlyList<StoredEvent>> ReadTransactionalHistoryAsync(SqliteConnection connection,
        DbTransaction transaction, string aggregateId, string aggregateType, string tenant, CancellationToken token)
    {
        var rows = await connection.QueryAsync<StoredEventRow>(new CommandDefinition(
            $"SELECT EventId, AggregateId, AggregateType, EventType, EventData, Metadata, Version, Timestamp FROM [{_table}] "
            + "WHERE AggregateId = @AggregateId AND AggregateType = @AggregateType AND TenantId = @Tenant ORDER BY Version",
            new { AggregateId = aggregateId, AggregateType = aggregateType, Tenant = tenant }, transaction,
            cancellationToken: token)).ConfigureAwait(false);
        return rows.Select(row => new StoredEvent(row.EventId, row.AggregateId, row.AggregateType, row.EventType,
            row.EventData, row.Metadata, row.Version, DateTimeOffset.Parse(row.Timestamp, System.Globalization.CultureInfo.InvariantCulture))
            { TenantId = tenant }).ToArray();
    }
}
