// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using Excalibur.Data.DataProcessing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.DataProcessing;

[UnitTest]
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait(TraitNames.Pattern, "Regression")]
public sealed class DataProcessorRecoveryShould
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefuseToCheckpointPastFailure(bool checkpointFailure)
    {
        var handled = new List<int>();
        var checkpoints = new List<string?>();
        var services = new ServiceCollection();
        services.AddSingleton<IRecordHandler<int>>(new Handler((n, _) =>
        {
            handled.Add(n);
            if (n == 1 && !checkpointFailure) throw new InvalidOperationException("poison");
            return Task.CompletedTask;
        }));
        await using var provider = services.BuildServiceProvider();
        await using var processor = new Processor(provider, (cursor, _) => Task.FromResult(
            cursor is null ? new CursorFetchResult<int>([1, 2], "next") : new CursorFetchResult<int>([], null)));
        await Should.ThrowAsync<InvalidOperationException>(() => processor.RunAsync(0, null, (_, cursor, _) =>
        {
            if (checkpointFailure) throw new InvalidOperationException("checkpoint unavailable");
            checkpoints.Add(cursor);
            return Task.CompletedTask;
        }, CancellationToken.None));
        handled.ShouldBe([1]);
        checkpoints.ShouldBeEmpty();
    }

    [Fact]
    public async Task TraverseEmptyNonterminalPage()
    {
        var handled = new List<int>();
        var services = new ServiceCollection();
        services.AddSingleton<IRecordHandler<int>>(new Handler((n, _) => { handled.Add(n); return Task.CompletedTask; }));
        await using var provider = services.BuildServiceProvider();
        await using var processor = new Processor(provider, (cursor, _) => Task.FromResult(
            cursor is null ? new CursorFetchResult<int>([], "next") : new CursorFetchResult<int>([42], null)));
        var count = await processor.RunAsync(0, null, (_, _, _) => Task.CompletedTask, CancellationToken.None);
        count.ShouldBe(1);
        handled.ShouldBe([42]);
    }

    [Fact]
    public async Task RefuseMissingHandler()
    {
        await using var provider = new ServiceCollection().BuildServiceProvider();
        await using var processor = new Processor(provider, (_, _) => Task.FromResult(new CursorFetchResult<int>([1], null)));
        var checkpoints = 0;
        await Should.ThrowAsync<InvalidOperationException>(() => processor.RunAsync(0, null,
            (_, _, _) => { checkpoints++; return Task.CompletedTask; }, CancellationToken.None));
        checkpoints.ShouldBe(0);
    }

    [Fact]
    public async Task PropagateCancellationOnFinalRecord()
    {
        using var cancellation = new CancellationTokenSource();
        var services = new ServiceCollection();
        services.AddSingleton<IRecordHandler<int>>(new Handler((_, _) => { cancellation.Cancel(); return Task.CompletedTask; }));
        await using var provider = services.BuildServiceProvider();
        await using var processor = new Processor(provider, (_, _) => Task.FromResult(new CursorFetchResult<int>([1], null)));
        var checkpoints = 0;
        await Should.ThrowAsync<OperationCanceledException>(() => processor.RunAsync(0, null,
            (_, _, _) => { checkpoints++; return Task.CompletedTask; }, cancellation.Token));
        checkpoints.ShouldBe(0);
    }

    [Fact]
    public async Task JoinConcurrentDisposalAndDisposeEveryOwnedRecordExactlyOnce()
    {
        // Publishes the handler's own token so the test can order the release AFTER cancellation is
        // observable on it. Without that ordering the handler resumes before disposal has requested
        // cancellation, returns normally, and the consumer dispatches a SECOND record -- which re-enters
        // this singleton handler and fails with InvalidOperationException instead of the cancellation the
        // arm is about. That race is machine-dependent: it passed 5/5 on Windows and failed on Linux CI.
        var entered = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var records = Enumerable.Range(0, 20).Select(_ => new OwnedRecord()).ToArray();
        var services = new ServiceCollection();
        services.AddSingleton<IRecordHandler<OwnedRecord>>(new OwnedHandler(async token =>
        {
            // A broken callback must not bypass joining this still-active handler.
            using var callback = token.Register(() => throw new InvalidOperationException("callback failure"));
            // Deliberately SetResult, not TrySetResult: a second entry means a record was dispatched after
            // cancellation was already observed, which is the defect this arm exists to catch.
            entered.SetResult(token);
            await release.Task;
            token.ThrowIfCancellationRequested();
        }));
        await using var provider = services.BuildServiceProvider();
        var processor = new OwnedProcessor(provider, records);
        var checkpoints = 0;
        var run = processor.RunAsync(0, null, (_, _, _) => { checkpoints++; return Task.CompletedTask; }, CancellationToken.None);
        var handlerToken = await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var firstDispose = processor.DisposeAsync().AsTask();
        var secondDispose = processor.DisposeAsync().AsTask();
        firstDispose.IsCompleted.ShouldBeFalse();
        secondDispose.IsCompleted.ShouldBeFalse();
        (await global::Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync(
            () => handlerToken.IsCancellationRequested,
            TimeSpan.FromSeconds(10),
            TimeSpan.FromMilliseconds(5))).ShouldBeTrue(
            "disposal must request cancellation on the running handler's token before the handler resumes; "
            + "otherwise this arm measures a handler that was never cancelled");
        release.SetResult();
        await Task.WhenAll(firstDispose, secondDispose).WaitAsync(TimeSpan.FromSeconds(10));
        await Should.ThrowAsync<OperationCanceledException>(() => run);
        checkpoints.ShouldBe(0);
        records.ShouldAllBe(record => record.DisposeCount == 1);
    }

    private sealed class OwnedRecord : IAsyncDisposable
    {
        public int DisposeCount { get; private set; }
        public ValueTask DisposeAsync() { DisposeCount++; return ValueTask.CompletedTask; }
    }
    private sealed class OwnedHandler(Func<CancellationToken, Task> handle) : IRecordHandler<OwnedRecord>
    {
        public Task ProcessAsync(OwnedRecord record, CancellationToken cancellationToken) => handle(cancellationToken);
    }
    private sealed class OwnedProcessor(IServiceProvider services, OwnedRecord[] records)
        : DataProcessor<OwnedRecord>(A.Fake<IHostApplicationLifetime>(), Options.Create(new DataProcessingOptions
        { QueueSize = 2, ProducerBatchSize = 2, ConsumerBatchSize = 2 }), services, NullLogger.Instance)
    {
        public override Task<CursorFetchResult<OwnedRecord>> FetchBatchAsync(string? cursor, int batchSize, CancellationToken cancellationToken)
            => Task.FromResult(new CursorFetchResult<OwnedRecord>(records, null));
    }

    private sealed class Handler(Func<int, CancellationToken, Task> action) : IRecordHandler<int>
    {
        public Task ProcessAsync(int record, CancellationToken cancellationToken) => action(record, cancellationToken);
    }

    private sealed class Processor : DataProcessor<int>
    {
        private readonly Func<string?, CancellationToken, Task<CursorFetchResult<int>>> _fetch;
        public Processor(IServiceProvider services, Func<string?, CancellationToken, Task<CursorFetchResult<int>>> fetch)
            : base(A.Fake<IHostApplicationLifetime>(), Options.Create(new DataProcessingOptions
            { QueueSize = 2, ProducerBatchSize = 2, ConsumerBatchSize = 2 }), services, NullLogger.Instance) => _fetch = fetch;
        public override Task<CursorFetchResult<int>> FetchBatchAsync(string? cursor, int batchSize, CancellationToken cancellationToken)
            => _fetch(cursor, cancellationToken);
    }
}
