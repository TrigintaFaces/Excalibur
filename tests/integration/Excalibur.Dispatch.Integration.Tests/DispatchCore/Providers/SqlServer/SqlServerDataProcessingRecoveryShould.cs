// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Data;
using Dapper;
using Excalibur.Data.DataProcessing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Tests.Shared;
using Tests.Shared.Categories;
using Tests.Shared.Fixtures;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.SqlServer;

[IntegrationTest]
[Collection(ContainerCollections.SqlServerCdc)]
[Trait("Category", "Integration")]
[Trait("Component", "DataProcessing")]
[Trait("Pattern", "Recovery")]
public sealed class SqlServerDataProcessingRecoveryShould(SqlServerCdcContainerFixture fixture) : IntegrationTestBase
{
    [Fact]
    public async Task ContinueIndependentTasksButReportAPartiallyFailedCycle()
    {
        var connection = await CreateDatabaseAsync();
        await using var services = new ServiceCollection().BuildServiceProvider();
        var manager = CreateManager(connection, services, async (recordType, checkpoint, token) =>
        {
            if (recordType == "broken")
            {
                throw new InvalidOperationException("Handler failed");
            }
            await checkpoint(1, "complete", token);
            return 1;
        });
        var failedId = await manager.AddDataTaskForRecordTypeAsync("broken", TestCancellationToken);
        await manager.AddDataTaskForRecordTypeAsync("healthy", TestCancellationToken);

        var failure = await Should.ThrowAsync<AggregateException>(async () => await manager.ProcessDataTasksAsync(TestCancellationToken));
        failure.InnerExceptions.Single().Message.ShouldBe("Handler failed");
        await using var sql = new SqlConnection(connection);
        var remaining = (await sql.QueryAsync<DataTaskRequest>("SELECT * FROM dbo.Tasks")).ToList();
        remaining.Count.ShouldBe(1);
        remaining[0].DataTaskId.ShouldBe(failedId);
        remaining[0].Attempts.ShouldBe(1);
    }

    [Fact]
    public async Task StopTheStaleTaskWhenItsRowDisappearsAndProcessTheNextTask()
    {
        var connection = await CreateDatabaseAsync();
        await using var services = new ServiceCollection().BuildServiceProvider();
        var staleStopped = false;
        var healthyProcessed = false;
        var manager = CreateManager(connection, services, async (recordType, checkpoint, token) =>
        {
            if (recordType == "stale")
            {
                await ExecuteAsync(connection, "DELETE dbo.Tasks WHERE RecordType=N'stale';");
                await Should.ThrowAsync<OperationCanceledException>(() => checkpoint(1, "stale", token));
                staleStopped = token.IsCancellationRequested;
                token.ThrowIfCancellationRequested();
            }
            healthyProcessed = true;
            await checkpoint(1, "complete", token);
            return 1;
        });
        await manager.AddDataTaskForRecordTypeAsync("stale", TestCancellationToken);
        await manager.AddDataTaskForRecordTypeAsync("healthy", TestCancellationToken);

        await manager.ProcessDataTasksAsync(TestCancellationToken);
        staleStopped.ShouldBeTrue();
        healthyProcessed.ShouldBeTrue();
        await using var sql = new SqlConnection(connection);
        (await sql.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Tasks")).ShouldBe(0);
    }

    [Fact]
    public async Task KeepTheLastProcessingAttemptAvailableWhenCleanupFails()
    {
        var connection = await CreateDatabaseAsync();
        await using var services = new ServiceCollection().BuildServiceProvider();
        var manager = CreateManager(connection, services, async (_, checkpoint, token) =>
        {
            await checkpoint(1, "complete", token);
            return 1;
        });
        await manager.AddDataTaskForRecordTypeAsync("healthy", TestCancellationToken);
        await ExecuteAsync(connection, "UPDATE dbo.Tasks SET Attempts=MaxAttempts-1;");
        await ExecuteAsync(connection, "CREATE TRIGGER dbo.RefuseCleanup ON dbo.Tasks INSTEAD OF DELETE AS THROW 51000, 'Injected cleanup failure', 1;");

        await Should.ThrowAsync<AggregateException>(async () => await manager.ProcessDataTasksAsync(TestCancellationToken));
        await using var sql = new SqlConnection(connection);
        var task = await sql.QuerySingleAsync<DataTaskRequest>("SELECT * FROM dbo.Tasks");
        task.Attempts.ShouldBe(task.MaxAttempts - 1);
        task.CompletedCount.ShouldBe(1);
        await ExecuteAsync(connection, "DROP TRIGGER dbo.RefuseCleanup;");
        await manager.ProcessDataTasksAsync(TestCancellationToken);
        (await sql.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Tasks")).ShouldBe(0);
    }

    [Fact]
    public async Task ExcludeACompetingWorkerUntilTheOwnerFinishes()
    {
        var connection = await CreateDatabaseAsync();
        await using var services = new ServiceCollection().BuildServiceProvider();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var manager = CreateManager(connection, services, async (_, checkpoint, token) =>
        {
            Interlocked.Increment(ref calls);
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
            await checkpoint(1, "done", token);
            return 1;
        });
        await manager.AddDataTaskForRecordTypeAsync("shared", TestCancellationToken);
        var owner = manager.ProcessDataTasksAsync(TestCancellationToken).AsTask();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(15), TestCancellationToken);
            await manager.ProcessDataTasksAsync(TestCancellationToken);
            calls.ShouldBe(1);
        }
        finally
        {
            release.TrySetResult();
            await owner;
        }
        await using var sql = new SqlConnection(connection);
        (await sql.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Tasks")).ShouldBe(0);
    }

    [Fact]
    public async Task ReleaseOwnershipAfterCancellationWithoutChargingAnAttempt()
    {
        var connection = await CreateDatabaseAsync();
        await using var services = new ServiceCollection().BuildServiceProvider();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);
        var owner = CreateManager(connection, services, async (_, _, token) =>
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return 0;
        });
        await owner.AddDataTaskForRecordTypeAsync("cancelled", TestCancellationToken);
        var run = owner.ProcessDataTasksAsync(cancellation.Token).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(15), TestCancellationToken);
        await cancellation.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => run);
        await using var sql = new SqlConnection(connection);
        (await sql.ExecuteScalarAsync<int>("SELECT Attempts FROM dbo.Tasks")).ShouldBe(0);
        var successor = CreateManager(connection, services, async (_, checkpoint, token) =>
        {
            await checkpoint(1, "done", token);
            return 1;
        });
        await successor.ProcessDataTasksAsync(TestCancellationToken);
        (await sql.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Tasks")).ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FenceTheOldWorkerAfterItsSqlSessionIsKilled(bool checkpointBeforeReturn)
    {
        var connection = await CreateDatabaseAsync();
        await using var services = new ServiceCollection().BuildServiceProvider();
        var entered = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseOld = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var successorEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSuccessor = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        SqlConnection? claimConnection = null;
        var owner = new DataOrchestrationManager(() => claimConnection = new SqlConnection(connection),
            new ScriptRegistry(async (_, checkpoint, token) =>
            {
                entered.TrySetResult(await claimConnection!.ExecuteScalarAsync<int>("SELECT @@SPID"));
                await releaseOld.Task.WaitAsync(token);
                if (checkpointBeforeReturn) { await checkpoint(999, "stale", token); }
                return 999;
            }), services, Microsoft.Extensions.Options.Options.Create(new DataProcessingOptions { SchemaName = "dbo", TableName = "Tasks" }),
            NullLogger<DataOrchestrationManager>.Instance);
        await owner.AddDataTaskForRecordTypeAsync("fenced", TestCancellationToken);
        var oldRun = owner.ProcessDataTasksAsync(TestCancellationToken).AsTask();
        Task? newRun = null;
        try
        {
            var spid = await entered.Task.WaitAsync(TimeSpan.FromSeconds(15), TestCancellationToken);
            await ExecuteAsync(connection, $"KILL {spid};");
            var successor = CreateManager(connection, services, async (_, checkpoint, token) =>
            {
                await checkpoint(1, "successor", token);
                successorEntered.TrySetResult();
                await releaseSuccessor.Task.WaitAsync(token);
                return 1;
            });
            newRun = successor.ProcessDataTasksAsync(TestCancellationToken).AsTask();
            await successorEntered.Task.WaitAsync(TimeSpan.FromSeconds(15), TestCancellationToken);
            releaseOld.TrySetResult();
            await Should.ThrowAsync<AggregateException>(() => oldRun);
            await using var sql = new SqlConnection(connection);
            var row = await sql.QuerySingleAsync<DataTaskRequest>("SELECT * FROM dbo.Tasks");
            row.CompletedCount.ShouldBe(1);
            row.ProcessedCursor.ShouldBe("successor");
            row.Attempts.ShouldBe(0);
        }
        finally
        {
            releaseOld.TrySetResult();
            releaseSuccessor.TrySetResult();
            try { await oldRun; } catch (AggregateException) { }
            if (newRun is not null) { await newRun; }
        }
    }

    [Fact]
    public async Task ObserveThrowingDeadlineCallbacksAndKeepTheTaskForRetry()
    {
        var connection = await CreateDatabaseAsync();
        await using var services = new ServiceCollection().BuildServiceProvider();
        var manager = new DataOrchestrationManager(() => new SqlConnection(connection),
            new ScriptRegistry(async (_, _, token) =>
            {
                using var registration = token.Register(() => throw new InvalidOperationException("Cancellation callback failed"));
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return 0;
            }), services, Microsoft.Extensions.Options.Options.Create(new DataProcessingOptions
            { SchemaName = "dbo", TableName = "Tasks", DispatcherTimeoutMilliseconds = 50 }),
            NullLogger<DataOrchestrationManager>.Instance);
        await manager.AddDataTaskForRecordTypeAsync("timeout", TestCancellationToken);
        var failure = await Should.ThrowAsync<AggregateException>(async () => await manager.ProcessDataTasksAsync(TestCancellationToken));
        failure.InnerExceptions.Single().ShouldBeOfType<TimeoutException>();
        await using var sql = new SqlConnection(connection);
        var row = await sql.QuerySingleAsync<DataTaskRequest>("SELECT * FROM dbo.Tasks");
        row.Attempts.ShouldBe(1);
        row.CompletedCount.ShouldBe(0);
    }

    private async Task<string> CreateDatabaseAsync()
    {
        fixture.DockerAvailable.ShouldBeTrue("This regression requires an actual SQL Server.");
        var database = "ProcessingRecovery" + Guid.NewGuid().ToString("N")[..12];
        await ExecuteAsync(fixture.ConnectionString, $"CREATE DATABASE [{database}];");
        var connection = new SqlConnectionStringBuilder(fixture.ConnectionString) { InitialCatalog = database }.ConnectionString;
        await ExecuteAsync(connection, """
            CREATE TABLE dbo.Tasks(DataTaskId uniqueidentifier PRIMARY KEY, CreatedAt datetimeoffset NOT NULL,
                RecordType nvarchar(256) NOT NULL, Attempts int NOT NULL DEFAULT 0, MaxAttempts int NOT NULL,
                CompletedCount bigint NOT NULL DEFAULT 0, FetchCursor nvarchar(512) NULL, ProcessedCursor nvarchar(512) NULL);
            """);
        return connection;
    }

    private async Task ExecuteAsync(string connection, string statement)
    {
        await using var sql = new SqlConnection(connection);
        await sql.ExecuteAsync(new CommandDefinition(statement, cancellationToken: TestCancellationToken));
    }

    private static DataOrchestrationManager CreateManager(string connection, IServiceProvider services,
        Func<string, UpdateCompletedCount, CancellationToken, Task<long>> run) =>
        new(() => new SqlConnection(connection), new ScriptRegistry(run), services,
            Microsoft.Extensions.Options.Options.Create(new DataProcessingOptions { SchemaName = "dbo", TableName = "Tasks" }),
            NullLogger<DataOrchestrationManager>.Instance);

    private sealed class ScriptRegistry(Func<string, UpdateCompletedCount, CancellationToken, Task<long>> run) : IDataProcessorRegistry
    {
        public bool TryGetFactory(string recordType, out Func<IServiceProvider, IDataProcessor> processor)
        {
            processor = GetFactory(recordType);
            return true;
        }
        public Func<IServiceProvider, IDataProcessor> GetFactory(string recordType) => _ => new ScriptProcessor(recordType, run);
    }

    private sealed class ScriptProcessor(string recordType, Func<string, UpdateCompletedCount, CancellationToken, Task<long>> run) : IDataProcessor
    {
        public Task<long> RunAsync(long completedCount, string? processedCursor, UpdateCompletedCount updateCompletedCount, CancellationToken cancellationToken) =>
            run(recordType, updateCompletedCount, cancellationToken);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
