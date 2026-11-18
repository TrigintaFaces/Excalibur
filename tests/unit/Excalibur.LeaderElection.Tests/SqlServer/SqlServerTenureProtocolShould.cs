// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Transactions;
using Excalibur.Dispatch;
using Excalibur.Dispatch.LeaderElection.Fencing;
using Excalibur.LeaderElection.SqlServer;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Excalibur.LeaderElection.Tests.SqlServer;

[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
[Trait("Pattern", "Regression")]
public sealed class SqlServerTenureProtocolShould
{
    private static readonly int[] HealthyCommandTimeouts = [3, 5, 5];
    private static readonly long[] HealthyStarts = [TimeSpan.FromSeconds(5).Ticks, TimeSpan.FromSeconds(10).Ticks];
    private static readonly long[] RetryStarts = [TimeSpan.FromSeconds(5).Ticks, TimeSpan.FromSeconds(7).Ticks];
    [Fact]
    public async Task KeepFiveSecondRequestStartCadenceDespiteHealthyFourPointNineSecondReplies()
    {
        await using var fixture = new SqlServerTenureHarness();
        fixture.Acquire = (_, _) => { fixture.Clock.Advance(TimeSpan.FromMilliseconds(4900)); return Task.FromResult(0); };
        fixture.Verify = (_, _) => { fixture.Clock.Advance(TimeSpan.FromMilliseconds(4900)); return Task.FromResult(SqlServerSessionProof.Exclusive); };
        await fixture.StartAsync();
        await fixture.Clock.WaitForDelayAsync(TimeSpan.FromMilliseconds(100), 1);
        fixture.Clock.Advance(TimeSpan.FromMilliseconds(100));
        await fixture.Clock.WaitForDelayAsync(TimeSpan.FromMilliseconds(100), 2);
        fixture.Clock.Advance(TimeSpan.FromMilliseconds(100));
        await fixture.Clock.WaitForDelayAsync(TimeSpan.FromMilliseconds(100), 3);
        var session = fixture.Sessions.Single();
        session.VerificationStarts.ToArray().ShouldBe(HealthyStarts);
        session.CommandTimeouts.ToArray().ShouldBe(HealthyCommandTimeouts);
        fixture.Sut.IsLeader.ShouldBeTrue();
        session.Acquisitions.ShouldBe(1);
    }

    [Fact]
    public async Task RetryAnAmbiguousReplyAtSevenWithoutExtendingTheOldProof()
    {
        await using var fixture = new SqlServerTenureHarness();
        var attempts = 0;
        fixture.Verify = (_, _) =>
        {
            if (Interlocked.Increment(ref attempts) == 1)
            {
                fixture.Clock.Advance(TimeSpan.FromMilliseconds(100));
                return Task.FromException<SqlServerSessionProof>(new IOException("ambiguous reply"));
            }
            return Task.FromResult(SqlServerSessionProof.Exclusive);
        };
        await fixture.StartAsync();
        await fixture.Clock.WaitForDelayAsync(TimeSpan.FromSeconds(5), 1);
        fixture.Clock.Advance(TimeSpan.FromSeconds(5));
        await fixture.Clock.WaitForDelayAsync(TimeSpan.FromMilliseconds(1900), 1);
        fixture.Sut.IsLeader.ShouldBeTrue();
        fixture.Clock.Advance(TimeSpan.FromMilliseconds(1900));
        await fixture.Clock.WaitForDelayAsync(TimeSpan.FromSeconds(5), 2);
        fixture.Clock.Advance(TimeSpan.FromMilliseconds(3100));
        fixture.Sut.IsLeader.ShouldBeTrue();
        fixture.Sessions.Single().VerificationStarts.ToArray().ShouldBe(RetryStarts);
        fixture.Publications.ShouldBe(1);
    }

    [Fact]
    public async Task ClearTruthIndependentlyButJoinAHungVerificationBeforeSessionCleanup()
    {
        var reply = new TaskCompletionSource<SqlServerSessionProof>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var fixture = new SqlServerTenureHarness();
        fixture.Verify = (_, _) => reply.Task;
        var lost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Sut.LostLeadership += (_, _) => lost.TrySetResult();
        try
        {
            await fixture.StartAsync();
            await fixture.Clock.WaitForDelayAsync(TimeSpan.FromSeconds(5), 1);
            fixture.Clock.Advance(TimeSpan.FromSeconds(5));
            var session = fixture.Sessions.Single();
            await session.VerificationEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            fixture.Clock.Advance(TimeSpan.FromSeconds(5));
            await lost.Task.WaitAsync(TimeSpan.FromSeconds(5));
            fixture.Sut.CurrentLeadership.ShouldBeNull();
            session.Releases.ShouldBe(0); session.Disposals.ShouldBe(0);
            var stopping = fixture.Sut.StopAsync(CancellationToken.None);
            stopping.IsCompleted.ShouldBeFalse();
            reply.TrySetResult(SqlServerSessionProof.Exclusive);
            await stopping.WaitAsync(TimeSpan.FromSeconds(5));
            fixture.Sut.IsLeader.ShouldBeFalse();
            session.Releases.ShouldBe(1); session.Disposals.ShouldBe(1);
            fixture.Publications.ShouldBe(1);
        }
        finally { reply.TrySetResult(SqlServerSessionProof.Exclusive); }
    }

    [Fact]
    public async Task RetainAnUnfinishedAcquireAndRefuseItsLatePublicationAfterStop()
    {
        var reply = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var fixture = new SqlServerTenureHarness();
        fixture.Acquire = (_, _) => reply.Task;
        try
        {
            var starting = fixture.Sut.StartAsync(CancellationToken.None);
            await fixture.AcquireEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var session = fixture.Sessions.Single();
            var stopping = fixture.Sut.StopAsync(CancellationToken.None);
            stopping.IsCompleted.ShouldBeFalse(); session.Disposals.ShouldBe(0);
            fixture.Sut.IsLeader.ShouldBeFalse();
            reply.TrySetResult(0);
            await starting.WaitAsync(TimeSpan.FromSeconds(5));
            await stopping.WaitAsync(TimeSpan.FromSeconds(5));
            fixture.Publications.ShouldBe(0);
            session.Releases.ShouldBe(1); session.Disposals.ShouldBe(1);
        }
        finally { reply.TrySetResult(0); }
    }

    [Fact]
    public async Task RefuseSessionAllocationWhenStopWinsTheActualPostAdmissionLaunchBarrier()
    {
        using var clock = new AdmissionClock();
        await using var harness = new SqlServerTenureHarness();
        var allocations = 0;
        await using var sut = new SqlServerLeaderElection(() =>
        {
            Interlocked.Increment(ref allocations);
            return new FakeSqlElectionSession(harness, 1);
        }, "sql-tenure-test", Options.Create(new LeaderElectionOptions()),
            NullLogger<SqlServerLeaderElection>.Instance, timeProvider: clock);
        var starting = sut.StartAsync(CancellationToken.None);
        Task stopping = Task.CompletedTask;
        try
        {
            await clock.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            stopping = sut.StopAsync(CancellationToken.None);
            stopping.IsCompleted.ShouldBeFalse();
            allocations.ShouldBe(0);
            sut.IsLeader.ShouldBeFalse();
        }
        finally { clock.Release.Set(); }
        await starting.WaitAsync(TimeSpan.FromSeconds(5));
        await stopping.WaitAsync(TimeSpan.FromSeconds(5));
        allocations.ShouldBe(0);
    }

    [Fact]
    public async Task TreatUnrelatedCanceledTokensAsAmbiguousInsteadOfEndingTheActor()
    {
        await using var fixture = new SqlServerTenureHarness();
        fixture.Verify = (_, _) => Task.FromException<SqlServerSessionProof>(new OperationCanceledException(new CancellationToken(true)));
        var lost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Sut.LostLeadership += (_, _) => lost.TrySetResult();
        await fixture.StartAsync();
        await fixture.Clock.WaitForDelayAsync(TimeSpan.FromSeconds(5), 1);
        fixture.Clock.Advance(TimeSpan.FromSeconds(5));
        await fixture.Clock.WaitForDelayAsync(TimeSpan.FromSeconds(2), 1);
        fixture.Clock.Advance(TimeSpan.FromSeconds(2));
        await fixture.Clock.WaitForDelayAsync(TimeSpan.FromSeconds(2), 2);
        fixture.Sut.IsLeader.ShouldBeTrue();
        fixture.Sessions.First().VerificationStarts.Count.ShouldBe(2);
        fixture.Clock.Advance(TimeSpan.FromSeconds(3));
        await lost.Task.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Sut.IsLeader.ShouldBeFalse();
    }

    [Fact]
    public async Task RetireKnownLossImmediatelyAndDisposeTheOriginalSession()
    {
        await using var fixture = new SqlServerTenureHarness();
        fixture.Verify = (_, _) => Task.FromResult(SqlServerSessionProof.Lost);
        var lost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Sut.LostLeadership += (_, _) => lost.TrySetResult();
        await fixture.StartAsync();
        await fixture.Clock.WaitForDelayAsync(TimeSpan.FromSeconds(5), 1);
        fixture.Clock.Advance(TimeSpan.FromSeconds(5));
        await lost.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await fixture.Sut.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Sut.IsLeader.ShouldBeFalse();
        fixture.Sessions.First().Disposals.ShouldBe(1);
    }

    [Fact]
    public async Task AcceleratePermanentFailureDespiteABackwardsUtcStep()
    {
        var failure = new IOException("permanent verification failure");
        var classifier = A.Fake<IMessageFailureClassifier>();
        A.CallTo(() => classifier.Classify(failure)).Returns(MessageFailureKind.Permanent);
        await using var fixture = new SqlServerTenureHarness(classifier: classifier);
        fixture.Verify = (_, _) => Task.FromException<SqlServerSessionProof>(failure);
        var lost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Sut.LostLeadership += (_, _) => lost.TrySetResult();
        await fixture.StartAsync();
        var original = fixture.Sut.CurrentLeadership.ShouldNotBeNull();
        await fixture.Clock.WaitForDelayAsync(TimeSpan.FromSeconds(5), 1);
        fixture.Clock.UtcOffset = TimeSpan.FromHours(-12);
        fixture.Clock.Advance(TimeSpan.FromSeconds(5));
        await lost.Task.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Sut.IsLeader.ShouldBeFalse();
        fixture.Clock.GetUtcNow().ShouldBeLessThan(original.AcquiredAt);
        A.CallTo(() => classifier.Classify(failure)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task AttemptDisposalAfterReleaseFailureAndRefuseRestartWithTheRetainedFailure()
    {
        var released = new IOException("release failed");
        var disposed = new IOException("dispose failed");
        var fixture = new SqlServerTenureHarness();
        await fixture.StartAsync();
        var session = fixture.Sessions.Single();
        session.ReleaseFailure = released; session.DisposeFailure = disposed;
        var failure = await Should.ThrowAsync<AggregateException>(() => fixture.Sut.StopAsync(CancellationToken.None));
        failure.Flatten().InnerExceptions.ShouldContain(released);
        failure.Flatten().InnerExceptions.ShouldContain(disposed);
        session.Releases.ShouldBe(1); session.Disposals.ShouldBe(1);
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.Sut.StartAsync(CancellationToken.None));
        await Should.ThrowAsync<AggregateException>(() => fixture.Sut.DisposeAsync().AsTask());
        session.Disposals.ShouldBe(1);
        fixture.Sut.IsLeader.ShouldBeFalse();
    }

    [Fact]
    public async Task DeliverToRemainingSubscribersAndPermitNotificationReentrantStop()
    {
        await using var fixture = new SqlServerTenureHarness();
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Sut.BecameLeader += (_, _) => throw new InvalidOperationException("consumer failure");
        fixture.Sut.BecameLeader += (_, _) =>
        {
#pragma warning disable RS0030 // The synchronous callback is the deliberate Stop/self-join adversary.
            fixture.Sut.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
#pragma warning restore RS0030
            stopped.TrySetResult();
        };
        await fixture.StartAsync();
        await stopped.Task.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Sut.IsLeader.ShouldBeFalse();
        fixture.Sessions.First().Disposals.ShouldBe(1);
    }

    [Fact]
    public async Task MintBeforePublicationAndCarryTheExactFenceInTheNotification()
    {
        var fence = A.Fake<IFencingTokenIssuer>();
        A.CallTo(() => fence.IssueTokenAsync(A<string>._, A<CancellationToken>._)).Returns(new ValueTask<long>(42L));
        await using var fixture = new SqlServerTenureHarness(fence: fence);
        var notification = new TaskCompletionSource<LeaderChangedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Sut.LeaderChanged += (_, args) => { if (args.NewLeaderId is not null) { notification.TrySetResult(args); } };
        await fixture.StartAsync();
        var acquired = await notification.Task.WaitAsync(TimeSpan.FromSeconds(5));
        acquired.FencingToken.ShouldBe(42);
        fixture.Sut.CurrentLeadership.ShouldNotBeNull().FencingToken.ShouldBe(42);
        A.CallTo(() => fence.IssueTokenAsync("sql-tenure-test", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task NeverPublishAnUnfencedTenureWhenAConfiguredMintFails()
    {
        var failure = new IOException("mint unavailable");
        var fence = A.Fake<IFencingTokenIssuer>();
        A.CallTo(() => fence.IssueTokenAsync(A<string>._, A<CancellationToken>._))
            .Returns(new ValueTask<long>(Task.FromException<long>(failure)));
        await using var fixture = new SqlServerTenureHarness(fence: fence);
        var observed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lost = 0;
        fixture.Sut.AcquisitionFailed += (_, _) => observed.TrySetResult();
        fixture.Sut.LostLeadership += (_, _) => Interlocked.Increment(ref lost);
        await fixture.StartAsync();
        await observed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await fixture.Sut.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Sut.IsLeader.ShouldBeFalse(); fixture.Sut.CurrentLeadership.ShouldBeNull();
        fixture.Publications.ShouldBe(0); lost.ShouldBe(0);
        fixture.Sessions.First().Disposals.ShouldBe(1);
        A.CallTo(() => fence.IssueTokenAsync("sql-tenure-test", A<CancellationToken>._)).MustHaveHappened(3, Times.Exactly);
    }

    [Fact]
    public async Task RefuseAdmissionAfterDisposalWithoutAllocatingASession()
    {
        var fixture = new SqlServerTenureHarness();
        await fixture.Sut.DisposeAsync();
        await Should.ThrowAsync<ObjectDisposedException>(() => fixture.Sut.StartAsync(CancellationToken.None));
        await Should.ThrowAsync<ObjectDisposedException>(() => fixture.Sut.StopAsync(CancellationToken.None));
        await fixture.Sut.DisposeAsync();
        fixture.Sessions.ShouldBeEmpty();
        fixture.Sut.IsLeader.ShouldBeFalse();
    }

    [Theory]
    [InlineData("Exclusive", "Exclusive")]
    [InlineData("NoLock", "Lost")]
    [InlineData("Shared", "Lost")]
    [InlineData("Update", "Lost")]
    [InlineData("IntentShared", "Lost")]
    [InlineData("IntentExclusive", "Lost")]
    [InlineData("SharedIntentExclusive", "Lost")]
    [InlineData("UpdateIntentExclusive", "Lost")]
    [InlineData("unexpected", "Indeterminate")]
    public void ClassifyOnlyTheDocumentedExclusiveModeAsProof(string mode, string expected) =>
        SqlServerElectionSession.ClassifyMode(mode).ToString().ShouldBe(expected);

    [Fact]
    public async Task IsolateOwnedMintAndCleanupFromBusinessTransactionsWithoutDroppingDiagnostics()
    {
        var previous = SqlServerTenureHarness.DiagnosticMarker.Value;
        var marker = Guid.NewGuid().ToString("N");
        SqlServerTenureHarness.DiagnosticMarker.Value = marker;
        try
        {
            using var activity = new Activity("SQL election context control").Start();
            using var business = new TransactionScope(TransactionScopeOption.Required, TransactionScopeAsyncFlowOption.Enabled);
            var callerTransaction = Transaction.Current.ShouldNotBeNull();
            var observed = new TaskCompletionSource<(bool AmbientPresent, string? ActivityId, string? Marker)>(TaskCreationOptions.RunContinuationsAsynchronously);
            var fence = A.Fake<IFencingTokenIssuer>();
            A.CallTo(() => fence.IssueTokenAsync(A<string>._, A<CancellationToken>._)).ReturnsLazily(() =>
            {
                observed.TrySetResult((Transaction.Current is not null, Activity.Current?.Id, SqlServerTenureHarness.DiagnosticMarker.Value));
                return new ValueTask<long>(42L);
            });
            await using var fixture = new SqlServerTenureHarness(fence: fence);
            await fixture.StartAsync();
            var minted = await observed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            minted.AmbientPresent.ShouldBeFalse(); minted.ActivityId.ShouldBe(activity.Id); minted.Marker.ShouldBe(marker);
            Transaction.Current.ShouldBeSameAs(callerTransaction);
            await fixture.Sut.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            var session = fixture.Sessions.First();
            session.Releases.ShouldBe(1); session.Disposals.ShouldBe(1);
            session.ReleaseHadAmbientTransaction.ShouldBeFalse(); session.DisposalHadAmbientTransaction.ShouldBeFalse();
            session.ReleaseDiagnosticMarker.ShouldBe(marker);
            Transaction.Current.ShouldBeSameAs(callerTransaction);
        }
        finally { SqlServerTenureHarness.DiagnosticMarker.Value = previous; }
    }


    [Theory]
    [InlineData(null)]
    [InlineData(MessageFailureKind.Transient)]
    [InlineData(MessageFailureKind.Poison)]
    [InlineData(MessageFailureKind.Uncertain)]
    public async Task KeepAmbiguousFailuresUntilTheOriginalDeadlineWithoutRefreshingIt(MessageFailureKind? kind)
    {
        var failure = new IOException("same indeterminate SQL proof fault");
        var classifier = kind.HasValue ? A.Fake<IMessageFailureClassifier>() : null;
        if (classifier is not null) { A.CallTo(() => classifier.Classify(failure)).Returns(kind!.Value); }
        var reply = new TaskCompletionSource<SqlServerSessionProof>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = 0;
        await using var fixture = new SqlServerTenureHarness(classifier: classifier);
        fixture.Verify = (_, _) =>
        {
            if (Interlocked.Increment(ref attempts) < 3) { return Task.FromException<SqlServerSessionProof>(failure); }
            entered.TrySetResult();
            return reply.Task;
        };
        var lost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Sut.LostLeadership += (_, _) => lost.TrySetResult();
        try
        {
            await fixture.StartAsync();
            var session = fixture.Sessions.Single();
            await fixture.Clock.WaitForDelayAsync(TimeSpan.FromSeconds(5), 1);
            fixture.Clock.Advance(TimeSpan.FromSeconds(5));
            await fixture.Clock.WaitForDelayAsync(TimeSpan.FromSeconds(2), 1);
            attempts.ShouldBe(1);
            fixture.Sut.IsLeader.ShouldBeTrue();
            fixture.Clock.Advance(TimeSpan.FromSeconds(2));
            await fixture.Clock.WaitForDelayAsync(TimeSpan.FromSeconds(2), 2);
            attempts.ShouldBe(2);
            fixture.Sut.IsLeader.ShouldBeTrue();
            fixture.Clock.Advance(TimeSpan.FromSeconds(2));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            fixture.Clock.Advance(TimeSpan.FromMilliseconds(999));
            fixture.Sut.CurrentLeadership.ShouldNotBeNull();
            lost.Task.IsCompleted.ShouldBeFalse();
            fixture.Clock.Advance(TimeSpan.FromMilliseconds(1));
            await lost.Task.WaitAsync(TimeSpan.FromSeconds(5)); // third SDK call remains pending: loss is independent.
            fixture.Sut.CurrentLeadership.ShouldBeNull();
            session.Releases.ShouldBe(0);
            session.Disposals.ShouldBe(0);
            fixture.Sessions.Count.ShouldBe(1);
            attempts.ShouldBe(3);
            if (classifier is not null) { A.CallTo(() => classifier.Classify(failure)).MustHaveHappenedTwiceExactly(); }
            var stop = fixture.Sut.StopAsync(CancellationToken.None);
            stop.IsCompleted.ShouldBeFalse();
            reply.TrySetResult(SqlServerSessionProof.Exclusive);
            await stop.WaitAsync(TimeSpan.FromSeconds(5));
            fixture.Sut.CurrentLeadership.ShouldBeNull();
            session.Releases.ShouldBe(1);
            session.Disposals.ShouldBe(1);
            fixture.Publications.ShouldBe(1);
        }
        finally { reply.TrySetResult(SqlServerSessionProof.Exclusive); }
    }

    [Theory]
    [InlineData(null, -24)]
    [InlineData(null, 24)]
    [InlineData(MessageFailureKind.Transient, -24)]
    [InlineData(MessageFailureKind.Transient, 24)]
    [InlineData(MessageFailureKind.Poison, -24)]
    [InlineData(MessageFailureKind.Poison, 24)]
    [InlineData(MessageFailureKind.Uncertain, -24)]
    [InlineData(MessageFailureKind.Uncertain, 24)]
    public async Task RetireForDefinitiveSessionLossRegardlessOfClassifierOrUtcSkew(MessageFailureKind? kind, int utcShiftHours)
    {
        var classifier = kind.HasValue ? A.Fake<IMessageFailureClassifier>() : null;
        if (classifier is not null) { A.CallTo(() => classifier.Classify(A<Exception>._)).Returns(kind!.Value); }
        await using var fixture = new SqlServerTenureHarness(classifier: classifier);
        fixture.Verify = (_, _) => Task.FromResult(SqlServerSessionProof.Lost);
        var lost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task stop = Task.CompletedTask;
        fixture.Sut.LostLeadership += (_, _) =>
        {
            stop = fixture.Sut.StopAsync(CancellationToken.None);
            lost.TrySetResult();
        };
        await fixture.StartAsync();
        await fixture.Clock.WaitForDelayAsync(TimeSpan.FromSeconds(5), 1);
        var monotonicBeforeShift = fixture.Clock.GetTimestamp();
        fixture.Clock.UtcOffset = TimeSpan.FromHours(utcShiftHours);
        fixture.Clock.GetTimestamp().ShouldBe(monotonicBeforeShift);
        fixture.Clock.Advance(TimeSpan.FromSeconds(5));
        await lost.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await stop.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Sut.CurrentLeadership.ShouldBeNull();
        var session = fixture.Sessions.Single();
        session.VerificationStarts.Count.ShouldBe(1);
        session.Releases.ShouldBe(1);
        session.Disposals.ShouldBe(1);
        if (classifier is not null) { A.CallTo(() => classifier.Classify(A<Exception>._)).MustNotHaveHappened(); }
    }

    [Theory]
    [InlineData(-24)]
    [InlineData(24)]
    public async Task RetireForPermanentFaultDespiteAnIndependentUtcStep(int utcShiftHours)
    {
        var failure = new IOException("permanent SQL proof failure");
        var classifier = A.Fake<IMessageFailureClassifier>();
        A.CallTo(() => classifier.Classify(failure)).Returns(MessageFailureKind.Permanent);
        await using var fixture = new SqlServerTenureHarness(classifier: classifier);
        fixture.Verify = (_, _) => Task.FromException<SqlServerSessionProof>(failure);
        var lost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task stop = Task.CompletedTask;
        fixture.Sut.LostLeadership += (_, _) =>
        {
            stop = fixture.Sut.StopAsync(CancellationToken.None);
            lost.TrySetResult();
        };
        await fixture.StartAsync();
        await fixture.Clock.WaitForDelayAsync(TimeSpan.FromSeconds(5), 1);
        var monotonicBeforeShift = fixture.Clock.GetTimestamp();
        fixture.Clock.UtcOffset = TimeSpan.FromHours(utcShiftHours);
        fixture.Clock.GetTimestamp().ShouldBe(monotonicBeforeShift);
        fixture.Clock.Advance(TimeSpan.FromSeconds(5));
        await lost.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await stop.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Sut.IsLeader.ShouldBeFalse();
        var session = fixture.Sessions.Single();
        session.Releases.ShouldBe(1);
        session.Disposals.ShouldBe(1);
        A.CallTo(() => classifier.Classify(failure)).MustHaveHappenedOnceExactly();
    }

    private sealed class AdmissionClock : TimeProvider, IDisposable
    {
        private int _reads;
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal ManualResetEventSlim Release { get; } = new(false);
        public override long GetTimestamp()
        {
            if (Interlocked.Increment(ref _reads) == 1)
            {
                Entered.TrySetResult();
#pragma warning disable RS0030 // This synchronous clock read freezes the real post-admission/pre-SDK launch site.
                Release.Wait();
#pragma warning restore RS0030
            }
            return base.GetTimestamp();
        }
        public void Dispose() => Release.Dispose();
    }
}

internal sealed class SqlServerTenureHarness : IAsyncDisposable
{
    internal static readonly AsyncLocal<string?> DiagnosticMarker = new();
    internal SqlServerTenureClock Clock { get; } = new();
    internal ConcurrentQueue<FakeSqlElectionSession> Sessions { get; } = new();
    internal TaskCompletionSource AcquireEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal Func<FakeSqlElectionSession, CancellationToken, Task<int>> Acquire { get; set; }
    internal Func<FakeSqlElectionSession, CancellationToken, Task<SqlServerSessionProof>> Verify { get; set; } = static (_, _) => Task.FromResult(SqlServerSessionProof.Exclusive);
    internal SqlServerLeaderElection Sut { get; }
    internal int Publications;
    private readonly TaskCompletionSource _published = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal SqlServerTenureHarness(IMessageFailureClassifier? classifier = null, IFencingTokenIssuer? fence = null,
        Microsoft.Extensions.Logging.ILogger<SqlServerLeaderElection>? logger = null)
    {
        Acquire = (session, _) => Task.FromResult(session.Ordinal == 1 ? 0 : -1);
        var created = 0;
        Sut = new SqlServerLeaderElection(() =>
        {
            var session = new FakeSqlElectionSession(this, Interlocked.Increment(ref created));
            Sessions.Enqueue(session); return session;
        }, "sql-tenure-test", Options.Create(new LeaderElectionOptions { InstanceId = "same-public-id" }),
            logger ?? NullLogger<SqlServerLeaderElection>.Instance, classifier, fence, Clock);
        Sut.BecameLeader += (_, _) => { Interlocked.Increment(ref Publications); _published.TrySetResult(); };
    }

    internal async Task StartAsync()
    {
        await Sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        if (Sut.IsLeader) { await _published.Task.WaitAsync(TimeSpan.FromSeconds(5)); }
    }
    public ValueTask DisposeAsync() => Sut.DisposeAsync();
}

internal sealed class FakeSqlElectionSession(SqlServerTenureHarness owner, int ordinal) : ISqlServerElectionSession
{
    internal int Ordinal { get; } = ordinal;
    internal int Acquisitions;
    internal int Releases;
    internal int Disposals;
    internal Exception? ReleaseFailure;
    internal Exception? DisposeFailure;
    internal bool ReleaseHadAmbientTransaction;
    internal bool DisposalHadAmbientTransaction;
    internal string? ReleaseDiagnosticMarker;
    internal TaskCompletionSource VerificationEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal ConcurrentQueue<long> VerificationStarts { get; } = new();
    internal ConcurrentQueue<int> CommandTimeouts { get; } = new();
    public Task<int> AcquireAsync(string resource, int lockTimeoutMilliseconds, int commandTimeoutSeconds, CancellationToken cancellationToken)
    {
        resource.ShouldBe("sql-tenure-test"); lockTimeoutMilliseconds.ShouldBeGreaterThan(0);
        commandTimeoutSeconds.ShouldBeGreaterThan(0); CommandTimeouts.Enqueue(commandTimeoutSeconds); Interlocked.Increment(ref Acquisitions);
        owner.AcquireEntered.TrySetResult(); return owner.Acquire(this, cancellationToken);
    }
    public Task<SqlServerSessionProof> VerifyAsync(string resource, int commandTimeoutSeconds, CancellationToken cancellationToken)
    {
        resource.ShouldBe("sql-tenure-test"); commandTimeoutSeconds.ShouldBeGreaterThan(0); CommandTimeouts.Enqueue(commandTimeoutSeconds);
        VerificationStarts.Enqueue(owner.Clock.GetTimestamp()); VerificationEntered.TrySetResult();
        return owner.Verify(this, cancellationToken);
    }
    public Task ReleaseAsync(string resource, int commandTimeoutSeconds, CancellationToken cancellationToken)
    {
        ReleaseHadAmbientTransaction = Transaction.Current is not null;
        ReleaseDiagnosticMarker = SqlServerTenureHarness.DiagnosticMarker.Value;
        resource.ShouldBe("sql-tenure-test"); commandTimeoutSeconds.ShouldBeGreaterThan(0); CommandTimeouts.Enqueue(commandTimeoutSeconds);
        Interlocked.Increment(ref Releases);
        return ReleaseFailure is { } error ? Task.FromException(error) : Task.CompletedTask;
    }
    public ValueTask DisposeAsync()
    {
        DisposalHadAmbientTransaction = Transaction.Current is not null;
        Interlocked.Increment(ref Disposals);
        return DisposeFailure is { } error ? ValueTask.FromException(error) : ValueTask.CompletedTask;
    }
}

internal sealed class SqlServerTenureClock : FakeTimeProvider
{
    private readonly long _originUtcTicks;
    internal SqlServerTenureClock() => _originUtcTicks = base.GetUtcNow().UtcTicks;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    // Wall-clock skew must not change the independent monotonic test timeline.
    public override long GetTimestamp() => base.GetUtcNow().UtcTicks - _originUtcTicks;
    private readonly Lock _gate = new();
    private readonly Dictionary<long, int> _counts = [];
    private readonly List<(long Due, int Occurrence, TaskCompletionSource Signal)> _waiters = [];
    private long _utcOffsetTicks;
    internal TimeSpan UtcOffset
    {
        get => TimeSpan.FromTicks(Interlocked.Read(ref _utcOffsetTicks));
        set => Interlocked.Exchange(ref _utcOffsetTicks, value.Ticks);
    }
    public override DateTimeOffset GetUtcNow() => base.GetUtcNow() + UtcOffset;
    internal Task WaitForDelayAsync(TimeSpan due, int occurrence)
    {
        lock (_gate)
        {
            if (_counts.GetValueOrDefault(due.Ticks) >= occurrence) { return Task.CompletedTask; }
            var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _waiters.Add((due.Ticks, occurrence, signal));
            return signal.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = base.CreateTimer(callback, state, dueTime, period);
        lock (_gate)
        {
            var count = _counts.GetValueOrDefault(dueTime.Ticks) + 1;
            _counts[dueTime.Ticks] = count;
            foreach (var item in _waiters.Where(item => item.Due == dueTime.Ticks && item.Occurrence <= count)) { item.Signal.TrySetResult(); }
        }
        return timer;
    }
}
