// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using Excalibur.Dispatch.LeaderElection.Fencing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Excalibur.LeaderElection.Tests.InMemory;

[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
[Trait("Pattern", "Regression")]
public sealed class InMemoryNotificationLinearizationShould
{
    private static readonly long?[] ExpectedTransitions = [41, null, 42];

    [Fact]
    public async Task FinishOwnedResourceDisposalWhileAHealthDiagnosticCallbackIsHeld()
    {
        var entered = new TaskCompletionSource<Task>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        InMemoryLeaderElection? election = null;
        var logger = new CallbackLogger(message =>
        {
            if (!message.StartsWith("Updated health status", StringComparison.Ordinal)) { return; }
            entered.TrySetResult(election!.DisposeAsync().AsTask());
#pragma warning disable RS0030 // A synchronous logger is deliberately held while its reentrant disposal must finish.
            release.Task.GetAwaiter().GetResult();
#pragma warning restore RS0030
        });
        var clock = new FakeTimeProvider();
        await using var sut = new InMemoryLeaderElection("health-logger",
            Options.Create(new LeaderElectionOptions { InstanceId = "candidate" }), logger,
            new InMemoryLeaderElectionSharedState(), clock);
        election = sut;
        Task update = Task.CompletedTask;
        Task? disposing = null;
        try
        {
            await sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            update = Task.Run(() => sut.UpdateHealthAsync(true, null, CancellationToken.None));
            disposing = await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await disposing.WaitAsync(TimeSpan.FromSeconds(5));
            release.Task.IsCompleted.ShouldBeFalse();
            sut.IsLeader.ShouldBeFalse();
            sut.NotificationsCompleted.IsCompleted.ShouldBeFalse("the held logger belongs to the separate notification lane");
        }
        finally
        {
            release.TrySetResult();
            await update.WaitAsync(TimeSpan.FromSeconds(5));
            if (disposing is not null) { await disposing.WaitAsync(TimeSpan.FromSeconds(5)); }
            await sut.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            await sut.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public async Task AllowAQueuedBecameConsumerToJoinItsOwnResourceDisposal()
    {
        var clock = new FakeTimeProvider();
        await using var sut = new InMemoryLeaderElection("callback-disposal",
            Options.Create(new LeaderElectionOptions { InstanceId = "candidate" }),
            NullLogger<InMemoryLeaderElection>.Instance, new InMemoryLeaderElectionSharedState(), clock);
        var callback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        sut.BecameLeader += (_, _) =>
        {
            try
            {
#pragma warning disable RS0030 // Deliberate bounded synchronous callback Dispose adversary; a failure returns so cleanup remains joinable.
                sut.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
#pragma warning restore RS0030
                callback.TrySetResult();
            }
            catch (Exception failure) { callback.TrySetException(failure); }
        };
        try
        {
            await sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            await callback.Task.WaitAsync(TimeSpan.FromSeconds(10));
            sut.IsLeader.ShouldBeFalse();
        }
        finally
        {
            await sut.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
            await sut.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public async Task QueueTheOldLossBeforeATimerCanPublishTheNextTenure()
    {
        using var clock = new HealthTimestampBarrierClock();
        var fence = A.Fake<IFencingTokenIssuer>();
        long issued = 40;
        A.CallTo(() => fence.IssueTokenAsync(A<string>._, A<CancellationToken>._))
            .ReturnsLazily(() => ValueTask.FromResult(Interlocked.Increment(ref issued)));
        await using var sut = new InMemoryLeaderElection("health-order",
            Options.Create(new LeaderElectionOptions { InstanceId = "candidate", StepDownWhenUnhealthy = true }),
            NullLogger<InMemoryLeaderElection>.Instance, new InMemoryLeaderElectionSharedState(), clock, fence);
        var first = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var transitions = new ConcurrentQueue<long?>();
        sut.LeaderChanged += (_, change) =>
        {
            transitions.Enqueue(change.FencingToken);
            if (change.NewLeaderId is null) { lost.TrySetResult(); }
            else if (change.FencingToken == 41) { first.TrySetResult(); }
            else if (change.FencingToken == 42) { second.TrySetResult(); }
        };
        Task unhealthy = Task.CompletedTask;
        try
        {
            await sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            await first.Task.WaitAsync(TimeSpan.FromSeconds(5));
            unhealthy = Task.Run(() =>
            {
                clock.ArmSecondUtcReadOnCurrentThread();
                return sut.UpdateHealthAsync(false, null, CancellationToken.None);
            });
            await clock.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var releasePrecededTimestamp = !sut.IsLeader;
            await sut.UpdateHealthAsync(true, null, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            clock.Advance(TimeSpan.FromSeconds(5));
            if (releasePrecededTimestamp)
            {
                // The broken proposal released CandidateGate before obtaining this timestamp.
                // Force its admitted timer acquisition to publish before the delayed old loss.
                await second.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
            clock.Release.Set();
            await unhealthy.WaitAsync(TimeSpan.FromSeconds(5));
            await lost.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await sut.UpdateHealthAsync(true, null, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            clock.Advance(TimeSpan.FromSeconds(5));
            await second.Task.WaitAsync(TimeSpan.FromSeconds(5));
            transitions.ToArray().ShouldBe(ExpectedTransitions,
                "a later acquisition's notification cannot overtake the old tenure's loss at state commit");
            Volatile.Read(ref issued).ShouldBe(42);
        }
        finally
        {
            clock.Release.Set();
            await unhealthy.WaitAsync(TimeSpan.FromSeconds(5));
            await sut.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            await sut.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    private sealed class CallbackLogger(Action<string> callback) : ILogger<InMemoryLeaderElection>
    {
        public bool IsEnabled(LogLevel logLevel) => true;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => callback(formatter(state, exception));
    }

    private sealed class HealthTimestampBarrierClock : FakeTimeProvider, IDisposable
    {
        private int _targetThread;
        private int _reads;
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal ManualResetEventSlim Release { get; } = new(false);
        internal void ArmSecondUtcReadOnCurrentThread()
        {
            _reads = 0;
            Volatile.Write(ref _targetThread, Environment.CurrentManagedThreadId);
        }
        public override DateTimeOffset GetUtcNow()
        {
            if (Environment.CurrentManagedThreadId == Volatile.Read(ref _targetThread) &&
                Interlocked.Increment(ref _reads) == 2)
            {
                Entered.TrySetResult();
#pragma warning disable RS0030 // Freeze the actual health-stepdown timestamp sampling site; finally always releases this barrier.
                Release.Wait();
#pragma warning restore RS0030
            }
            return base.GetUtcNow();
        }
        public void Dispose() => Release.Dispose();
    }
}
