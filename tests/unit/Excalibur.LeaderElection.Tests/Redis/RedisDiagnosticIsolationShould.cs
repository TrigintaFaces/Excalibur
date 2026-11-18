// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using Excalibur.LeaderElection.Redis;
namespace Excalibur.LeaderElection.Tests.Redis;
[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
[Trait("Pattern", "Regression")]
public sealed class RedisDiagnosticIsolationShould
{
    [Fact]
    public async Task PreserveNormalGeneratedDiagnosticsAfterJoinedStopAndTerminalDelivery()
    {
        var logger = new ProbeLogger();
        var h = new RedisTenureProtocolShould.Harness(logger: logger, releaseFailure: null);
        try { await StartOwnedAsync(h.Sut); await h.Sut.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)); }
        finally { await CompleteAsync(h, null, null); }
        logger.Count.ShouldBeGreaterThan(0, "normal authority transitions must still emit generated diagnostics");
        AssertNativeRetired(h);
    }

    [Fact]
    public async Task FinishActualAcquisitionAndResourceCleanupWhileTheFirstRuntimeLoggerIsHeld()
    {
        var logger = new ProbeLogger();
        using var release = new ManualResetEventSlim();
        var entered = Signal(); var held = 0;
        var h = new RedisTenureProtocolShould.Harness(logger: logger, releaseFailure: null);
        Task? starting = null;
        logger.OnLog = () =>
        {
            if (Interlocked.CompareExchange(ref held, 1, 0) != 0) { return; }
            entered.TrySetResult();
#pragma warning disable RS0030 // Actual foreign logger remains held until the independent controller releases it in finally.
            release.Wait();
#pragma warning restore RS0030
        };
        try
        {
            starting = h.Sut.StartAsync(CancellationToken.None);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await starting.WaitAsync(TimeSpan.FromSeconds(5));
            h.Sut.IsLeader.ShouldBeTrue();
            await h.Sut.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            h.Sut.CurrentLeadership.ShouldBeNull();
            AssertNativeRetired(h);
            h.Sut.NotificationsCompleted.IsCompleted.ShouldBeFalse("the held logger remains admitted terminal work");
        }
        finally
        {
            release.Set();
            var startupFailure = starting is null ? null : await Record.ExceptionAsync(() => starting.WaitAsync(TimeSpan.FromSeconds(5)));
            await CompleteAsync(h, null, null);
            startupFailure.ShouldBeNull();
        }
    }

    [Fact]
    public async Task KeepNativeCleanupAndBothPrimaryEventFamiliesIndependentOfAThrowingLogger()
    {
        var logger = new ProbeLogger(); var diagnosticFailure = new InvalidOperationException("logger failed");
        var armed = 0;
        logger.OnLog = () => { if (Volatile.Read(ref armed) != 0) { throw diagnosticFailure; } };
        var h = new RedisTenureProtocolShould.Harness(logger: logger, releaseFailure: null);
        var events = ObserveFamilies(h.Sut);
        Exception? stopped = null;
        try
        {
            await StartOwnedAsync(h.Sut); h.Sut.IsLeader.ShouldBeTrue();
            Volatile.Write(ref armed, 1);
            stopped = await Record.ExceptionAsync(() => h.Sut.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally { await CompleteAsync(h, null, diagnosticFailure); }
        stopped.ShouldBeNull("a diagnostic failure belongs to terminal notifications, not resource cleanup");
        AssertNativeRetired(h); events.AssertOneTenure();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PermitLoggerCallbackStopOrDisposeWithoutJoiningItsOwnActor(bool dispose)
    {
        var logger = new ProbeLogger(); var callback = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var once = 0; var h = new RedisTenureProtocolShould.Harness(logger: logger, releaseFailure: null);
        logger.OnLog = () =>
        {
            if (Interlocked.CompareExchange(ref once, 1, 0) != 0) { return; }
            Exception? failure = null;
            try
            {
#pragma warning disable RS0030 // The logger deliberately joins resource work; it must not be running on that owned resource actor.
                (dispose ? h.Sut.DisposeAsync().AsTask() : h.Sut.StopAsync(CancellationToken.None)).WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
#pragma warning restore RS0030
            }
            catch (Exception error) { failure = error; }
            callback.TrySetResult(failure);
        };
        try
        {
            var starting = h.Sut.StartAsync(CancellationToken.None);
            (await callback.Task.WaitAsync(TimeSpan.FromSeconds(12))).ShouldBeNull();
            var outcome = await Record.ExceptionAsync(() => starting.WaitAsync(TimeSpan.FromSeconds(5)));
            if (outcome is not null)
            {
                var cancellation = outcome.ShouldBeAssignableTo<OperationCanceledException>();
                cancellation.CancellationToken.CanBeCanceled.ShouldBeTrue();
                cancellation.CancellationToken.IsCancellationRequested.ShouldBeTrue();
            }
        }
        finally { await CompleteAsync(h, null, null); }
        h.Sut.IsLeader.ShouldBeFalse(); AssertNativeRetired(h);
    }

    [Fact]
    public async Task PreserveTheExactNativeCleanupFaultSeparatelyFromTheLoggerFault()
    {
        var logger = new ProbeLogger(); var diagnosticFailure = new InvalidOperationException("logger failed independently");
        var nativeFailure = new IOException("native release failed"); var armed = 0;
        logger.OnLog = () => { if (Volatile.Read(ref armed) != 0) { throw diagnosticFailure; } };
        var h = new RedisTenureProtocolShould.Harness(logger: logger, releaseFailure: nativeFailure);
        var events = ObserveFamilies(h.Sut);
        Exception? stopped = null;
        try
        {
            await StartOwnedAsync(h.Sut); h.Sut.IsLeader.ShouldBeTrue();
            
            Volatile.Write(ref armed, 1);
            stopped = await Record.ExceptionAsync(() => h.Sut.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally { await CompleteAsync(h, nativeFailure, diagnosticFailure); }
        stopped.ShouldBeSameAs(nativeFailure); AssertNativeRetired(h); events.AssertOneTenure();
    }

    private static async Task StartOwnedAsync(ILeaderElection election)
    {
        var acquired = Signal();
        election.LeaderChanged += (_, args) => { if (args.NewLeaderId == election.CandidateId) { acquired.TrySetResult(); } };
        await election.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        await acquired.Task.WaitAsync(TimeSpan.FromSeconds(5));
        election.IsLeader.ShouldBeTrue();
    }
    private static async Task CompleteAsync(RedisTenureProtocolShould.Harness h, Exception? expectedResource, Exception? expectedDiagnostic)
    {
        var resource = await Record.ExceptionAsync(() => h.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
        var tail = await Record.ExceptionAsync(() => h.Sut.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(5)));
        resource.ShouldBeSameAs(expectedResource); tail.ShouldBeSameAs(expectedDiagnostic);
    }
    private static void AssertNativeRetired(RedisTenureProtocolShould.Harness h) { h.Releases.Count.ShouldBe(h.Owners.Count); A.CallTo(() => h.Redis.Dispose()).MustNotHaveHappened(); }
    private static Families ObserveFamilies(ILeaderElection election)
    {
        var counts = new Families();
        election.BecameLeader += (_, _) => Interlocked.Increment(ref counts.Acquired);
        election.LostLeadership += (_, _) => Interlocked.Increment(ref counts.Lost);
        election.LeaderChanged += (_, args) =>
        {
            if (args.NewLeaderId == election.CandidateId) { Interlocked.Increment(ref counts.ChangedAcquired); }
            else if (args.PreviousLeaderId == election.CandidateId && args.NewLeaderId is null) { Interlocked.Increment(ref counts.ChangedLost); }
        };
        return counts;
    }
    private sealed class Families
    {
        internal int Acquired; internal int Lost; internal int ChangedAcquired; internal int ChangedLost;
        internal void AssertOneTenure() { Acquired.ShouldBe(1); Lost.ShouldBe(1); ChangedAcquired.ShouldBe(1); ChangedLost.ShouldBe(1); }
    }
    private sealed class ProbeLogger : ILogger<RedisLeaderElection>
    {
        internal int Count; internal Action? OnLog;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> formatter)
        { Interlocked.Increment(ref Count); OnLog?.Invoke(); }
    }
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
