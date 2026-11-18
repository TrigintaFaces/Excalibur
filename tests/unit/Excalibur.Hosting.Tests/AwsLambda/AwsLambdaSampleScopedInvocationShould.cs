// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;

using Amazon.Lambda.SQSEvents;

using AwsLambdaSample.Functions;
using AwsLambdaSample.Handlers;
using AwsLambdaSample.Messages;

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;

namespace Excalibur.Hosting.Tests.AwsLambda;

/// <summary>
/// Observes real handler dependencies through the public sample invocation path.
/// The dispatcher owns these scopes; the sample's outer scope is not passed into its context.
/// </summary>
[Collection("EnvironmentVariableTests")]
[Trait("Category", "Unit")]
[Trait("Component", "Hosting")]
[Trait("Pattern", "Regression")]
public sealed class AwsLambdaSampleScopedInvocationShould : UnitTestBase
{
    private static readonly TimeSpan GateTimeout = TimeSpan.FromSeconds(15);
    private static readonly string[] ExpectedOrderIds = ["scope-first", "scope-fault", "scope-next"];
    private static readonly string[] ExpectedStages =
        ["handler-0", "disposed-0", "handler-1", "disposed-1", "handler-2", "disposed-2"];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisposeActualHandlerDependenciesBeforeTheNextRecordAndInvocation(bool asynchronous)
    {
        var state = new ScopeObservations();
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddSingleton(state);
        if (asynchronous)
        {
            _ = services.AddScoped<IScopeProbe, AsyncScopeProbe>();
        }
        else
        {
            _ = services.AddScoped<IScopeProbe, SyncScopeProbe>();
        }

        _ = services.AddScoped<ScopeConsumer>();
        _ = services.AddScoped<IEventHandler<OrderCreatedEvent>, ScopeWitnessHandler>();
        _ = services.AddDispatch(dispatch =>
            dispatch.AddHandlersFromAssembly(typeof(OrderCreatedHandler).Assembly));
        _ = services.AddExcaliburAwsLambdaServerless();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true,
        });
        var handler = new SqsHandler(provider);
        Task<SQSBatchResponse>? invocation = null;
        Exception? primary = null;
        try
        {
            invocation = handler.ProcessOrderEventsAsync(new SQSEvent
            {
                Records = [Record("scope-first"), Record("scope-fault")],
            }, new TestLambdaContext { AwsRequestId = "b07-scoped-first" });
            if (asynchronous)
            {
                await AssertBlockedAtDisposalAsync(state, invocation, 0).ConfigureAwait(false);
                state.Release(0);
                await AssertBlockedAtDisposalAsync(state, invocation, 1).ConfigureAwait(false);
                state.Release(1);
            }

            _ = await invocation.WaitAsync(GateTimeout, TestContext.Current.CancellationToken).ConfigureAwait(false);
            state.Handlers.Count.ShouldBe(2);
            state.CompletedDisposals.ShouldBe(2);
            invocation = handler.ProcessOrderEventsAsync(new SQSEvent
            {
                Records = [Record("scope-next")],
            }, new TestLambdaContext { AwsRequestId = "b07-scoped-next" });
            if (asynchronous)
            {
                await AssertBlockedAtDisposalAsync(state, invocation, 2).ConfigureAwait(false);
                state.Release(2);
            }

            var next = await invocation.WaitAsync(GateTimeout, TestContext.Current.CancellationToken).ConfigureAwait(false);
            next.BatchItemFailures.ShouldBeEmpty();
            var observed = state.Handlers.ToArray();
            observed.Select(entry => entry.OrderId).ShouldBe(ExpectedOrderIds);
            observed.ShouldAllBe(entry => entry.SameWithinScope && !entry.DisposedOnEntry);
            observed.Select(entry => entry.Probe).Distinct().Count().ShouldBe(3);
            observed.ShouldAllBe(entry => entry.Probe.DisposeCalls == 1);
            state.FaultedHandlers.ShouldBe(1);
            state.CompletedDisposals.ShouldBe(3);
            state.Stages.ToArray().ShouldBe(ExpectedStages);
            provider.GetRequiredService<ILoggerFactory>().ShouldNotBeNull();
        }
        catch (Exception error)
        {
            primary = error;
            throw;
        }
        finally
        {
            state.ReleaseAll();
            if (invocation is not null)
            {
                try
                {
                    _ = await invocation.WaitAsync(GateTimeout).ConfigureAwait(false);
                }
                catch (Exception cleanupError) when (primary is not null)
                {
                    primary.Data["B07.InvocationDrainFailure"] = cleanupError;
                }
            }
        }
    }

    private static async Task AssertBlockedAtDisposalAsync(
        ScopeObservations state, Task<SQSBatchResponse> invocation, int index)
    {
        await state.Started(index).WaitAsync(GateTimeout, TestContext.Current.CancellationToken).ConfigureAwait(false);
        state.Handlers.Count.ShouldBe(index + 1);
        state.CompletedDisposals.ShouldBe(index);
        invocation.IsCompleted.ShouldBeFalse();
    }

    private static SQSEvent.SQSMessage Record(string orderId) => new()
    {
        MessageId = orderId,
        Body = "{\"OrderId\":\"" + orderId
            + "\",\"CustomerId\":\"b07-customer\",\"TotalAmount\":12.5,\"CreatedAt\":\"2026-11-14T00:00:00Z\"}",
    };

    internal interface IScopeProbe
    {
        int Index { get; }
        int DisposeCalls { get; }
    }

    internal sealed class ScopeConsumer(IScopeProbe probe)
    {
        public IScopeProbe Probe { get; } = probe;
    }

    internal sealed class ScopeWitnessHandler(
        IScopeProbe probe, ScopeConsumer consumer, ScopeObservations state) : IEventHandler<OrderCreatedEvent>
    {
        public Task HandleAsync(OrderCreatedEvent eventMessage, CancellationToken cancellationToken)
        {
            state.Handlers.Enqueue(new HandlerObservation(eventMessage.OrderId, probe,
                ReferenceEquals(probe, consumer.Probe), probe.DisposeCalls != 0));
            state.Stages.Enqueue($"handler-{probe.Index}");
            if (eventMessage.OrderId == "scope-fault")
            {
                state.MarkHandlerFault();
                return Task.FromException(new InvalidOperationException("B07-CONTROLLED-HANDLER-FAULT"));
            }

            return Task.CompletedTask;
        }
    }

    internal sealed record HandlerObservation(
        string OrderId, IScopeProbe Probe, bool SameWithinScope, bool DisposedOnEntry);

    internal sealed class ScopeObservations
    {
        private readonly DisposalGate[] _gates = [new(), new(), new()];
        private int _created;
        private int _completed;
        private int _faulted;

        public ConcurrentQueue<HandlerObservation> Handlers { get; } = new();
        public ConcurrentQueue<string> Stages { get; } = new();
        public int CompletedDisposals => Volatile.Read(ref _completed);
        public int FaultedHandlers => Volatile.Read(ref _faulted);

        public int AllocateProbe() => Interlocked.Increment(ref _created) - 1;
        public void MarkHandlerFault() => Interlocked.Increment(ref _faulted);
        public Task Started(int index) => _gates[index].Started.Task;
        public void Release(int index) => _gates[index].Release.TrySetResult(true);
        public void ReleaseAll()
        {
            foreach (var gate in _gates)
            {
                gate.Release.TrySetResult(true);
            }
        }

        public async ValueTask AwaitReleaseAsync(int index)
        {
            _gates[index].Started.TrySetResult(true);
            await _gates[index].Release.Task.WaitAsync(GateTimeout).ConfigureAwait(false);
        }

        public void RecordDisposal(int index)
        {
            Stages.Enqueue($"disposed-{index}");
            _ = Interlocked.Increment(ref _completed);
        }

        private sealed class DisposalGate
        {
            internal TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            internal TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    internal sealed class SyncScopeProbe(ScopeObservations state) : IScopeProbe, IDisposable
    {
        private int _disposeCalls;
        public int Index { get; } = state.AllocateProbe();
        public int DisposeCalls => Volatile.Read(ref _disposeCalls);
        public void Dispose()
        {
            _ = Interlocked.Increment(ref _disposeCalls);
            state.RecordDisposal(Index);
        }
    }

    internal sealed class AsyncScopeProbe(ScopeObservations state) : IScopeProbe, IAsyncDisposable
    {
        private int _disposeCalls;
        public int Index { get; } = state.AllocateProbe();
        public int DisposeCalls => Volatile.Read(ref _disposeCalls);
        public async ValueTask DisposeAsync()
        {
            _ = Interlocked.Increment(ref _disposeCalls);
            await state.AwaitReleaseAsync(Index).ConfigureAwait(false);
            state.RecordDisposal(Index);
        }
    }
}
