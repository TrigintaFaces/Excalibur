// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace Excalibur.Dispatch.Benchmarks.Patterns;

/// <summary>Provisions only the fresh SQL container owned by a SQL benchmark.</summary>
internal static class SqlServerBenchmarkSchema
{
    internal const string ResourceName = "BenchmarkSchema.001_CreateEventStoreSchema.sql";

    internal const string OutboxResourceName = "BenchmarkSchema.001_CreateOutboxSchema.sql";

    internal static Task CreateAsync(MsSqlContainer ownedContainer) =>
        CreateResourceAsync(ownedContainer, ResourceName);

    internal static Task CreateOutboxAsync(MsSqlContainer ownedContainer) =>
        CreateResourceAsync(ownedContainer, OutboxResourceName);

    private static async Task CreateResourceAsync(MsSqlContainer ownedContainer, string resourceName)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var cancellationToken = deadline.Token;
        await using var connection = new SqlConnection(ownedContainer.GetConnectionString());
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var stream = typeof(SqlServerBenchmarkSchema).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("Missing shipped SQL benchmark schema resource");
        using var reader = new StreamReader(stream);
        var batch = new StringBuilder();
        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            if (string.Equals(line.Trim(), "GO", StringComparison.OrdinalIgnoreCase))
            {
                await ExecuteBatchAsync(batch.ToString()).ConfigureAwait(false);
                batch.Clear();
            }
            else
            {
                batch.AppendLine(line);
            }
        }

        if (batch.Length > 0)
        {
            await ExecuteBatchAsync(batch.ToString()).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        async Task ExecuteBatchAsync(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql)) return;
#pragma warning disable CA2100 // SQL comes exclusively from the fixed embedded shipped schema; no user input or identifier replacement.
            await using var command = new SqlCommand(sql, connection, transaction);
#pragma warning restore CA2100
            command.CommandTimeout = 30;
            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Resets a quiescent, exclusively owned fixture database outside the measured invocation.</summary>
    internal static async Task ResetEventsAsync(MsSqlContainer ownedContainer)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var cancellationToken = deadline.Token;
        await using var connection = new SqlConnection(ownedContainer.GetConnectionString());
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var reset = new SqlCommand("TRUNCATE TABLE [dbo].[EventStoreEvents]; UPDATE [dbo].[EventStoreEventsPosition] SET [Value] = 0 WHERE [Id] = 1;", connection, transaction);
        reset.CommandTimeout = 20;
        if (await reset.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != 1)
        {
            throw new InvalidOperationException("Benchmark position-counter invariant failed; reset was not committed");
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
