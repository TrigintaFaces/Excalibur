// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Dapper;
using Excalibur.Dispatch;
using Oracle.ManagedDataAccess.Client;

namespace Excalibur.EventSourcing.Oracle;

internal static class OracleTransactionalHistory
{
    internal static async Task<IReadOnlyList<StoredEvent>> LoadAsync(OracleTransaction transaction,
        string table, string aggregateId, string aggregateType, string tenant, CancellationToken cancellationToken)
    {
        // Lock the payload rows through commit, including against concurrent erasure.
        var rows = await transaction.Connection.QueryAsync<EventRow>(new CommandDefinition($"""
            SELECT EVENTID AS EventId, AGGREGATEID AS AggregateId, AGGREGATETYPE AS AggregateType,
                   EVENTTYPE AS EventType, EVENTDATA AS EventData, METADATA AS Metadata,
                   VERSION AS Version, EVENTTIMESTAMP AS "Timestamp"
            FROM {table}
            WHERE AGGREGATEID = :Id AND AGGREGATETYPE = :Type
              AND COALESCE(TENANTID, :Untenanted) = :Tenant
            ORDER BY VERSION FOR UPDATE
            """, new { Id = aggregateId, Type = aggregateType, Untenanted = KeyedTenantPartition.Untenanted.TenantId, Tenant = tenant },
            transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(row => new StoredEvent(row.EventId, row.AggregateId, row.AggregateType, row.EventType,
            row.EventData, row.Metadata, (long)row.Version, row.Timestamp.ToUniversalTime()) { TenantId = tenant }).ToArray();
    }

    private sealed record EventRow(string EventId, string AggregateId, string AggregateType, string EventType,
        byte[]? EventData, byte[]? Metadata, decimal Version, DateTimeOffset Timestamp);
}
