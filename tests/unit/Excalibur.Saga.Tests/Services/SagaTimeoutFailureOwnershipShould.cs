// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using Excalibur.Dispatch;
using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Services;
using Excalibur.Saga.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Excalibur.Saga.Tests.Services;

[Trait("Category", "Unit")]
[Trait("Component", "Saga.Services")]
[Trait("Pattern", "Regression")]
public sealed class SagaTimeoutFailureOwnershipShould
{
    [Theory]
    [InlineData(MessageFailureKind.Poison)]
    [InlineData(MessageFailureKind.Permanent)]
    [InlineData(MessageFailureKind.Unknown)]
    public async Task RetainNonRetryableOriginalWithoutDispatchingItAgain(MessageFailureKind kind)
    {
        Exception primary = kind switch
        {
            MessageFailureKind.Poison => new JsonException("Controlled typed handler serialization fault."),
            MessageFailureKind.Permanent => new ArgumentException("Controlled typed handler contract fault."),
            MessageFailureKind.Unknown => new UnclassifiedFailureException("Controlled unclassified handler fault."),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        await using var harness = await WorkerHarness.CreateAsync((_, _, _) => Task.FromException<IMessageResult>(primary));
        harness.Classifier.Classify(primary).ShouldBe(kind);
        await harness.RunTwoCompletedCyclesAsync();
        harness.Store.RetirementCalls.ShouldBe(0, "failure is not a successful delivery acknowledgement");
        harness.Log.Errors.Any(error => ReferenceEquals(error, primary)).ShouldBeTrue();
        var retained = (await harness.Store.GetDueTimeoutsAsync(DateTimeOffset.MaxValue, harness.Deadline)).ShouldHaveSingleItem();
        retained.TimeoutId.ShouldBe(harness.TimeoutId);
        retained.TimeoutData.ShouldBe(harness.OriginalPayload);
        harness.Store.Claims.All(claim => claim.Timeout.TimeoutId == harness.TimeoutId).ShouldBeTrue();
        harness.Store.Claims.Select(claim => claim.ClaimToken).Distinct(StringComparer.Ordinal).Count().ShouldBe(harness.Store.Claims.Count);
        harness.Calls.ShouldBe(1, "known nonretryable/unknown failure does not authorize another automatic dispatch after lease expiry");
    }

    [Fact]
    public async Task RetryKnownTransientThroughARealFreshClaimThenRetireOnSuccess()
    {
        var primary = new TimeoutException("Controlled recoverable handler timeout.");
        var attempts = 0;
        await using var harness = await WorkerHarness.CreateAsync((_, _, _) =>
            Interlocked.Increment(ref attempts) == 1 ? Task.FromException<IMessageResult>(primary) : Task.FromResult(MessageResult.Success()));
        harness.Classifier.Classify(primary).ShouldBe(MessageFailureKind.Transient);
        await harness.RunTwoCompletedCyclesAsync();
        harness.Calls.ShouldBe(2);
        harness.Store.Claims.Count.ShouldBe(2);
        harness.Store.Claims[0].ClaimToken.ShouldNotBe(harness.Store.Claims[1].ClaimToken);
        harness.Store.Claims.All(claim => claim.Timeout.TimeoutId == harness.TimeoutId).ShouldBeTrue();
        harness.Store.RetirementCalls.ShouldBe(1);
        harness.Store.AppliedRetirements.ShouldBe(1);
        (await harness.Store.GetDueTimeoutsAsync(DateTimeOffset.MaxValue, harness.Deadline)).ShouldBeEmpty();
        harness.Log.Errors.Any(error => ReferenceEquals(error, primary)).ShouldBeTrue();
    }

    [Fact]
    public async Task DeliverHealthyOriginalOnceAndLeaveTheNextCycleEmpty()
    {
        await using var harness = await WorkerHarness.CreateAsync((_, _, _) => Task.FromResult(MessageResult.Success()));
        await harness.RunTwoCompletedCyclesAsync();
        harness.Calls.ShouldBe(1);
        harness.Store.Claims.Count.ShouldBe(1);
        harness.Store.RetirementCalls.ShouldBe(1);
        harness.Store.AppliedRetirements.ShouldBe(1);
        (await harness.Store.GetDueTimeoutsAsync(DateTimeOffset.MaxValue, harness.Deadline)).ShouldBeEmpty();
        harness.Log.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task PreserveCallerCancellationWithoutAcknowledgingOrLosingTheOriginal()
    {
        var entered = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var harness = await WorkerHarness.CreateAsync(async (_, _, token) =>
        {
            entered.TrySetResult(token);
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return MessageResult.Success();
        });
        await harness.StartFirstCycleAsync();
        var dispatchToken = await entered.Task.WaitAsync(harness.Deadline);
        await harness.CancelCallerAndStopAsync();
        dispatchToken.IsCancellationRequested.ShouldBeTrue();
        harness.Calls.ShouldBe(1);
        harness.Store.RetirementCalls.ShouldBe(0);
        var retained = (await harness.Store.GetDueTimeoutsAsync(DateTimeOffset.MaxValue, harness.Deadline)).ShouldHaveSingleItem();
        retained.TimeoutId.ShouldBe(harness.TimeoutId);
        retained.TimeoutData.ShouldBe(harness.OriginalPayload);
    }

    private sealed class UnclassifiedFailureException(string message) : Exception(message);

    private sealed class WorkerTimeout : IDispatchMessage
    {
        public WorkerTimeout() { }
        public string Value { get; set; } = string.Empty;
    }

    private sealed class WorkerHarness : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly CancellationTokenSource _caller = new();
        private readonly CancellationTokenSource _deadline = new(global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(30)));
        private readonly SagaTimeoutDeliveryService _worker;
        private readonly DateTimeOffset _claimEpoch = DateTimeOffset.UtcNow;
        private int _calls;
        private bool _stopped;

        private WorkerHarness(ServiceProvider provider, GatedLeaseStore store, WorkerLog log,
            string timeoutId, byte[] payload, Func<IDispatchMessage, IMessageContext, CancellationToken, Task<IMessageResult>> dispatch)
        {
            _provider = provider;
            Store = store;
            Log = log;
            TimeoutId = timeoutId;
            OriginalPayload = (byte[])payload.Clone();
            Classifier = provider.GetRequiredService<IMessageFailureClassifier>();
            var dispatcher = provider.GetRequiredService<IDispatcher>();
            A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
                .ReturnsLazily((IDispatchMessage message, IMessageContext context, CancellationToken token) =>
                {
                    Interlocked.Increment(ref _calls);
                    context.MessageId.ShouldBe(TimeoutId);
                    message.ShouldBeOfType<WorkerTimeout>().Value.ShouldBe("original-payload");
                    return dispatch(message, context, token);
                });
            var registry = new SagaTypeRegistry();
            registry.RegisterType(typeof(WorkerTimeout));
            _worker = new SagaTimeoutDeliveryService(store, provider, log,
                global::Microsoft.Extensions.Options.Options.Create(new SagaTimeoutOptions
                {
                    PollInterval = TimeSpan.FromMilliseconds(100), BatchSize = 1, EnableVerboseLogging = false,
                }), registry);
        }

        internal GatedLeaseStore Store { get; }
        internal WorkerLog Log { get; }
        internal string TimeoutId { get; }
        internal byte[] OriginalPayload { get; }
        internal IMessageFailureClassifier Classifier { get; }
        internal int Calls => Volatile.Read(ref _calls);
        internal CancellationToken Deadline => _deadline.Token;

        internal static async Task<WorkerHarness> CreateAsync(Func<IDispatchMessage, IMessageContext, CancellationToken, Task<IMessageResult>> dispatch)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDispatch(static _ => { });
            services.AddDefaultTenantContext();
            services.AddSingleton(A.Fake<IDispatcher>());
            var provider = services.BuildServiceProvider();
            var inner = new InMemorySagaTimeoutStore(provider.GetRequiredService<ITenantContext>());
            var store = new GatedLeaseStore(inner);
            var id = Guid.NewGuid().ToString("N");
            var payload = JsonSerializer.SerializeToUtf8Bytes(new WorkerTimeout { Value = "original-payload" });
            await store.ScheduleTimeoutAsync(new SagaTimeout(id, Guid.NewGuid().ToString(), "worker-saga", typeof(WorkerTimeout).FullName!,
                payload, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch), CancellationToken.None);
            return new WorkerHarness(provider, store, new WorkerLog(), id, payload, dispatch);
        }

        internal async Task StartFirstCycleAsync()
        {
            await _worker.StartAsync(_caller.Token);
            (await Store.WaitForPollAsync(Deadline)).ShouldBe(1);
            await Store.ReleaseCycleAsync(_claimEpoch, Deadline);
        }

        internal async Task RunTwoCompletedCyclesAsync()
        {
            await StartFirstCycleAsync();
            (await Store.WaitForPollAsync(Deadline)).ShouldBe(2);
            // The real selected store's public asOf parameter is its lease-clock seam. Advance past
            // its supported120-second lease, without private state writes or manufactured claims.
            await Store.ReleaseCycleAsync(_claimEpoch.AddSeconds(121), Deadline);
            (await Store.WaitForPollAsync(Deadline)).ShouldBe(3);
            // A third poll can start only after the second dispatch/retirement and poll delay complete.
            // Do not release it: stop now, so all positive and zero assertions have a completed boundary.
            await CancelCallerAndStopAsync();
        }

        internal async Task CancelCallerAndStopAsync()
        {
            if (_stopped) { return; }
            await _caller.CancelAsync();
            await _worker.StopAsync(Deadline);
            await AwaitWorkerTerminationAsync(Deadline);
            _stopped = true;
        }

        private async Task AwaitWorkerTerminationAsync(CancellationToken token)
        {
            if (_worker.ExecuteTask is not { } execution) { return; }
            try { await execution.WaitAsync(token); }
            catch (OperationCanceledException) when (execution.IsCanceled && _caller.IsCancellationRequested)
            {
                // Actual worker cancellation is terminal; a cancelled wait on a LIVE worker is not.
            }
            execution.IsCompleted.ShouldBeTrue();
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (!_stopped)
                {
                    await _caller.CancelAsync();
                    using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    await _worker.StopAsync(cleanup.Token);
                    await AwaitWorkerTerminationAsync(cleanup.Token);
                }
            }
            finally
            {
                _worker.Dispose();
                await _provider.DisposeAsync();
                _caller.Dispose();
                _deadline.Dispose();
            }
        }
    }

    private sealed class GatedLeaseStore(ISagaTimeoutStore inner) : ISagaTimeoutStore
    {
        private readonly Channel<int> _polls = Channel.CreateUnbounded<int>();
        private readonly Channel<DateTimeOffset> _permits = Channel.CreateUnbounded<DateTimeOffset>();
        private readonly ConcurrentQueue<ClaimedSagaTimeout> _claims = new();
        private int _pollNumber;
        private int _retirementCalls;
        private int _appliedRetirements;

        internal IReadOnlyList<ClaimedSagaTimeout> Claims => _claims.ToArray();
        internal int RetirementCalls => Volatile.Read(ref _retirementCalls);
        internal int AppliedRetirements => Volatile.Read(ref _appliedRetirements);
        internal ValueTask<int> WaitForPollAsync(CancellationToken token) => _polls.Reader.ReadAsync(token);
        internal ValueTask ReleaseCycleAsync(DateTimeOffset asOf, CancellationToken token) => _permits.Writer.WriteAsync(asOf, token);
        public object? GetService(Type serviceType)
        {
            ArgumentNullException.ThrowIfNull(serviceType);
            return serviceType.IsInstanceOfType(this) ? this : inner.GetService(serviceType);
        }
        public Task ScheduleTimeoutAsync(SagaTimeout timeout, CancellationToken cancellationToken) => inner.ScheduleTimeoutAsync(timeout, cancellationToken);
        public Task CancelTimeoutAsync(string sagaId, string timeoutId, CancellationToken cancellationToken) => inner.CancelTimeoutAsync(sagaId, timeoutId, cancellationToken);
        public Task CancelAllTimeoutsAsync(string sagaId, CancellationToken cancellationToken) => inner.CancelAllTimeoutsAsync(sagaId, cancellationToken);
        public Task<IReadOnlyList<SagaTimeout>> GetDueTimeoutsAsync(DateTimeOffset asOf, CancellationToken cancellationToken) => inner.GetDueTimeoutsAsync(asOf, cancellationToken);

        public async Task<IReadOnlyList<ClaimedSagaTimeout>> ClaimDueTimeoutsAsync(DateTimeOffset asOf, int batchSize, CancellationToken cancellationToken)
        {
            await _polls.Writer.WriteAsync(Interlocked.Increment(ref _pollNumber), cancellationToken);
            var controlledClock = await _permits.Reader.ReadAsync(cancellationToken);
            var actual = await inner.ClaimDueTimeoutsAsync(controlledClock, batchSize, cancellationToken);
            foreach (var claim in actual) { _claims.Enqueue(claim); }
            return actual;
        }

        public async Task<SagaTimeoutRetirementOutcome> MarkDeliveredAsync(ClaimedSagaTimeout claim, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _retirementCalls);
            var actual = await inner.MarkDeliveredAsync(claim, cancellationToken);
            if (actual == SagaTimeoutRetirementOutcome.Retired) { Interlocked.Increment(ref _appliedRetirements); }
            return actual;
        }
    }

    private sealed class WorkerLog : ILogger<SagaTimeoutDeliveryService>
    {
        internal ConcurrentQueue<Exception> Errors { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (exception is not null) { Errors.Enqueue(exception); }
        }
    }
}
