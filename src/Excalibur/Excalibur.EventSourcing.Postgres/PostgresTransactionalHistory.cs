// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Npgsql;

namespace Excalibur.EventSourcing.Postgres;

internal static class PostgresTransactionalHistory
{
    internal static async Task<IReadOnlyList<StoredEvent>> LoadAsync(NpgsqlTransaction transaction,
        string table, string aggregateId, string aggregateType, string tenant, CancellationToken cancellationToken)
    {
                    await using var command = transaction.Connection!.CreateCommand();
                    command.Transaction = transaction;
                    // Lock payload rows through commit so an erasure cannot finish between
                    // history observation and projection publication. Do not skip locked rows.
#pragma warning disable CA2100 // Identifiers validated by PgTableName; all values are parameters.
                    command.CommandText = $"""
                        SELECT event_id, aggregate_id, aggregate_type, event_type, event_data, metadata, version, timestamp
                        FROM {table}
                        WHERE aggregate_id = @Id AND aggregate_type = @Type
                          AND COALESCE(tenant_id, @Untenanted) = @Tenant
                        ORDER BY version FOR UPDATE
                        """;
#pragma warning restore CA2100
                    command.Parameters.AddWithValue("Id", aggregateId);
                    command.Parameters.AddWithValue("Type", aggregateType);
                    command.Parameters.AddWithValue("Tenant", tenant);
                    command.Parameters.AddWithValue("Untenanted", Excalibur.Dispatch.KeyedTenantPartition.Untenanted.TenantId);
                    var history = new List<StoredEvent>();
                    await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                        {
                            history.Add(new StoredEvent(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                                await reader.IsDBNullAsync(4, cancellationToken).ConfigureAwait(false) ? null : (byte[])reader.GetValue(4),
                                await reader.IsDBNullAsync(5, cancellationToken).ConfigureAwait(false) ? null : (byte[])reader.GetValue(5),
                                reader.GetInt64(6), await reader.GetFieldValueAsync<DateTimeOffset>(7, cancellationToken).ConfigureAwait(false)) { TenantId = tenant });
                        }
                    }

        return history;
    }
}
