// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Benchmarks.EventSourcing;
using Excalibur.Benchmarks;

using Microsoft.Data.SqlClient;

using Tests.Shared.Fixtures;

namespace Excalibur.EventSourcing.Tests.Benchmarks;

[Trait("Category", "Integration")]
[Trait("Component", "EventSourcing")]
[Trait("Pattern", "Regression")]
[Collection("BenchmarkValidationLifecycle")]
public sealed class ConcurrentAppendSqlServerMeasurementShould(SqlServerContainerFixture database)
    : IClassFixture<SqlServerContainerFixture>
{
    [Fact]
    public void ValidateTheRealSqlBenchmarkThroughItsMeasuredLifecycle()
    {
        var previous = Environment.GetEnvironmentVariable("BENCHMARK_SQL_CONNECTIONSTRING");
        try
        {
            Environment.SetEnvironmentVariable("BENCHMARK_SQL_CONNECTIONSTRING", database.ConnectionString);
            var artifacts = Path.Combine(Path.GetTempPath(), "es15-bdn-sql", Guid.NewGuid().ToString("N"));
            BenchmarkExecution.Run(typeof(SqlServerConcurrentAppendBenchmarks).Assembly, [
                "--filter", "*SqlServerConcurrentAppendBenchmarks*", "--artifacts", artifacts,
                "--warmupCount", "1", "--iterationCount", "1",
            ]).ShouldBe(0);
        }
        finally
        {
            Environment.SetEnvironmentVariable("BENCHMARK_SQL_CONNECTIONSTRING", previous);
        }
    }

    [Fact]
    public async Task ValidateRealCommittedWritesAcrossSuccessiveWaves()
    {
        var benchmark = new SqlServerConcurrentAppendBenchmarks(database.ConnectionString) { WriterCount = 4 };
        await benchmark.GlobalSetup();

        for (var iteration = 0; iteration < 2; iteration++)
        {
            benchmark.IterationSetup();
            await benchmark.ConcurrentAppendToDistinctAggregates();
            await benchmark.IterationCleanup();
        }
    }

    [Fact]
    public async Task RejectReportedSuccessWhenDurableEventsHaveDisappeared()
    {
        // Fixture owns this disposable database; no other test classes share its container.
        var benchmark = new SqlServerConcurrentAppendBenchmarks(database.ConnectionString) { WriterCount = 4 };
        await benchmark.GlobalSetup();
        benchmark.IterationSetup();
        await benchmark.ConcurrentAppendToDistinctAggregates();
        await using var connection = new SqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var delete = new SqlCommand("DELETE FROM [dispatch].[ConcurrentAppendEvents];", connection);
        (await delete.ExecuteNonQueryAsync()).ShouldBeGreaterThanOrEqualTo(4);

        await Should.ThrowAsync<InvalidOperationException>(benchmark.IterationCleanup);
    }

    [Fact]
    public async Task RejectUnexpectedAdditionalVersions()
    {
        var benchmark = new SqlServerConcurrentAppendBenchmarks(database.ConnectionString) { WriterCount = 4 };
        await benchmark.GlobalSetup();
        benchmark.IterationSetup();
        await benchmark.ConcurrentAppendToDistinctAggregates();
        await using var connection = new SqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        const string Sql = """
            BEGIN TRANSACTION;
            UPDATE [dispatch].[ConcurrentAppendEventsPosition] SET [Value] = [Value] + 1 WHERE [Id] = 1;
            INSERT INTO [dispatch].[ConcurrentAppendEvents]
                ([Position], [EventId], [AggregateId], [AggregateType], [EventType], [EventData], [Version], [Timestamp], [TenantId])
            SELECT TOP (1) (SELECT [Value] FROM [dispatch].[ConcurrentAppendEventsPosition] WHERE [Id] = 1),
                CONVERT(nvarchar(255), NEWID()), [AggregateId], [AggregateType], [EventType], [EventData], 1, [Timestamp], [TenantId]
            FROM [dispatch].[ConcurrentAppendEvents] ORDER BY [Position] DESC;
            COMMIT;
            """;
        await using var insert = new SqlCommand(Sql, connection);
        _ = await insert.ExecuteNonQueryAsync();

        await Should.ThrowAsync<InvalidOperationException>(benchmark.IterationCleanup);
    }
}
