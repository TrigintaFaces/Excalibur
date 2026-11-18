// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Cdc.SqlServer;
using Excalibur.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Polly;
using Polly.Retry;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

public sealed partial class CdcChangeApplierShould
{
    // This delay runs ONLY on the per-case FakeTimeProvider, never on the wall clock.
    private static readonly TimeSpan CarriedBackoffDelay = TimeSpan.FromHours(1);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Pattern", "Regression")]
    public async Task CancelTheActualHandlerOrCheckpointRetryBackoffWithoutAnotherAttempt(bool checkpoint)
    {
        using var caller = new CancellationTokenSource();
        await using var ordered = new OrderedEventProcessor();
        var clock = new ObservedBackoffClock();
        var probe = new BackoffProbe(checkpoint);
        var applier = CreateBackoffApplier(clock, probe, ordered);
        var change = CreateEvent("orders", [1], [1]);
        var running = InvokeProcessBatchAsync(applier, [change], probe.HandleAsync, caller.Token);
        Exception? primary = null;
        try
        {
            _ = await probe.RetryEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);
            await clock.TimerRegistered.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);
            probe.HandlerCalls.ShouldBe(1);
            probe.CheckpointCalls.ShouldBe(checkpoint ? 1 : 0);
            running.IsCompleted.ShouldBeFalse();

            await caller.CancelAsync();
            OperationCanceledException? observed = null;
            try
            {
                // The wall-clock bound is a FAILURE guard, not cancellation of the SUT task.
                await running.WaitAsync(TimeSpan.FromSeconds(3), Xunit.TestContext.Current.CancellationToken);
            }
            catch (OperationCanceledException exception)
            {
                observed = exception;
            }

            observed.ShouldNotBeNull().CancellationToken.ShouldBe(caller.Token);
            running.IsCompleted.ShouldBeTrue();
            probe.RetryToken.ShouldBe(caller.Token);
            probe.HandlerCalls.ShouldBe(1);
            probe.CheckpointCalls.ShouldBe(checkpoint ? 1 : 0);
            probe.CommittedWrites.ShouldBe(0);
        }
        catch (Exception failure)
        {
            primary = failure;
            throw;
        }
        finally
        {
            await JoinBackoffAttemptAsync(running, caller, clock, primary);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Pattern", "Regression")]
    public async Task RecoverTheActualHandlerOrCheckpointAfterTheSameBackoffWithALiveCaller(bool checkpoint)
    {
        using var caller = new CancellationTokenSource();
        await using var ordered = new OrderedEventProcessor();
        var clock = new ObservedBackoffClock();
        var probe = new BackoffProbe(checkpoint);
        var applier = CreateBackoffApplier(clock, probe, ordered);
        var change = CreateEvent("orders", [1], [1]);
        var running = InvokeProcessBatchAsync(applier, [change], probe.HandleAsync, caller.Token);
        Exception? primary = null;
        try
        {
            _ = await probe.RetryEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);
            await clock.TimerRegistered.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);
            running.IsCompleted.ShouldBeFalse();
            probe.HandlerCalls.ShouldBe(1);
            probe.CheckpointCalls.ShouldBe(checkpoint ? 1 : 0);
            clock.AdvanceBackoff();
            await running.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);

            caller.IsCancellationRequested.ShouldBeFalse();
            probe.HandlerCalls.ShouldBe(checkpoint ? 1 : 2);
            probe.CheckpointCalls.ShouldBe(checkpoint ? 2 : 1);
            probe.CommittedWrites.ShouldBe(1);
        }
        catch (Exception failure)
        {
            primary = failure;
            throw;
        }
        finally
        {
            await JoinBackoffAttemptAsync(running, caller, clock, primary);
        }
    }

    private static CdcChangeApplier CreateBackoffApplier(ObservedBackoffClock clock, BackoffProbe probe,
        OrderedEventProcessor ordered)
    {
        var config = A.Fake<IDatabaseOptions>();
        A.CallTo(() => config.DatabaseConnectionIdentifier).Returns("r08-carried-backoff");
        A.CallTo(() => config.DatabaseName).Returns("r08-database");
        var factory = A.Fake<IDataAccessPolicyFactory>();
        var policy = new ResiliencePipelineBuilder { TimeProvider = clock }
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 1,
                Delay = CarriedBackoffDelay,
                MaxDelay = CarriedBackoffDelay,
                BackoffType = DelayBackoffType.Constant,
                UseJitter = false,
                ShouldHandle = new PredicateBuilder().Handle<TimeoutException>(),
                OnRetry = arguments =>
                {
                    probe.RetryToken = arguments.Context.CancellationToken;
                    probe.RetryEntered.TrySetResult(arguments.Context.CancellationToken);
                    return ValueTask.CompletedTask;
                },
            }).Build().AsAsyncPolicy();
        A.CallTo(() => factory.GetComprehensivePolicy()).Returns(policy);
        var state = A.Fake<ISqlServerCdcStateStore>();
        A.CallTo(() => state.UpdateLastProcessedPositionAsync(A<string>._, A<string>._, A<string>._,
                A<byte[]>._, A<byte[]?>._, A<DateTime?>._, A<long?>._, A<CancellationToken>._))
            .ReturnsLazily((string _, string _, string _, byte[] _, byte[]? _, DateTime? _, long? _, CancellationToken token) =>
                probe.SaveAsync(token));
        var checkpoints = new CdcCheckpointManager(config, A.Fake<ICdcRepository>(), state, NullLogger.Instance, factory);
        return new CdcChangeApplier(config, factory, checkpoints, ordered, NullLogger.Instance, onFatalError: null);
    }

    private static async Task JoinBackoffAttemptAsync(Task running, CancellationTokenSource caller,
        ObservedBackoffClock clock, Exception? primary)
    {
        try
        {
            await caller.CancelAsync();
            // Only cleanup advances a failed negative's fake timer, AFTER its outcome was observed.
            clock.AdvanceBackoff();
            try
            {
                await running.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
            }
            catch (OperationCanceledException) when (caller.IsCancellationRequested)
            {
            }
        }
        catch (Exception cleanup) when (primary is not null)
        {
            primary.Data["CdcBackoffAttemptCleanup"] = cleanup;
        }
    }

    private sealed class BackoffProbe(bool checkpoint)
    {
        private int _handlerCalls;
        private int _checkpointCalls;
        private int _writes;
        internal TaskCompletionSource<CancellationToken> RetryEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal CancellationToken RetryToken { get; set; }
        internal int HandlerCalls => Volatile.Read(ref _handlerCalls);
        internal int CheckpointCalls => Volatile.Read(ref _checkpointCalls);
        internal int CommittedWrites => Volatile.Read(ref _writes);

        internal Task HandleAsync(DataChangeEvent change, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(change);
            var attempt = Interlocked.Increment(ref _handlerCalls);
            token.ThrowIfCancellationRequested();
            return !checkpoint && attempt == 1
                ? Task.FromException(new TimeoutException("controlled handler transient"))
                : Task.CompletedTask;
        }

        internal Task<int> SaveAsync(CancellationToken token)
        {
            var attempt = Interlocked.Increment(ref _checkpointCalls);
            token.ThrowIfCancellationRequested();
            if (checkpoint && attempt == 1)
            {
                return Task.FromException<int>(new TimeoutException("controlled checkpoint transient"));
            }

            Interlocked.Increment(ref _writes);
            return Task.FromResult(1);
        }
    }

    // Observes the real Polly delay registration; scheduling remains FakeTimeProvider's implementation.
    private sealed class ObservedBackoffClock : TimeProvider
    {
        private readonly FakeTimeProvider _inner = new();
        private readonly TaskCompletionSource _registered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Task TimerRegistered => _registered.Task;
        public override long TimestampFrequency => _inner.TimestampFrequency;
        public override long GetTimestamp() => _inner.GetTimestamp();
        public override DateTimeOffset GetUtcNow() => _inner.GetUtcNow();
        internal void AdvanceBackoff() => _inner.Advance(CarriedBackoffDelay);

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = _inner.CreateTimer(callback, state, dueTime, period);
            if (dueTime == CarriedBackoffDelay)
            {
                _registered.TrySetResult();
            }

            return timer;
        }
    }
}
