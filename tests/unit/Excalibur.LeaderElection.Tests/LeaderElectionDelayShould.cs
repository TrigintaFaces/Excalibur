// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Threading.Channels;

namespace Excalibur.LeaderElection.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
[Trait("Pattern", "Regression")]
public sealed class LeaderElectionDelayShould
{
    [Theory]
    [InlineData(1L)]
    [InlineData(5000L)]
    [InlineData(9999L)]
    public async Task NormalizeSubmillisecondIntervalBeforeElapsedComparison(long ticks)
    {
        var clock = new ManualClock();
        var start = clock.GetTimestamp();
        clock.Advance(TimeSpan.FromTicks(ticks));
        var waiting = LeaderElectionDelay.WaitFromAsync(start, TimeSpan.FromTicks(ticks), clock, TestContext.Current.CancellationToken);
        var timer = await clock.NextTimerAsync();
        timer.Due.ShouldBe(TimeSpan.FromMilliseconds(1));
        waiting.IsCompleted.ShouldBeFalse();
        clock.Advance(TimeSpan.FromTicks(TimeSpan.TicksPerMillisecond - ticks));
        timer.Fire();
        await waiting.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task PreserveFiveSecondStartCadenceAcrossFourPointNineSecondReplies()
    {
        var clock = new ManualClock();
        var firstRequestStart = clock.GetTimestamp();
        clock.Advance(TimeSpan.FromMilliseconds(4900));
        var firstWait = LeaderElectionDelay.WaitFromAsync(firstRequestStart, TimeSpan.FromSeconds(5), clock, TestContext.Current.CancellationToken);
        var first = await clock.NextTimerAsync();
        first.Due.ShouldBe(TimeSpan.FromMilliseconds(100));
        clock.Advance(TimeSpan.FromMilliseconds(100)); first.Fire();
        await firstWait.WaitAsync(TimeSpan.FromSeconds(5));
        var secondRequestStart = clock.GetTimestamp();
        secondRequestStart.ShouldBe(TimeSpan.FromSeconds(5).Ticks);
        clock.Advance(TimeSpan.FromMilliseconds(4900));
        var secondWait = LeaderElectionDelay.WaitFromAsync(secondRequestStart, TimeSpan.FromSeconds(5), clock, TestContext.Current.CancellationToken);
        var second = await clock.NextTimerAsync();
        second.Due.ShouldBe(TimeSpan.FromMilliseconds(100));
        clock.Advance(TimeSpan.FromMilliseconds(100)); second.Fire();
        await secondWait.WaitAsync(TimeSpan.FromSeconds(5));
        clock.GetTimestamp().ShouldBe(TimeSpan.FromSeconds(10).Ticks);
    }

    [Fact]
    public async Task AnchorRetryToTheFailedRequestStartRatherThanItsReply()
    {
        var clock = new ManualClock();
        clock.Advance(TimeSpan.FromSeconds(5));
        var requestStart = clock.GetTimestamp();
        clock.Advance(TimeSpan.FromMilliseconds(100));
        var waiting = LeaderElectionDelay.WaitFromAsync(requestStart, TimeSpan.FromSeconds(2), clock, TestContext.Current.CancellationToken);
        var timer = await clock.NextTimerAsync();
        timer.Due.ShouldBe(TimeSpan.FromMilliseconds(1900));
        clock.Advance(TimeSpan.FromMilliseconds(1900)); timer.Fire();
        await waiting.WaitAsync(TimeSpan.FromSeconds(5));
        clock.GetTimestamp().ShouldBe(TimeSpan.FromSeconds(7).Ticks);
    }

    [Fact]
    public async Task CompleteOneOverdueIntervalWithoutReplayingMissedTicks()
    {
        var clock = new ManualClock();
        var oldStart = clock.GetTimestamp();
        clock.Advance(TimeSpan.FromSeconds(10));
        await LeaderElectionDelay.WaitFromAsync(oldStart, TimeSpan.FromSeconds(2), clock, TestContext.Current.CancellationToken);
        clock.TimerCount.ShouldBe(0);
        var newRequestStart = clock.GetTimestamp();
        var waiting = LeaderElectionDelay.WaitFromAsync(newRequestStart, TimeSpan.FromSeconds(2), clock, TestContext.Current.CancellationToken);
        var timer = await clock.NextTimerAsync();
        timer.Due.ShouldBe(TimeSpan.FromSeconds(2));
        waiting.IsCompleted.ShouldBeFalse();
        clock.Advance(TimeSpan.FromSeconds(2)); timer.Fire();
        await waiting.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ReArmAnEarlyCallbackUntilTheMonotonicTarget()
    {
        var clock = new ManualClock();
        var waiting = LeaderElectionDelay.WaitAsync(TimeSpan.FromTicks(35000), clock, TestContext.Current.CancellationToken);
        var first = await clock.NextTimerAsync();
        first.Due.ShouldBe(TimeSpan.FromMilliseconds(4));
        clock.Advance(TimeSpan.FromMilliseconds(1)); first.Fire();
        var second = await clock.NextTimerAsync();
        second.Due.ShouldBe(TimeSpan.FromMilliseconds(3));
        waiting.IsCompleted.ShouldBeFalse();
        clock.Advance(TimeSpan.FromTicks(25000)); second.Fire();
        await waiting.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task SliceMaxValueWithoutOverflowAndStillObserveCancellation()
    {
        var clock = new ManualClock();
        using var cancellation = new CancellationTokenSource();
        var waiting = LeaderElectionDelay.WaitAsync(TimeSpan.MaxValue, clock, cancellation.Token);
        var first = await clock.NextTimerAsync();
        var maximum = TimeSpan.FromTicks((uint.MaxValue - 1L) * TimeSpan.TicksPerMillisecond);
        first.Due.ShouldBe(maximum);
        clock.Advance(maximum); first.Fire();
        var second = await clock.NextTimerAsync();
        second.Due.ShouldBe(maximum);
        waiting.IsCompleted.ShouldBeFalse();
        await cancellation.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => waiting);
        clock.TimerCount.ShouldBe(2);
    }

    [Fact]
    public async Task ObserveCancellationBeforeAllocatingATimer()
    {
        var clock = new ManualClock();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => LeaderElectionDelay.WaitAsync(TimeSpan.FromSeconds(1), clock, cancellation.Token));
        clock.TimerCount.ShouldBe(0);
    }

    [Fact]
    public async Task RefuseABackwardsElapsedClockWithoutAnImmediateRetryLoop()
    {
        var clock = new ManualClock();
        var start = clock.GetTimestamp();
        clock.Advance(TimeSpan.FromTicks(-1));
        await Should.ThrowAsync<InvalidOperationException>(() => LeaderElectionDelay.WaitFromAsync(start, TimeSpan.FromSeconds(1), clock, TestContext.Current.CancellationToken));
        clock.TimerCount.ShouldBe(0);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public async Task RejectNonpositiveIntervals(long ticks)
    {
        var clock = new ManualClock();
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => LeaderElectionDelay.WaitAsync(TimeSpan.FromTicks(ticks), clock, TestContext.Current.CancellationToken));
        clock.TimerCount.ShouldBe(0);
    }

    private sealed class ManualClock : TimeProvider
    {
        private readonly Channel<ManualTimer> _timers = Channel.CreateUnbounded<ManualTimer>(
            new UnboundedChannelOptions { SingleReader = true, AllowSynchronousContinuations = false });
        private long _ticks;
        private int _timerCount;
        internal int TimerCount => Volatile.Read(ref _timerCount);
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Volatile.Read(ref _ticks);
        public override DateTimeOffset GetUtcNow() => throw new InvalidOperationException("This control only permits monotonic time.");
        internal void Advance(TimeSpan amount) => Interlocked.Add(ref _ticks, amount.Ticks);
        internal async Task<ManualTimer> NextTimerAsync() =>
            await _timers.Reader.ReadAsync(TestContext.Current.CancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(callback, state, dueTime);
            Interlocked.Increment(ref _timerCount);
            if (!_timers.Writer.TryWrite(timer)) { throw new InvalidOperationException("The control could not record its timer."); }
            return timer;
        }
    }

    private sealed class ManualTimer(TimerCallback callback, object? state, TimeSpan due) : ITimer
    {
        internal TimeSpan Due { get; private set; } = due;
        public bool Change(TimeSpan dueTime, TimeSpan period) { Due = dueTime; return true; }
        internal void Fire() => callback(state);
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
