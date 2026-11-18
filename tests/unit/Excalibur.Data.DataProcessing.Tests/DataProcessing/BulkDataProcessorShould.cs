// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using Excalibur.Data.DataProcessing;
using Excalibur.EventSourcing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;

namespace Excalibur.Data.Tests.DataProcessing;

[UnitTest]
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait(TraitNames.Pattern, "Regression")]
public sealed class BulkDataProcessorShould
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreserveSequentialCheckpointsAndDeferOnlyForBulk(bool bulk)
    {
        var events = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton<IRecordHandler<int>>(new Handler(async (n, _) =>
        {
            InlineProjectionWait.IsSuppressed.ShouldBe(bulk);
            events.Add($"start{n}");
            await Task.Yield();
            events.Add($"end{n}");
        }));
        services.AddSingleton<IProjectionProcessor>(new Projection(_ =>
        {
            InlineProjectionWait.IsSuppressed.ShouldBeFalse();
            events.Add("fold");
            return Task.FromResult(CaughtUp);
        }));
        await using var provider = services.BuildServiceProvider();
        await using DataProcessor<int> processor = bulk ? new Bulk(provider) : new Regular(provider);
        var count = await processor.RunAsync(7, null, (n, cursor, _) =>
        {
            events.Add($"checkpoint{n}:{cursor}");
            return Task.CompletedTask;
        }, CancellationToken.None);
        count.ShouldBe(9);
        events.ShouldBe(bulk
            ? ["start1", "end1", "checkpoint8:", "start2", "end2", "checkpoint9:next", "fold"]
            : ["start1", "end1", "checkpoint8:", "start2", "end2", "checkpoint9:next"]);
        InlineProjectionWait.IsSuppressed.ShouldBeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SkipFoldAfterHandlerOrCheckpointFailure(bool checkpointFailure)
    {
        var folds = 0;
        var services = new ServiceCollection();
        services.AddSingleton<IRecordHandler<int>>(new Handler((_, _) =>
            checkpointFailure ? Task.CompletedTask : Task.FromException(new InvalidOperationException("handler"))));
        services.AddSingleton<IProjectionProcessor>(new Projection(_ => { folds++; return Task.FromResult(CaughtUp); }));
        await using var provider = services.BuildServiceProvider();
        await using var processor = new Bulk(provider);
        await Should.ThrowAsync<InvalidOperationException>(() => processor.RunAsync(0, null,
            (_, _, _) => Task.FromException(new InvalidOperationException("checkpoint")), CancellationToken.None));
        folds.ShouldBe(0);
        InlineProjectionWait.IsSuppressed.ShouldBeFalse();
    }

    [Fact]
    public async Task RequireProjectionProcessorBeforeHandlingRecords()
    {
        var handled = 0;
        var services = new ServiceCollection();
        services.AddSingleton<IRecordHandler<int>>(new Handler((_, _) => { handled++; return Task.CompletedTask; }));
        await using var provider = services.BuildServiceProvider();
        await using var processor = new Bulk(provider);
        await Should.ThrowAsync<InvalidOperationException>(() => processor.RunAsync(0, null, Checkpoint, CancellationToken.None));
        handled.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PropagateFoldFailureWithoutUndoingCheckpoints(bool cancelled)
    {
        var checkpoints = 0;
        var services = Services(new Projection(_ => cancelled
            ? Task.FromResult(new ProjectionProcessingResult(0, ProjectionProcessingOutcome.Cancelled, ProjectionHaltReason.None))
            : Task.FromException<ProjectionProcessingResult>(new InvalidOperationException("fold"))));
        await using var provider = services.BuildServiceProvider();
        await using var processor = new Bulk(provider);
        var run = processor.RunAsync(0, null, (_, _, _) => { checkpoints++; return Task.CompletedTask; }, CancellationToken.None);
        if (cancelled) await Should.ThrowAsync<OperationCanceledException>(() => run);
        else await Should.ThrowAsync<InvalidOperationException>(() => run);
        checkpoints.ShouldBe(2);
    }

    [Fact]
    public async Task RestoreOuterSuppressionDuringFold()
    {
        using var suppression = InlineProjectionWait.Suppress();
        var services = Services(new Projection(_ =>
        {
            InlineProjectionWait.IsSuppressed.ShouldBeTrue();
            return Task.FromResult(CaughtUp);
        }));
        await using var provider = services.BuildServiceProvider();
        await using var processor = new Bulk(provider);
        await processor.RunAsync(0, null, Checkpoint, CancellationToken.None);
        InlineProjectionWait.IsSuppressed.ShouldBeTrue();
    }

    [Theory]
    [InlineData(ProjectionProcessingOutcome.NotLeader)]
    [InlineData(ProjectionProcessingOutcome.Contested)]
    [InlineData(ProjectionProcessingOutcome.Halted)]
    [InlineData(ProjectionProcessingOutcome.BackgroundWorkerOwnsCursor)]
    public async Task PreserveCommittedCountWhenCatchUpReportsLag(ProjectionProcessingOutcome outcome)
    {
        var services = Services(new Projection(_ => Task.FromResult(
            new ProjectionProcessingResult(0, outcome, ProjectionHaltReason.None))));
        await using var provider = services.BuildServiceProvider();
        var logger = new RecordingLogger();
        await using var processor = new Bulk(provider, logger);
        (await processor.RunAsync(0, null, Checkpoint, CancellationToken.None)).ShouldBe(2);
        logger.Messages.ShouldContain(message => message.Level == LogLevel.Warning && message.Text.Contains(outcome.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task PublishRunOwnershipBeforeResolvingProjectionServices()
    {
        Bulk? processor = null;
        Task? disposal = null;
        var handled = 0;
        var services = new ServiceCollection();
        services.AddSingleton<IRecordHandler<int>>(new Handler((_, _) => { handled++; return Task.CompletedTask; }));
        services.AddScoped<IProjectionProcessor>(_ =>
        {
            disposal = processor!.DisposeAsync().AsTask();
            return new Projection(_ => Task.FromResult(CaughtUp));
        });
        await using var provider = services.BuildServiceProvider();
        await using var owned = processor = new Bulk(provider);
        var run = processor.RunAsync(0, null, Checkpoint, CancellationToken.None);
        await Should.ThrowAsync<OperationCanceledException>(() => run.WaitAsync(TimeSpan.FromSeconds(10)));
        await disposal.ShouldNotBeNull().WaitAsync(TimeSpan.FromSeconds(10));
        handled.ShouldBe(0);
    }

    [Fact]
    public async Task JoinFoldAndScopeDisposalOnShutdown()
    {
        var entered = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var projection = new Projection(async token =>
        {
            using var registration = token.Register(() => cancelled.TrySetResult());
            entered.SetResult(token);
            await release.Task;
            return CaughtUp;
        });
        var services = new ServiceCollection();
        services.AddSingleton<IRecordHandler<int>>(new Handler((_, _) => Task.CompletedTask));
        services.AddScoped<IProjectionProcessor>(_ => projection);
        await using var provider = services.BuildServiceProvider();
        var processor = new Bulk(provider);
        var run = processor.RunAsync(0, null, Checkpoint, CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var disposal = processor.DisposeAsync().AsTask();
        try
        {
            await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
            disposal.IsCompleted.ShouldBeFalse();
            projection.Disposed.ShouldBeFalse();
        }
        finally { release.TrySetResult(); }
        await disposal.WaitAsync(TimeSpan.FromSeconds(10));
        await Should.ThrowAsync<OperationCanceledException>(() => run);
        projection.Disposed.ShouldBeTrue();
    }

    [Fact]
    public async Task ObserveCancellationDuringProjectionScopeCleanup()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var projection = new Projection(_ => Task.FromResult(CaughtUp), async () =>
        {
            entered.SetResult();
            await release.Task;
        });
        var services = new ServiceCollection();
        services.AddSingleton<IRecordHandler<int>>(new Handler((_, _) => Task.CompletedTask));
        services.AddScoped<IProjectionProcessor>(_ => projection);
        await using var provider = services.BuildServiceProvider();
        await using var processor = new Bulk(provider);
        using var cancellation = new CancellationTokenSource();
        var run = processor.RunAsync(0, null, Checkpoint, cancellation.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            cancellation.Cancel();
        }
        finally { release.TrySetResult(); }
        await Should.ThrowAsync<OperationCanceledException>(() => run);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreservePrimaryFailureWhenProjectionScopeCleanupFails(bool throwingLogger)
    {
        var projection = new Projection(_ => Task.FromException<ProjectionProcessingResult>(new InvalidOperationException("primary")),
            () => Task.FromException(new IOException("cleanup")));
        var services = new ServiceCollection();
        services.AddSingleton<IRecordHandler<int>>(new Handler((_, _) => Task.CompletedTask));
        services.AddScoped<IProjectionProcessor>(_ => projection);
        await using var provider = services.BuildServiceProvider();
        await using var processor = new Bulk(provider, throwingLogger ? new ThrowingLogger() : NullLogger.Instance);
        var failure = await Should.ThrowAsync<InvalidOperationException>(() => processor.RunAsync(0, null, Checkpoint, CancellationToken.None));
        failure.Message.ShouldBe("primary");
    }

    private static ProjectionProcessingResult CaughtUp => new(0, ProjectionProcessingOutcome.CaughtUp, ProjectionHaltReason.None);
    private static Task Checkpoint(long count, string? cursor, CancellationToken token) => Task.CompletedTask;
    private static ServiceCollection Services(IProjectionProcessor projection)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRecordHandler<int>>(new Handler((_, _) => Task.CompletedTask));
        services.AddSingleton(projection);
        return services;
    }
    private sealed class Handler(Func<int, CancellationToken, Task> action) : IRecordHandler<int>
    {
        public Task ProcessAsync(int record, CancellationToken cancellationToken) => action(record, cancellationToken);
    }
    private sealed class Projection(Func<CancellationToken, Task<ProjectionProcessingResult>> action, Func<Task>? cleanup = null) : IProjectionProcessor, IAsyncDisposable
    {
        public bool Disposed { get; private set; }
        public Task<ProjectionProcessingResult> ProcessPendingAsync(CancellationToken cancellationToken) => action(cancellationToken);
        public async ValueTask DisposeAsync() { if (cleanup is not null) await cleanup(); Disposed = true; }
    }
    private static Task<CursorFetchResult<int>> Fetch(string? cursor) => Task.FromResult(
        cursor is null ? new CursorFetchResult<int>([1, 2], "next") : new CursorFetchResult<int>([], null));
    private sealed class ThrowingLogger : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (exception?.Message == "cleanup") throw new IOException("logger");
        }
    }
    private sealed class RecordingLogger : ILogger
    {
        public List<(LogLevel Level, string Text)> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add((logLevel, formatter(state, exception)));
    }
    private sealed class Bulk(IServiceProvider services, ILogger? logger = null) : BulkDataProcessor<int>(A.Fake<IHostApplicationLifetime>(),
        Options.Create(new DataProcessingOptions()), services, logger ?? NullLogger.Instance)
    {
        public override Task<CursorFetchResult<int>> FetchBatchAsync(string? cursor, int batchSize, CancellationToken cancellationToken) => Fetch(cursor);
    }
    private sealed class Regular(IServiceProvider services) : DataProcessor<int>(A.Fake<IHostApplicationLifetime>(),
        Options.Create(new DataProcessingOptions()), services, NullLogger.Instance)
    {
        public override Task<CursorFetchResult<int>> FetchBatchAsync(string? cursor, int batchSize, CancellationToken cancellationToken) => Fetch(cursor);
    }
}
