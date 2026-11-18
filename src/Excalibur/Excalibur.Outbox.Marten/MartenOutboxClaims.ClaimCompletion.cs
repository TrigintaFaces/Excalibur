// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Npgsql;

namespace Excalibur.Outbox.Marten;

internal static partial class MartenOutboxClaims
{
    // Identifiers are validated by Qualify; all values are bound. All callers own the transaction.
#pragma warning disable CA2100 // Framework SQL with schema/table identifiers validated by Qualify.
    internal static async Task<HashSet<string>> ClaimInTransactionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        string schema, string table, string[] ids, string acquisition, TimeSpan timeout, CancellationToken token)
    {
        var qualified = Qualify(schema, table);
        await using var command = new NpgsqlCommand($"""
            INSERT INTO {qualified} (message_id, dispatcher_id, claimed_at, next_attempt_at)
            SELECT id, @Acquisition, clock_timestamp(), NULL
            FROM unnest(@Ids) WITH ORDINALITY AS ids(id, lock_order) ORDER BY lock_order
            ON CONFLICT (message_id) DO UPDATE
                SET dispatcher_id = EXCLUDED.dispatcher_id, claimed_at = EXCLUDED.claimed_at, next_attempt_at = NULL
                WHERE {qualified}.dispatcher_id <> @Terminal
                  AND {qualified}.claimed_at < clock_timestamp() - make_interval(secs => @Timeout)
                  AND ({qualified}.next_attempt_at IS NULL OR {qualified}.next_attempt_at <= clock_timestamp())
            RETURNING message_id;
            """, connection, transaction);
        // The same CLR ordinal ordering is used by cleanup. Database collation must not reorder locks.
        command.Parameters.AddWithValue("Ids", ids.Order(StringComparer.Ordinal).ToArray());
        command.Parameters.AddWithValue("Acquisition", acquisition);
        command.Parameters.AddWithValue("Terminal", TerminalDispatcherId);
        command.Parameters.AddWithValue("Timeout", timeout.TotalSeconds);
        var result = new HashSet<string>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        while (await reader.ReadAsync(token).ConfigureAwait(false)) { result.Add(reader.GetString(0)); }
        return result;
    }

    internal static async Task<DateTimeOffset> GetDatabaseTimeAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken token)
    {
        await using var command = new NpgsqlCommand("SELECT clock_timestamp();", connection, transaction);
        return new DateTimeOffset((DateTime)(await command.ExecuteScalarAsync(token).ConfigureAwait(false))!);
    }

    internal static async Task LockAcquisitionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        string schema, string table, string id, CancellationToken token)
    {
        await using var command = new NpgsqlCommand($"SELECT message_id FROM {Qualify(schema, table)} WHERE message_id = @Id FOR UPDATE;", connection, transaction);
        command.Parameters.AddWithValue("Id", id);
        await command.ExecuteScalarAsync(token).ConfigureAwait(false);
    }

    internal static async Task<bool> IsLiveAcquisitionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        string schema, string table, string id, string acquisition, TimeSpan timeout, CancellationToken token)
    {
        await using var command = new NpgsqlCommand($"""
            SELECT EXISTS (SELECT 1 FROM {Qualify(schema, table)}
                WHERE message_id = @Id AND dispatcher_id = @Acquisition
                  AND claimed_at > clock_timestamp() - make_interval(secs => @Timeout));
            """, connection, transaction);
        command.Parameters.AddWithValue("Id", id);
        command.Parameters.AddWithValue("Acquisition", acquisition);
        command.Parameters.AddWithValue("Timeout", timeout.TotalSeconds);
        return (bool)(await command.ExecuteScalarAsync(token).ConfigureAwait(false))!;
    }

    internal static async Task ReleaseAcquisitionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        string schema, string table, string id, string acquisition, CancellationToken token)
    {
        await using var command = new NpgsqlCommand($"DELETE FROM {Qualify(schema, table)} WHERE message_id = @Id AND dispatcher_id = @Acquisition;", connection, transaction);
        command.Parameters.AddWithValue("Id", id);
        command.Parameters.AddWithValue("Acquisition", acquisition);
        await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
    }

    internal static async Task PurgeInTransactionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        string schema, string table, string id, CancellationToken token)
    {
        await using var command = new NpgsqlCommand($"DELETE FROM {Qualify(schema, table)} WHERE message_id = @Id;", connection, transaction);
        command.Parameters.AddWithValue("Id", id);
        await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
    }

    internal static async Task<bool> TrySettleInTransactionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        string schema, string table, string id, CancellationToken token)
    {
        var qualified = Qualify(schema, table);
        await using var command = new NpgsqlCommand($"""
            INSERT INTO {qualified} (message_id, dispatcher_id, claimed_at)
            VALUES (@Id, @Terminal, clock_timestamp())
            ON CONFLICT (message_id) DO UPDATE
                SET dispatcher_id = @Terminal, claimed_at = clock_timestamp()
                WHERE {qualified}.dispatcher_id <> @Terminal
            RETURNING message_id;
            """, connection, transaction);
        command.Parameters.AddWithValue("Id", id);
        command.Parameters.AddWithValue("Terminal", TerminalDispatcherId);
        return await command.ExecuteScalarAsync(token).ConfigureAwait(false) is not null;
    }

    internal static async Task<bool> TransitionAcquisitionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        string schema, string table, string id, string acquisition, TimeSpan timeout, bool sent,
        int floorSeconds, DateTimeOffset? requested, CancellationToken token)
    {
        await using var command = new NpgsqlCommand($"""
            UPDATE {Qualify(schema, table)}
            SET dispatcher_id = CASE WHEN @Sent THEN @Terminal ELSE @Released END,
                claimed_at = CASE WHEN @Sent THEN clock_timestamp() ELSE TIMESTAMPTZ 'epoch' END,
                next_attempt_at = CASE WHEN @Sent THEN NULL ELSE
                    GREATEST(clock_timestamp() + make_interval(secs => @Floor), @Requested) END
            WHERE message_id = @Id AND dispatcher_id = @Acquisition
              AND claimed_at > clock_timestamp() - make_interval(secs => @Timeout)
            RETURNING message_id;
            """, connection, transaction);
        command.Parameters.AddWithValue("Id", id);
        command.Parameters.AddWithValue("Acquisition", acquisition);
        command.Parameters.AddWithValue("Timeout", timeout.TotalSeconds);
        command.Parameters.AddWithValue("Sent", sent);
        command.Parameters.AddWithValue("Terminal", TerminalDispatcherId);
        command.Parameters.AddWithValue("Released", "__excalibur_released__");
        command.Parameters.AddWithValue("Floor", floorSeconds);
        command.Parameters.Add("Requested", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = requested is { } value ? value.ToUniversalTime() : DBNull.Value;
        return await command.ExecuteScalarAsync(token).ConfigureAwait(false) is not null;
    }
#pragma warning restore CA2100
}
