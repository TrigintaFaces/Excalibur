// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using Excalibur.Dispatch.LeaderElection;
using Excalibur.LeaderElection.Postgres;

namespace Excalibur.Data.Tests.Postgres;

[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
[Trait("Pattern", "Regression")]
public sealed class PostgresDiagnosticLifetimeShould
{
    [Fact]
    public async Task RetainPositiveNormalDiagnosticsAndBothOwnershipEventFamilies()
    {
        var logger = new DiagnosticLogger(); var fixture = new Fixture(logger);
        try
        {
            await fixture.Sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            fixture.Sut.IsLeader.ShouldBeTrue();
            await fixture.Sut.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            await fixture.Sut.DisposeAsync(); await fixture.Sut.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(5));
            logger.Calls.ShouldBeGreaterThan(0); fixture.AssertRetired(); fixture.AssertEvents();
        }
        finally { await fixture.DisposeAsync(); }
    }

    [Fact]
    public async Task JoinActualResourcesWhileTheFirstExternalLoggerRemainsHeld()
    {
        var entered = Signal(); using var release = new ManualResetEventSlim();
        var logger = new DiagnosticLogger(); var held = 0;
        logger.Callback = _ =>
        {
            if (Interlocked.Increment(ref held) != 1) { return; }
            entered.TrySetResult();
#pragma warning disable RS0030 // Deliberate foreign synchronous logger barrier, released and joined in finally.
            if (!release.Wait(TimeSpan.FromSeconds(15))) { throw new TimeoutException("Logger gate not released."); }
#pragma warning restore RS0030
        };
        var fixture = new Fixture(logger);
        try
        {
            var starting = fixture.Sut.StartAsync(CancellationToken.None);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await starting.WaitAsync(TimeSpan.FromSeconds(5));
            fixture.Sut.IsLeader.ShouldBeTrue(); fixture.AssertAcquired();
            await fixture.Sut.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            await fixture.Sut.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            fixture.AssertRetired(); fixture.Sut.NotificationsCompleted.IsCompleted.ShouldBeFalse();
        }
        finally { release.Set(); await fixture.DisposeAsync(); }
        fixture.AssertEvents(); logger.Calls.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task RetainThrowingLoggerFailureOnlyOnTerminalDeliveryWithoutSkippingCleanupOrEvents()
    {
        var error = new IOException("exact external logger error");
        var logger = new DiagnosticLogger { Callback = _ => throw error }; var fixture = new Fixture(logger, terminal: error);
        try
        {
            await fixture.Sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            fixture.Sut.IsLeader.ShouldBeTrue(); fixture.AssertAcquired();
            await fixture.Sut.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            await fixture.Sut.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            (await Should.ThrowAsync<IOException>(() => fixture.Sut.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(5)))).ShouldBeSameAs(error);
            fixture.AssertRetired(); fixture.AssertEvents(); logger.Calls.ShouldBeGreaterThan(0);
        }
        finally { await fixture.DisposeAsync(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PermitAnAcquiredDiagnosticCallbackToJoinStopOrResourceDisposal(bool dispose)
    {
        var completed = Signal(); var logger = new DiagnosticLogger(); var fixture = new Fixture(logger);
        var armed = 0; var invoked = 0;
        fixture.Sut.BecameLeader += (_, _) => Volatile.Write(ref armed, 1);
        logger.Callback = _ =>
        {
            if (Volatile.Read(ref armed) == 0 || Interlocked.CompareExchange(ref invoked, 1, 0) != 0) { return; }
#pragma warning disable RS0030 // Foreign logger callback deliberately joins lifecycle; it must not self-join diagnostic delivery.
            if (dispose) { fixture.Sut.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult(); }
            else { fixture.Sut.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult(); }
#pragma warning restore RS0030
            completed.TrySetResult();
        };
        try
        {
            await fixture.Sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            await completed.Task.WaitAsync(TimeSpan.FromSeconds(8));
            fixture.AssertRetired(); invoked.ShouldBe(1);
        }
        finally { await fixture.DisposeAsync(); }
        fixture.AssertEvents();
    }

    [Fact]
    public async Task KeepTheExactNativeCleanupFailureSeparateFromTheExactLoggerFailure()
    {
        var cleanup = new IOException("exact native cleanup error"); var logging = new IOException("exact logger error");
        var logger = new DiagnosticLogger { Callback = _ => throw logging }; var fixture = new Fixture(logger, cleanup, logging);
        try
        {
            await fixture.Sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            fixture.Sut.IsLeader.ShouldBeTrue(); fixture.AssertAcquired();
            fixture.ArmCleanupFailure(cleanup);
            (await Should.ThrowAsync<IOException>(() => fixture.Sut.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)))).ShouldBeSameAs(cleanup);
            var first = fixture.Sut.DisposeAsync().AsTask(); var second = fixture.Sut.DisposeAsync().AsTask();
            first.ShouldBeSameAs(second);
            (await Should.ThrowAsync<IOException>(() => first.WaitAsync(TimeSpan.FromSeconds(5)))).ShouldBeSameAs(cleanup);
            (await Should.ThrowAsync<IOException>(() => second.WaitAsync(TimeSpan.FromSeconds(5)))).ShouldBeSameAs(cleanup);
            (await Should.ThrowAsync<IOException>(() => fixture.Sut.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(5)))).ShouldBeSameAs(logging);
            fixture.AssertRetired(); fixture.AssertEvents();
        }
        finally { await fixture.DisposeAsync(); }
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private sealed class DiagnosticLogger : ILogger<PostgresLeaderElection>
    {
        private int _calls;
        internal int Calls => Volatile.Read(ref _calls);
        internal Action<string>? Callback { get; set; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? error, Func<TState, Exception?, string> formatter)
        { Interlocked.Increment(ref _calls); Callback?.Invoke(formatter(state, error)); }
    }
    private sealed class Fixture : IAsyncDisposable
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "The provider owns this injected session; Fixture.DisposeAsync joins provider resource cleanup before terminal delivery. Tests assert exactly one native session disposal; a second fixture-side native Dispose would violate that ownership.")]
        private readonly PostgresTenureProtocolShould.Session _session;
        internal ILeaderElection Sut { get; }
        private readonly Exception? _expectedResource; private readonly Exception? _expectedTerminal;
        private int _acquired; private int _lost; private int _acquiredChange; private int _lostChange;
        internal Fixture(DiagnosticLogger logger, Exception? cleanup = null, Exception? terminal = null)
        {
            _expectedResource = cleanup; _expectedTerminal = terminal;
            _session = new PostgresTenureProtocolShould.Session { UnlockFailure = cleanup };
            Sut = PostgresTenureProtocolShould.Create(_session, new PostgresTenureProtocolShould.ScheduledClock(), logger: logger);
            Sut.BecameLeader += (_, _) => Interlocked.Increment(ref _acquired);
            Sut.LostLeadership += (_, _) => Interlocked.Increment(ref _lost);
            Sut.LeaderChanged += (_, change) =>
            {
                if (change.NewLeaderId == Sut.CandidateId) { Interlocked.Increment(ref _acquiredChange); }
                else if (change.PreviousLeaderId == Sut.CandidateId && change.NewLeaderId is null) { Interlocked.Increment(ref _lostChange); }
            };
        }
        internal void AssertAcquired() { _session.OpenCount.ShouldBe(1); _session.Commands.Count(command => command.Text.Contains("pg_try_advisory_lock", StringComparison.Ordinal)).ShouldBe(1); }
        internal void ArmCleanupFailure(Exception error) { _session.UnlockFailure.ShouldBeSameAs(error); }
        internal void AssertRetired()
        {
            Sut.IsLeader.ShouldBeFalse(); Sut.CurrentLeadership.ShouldBeNull();
            _session.Commands.Count(command => command.Text.Contains("pg_advisory_unlock", StringComparison.Ordinal)).ShouldBe(1); _session.DisposeCount.ShouldBe(1);
        }
        internal void AssertEvents()
        { _acquired.ShouldBe(1); _lost.ShouldBe(1); _acquiredChange.ShouldBe(1); _lostChange.ShouldBe(1); }
        public ValueTask DisposeAsync() => new(CloseAsync(_expectedResource, _expectedTerminal));
        private async Task CloseAsync(Exception? expectedResource, Exception? expectedTerminal)
        {
            var failures = new List<Exception>();
            try { await Sut.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(8)); }
            catch (Exception error) { if (!ReferenceEquals(error, expectedResource)) { failures.Add(error); } }
            try { await Sut.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(8)); }
            catch (Exception error) { if (!ReferenceEquals(error, expectedTerminal)) { failures.Add(error); } }
            if (failures.Count == 1) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw(); }
            if (failures.Count > 1) { throw new AggregateException(failures); }
        }
    }
}
