// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Reflection;
using Excalibur.Cdc;
using Excalibur.Cdc.SqlServer;
using Excalibur.Domain;
using Excalibur.Data.SqlServer.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;
namespace Excalibur.Data.Tests.SqlServer.Cdc;

[Trait("Category", "Unit")]
[Trait("Component", "Data.SqlServer")]
[Trait("Pattern", "Regression")]
public sealed class CdcProcessorStalePositionRecoveryShould
{
    [Fact]
    public async Task RetryAtAQuiescentBoundaryWhenHeadChangesAfterInitialization()
    {
        var (processor, repo, config) = Create();
        await using var owned = processor;
        // Initial bounds accept checkpoint 5; detector observes restored maximum 3.
        var heads = new Queue<byte[]>([new byte[] { 10 }, new byte[] { 3 }, new byte[] { 3 }, new byte[] { 3 }]);
        A.CallTo(() => repo.GetMaxPositionAsync(A<CancellationToken>._)).ReturnsLazily(() => heads.Dequeue());
        var resets = 0;
        config.RecoveryOptions!.OnPositionReset = (_, _) => { resets++; return Task.CompletedTask; };
        (await processor.ProcessBatchAsync((_, _) => Task.CompletedTask, CancellationToken.None)).ShouldBe(0);
        resets.ShouldBe(1);
        heads.ShouldBeEmpty();
    }

    [Fact]
    public async Task BoundRetriesWhenHistoryKeepsChanging()
    {
        var (processor, repo, config) = Create();
        await using var owned = processor;
        config.RecoveryOptions!.MaxRecoveryAttempts = 2;
        var calls = 0;
        A.CallTo(() => repo.GetMaxPositionAsync(A<CancellationToken>._))
            .ReturnsLazily(() => new byte[] { ++calls % 2 == 1 ? (byte)10 : (byte)3 });
        await Should.ThrowAsync<SqlServerCdcStalePositionException>(() => processor.ProcessBatchAsync((_, _) => Task.CompletedTask, CancellationToken.None));
        calls.ShouldBe(6);
    }

    [Fact]
    public async Task PreserveHealthySequenceWhileRecoveringOnlyTheStaleTable()
    {
        var repo = A.Fake<ICdcRepository>();
        var state = A.Fake<ISqlServerCdcStateStore>();
        var config = A.Fake<IDatabaseOptions>();
        A.CallTo(() => config.CaptureInstances).Returns(["stale", "healthy"]);
        A.CallTo(() => config.RecoveryOptions).Returns(new CdcRecoveryOptions { RecoveryStrategy = StalePositionRecoveryStrategy.FallbackToLatest });
        A.CallTo(() => repo.GetMaxPositionAsync(A<CancellationToken>._)).Returns(new byte[] { 10 });
        A.CallTo(() => repo.GetMinPositionAsync(A<string>._, A<CancellationToken>._)).Returns(new byte[] { 2 });
        A.CallTo(() => state.GetLastProcessedPositionAsync(A<string>._, A<string>._, A<CancellationToken>._)).Returns(new[]
        {
            new CdcProcessingState { TableName = "stale", LastProcessedLsn = [1], LastProcessedSequenceValue = [7] },
            new CdcProcessingState { TableName = "healthy", LastProcessedLsn = [5], LastProcessedSequenceValue = [8] },
        });
        var checkpoints = new CdcCheckpointManager(config, repo, state, NullLogger.Instance);
        await checkpoints.InitializeTrackingAsync(CancellationToken.None);
        checkpoints.GetTracking("stale")!.Lsn.ShouldBe(new byte[] { 10 });
        checkpoints.GetTracking("stale")!.SequenceValue.ShouldBeNull();
        checkpoints.GetTracking("healthy")!.Lsn.ShouldBe(new byte[] { 5 });
        checkpoints.GetTracking("healthy")!.SequenceValue.ShouldBe(new byte[] { 8 });
    }

    [Fact]
    public async Task WaitForTheActiveHandlerBeforeReloadingStateAfterProducerFailure()
    {
        var (processor, repo, config) = Create();
        await using var owned = processor;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reads = 0;
        A.CallTo(() => repo.GetMaxPositionAsync(A<CancellationToken>._)).ReturnsLazily(() =>
        {
            Interlocked.Increment(ref reads);
            return new byte[] { 10 };
        });
        var fetches = 0;
        A.CallTo(() => repo.FetchChangesAsync(A<string>._, A<int>._, A<byte[]>._, A<byte[]>._, A<byte[]?>._,
            A<CdcOperationCodes>._, A<CancellationToken>._, A<string?>._)).ReturnsLazily(async call =>
        {
            if (Interlocked.Increment(ref fetches) == 1)
            {
                return (IEnumerable<CdcRow>)new List<CdcRow> { new() { TableName = "dbo_items", Lsn = [5], SeqVal = [1],
                    OperationCode = CdcOperationCodes.Insert, Changes = new Dictionary<string, object> { ["Id"] = 1 },
                    DataTypes = new Dictionary<string, Type> { ["Id"] = typeof(int) } } };
            }
            if (fetches == 2)
            {
                await entered.Task;
                throw new SqlServerCdcStalePositionException(new CdcPositionResetEventArgs
                { ProcessorId = "consumer", ProviderType = "SqlServer", ReasonCode = StalePositionReasonCodes.CdcCleanup });
            }
            return new List<CdcRow>();
        });
        var run = processor.ProcessBatchAsync(async (_, token) =>
        {
            using var callback = token.Register(() => { canceled.SetResult(); throw new InvalidOperationException("callback failure"); });
            entered.SetResult();
            await release.Task;
            token.ThrowIfCancellationRequested();
        }, CancellationToken.None);
        try
        {
            await canceled.Task.WaitAsync(TimeSpan.FromSeconds(10));
            reads.ShouldBe(2, "a retry must not read new bounds while the old handler is active");
            run.IsCompleted.ShouldBeFalse();
        }
        finally { release.TrySetResult(); }
        (await run.WaitAsync(TimeSpan.FromSeconds(10))).ShouldBe(0);
        reads.ShouldBe(4);
    }

    [Fact]
    public async Task JoinActiveWorkBeforeBothDisposersCompleteAndRejectQueuedWork()
    {
        var (processor, repo, _) = Create();
        Set(processor, "_cdcRepository", repo);
        A.CallTo(() => repo.GetMaxPositionAsync(A<CancellationToken>._)).Returns(new byte[] { 10 });
        var fetches = 0;
        A.CallTo(() => repo.FetchChangesAsync(A<string>._, A<int>._, A<byte[]>._, A<byte[]>._, A<byte[]?>._,
            A<CdcOperationCodes>._, A<CancellationToken>._, A<string?>._)).ReturnsLazily(() =>
                Interlocked.Increment(ref fetches) == 1
                    ? new[] { new CdcRow { TableName = "dbo_items", Lsn = [5], SeqVal = [1], OperationCode = CdcOperationCodes.Insert,
                        Changes = new Dictionary<string, object> { ["Id"] = 1 }, DataTypes = new Dictionary<string, Type> { ["Id"] = typeof(int) } } }
                    : Array.Empty<CdcRow>());
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var run = processor.ProcessBatchAsync(async (_, token) =>
        {
            using var registration = token.Register(() => { canceled.TrySetResult(); throw new InvalidOperationException("callback failure"); });
            entered.TrySetResult();
            await release.Task;
            token.ThrowIfCancellationRequested();
        }, CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var queued = processor.ProcessBatchAsync((_, _) => throw new InvalidOperationException("Queued handler must not run"), CancellationToken.None);
        var first = processor.DisposeAsync().AsTask();
        var second = processor.DisposeAsync().AsTask();
        try
        {
            await canceled.Task.WaitAsync(TimeSpan.FromSeconds(10));
            first.IsCompleted.ShouldBeFalse();
            second.IsCompleted.ShouldBeFalse();
            A.CallTo(() => repo.DisposeAsync()).MustNotHaveHappened();
        }
        finally { release.TrySetResult(); }
        await Should.ThrowAsync<OperationCanceledException>(() => run.WaitAsync(TimeSpan.FromSeconds(10)));
        await Should.ThrowAsync<OperationCanceledException>(() => queued.WaitAsync(TimeSpan.FromSeconds(10)));
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10));
        A.CallTo(() => repo.DisposeAsync()).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task CancelInitializationOnHostStoppingAndAllowSafeDisposal()
    {
        using var stopping = new CancellationTokenSource();
        var lifetime = A.Fake<IHostApplicationLifetime>();
        A.CallTo(() => lifetime.ApplicationStopping).Returns(stopping.Token);
        var (processor, repo, _) = Create(lifetime);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => repo.GetMaxPositionAsync(A<CancellationToken>._)).ReturnsLazily(async (CancellationToken token) =>
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return new byte[] { 10 };
        });
        var run = processor.ProcessBatchAsync((_, _) => Task.CompletedTask, CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await stopping.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => run.WaitAsync(TimeSpan.FromSeconds(10)));
        await processor.DisposeAsync();
    }

    [Fact]
    public async Task AllowDisposalAfterAnActivityListenerThrows()
    {
        var (processor, _, _) = Create();
        var targetCall = new AsyncLocal<bool>();
        using var listener = new System.Diagnostics.ActivityListener
        {
            ShouldListenTo = source => source.Name == CdcTelemetryConstants.ActivitySource.Name,
            Sample = (ref System.Diagnostics.ActivityCreationOptions<System.Diagnostics.ActivityContext> _) =>
                targetCall.Value ? throw new InvalidOperationException("Listener failed") : System.Diagnostics.ActivitySamplingResult.None,
        };
        System.Diagnostics.ActivitySource.AddActivityListener(listener);
        targetCall.Value = true;
        try
        {
            await Should.ThrowAsync<InvalidOperationException>(() => processor.ProcessBatchAsync((_, _) => Task.CompletedTask, CancellationToken.None));
        }
        finally { targetCall.Value = false; }
        await processor.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
    }

    private static (CdcProcessor Processor, ICdcRepository Repo, IDatabaseOptions Config) Create(IHostApplicationLifetime? lifetime = null)
    {
        var config = A.Fake<IDatabaseOptions>();
        A.CallTo(() => config.QueueSize).Returns(2);
        A.CallTo(() => config.ProducerBatchSize).Returns(2);
        A.CallTo(() => config.ConsumerBatchSize).Returns(1);
        A.CallTo(() => config.CaptureInstances).Returns(["dbo_items"]);
        A.CallTo(() => config.DatabaseName).Returns("source");
        A.CallTo(() => config.DatabaseConnectionIdentifier).Returns("consumer");
        A.CallTo(() => config.RecoveryOptions).Returns(new CdcRecoveryOptions { RecoveryAttemptDelay = TimeSpan.Zero });
        var policy = A.Fake<IDataAccessPolicyFactory>();
        A.CallTo(() => policy.GetComprehensivePolicy()).Returns(Policy.NoOpAsync());
        var processor = new CdcProcessor(lifetime ?? A.Fake<IHostApplicationLifetime>(), config,
            new CdcRepository(new SqlConnection()), () => new SqlConnection(), null,
            policy, TimeProvider.System, NullLogger<CdcProcessor>.Instance);
        var repo = A.Fake<ICdcRepository>();
        var mapping = A.Fake<ICdcRepositoryLsnMapping>();
        var state = A.Fake<ISqlServerCdcStateStore>();
        A.CallTo(() => repo.GetMinPositionAsync(A<string>._, A<CancellationToken>._)).Returns(new byte[] { 1 });
        A.CallTo(() => repo.FetchChangesAsync(A<string>._, A<int>._, A<byte[]>._, A<byte[]>._, A<byte[]?>._,
            A<CdcOperationCodes>._, A<CancellationToken>._, A<string?>._)).Returns(Array.Empty<CdcRow>());
        A.CallTo(() => mapping.GetNextLsnAsync(A<string>._, A<byte[]>._, A<CancellationToken>._)).Returns(Task.FromResult<byte[]?>(null));
        A.CallTo(() => state.GetLastProcessedPositionAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .Returns(new[] { new CdcProcessingState { TableName = "dbo_items", LastProcessedLsn = [5] } });
        var checkpoints = new CdcCheckpointManager(config, repo, state, NullLogger.Instance, policy);
        // Inject storage seams into the composed components; exercise the PUBLIC batch operation.
        Set(processor, "_checkpointManager", checkpoints);
        Set(processor, "_changeDetector", new CdcChangeDetector(repo, mapping, config, policy, checkpoints, NullLogger.Instance));
        var ordered = (OrderedEventProcessor)typeof(CdcProcessor).GetField("_orderedEventProcessor", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(processor)!;
        Set(processor, "_changeApplier", new CdcChangeApplier(config, policy, checkpoints, ordered, NullLogger.Instance, null, null));
        return (processor, repo, config);
    }
    private static void Set(CdcProcessor processor, string field, object value) =>
        typeof(CdcProcessor).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(processor, value);
}
