// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.Time.Testing;

namespace Excalibur.LeaderElection.Tests.Redis;

/// <summary>
/// A clean stop that seals the acquisition latch before it launches must be reported as a stop,
/// never as a lost contention race. The pair is deliberate: the safety arm alone is satisfied by
/// a provider that never reports contention at all, so the liveness arm fixes the other side.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
[Trait("Pattern", "Regression")]
public sealed class RedisStopDuringAcquisitionShould
{
    // LeaderElectionEventId.RedisLockAcquisitionFailed.
    private const int LockAcquisitionFailed = 183002;

    /// <summary>
    /// SAFETY. The latch exists and is sealed before it launches, so no question ever reached
    /// Redis. The provider must not log a contention failure for a race it never entered.
    /// RED on pre-fix code: the not-acquired branch emits LogLockAcquisitionFailed unconditionally.
    /// </summary>
    [Fact]
    public async Task NotLogAContentionFailureWhenTheStopSealedTheLatchBeforeItLaunched()
    {
        var logger = new EventRecorder();
        var clock = new SeamClock();
        var harness = new RedisTenureProtocolShould.Harness(clock: clock, logger: logger);
        // The seam is the one statement between Kernel.BeginAcquisition and Acquire.TryLaunch.
        // Stopping there seals the latch while its state is still Ready, so TryLaunch refuses.
        clock.OnSeam = () => harness.Sut.StopAsync(CancellationToken.None);
        var starting = harness.Sut.StartAsync(CancellationToken.None);
        _ = await Record.ExceptionAsync(() => starting.WaitAsync(Backstop));
        await harness.Sut.DisposeAsync().AsTask().WaitAsync(Backstop);
        await harness.Sut.NotificationsCompleted.WaitAsync(Backstop);
        _ = await Record.ExceptionAsync(() => clock.SeamWork!.WaitAsync(Backstop));

        // NON-VACUITY GUARD. Proves the fixture reached the sealed-unlaunched state rather than
        // the far easier never-begun state: a launched attempt always enqueues one owner.
        clock.SeamFired.ShouldBeTrue("the stop must land between BeginAcquisition and TryLaunch");
        harness.Owners.Count.ShouldBe(0, "the sealed latch must refuse the launch, so Redis is never asked");

        harness.Sut.IsLeader.ShouldBeFalse();
        logger.Count(LockAcquisitionFailed)
            .ShouldBe(0, "a stop that sealed the latch is not a lost race: nothing was ever asked of Redis");
    }

    /// <summary>
    /// SAFETY, the OTHER fixture state: the latch DID launch and the native answer arrives after
    /// the stop. Distinct from the arm above, whose whole point is that the work never ran.
    /// RED on pre-fix code; GREEN under either candidate guard, so it locks the branch rather
    /// than discriminating between them.
    /// </summary>
    [Fact]
    public async Task NotLogAContentionFailureWhenTheStopSealedAnAlreadyLaunchedAcquisition()
    {
        var logger = new EventRecorder();
        var held = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var harness = new RedisTenureProtocolShould.Harness(clock: new FakeTimeProvider(), heldAcquire: held.Task, logger: logger);

        var starting = harness.Sut.StartAsync(CancellationToken.None);
        await harness.SetEntered.Task.WaitAsync(Backstop);   // the attempt is genuinely in flight
        var stopping = harness.Sut.StopAsync(CancellationToken.None);
        held.TrySetResult(false);                            // the answer lands after the seal
        _ = await Record.ExceptionAsync(() => starting.WaitAsync(Backstop));
        await stopping.WaitAsync(Backstop);
        await harness.Sut.DisposeAsync().AsTask().WaitAsync(Backstop);
        await harness.Sut.NotificationsCompleted.WaitAsync(Backstop);

        // NON-VACUITY GUARD, and deliberately the inverse of the sealed-unlaunched arm's zero:
        // the work ran here, so this arm cannot silently collapse into that one.
        harness.Owners.Count.ShouldBe(1, "this arm must exercise a launched attempt, not a refused launch");
        harness.Sut.IsLeader.ShouldBeFalse();
        logger.Count(LockAcquisitionFailed)
            .ShouldBe(0, "a stop that sealed a launched attempt is still a stop, not a lost race");
    }

    /// <summary>
    /// LIVENESS. A genuine contention loss -- Redis really reports another holder -- must still
    /// log the contention failure. Without this arm the safety arm above is satisfied by deleting
    /// the diagnostic outright.
    /// </summary>
    [Fact]
    public async Task StillLogTheContentionFailureWhenRedisReportsAnotherHolder()
    {
        var logger = new EventRecorder();
        var harness = new RedisTenureProtocolShould.Harness(acquire: false, clock: new FakeTimeProvider(), logger: logger);
        await harness.Sut.StartAsync(CancellationToken.None).WaitAsync(Backstop);
        await harness.Sut.DisposeAsync().AsTask().WaitAsync(Backstop);
        await harness.Sut.NotificationsCompleted.WaitAsync(Backstop);

        harness.Owners.Count.ShouldBeGreaterThan(0, "the attempt must actually have asked Redis");
        harness.Sut.IsLeader.ShouldBeFalse();
        logger.Count(LockAcquisitionFailed)
            .ShouldBeGreaterThan(0, "a real race loss stays observable to an operator reading logs");
    }

    // Generous on purpose. This suite already carries 5s real-clock watchdogs that flake on a
    // loaded 2-CPU container; nothing here waits on the clock, so a long backstop costs nothing.
    private static TimeSpan Backstop => TimeSpan.FromSeconds(30);

    /// <summary>A fake clock that hands control to the test once, at the Nth timestamp read.</summary>
    private sealed class SeamClock : FakeTimeProvider
    {
        private int _reads;
        internal Func<Task>? OnSeam { get; set; }
        internal Task? SeamWork { get; private set; }
        internal bool SeamFired => SeamWork is not null;

        public override long GetTimestamp()
        {
            if (Interlocked.Increment(ref _reads) == 1 && OnSeam is { } seam)
            {
                // Synchronous by design: BeginStop seals the latch before it returns, so the
                // actor cannot reach TryLaunch until the seal has been linearized.
                SeamWork = seam();
            }
            return base.GetTimestamp();
        }
    }

    /// <summary>Records which generated diagnostics actually reached a logger.</summary>
    private sealed class EventRecorder : ILogger<RedisLeaderElection>
    {
        private readonly System.Collections.Concurrent.ConcurrentBag<int> _ids = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error,
            Func<TState, Exception?, string> formatter) => _ids.Add(id.Id);
        internal int Count(int eventId) => _ids.Count(candidate => candidate == eventId);
    }
}
