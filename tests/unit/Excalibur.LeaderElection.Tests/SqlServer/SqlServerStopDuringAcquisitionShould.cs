// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.LeaderElection.SqlServer;
using Microsoft.Extensions.Time.Testing;

namespace Excalibur.LeaderElection.Tests.SqlServer;

/// <summary>
/// MEASUREMENT, not a shipped regression arm. This provider already carries the stop arm
/// (`else if (run.Stop.IsCancellationRequested)`), so the question is only whether that
/// discriminator actually catches the sealed-mid-acquisition case or whether it falls through
/// to the not-granted arm that logs SqlServerLockAcquisitionFailed.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
[Trait("Pattern", "Regression")]
public sealed class SqlServerStopDuringAcquisitionShould
{
    // LeaderElectionEventId.SqlServerLockAcquisitionFailed.
    private const int LockAcquisitionFailed = 184002;

    [Fact]
    public async Task NotLogAContentionFailureWhenTheStopSealedTheLatchBeforeItLaunched()
    {
        var logger = new EventRecorder();
        var clock = new SeamClock();
        var sessions = 0;
        var sut = new SqlServerLeaderElection(() => { Interlocked.Increment(ref sessions); return new Session(granted: true); },
            "sql-stop-test", Options.Create(new LeaderElectionOptions { InstanceId = "candidate" }), logger, null, null, clock);
        // The seam is the one statement between Kernel.BeginAcquisition and Acquire.TryLaunch.
        clock.OnSeam = () => sut.StopAsync(CancellationToken.None);

        var starting = sut.StartAsync(CancellationToken.None);
        _ = await Record.ExceptionAsync(() => starting.WaitAsync(Backstop));
        await sut.DisposeAsync().AsTask().WaitAsync(Backstop);
        await sut.NotificationsCompleted.WaitAsync(Backstop);
        _ = await Record.ExceptionAsync(() => clock.SeamWork!.WaitAsync(Backstop));

        // NON-VACUITY GUARD. This provider creates its session INSIDE the launched operation, so
        // a zero here is proof the sealed latch refused the launch -- not that the loop never began.
        clock.SeamFired.ShouldBeTrue("the stop must land between BeginAcquisition and TryLaunch");
        Volatile.Read(ref sessions).ShouldBe(0, "the sealed latch must refuse the launch, so no session is opened");

        sut.IsLeader.ShouldBeFalse();
        logger.Count(LockAcquisitionFailed)
            .ShouldBe(0, "a stop that sealed the latch is not a lost race: nothing was ever asked of SQL Server");
    }

    [Fact]
    public async Task StillLogTheContentionFailureWhenTheApplicationLockIsNotGranted()
    {
        var logger = new EventRecorder();
        var sessions = 0;
        var sut = new SqlServerLeaderElection(() => { Interlocked.Increment(ref sessions); return new Session(granted: false); },
            "sql-stop-test", Options.Create(new LeaderElectionOptions { InstanceId = "candidate" }), logger, null, null, new FakeTimeProvider());

        await sut.StartAsync(CancellationToken.None).WaitAsync(Backstop);
        await sut.DisposeAsync().AsTask().WaitAsync(Backstop);
        await sut.NotificationsCompleted.WaitAsync(Backstop);

        Volatile.Read(ref sessions).ShouldBeGreaterThan(0, "the attempt must actually have asked SQL Server");
        sut.IsLeader.ShouldBeFalse();
        logger.Count(LockAcquisitionFailed)
            .ShouldBeGreaterThan(0, "a real race loss stays observable to an operator reading logs");
    }

    private static TimeSpan Backstop => TimeSpan.FromSeconds(30);

    private sealed class Session(bool granted) : ISqlServerElectionSession
    {
        public Task<int> AcquireAsync(string resource, int lockTimeoutMilliseconds, int commandTimeoutSeconds, CancellationToken token) =>
            Task.FromResult(granted ? 0 : -1);
        public Task<SqlServerSessionProof> VerifyAsync(string resource, int commandTimeoutSeconds, CancellationToken token) =>
            Task.FromResult(SqlServerSessionProof.Exclusive);
        public Task ReleaseAsync(string resource, int commandTimeoutSeconds, CancellationToken token) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class SeamClock : FakeTimeProvider
    {
        private int _reads;
        internal Func<Task>? OnSeam { get; set; }
        internal Task? SeamWork { get; private set; }
        internal bool SeamFired => SeamWork is not null;
        public override long GetTimestamp()
        {
            if (Interlocked.Increment(ref _reads) == 1 && OnSeam is { } seam) { SeamWork = seam(); }
            return base.GetTimestamp();
        }
    }

    private sealed class EventRecorder : ILogger<SqlServerLeaderElection>
    {
        private readonly System.Collections.Concurrent.ConcurrentBag<int> _ids = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error,
            Func<TState, Exception?, string> formatter) => _ids.Add(id.Id);
        internal int Count(int eventId) => _ids.Count(candidate => candidate == eventId);
    }
}
