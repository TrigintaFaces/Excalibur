// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using System.Data.Common;
using Excalibur.LeaderElection.Postgres;
using Excalibur.LeaderElection.Postgres.Diagnostics;
using Microsoft.Extensions.Time.Testing;

namespace Excalibur.Data.Tests.Postgres;

/// <summary>
/// A clean stop that seals the acquisition latch before it launches must be reported as a stop,
/// never as a lost contention race. The pair is deliberate: the safety arm alone is satisfied by
/// a provider that never reports contention at all, so the liveness arm fixes the other side.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Pattern", "Regression")]
public sealed class PostgresStopDuringAcquisitionShould
{
    /// <summary>
    /// SAFETY. The latch exists and is sealed before it launches, so the advisory-lock question
    /// never reached Postgres. The provider must not log a contention failure for a race it never
    /// entered. RED on pre-fix code: the not-acquired branch logs LockAcquisitionFailed regardless.
    /// </summary>
    [Fact]
    public async Task NotLogAContentionFailureWhenTheStopSealedTheLatchBeforeItLaunched()
    {
        var logger = new EventRecorder();
        var clock = new SeamClock();
        var sessions = new Sessions();
        var sut = PostgresTenureProtocolShould.Create(sessions.First, clock, factory: sessions.Next, logger: logger);
        // The seam is the one statement between Kernel.BeginAcquisition and Acquire.TryLaunch.
        // Stopping there seals the latch while its state is still Ready, so TryLaunch refuses.
        clock.OnSeam = () => sut.StopAsync(CancellationToken.None);

        var starting = sut.StartAsync(CancellationToken.None);
        _ = await Record.ExceptionAsync(() => starting.WaitAsync(Backstop));
        await sut.DisposeAsync().AsTask().WaitAsync(Backstop);
        await sut.NotificationsCompleted.WaitAsync(Backstop);
        _ = await Record.ExceptionAsync(() => clock.SeamWork!.WaitAsync(Backstop));

        // NON-VACUITY GUARD. Proves the fixture reached the sealed-unlaunched state rather than
        // the far easier never-begun state: a launched attempt always opens its connection first.
        clock.SeamFired.ShouldBeTrue("the stop must land between BeginAcquisition and TryLaunch");
        sessions.Opens.ShouldBe(0, "the sealed latch must refuse the launch, so Postgres is never asked");

        sut.IsLeader.ShouldBeFalse();
        logger.Count(LeaderElectionPostgresEventId.LockAcquisitionFailed)
            .ShouldBe(0, "a stop that sealed the latch is not a lost race: nothing was ever asked of Postgres");
    }

    /// <summary>
    /// LIVENESS. A genuine contention loss -- pg_try_advisory_lock really returns false -- must
    /// still log the contention failure. Without this arm the safety arm above is satisfied by
    /// deleting the diagnostic outright.
    /// </summary>
    [Fact]
    public async Task StillLogTheContentionFailureWhenTheAdvisoryLockIsRefused()
    {
        var logger = new EventRecorder();
        var sessions = new Sessions(acquire: false);
        var sut = PostgresTenureProtocolShould.Create(sessions.First, new FakeTimeProvider(), factory: sessions.Next, logger: logger);

        await sut.StartAsync(CancellationToken.None).WaitAsync(Backstop);
        await sut.DisposeAsync().AsTask().WaitAsync(Backstop);
        await sut.NotificationsCompleted.WaitAsync(Backstop);

        sessions.Opens.ShouldBeGreaterThan(0, "the attempt must actually have asked Postgres");
        sut.IsLeader.ShouldBeFalse();
        logger.Count(LeaderElectionPostgresEventId.LockAcquisitionFailed)
            .ShouldBeGreaterThan(0, "a real race loss stays observable to an operator reading logs");
    }

    // Generous on purpose. This suite already carries 5s real-clock watchdogs that flake on a
    // loaded 2-CPU container; nothing here waits on the clock, so a long backstop costs nothing.
    private static TimeSpan Backstop => TimeSpan.FromSeconds(30);

    /// <summary>Hands out a fresh closed connection per attempt, as the provider requires.</summary>
    private sealed class Sessions(bool acquire = true)
    {
        private readonly ConcurrentBag<PostgresTenureProtocolShould.Session> _issued = [];
        internal PostgresTenureProtocolShould.Session First { get; } = Make(acquire);
        internal DbConnection Next(string _)
        {
            var session = Make(acquire);
            _issued.Add(session);
            return session;
        }
        internal int Opens => _issued.Sum(session => session.OpenCount);
        private static PostgresTenureProtocolShould.Session Make(bool acquire) =>
            new() { Acquire = _ => Task.FromResult<object?>(acquire) };
    }

    /// <summary>A fake clock that hands control to the test once, at the first timestamp read.</summary>
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
    private sealed class EventRecorder : ILogger<PostgresLeaderElection>
    {
        private readonly ConcurrentBag<int> _ids = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error,
            Func<TState, Exception?, string> formatter) => _ids.Add(id.Id);
        internal int Count(int eventId) => _ids.Count(candidate => candidate == eventId);
    }
}
