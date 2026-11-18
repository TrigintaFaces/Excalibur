// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using Excalibur.LeaderElection.Kubernetes;
using Microsoft.Extensions.Time.Testing;

namespace Excalibur.LeaderElection.Tests.Kubernetes;

[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
[Trait("Pattern", "Regression")]
public sealed class KubernetesCallerWaitShould(ITestOutputHelper output)
{
    [Fact]
    public async Task RearmAnActualEarlyWaitTimerWithoutCompletingTheLogicalWait()
    {
        var clock = new WaitClock(); var actual = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var waiting = KubernetesCallerWait.WaitAsync(actual.Task, clock.GetTimestamp(), TimeSpan.FromSeconds(10), clock, CancellationToken.None);
        try
        {
            clock.FireFirstEarly(); await clock.SecondTimer.Task.WaitAsync(TimeSpan.FromSeconds(5));
            waiting.IsCompleted.ShouldBeFalse(); clock.Seen[1].Due.ShouldBe(TimeSpan.FromSeconds(10));
            clock.Advance(TimeSpan.FromSeconds(10));
            await TerminalAsync(waiting); await Should.ThrowAsync<TimeoutException>(() => waiting);
            actual.Task.IsCompleted.ShouldBeFalse();
        }
        finally { actual.TrySetResult(); await ObserveAsync(waiting); }
    }

    [Fact]
    public async Task SplitAnOversizedBudgetWithoutResettingTheAbsoluteStart()
    {
        var clock = new WaitClock(); clock.Advance(TimeSpan.FromDays(30));
        var slice = TimeSpan.FromMilliseconds(uint.MaxValue - 1d); var budget = slice + TimeSpan.FromSeconds(2);
        var actual = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var waiting = KubernetesCallerWait.WaitAsync(actual.Task, clock.GetTimestamp(), budget, clock, CancellationToken.None);
        try
        {
            clock.Seen[0].Due.ShouldBe(slice); clock.Advance(slice);
            await clock.SecondTimer.Task.WaitAsync(TimeSpan.FromSeconds(5));
            clock.Seen[1].Due.ShouldBe(TimeSpan.FromSeconds(2)); waiting.IsCompleted.ShouldBeFalse();
            clock.Advance(TimeSpan.FromSeconds(2));
            await TerminalAsync(waiting); await Should.ThrowAsync<TimeoutException>(() => waiting);
            actual.Task.IsCompleted.ShouldBeFalse();
        }
        finally { actual.TrySetResult(); await ObserveAsync(waiting); }
    }

    [Fact]
    public async Task PropagateTheOperationsOwnTimeoutExceptionWithoutStartingAReplacementWait()
    {
        var clock = new WaitClock(); var expected = new TimeoutException("actual operation failed");
        var waiting = KubernetesCallerWait.WaitAsync(Task.FromException(expected), clock.GetTimestamp(), TimeSpan.FromSeconds(10), clock, CancellationToken.None);
        var observed = await Should.ThrowAsync<TimeoutException>(() => waiting);
        observed.ShouldBeSameAs(expected); clock.Seen.Length.ShouldBe(0);
    }

    [Fact]
    public async Task RefuseACompletedResultAtTheAbsoluteDeadline()
    {
        var clock = new WaitClock(); var start = clock.GetTimestamp(); clock.Advance(TimeSpan.FromSeconds(10));
        var waiting = KubernetesCallerWait.WaitAsync(Task.CompletedTask, start, TimeSpan.FromSeconds(10), clock, CancellationToken.None);
        await Should.ThrowAsync<TimeoutException>(() => waiting); clock.Seen.Length.ShouldBe(0);
    }

    [Fact]
    public async Task CalibrateActualDelayAndCallerWaitTimerSourcesBeforeUsingPurposeCensus()
    {
        var delayClock = new WaitClock(); var waitClock = new WaitClock();
        using var delayCancellation = new CancellationTokenSource(); using var waitCancellation = new CancellationTokenSource();
        var actual = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var delay = Task.Delay(TimeSpan.FromSeconds(10), delayClock, delayCancellation.Token); // delay-ok: This fake-clock delay calibrates actual timer identity and is canceled and joined; it does not wait ten wall-clock seconds.
        var wait = KubernetesCallerWait.WaitAsync(actual.Task, waitClock.GetTimestamp(), TimeSpan.FromSeconds(10), waitClock, waitCancellation.Token);
        try
        {
            var delaySource = delayClock.Seen.Single(); var waitSource = waitClock.Seen.Single();
            output.WriteLine($"Runtime={Environment.Version}; Delay={delaySource.StateType}; Wait={waitSource.StateType}; DelayCallback={delaySource.CallbackType}; WaitCallback={waitSource.CallbackType}");
            delaySource.StateType.ShouldNotBe(waitSource.StateType); // Collision is a real HOLD, never guessed away.
            delaySource.Due.ShouldBe(waitSource.Due); // Same due value demonstrably does not identify purpose.
        }
        finally
        {
            delayCancellation.Cancel(); waitCancellation.Cancel(); actual.TrySetResult();
            await ObserveAsync(delay); await ObserveAsync(wait);
        }
    }

    [Fact]
    public async Task SealTheRealRegisteredLatchBeforeItsLaunchWithoutAProductionTestHook()
    {
        var clock = new WaitClock(); var calls = 0;
        await using var kernel = new RenewalTenureDeadline(clock, TimeSpan.FromSeconds(10),
            retirement => retirement.OperationCompletion ?? Task.CompletedTask);
        var latch = new OwnedOperationLatch();
        kernel.BeginAcquisition(new object(), latch);
        kernel.RequestStop();
        latch.TryLaunch(() => { Interlocked.Increment(ref calls); return Task.CompletedTask; }).ShouldBeFalse();
        await latch.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        await kernel.DisposeAsync(); calls.ShouldBe(0); kernel.Current.ShouldBeNull();
        // This proves the real latch/kernel seam, not a particular public actor statement.
    }

    private static async Task TerminalAsync(Task actual)
    {
        var terminal = actual.ContinueWith(static _ => { }, CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        await terminal.WaitAsync(TimeSpan.FromSeconds(5));
    }
    private static async Task ObserveAsync(Task actual)
    {
        try { await actual.WaitAsync(TimeSpan.FromSeconds(5)); }
        catch (TimeoutException) when (actual.IsCompleted) { }
        catch (OperationCanceledException) when (actual.IsCompleted) { }
    }
    private sealed record TimerSource(TimeSpan Due, string StateType, string CallbackType);
    // This clock is used only by the actual helper or one explicit Task.Delay calibration invocation.
    // It never infers a production actor source statement from a framework type or stack ordinal.
    private sealed class WaitClock : FakeTimeProvider
    {
        private readonly Lock _gate = new(); private readonly List<TimerSource> _seen = [];
        private TimerCallback? _firstCallback; private object? _firstState;
        internal TaskCompletionSource SecondTimer { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TimerSource[] Seen { get { lock (_gate) { return [.. _seen]; } } }
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = base.CreateTimer(callback, state, dueTime, period);
            lock (_gate)
            {
                _firstCallback ??= callback; if (_seen.Count == 0) { _firstState = state; }
                _seen.Add(new(dueTime, state?.GetType().FullName ?? "<null>", callback.Method.DeclaringType?.FullName ?? "<null>"));
                if (_seen.Count == 2) { SecondTimer.TrySetResult(); }
            }
            return timer;
        }
        internal void FireFirstEarly() => (_firstCallback ?? throw new InvalidOperationException("No actual wait timer was created."))(_firstState);
    }
}
