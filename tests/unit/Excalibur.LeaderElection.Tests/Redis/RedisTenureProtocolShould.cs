// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Transactions;
using Excalibur.Dispatch.LeaderElection;
using Excalibur.Dispatch.LeaderElection.Fencing;
using Excalibur.Dispatch;
using Excalibur.LeaderElection.Redis;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using StackExchange.Redis;
using Xunit;

namespace Excalibur.LeaderElection.Tests.Redis;

[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
[Trait("Pattern", "Regression")]
public sealed class RedisTenureProtocolShould
{
    [Fact]
    public async Task IsolateInfrastructureFencingFromBusinessTransactionsAndPreserveDiagnostics()
    {
        using var transaction = new TransactionScope(TransactionScopeOption.RequiresNew, TransactionScopeAsyncFlowOption.Enabled);
        var callerTransaction = Transaction.Current.ShouldNotBeNull();
        using var activity = new Activity("leader-ambient-control").Start();
        var diagnostic = new AsyncLocal<string?> { Value = "caller-diagnostic" };
        var observed = new TaskCompletionSource<(Transaction? Transaction, string? Diagnostic, ActivityTraceId Trace)>(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = A.Fake<IFencingTokenIssuer>();
        A.CallTo(() => provider.IssueTokenAsync(A<string>._, A<CancellationToken>._))
            .ReturnsLazily(() =>
            {
                observed.TrySetResult((Transaction.Current, diagnostic.Value, Activity.Current?.TraceId ?? default));
                return ValueTask.FromResult(41L);
            });
        await using var fixture = new Harness(fencing: provider);
        await fixture.Sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        var callback = await observed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        callback.Transaction.ShouldBeNull();
        callback.Diagnostic.ShouldBe("caller-diagnostic");
        callback.Trace.ShouldBe(activity.TraceId);
        Transaction.Current.ShouldBeSameAs(callerTransaction);
        fixture.Sut.IsLeader.ShouldBeTrue();
    }

    [Fact]
    public async Task CarryTheExactAcquiredFenceOnEachLeaderChangedTransition()
    {
        var provider = A.Fake<IFencingTokenIssuer>();
        long issued = 40;
        A.CallTo(() => provider.IssueTokenAsync(A<string>._, A<CancellationToken>._))
            .ReturnsLazily(() => ValueTask.FromResult(Interlocked.Increment(ref issued)));
        await using var fixture = new Harness(fencing: provider);
        var first = new TaskCompletionSource<LeaderChangedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource<LeaderChangedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        var acquisitions = 0;
        var losses = new ConcurrentQueue<LeaderChangedEventArgs>();
        fixture.Sut.LeaderChanged += (_, change) =>
        {
            if (change.NewLeaderId == fixture.Sut.CandidateId)
            {
                (Interlocked.Increment(ref acquisitions) == 1 ? first : second).TrySetResult(change);
            }
            else if (change.NewLeaderId is null) { losses.Enqueue(change); }
        };
        await fixture.Sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        var firstChange = await first.Task.WaitAsync(TimeSpan.FromSeconds(5));
        firstChange.FencingToken.ShouldBe(41);
        await fixture.Sut.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        await fixture.Sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        var secondChange = await second.Task.WaitAsync(TimeSpan.FromSeconds(5));
        secondChange.FencingToken.ShouldBe(42);
        firstChange.FencingToken.ShouldBe(41);
        fixture.Sut.CurrentLeadership.ShouldNotBeNull().FencingToken.ShouldBe(42);
        losses.Single().FencingToken.ShouldBeNull();
    }

    [Fact]
    public async Task UseDistinctOwnerProofsAcrossTwoTenuresWithTheSamePublicCandidateId()
    {
        await using var fixture = new Harness();
        await fixture.Sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Sut.IsLeader.ShouldBeTrue();
        await fixture.Sut.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        await fixture.Sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Sut.IsLeader.ShouldBeTrue();
        await fixture.Sut.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        var owners = fixture.Owners.ToArray();
        owners.Length.ShouldBe(2);
        owners[0].ShouldNotBe(owners[1]);
        owners[0].ShouldEndWith(":" + fixture.Sut.CandidateId);
        owners[1].ShouldEndWith(":" + fixture.Sut.CandidateId);
        fixture.Releases.ToArray().ShouldBe(owners);
        A.CallTo(() => fixture.Redis.Dispose()).MustNotHaveHappened();
    }

    [Fact]
    public async Task PreserveRemoteDisplayIdentityAndAcquisitionFailedNotification()
    {
        await using var fixture = new Harness(acquire: false);
        var failed = new TaskCompletionSource<LeaderElectionAcquisitionFailedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Sut.AcquisitionFailed += (_, args) => failed.TrySetResult(args);
        await fixture.Sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Sut.IsLeader.ShouldBeFalse();
        fixture.Sut.CurrentLeaderId.ShouldBe("other-candidate");
        await failed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Sut.CurrentLeadership.ShouldBeNull();
    }

    [Fact]
    public async Task RetainAnUnfinishedSetAndRefuseItsLatePublicationAfterStop()
    {
        var reply = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var fixture = new Harness(heldAcquire: reply.Task);
        var publications = 0;
        fixture.Sut.BecameLeader += (_, _) => Interlocked.Increment(ref publications);
        var start = fixture.Sut.StartAsync(CancellationToken.None);
        await fixture.SetEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var stop = fixture.Sut.StopAsync(CancellationToken.None);
        try
        {
            fixture.Sut.IsLeader.ShouldBeFalse();
            stop.IsCompleted.ShouldBeFalse();
            using var canceled = new CancellationTokenSource(); canceled.Cancel();
            await Should.ThrowAsync<OperationCanceledException>(() => fixture.Sut.StopAsync(canceled.Token));
            fixture.Owners.Count.ShouldBe(1);
        }
        finally { reply.TrySetResult(true); }
        await start.WaitAsync(TimeSpan.FromSeconds(5));
        await stop.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Sut.IsLeader.ShouldBeFalse();
        publications.ShouldBe(0);
        fixture.Releases.ToArray().ShouldBe(fixture.Owners.ToArray());
    }

    [Fact]
    public async Task SealTheActualAdmissionLaunchGapBeforeAnyRedisCallCanStart()
    {
        using var clock = new AdmissionBarrierClock();
        await using var fixture = new Harness(clock: clock);
        var start = fixture.Sut.StartAsync(CancellationToken.None);
        await clock.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var stop = fixture.Sut.StopAsync(CancellationToken.None);
        try
        {
            stop.IsCompleted.ShouldBeFalse(); // supervising actor still owns its admitted launch site.
            fixture.Owners.ShouldBeEmpty();
        }
        finally { clock.Release.Set(); }
        await start.WaitAsync(TimeSpan.FromSeconds(5));
        await stop.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Owners.ShouldBeEmpty();
        fixture.Sut.IsLeader.ShouldBeFalse();
    }

    [Fact]
    public async Task AvoidAllocatingNewKernelsDuringRepeatedCanceledPendingStarts()
    {
        var clock = new CountingClock();
        await using var fixture = new Harness(clock: clock);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Sut.BecameLeader += (_, _) =>
        {
            entered.TrySetResult();
#pragma warning disable RS0030 // Required adversarial synchronous EventHandler barrier; tests callback/Stop separation.
            release.Task.GetAwaiter().GetResult();
#pragma warning restore RS0030
        };
        try
        {
            await fixture.Sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await fixture.Sut.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            clock.KernelTimerCount.ShouldBe(1);
            for (var i = 0; i < 5; i++)
            {
                using var canceled = new CancellationTokenSource();
                var pending = fixture.Sut.StartAsync(canceled.Token);
                canceled.Cancel();
                await Should.ThrowAsync<OperationCanceledException>(() => pending);
                await fixture.Sut.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
                clock.KernelTimerCount.ShouldBe(1);
                fixture.Owners.Count.ShouldBe(1);
            }
        }
        finally { release.TrySetResult(); }
        await fixture.Sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        clock.KernelTimerCount.ShouldBe(2);
        fixture.Owners.Count.ShouldBe(2);
        fixture.Sut.IsLeader.ShouldBeTrue();
    }

    [Fact]
    public async Task AllowARealPublicationSubscriberToStopWithoutJoiningItself()
    {
        await using var fixture = new Harness();
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Sut.BecameLeader += (_, _) =>
        {
#pragma warning disable RS0030 // A synchronous consumer callback is the self-join counterexample under test.
            fixture.Sut.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
#pragma warning restore RS0030
            stopped.TrySetResult();
        };
        await fixture.Sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        await stopped.Task.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Sut.IsLeader.ShouldBeFalse();
        fixture.Releases.Count.ShouldBe(1);
    }

    [Fact]
    public async Task RetryOneIndeterminateProbeInsideGraceAndKeepTheSameTenure()
    {
        var clock = new ScheduledClock();
        var first = new TaskCompletionSource<RedisResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var count = 0;
        await using var fixture = new Harness(clock: clock, renewal: () =>
        {
            if (Interlocked.Increment(ref count) == 1) { entered.TrySetResult(); return first.Task; }
            return Task.FromResult(RedisResult.Create((RedisValue)1));
        });
        try
        {
            await fixture.Sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            await clock.WaitForDelayAsync(TimeSpan.FromSeconds(5), 1);
            clock.Advance(TimeSpan.FromSeconds(5));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            clock.Advance(TimeSpan.FromMilliseconds(100));
            first.TrySetException(new IOException("indeterminate probe"));
            await clock.WaitForDelayAsync(TimeSpan.FromMilliseconds(1900), 1);
            fixture.Sut.IsLeader.ShouldBeTrue();
            clock.Advance(TimeSpan.FromMilliseconds(1900));
            await clock.WaitForDelayAsync(TimeSpan.FromSeconds(5), 2);
            clock.Advance(TimeSpan.FromMilliseconds(3100));
            fixture.Sut.IsLeader.ShouldBeTrue();
            fixture.Owners.Count.ShouldBe(1);
            count.ShouldBe(2);
        }
        finally { first.TrySetResult(RedisResult.Create((RedisValue)1)); }
    }

    [Fact]
    public async Task KeepOneTenureAcrossHealthySlowRenewalsUsingRequestStartCadence()
    {
        var clock = new ScheduledClock();
        var entered = Enumerable.Range(0, 3).Select(_ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).ToArray();
        var replies = Enumerable.Range(0, 3).Select(_ => new TaskCompletionSource<RedisResult>(TaskCreationOptions.RunContinuationsAsynchronously)).ToArray();
        var count = 0;
        await using var fixture = new Harness(clock: clock, renewal: () =>
        {
            var index = Interlocked.Increment(ref count) - 1;
            entered[index].TrySetResult();
            return replies[index].Task;
        });
        var losses = 0;
        fixture.Sut.LostLeadership += (_, _) => Interlocked.Increment(ref losses);
        try
        {
            await fixture.Sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            await clock.WaitForTotalDelayCountAsync(1);
            clock.Advance(TimeSpan.FromSeconds(5));
            for (var index = 0; index < replies.Length; index++)
            {
                await entered[index].Task.WaitAsync(TimeSpan.FromSeconds(5));
                clock.Advance(TimeSpan.FromMilliseconds(4900));
                replies[index].TrySetResult(RedisResult.Create((RedisValue)1));
                await clock.WaitForTotalDelayCountAsync(index + 2);
                fixture.Sut.IsLeader.ShouldBeTrue();
                fixture.Owners.Count.ShouldBe(1);
                Volatile.Read(ref losses).ShouldBe(0);
                if (index + 1 < replies.Length) { clock.Advance(TimeSpan.FromMilliseconds(100)); }
            }
            count.ShouldBe(3);
        }
        finally
        {
            foreach (var reply in replies) { reply.TrySetResult(RedisResult.Create((RedisValue)1)); }
        }
    }

    [Fact]
    public async Task ExpireWhileRenewalRemainsHungAndRejectItsLateSuccess()
    {
        var clock = new ScheduledClock();
        var reply = new TaskCompletionSource<RedisResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var fixture = new Harness(clock: clock, renewal: () => { entered.TrySetResult(); return reply.Task; });
        fixture.Sut.LostLeadership += (_, _) => lost.TrySetResult();
        try
        {
            await fixture.Sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            await clock.WaitForDelayAsync(TimeSpan.FromSeconds(5), 1);
            clock.Advance(TimeSpan.FromSeconds(5));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            clock.Advance(TimeSpan.FromSeconds(5));
            await lost.Task.WaitAsync(TimeSpan.FromSeconds(5)); // timer lane, not a getter side effect.
            fixture.Sut.IsLeader.ShouldBeFalse();
            fixture.Releases.ShouldBeEmpty(); // actual Redis renewal is still owned, cleanup cannot race it.
            var stop = fixture.Sut.StopAsync(CancellationToken.None);
            stop.IsCompleted.ShouldBeFalse();
            reply.TrySetResult(RedisResult.Create((RedisValue)1));
            await stop.WaitAsync(TimeSpan.FromSeconds(5));
            fixture.Sut.IsLeader.ShouldBeFalse();
            fixture.Owners.Count.ShouldBe(1);
        }
        finally { reply.TrySetResult(RedisResult.Create((RedisValue)1)); }
    }

    [Fact]
    public async Task RetireImmediatelyForAPermanentFailureBeforeTheTotalDeadline()
    {
        var clock = new ScheduledClock();
        var error = new IOException("permanent renewal failure");
        var classifier = A.Fake<IMessageFailureClassifier>();
        A.CallTo(() => classifier.Classify(error)).Returns(MessageFailureKind.Permanent);
        await using var fixture = new Harness(clock: clock,
            renewal: () => Task.FromException<RedisResult>(error), classifier: classifier);
        var lost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Sut.LostLeadership += (_, _) => lost.TrySetResult();
        await fixture.Sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        await clock.WaitForDelayAsync(TimeSpan.FromSeconds(5), 1);
        clock.Advance(TimeSpan.FromSeconds(5));
        await lost.Task.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Sut.IsLeader.ShouldBeFalse();
        A.CallTo(() => classifier.Classify(error)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ReportCleanupFailureAndRefuseRestartInsteadOfClaimingQuiescence()
    {
        var error = new IOException("conditional release failed");
        var fixture = new Harness(releaseFailure: error);
        await fixture.Sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        var failure = await Should.ThrowAsync<IOException>(() => fixture.Sut.StopAsync(CancellationToken.None));
        failure.ShouldBeSameAs(error);
        fixture.Sut.IsLeader.ShouldBeFalse();
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.Sut.StartAsync(CancellationToken.None));
        var dispose = await Should.ThrowAsync<IOException>(() => fixture.DisposeAsync().AsTask());
        dispose.ShouldBeSameAs(error);
        fixture.Releases.Count.ShouldBe(1);
    }

    [Fact]
    public async Task SealARegisteredRenewalBeforeItsActualSdkLaunchWhenStopWins()
    {
        using var clock = new RenewalBarrierClock();
        await using var fixture = new Harness(clock: clock);
        var lost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Sut.LostLeadership += (_, _) => lost.TrySetResult();
        await fixture.Sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        await clock.DelayReady.Task.WaitAsync(TimeSpan.FromSeconds(5));
        clock.ArmPostRegistrationBarrier();
        Task firing = Task.CompletedTask;
        Task stop = Task.CompletedTask;
        try
        {
            // Task.Delay's timer callback may inline the actor continuation. Fire on an owned
            // worker so that freezing that continuation cannot also freeze this orchestrator.
            firing = Task.Run(clock.FireRenewalDelay);
            await clock.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            stop = fixture.Sut.StopAsync(CancellationToken.None);
            await lost.Task.WaitAsync(TimeSpan.FromSeconds(5)); // proves this barrier is outside the core gate.
            fixture.Renewals.ShouldBeEmpty();
            stop.IsCompleted.ShouldBeFalse(); // actor launch site is still owned.
        }
        finally
        {
            clock.Release.Set();
            await firing.WaitAsync(TimeSpan.FromSeconds(5));
        }
        await stop.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Renewals.ShouldBeEmpty();
        fixture.Sut.IsLeader.ShouldBeFalse();
        fixture.Releases.Count.ShouldBe(1);
    }


    // Replaces private ShouldRelinquish reflection with the actual Redis actor, SDK faults,
    // original request-start deadline and independently delivered timer loss notification.
    [Theory]
    [InlineData(null)]
    [InlineData(MessageFailureKind.Transient)]
    [InlineData(MessageFailureKind.Poison)]
    [InlineData(MessageFailureKind.Uncertain)]
    public async Task KeepAmbiguousFailuresWithinTheOriginalBudgetAndExpireWithoutExtendingIt(MessageFailureKind? kind)
    {
        var clock = new ScheduledClock();
        var error = new IOException("same indeterminate renewal fault");
        var classifier = kind.HasValue ? A.Fake<IMessageFailureClassifier>() : null;
        if (classifier is not null) { A.CallTo(() => classifier.Classify(error)).Returns(kind!.Value); }
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reply = new TaskCompletionSource<RedisResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var count = 0;
        await using var fixture = new Harness(clock: clock, classifier: classifier, renewal: () =>
        {
            if (Interlocked.Increment(ref count) < 3) { return Task.FromException<RedisResult>(error); }
            entered.TrySetResult();
            return reply.Task;
        });
        var lost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Sut.LostLeadership += (_, _) => lost.TrySetResult();
        try
        {
            await fixture.Sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            await clock.WaitForTotalDelayCountAsync(1);
            clock.Advance(TimeSpan.FromSeconds(5));
            await clock.WaitForTotalDelayCountAsync(2);
            count.ShouldBe(1);
            fixture.Sut.IsLeader.ShouldBeTrue();
            clock.Advance(TimeSpan.FromSeconds(2));
            await clock.WaitForTotalDelayCountAsync(3);
            count.ShouldBe(2);
            fixture.Sut.IsLeader.ShouldBeTrue();
            clock.Advance(TimeSpan.FromSeconds(2));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            clock.Advance(TimeSpan.FromMilliseconds(999));
            fixture.Sut.CurrentLeadership.ShouldNotBeNull();
            lost.Task.IsCompleted.ShouldBeFalse();
            clock.Advance(TimeSpan.FromMilliseconds(1));
            await lost.Task.WaitAsync(TimeSpan.FromSeconds(5)); // independent timer; third SDK reply is still pending.
            fixture.Sut.IsLeader.ShouldBeFalse();
            fixture.Releases.ShouldBeEmpty();
            fixture.Owners.Count.ShouldBe(1);
            count.ShouldBe(3);
            if (classifier is not null) { A.CallTo(() => classifier.Classify(error)).MustHaveHappenedTwiceExactly(); }
            var stop = fixture.Sut.StopAsync(CancellationToken.None);
            stop.IsCompleted.ShouldBeFalse();
            reply.TrySetResult(RedisResult.Create((RedisValue)1));
            await stop.WaitAsync(TimeSpan.FromSeconds(5));
            fixture.Sut.CurrentLeadership.ShouldBeNull();
        }
        finally { reply.TrySetResult(RedisResult.Create((RedisValue)1)); }
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
    public async Task RetireForDefinitiveLuaZeroRegardlessOfClassifierOrWallClock(MessageFailureKind? kind, int utcShiftHours)
    {
        var clock = new ScheduledClock();
        var classifier = kind.HasValue ? A.Fake<IMessageFailureClassifier>() : null;
        if (classifier is not null) { A.CallTo(() => classifier.Classify(A<Exception>._)).Returns(kind!.Value); }
        await using var fixture = new Harness(clock: clock, classifier: classifier,
            renewal: () => Task.FromResult(RedisResult.Create((RedisValue)0)));
        var lost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task stop = Task.CompletedTask;
        fixture.Sut.LostLeadership += (_, _) =>
        {
            stop = fixture.Sut.StopAsync(CancellationToken.None);
            lost.TrySetResult();
        };
        await fixture.Sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        await clock.WaitForTotalDelayCountAsync(1);
        var monotonicBeforeShift = clock.GetTimestamp();
        clock.UtcOffset = TimeSpan.FromHours(utcShiftHours);
        clock.GetTimestamp().ShouldBe(monotonicBeforeShift);
        clock.Advance(TimeSpan.FromSeconds(5));
        await lost.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await stop.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Sut.CurrentLeadership.ShouldBeNull();
        fixture.Renewals.Count.ShouldBe(1);
        if (classifier is not null) { A.CallTo(() => classifier.Classify(A<Exception>._)).MustNotHaveHappened(); }
    }

    [Theory]
    [InlineData(-24)]
    [InlineData(24)]
    public async Task RetireForPermanentFaultDespiteIndependentWallClockSkew(int utcShiftHours)
    {
        var clock = new ScheduledClock();
        var error = new IOException("permanent path failure");
        var classifier = A.Fake<IMessageFailureClassifier>();
        A.CallTo(() => classifier.Classify(error)).Returns(MessageFailureKind.Permanent);
        await using var fixture = new Harness(clock: clock, classifier: classifier,
            renewal: () => Task.FromException<RedisResult>(error));
        var lost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task stop = Task.CompletedTask;
        fixture.Sut.LostLeadership += (_, _) =>
        {
            stop = fixture.Sut.StopAsync(CancellationToken.None);
            lost.TrySetResult();
        };
        await fixture.Sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        await clock.WaitForTotalDelayCountAsync(1);
        var monotonicBeforeShift = clock.GetTimestamp();
        clock.UtcOffset = TimeSpan.FromHours(utcShiftHours);
        clock.GetTimestamp().ShouldBe(monotonicBeforeShift);
        clock.Advance(TimeSpan.FromSeconds(5));
        await lost.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await stop.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Sut.IsLeader.ShouldBeFalse();
        A.CallTo(() => classifier.Classify(error)).MustHaveHappenedOnceExactly();
    }

    internal static async Task VerifyPublishedLeadershipVisibilityAcrossThreadsAsync()
    {
        await using var fixture = new Harness();
        fixture.Sut.CurrentLeadership.ShouldBeNull();
        await fixture.Sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        var expected = fixture.Sut.CurrentLeadership.ShouldNotBeNull();
        var observations = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() => fixture.Sut.CurrentLeadership)));
        observations.ShouldAllBe(observation => observation == expected);
        await fixture.Sut.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        var retired = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() => fixture.Sut.CurrentLeadership)));
        retired.ShouldAllBe(observation => observation == null);
    }

    internal static async Task VerifyConcurrentStopAndLeadershipReadsAsync()
    {
        await using var fixture = new Harness();
        await fixture.Sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Sut.IsLeader.ShouldBeTrue();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reads = 0;
        var observer = Task.Run(async () =>
        {
            while (!release.Task.IsCompleted)
            {
                _ = fixture.Sut.CurrentLeadership;
                _ = fixture.Sut.IsLeader;
                _ = fixture.Sut.CurrentLeaderId;
                if (Interlocked.Increment(ref reads) == 100) { ready.TrySetResult(); }
                await Task.Yield();
            }
        });
        try
        {
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => fixture.Sut.StopAsync(CancellationToken.None))))
                .WaitAsync(TimeSpan.FromSeconds(5));
            fixture.Sut.CurrentLeadership.ShouldBeNull();
            fixture.Sut.IsLeader.ShouldBeFalse();
            fixture.Releases.Count.ShouldBe(1);
        }
        finally { release.TrySetResult(); await observer.WaitAsync(TimeSpan.FromSeconds(5)); }
    }

    internal static async Task VerifyCoherentSnapshotsAcrossRestartAsync()
    {
        var clock = new ScheduledClock();
        var fence = A.Fake<IFencingTokenIssuer>();
        long issued = 40;
        A.CallTo(() => fence.IssueTokenAsync(A<string>._, A<CancellationToken>._))
            .ReturnsLazily(() => ValueTask.FromResult(Interlocked.Increment(ref issued)));
        var first = new Leadership(41, clock.GetUtcNow());
        var second = new Leadership(42, clock.GetUtcNow() + TimeSpan.FromSeconds(1));
        await using var fixture = new Harness(clock: clock, fencing: fence);
        var invalid = new ConcurrentQueue<Leadership>();
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstReads = 0;
        var secondReads = 0;
        var observer = Task.Run(async () =>
        {
            while (!done.Task.IsCompleted)
            {
                if (fixture.Sut.CurrentLeadership is { } snapshot)
                {
                    if (snapshot == first)
                    {
                        if (Interlocked.Increment(ref firstReads) == 100) { firstObserved.TrySetResult(); }
                    }
                    else if (snapshot == second)
                    {
                        if (Interlocked.Increment(ref secondReads) == 100) { secondObserved.TrySetResult(); }
                    }
                    else { invalid.Enqueue(snapshot); }
                }
                await Task.Yield();
            }
        });
        try
        {
            await fixture.Sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            await firstObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await fixture.Sut.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            clock.Advance(TimeSpan.FromSeconds(1));
            await fixture.Sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            await secondObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await fixture.Sut.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally { done.TrySetResult(); await observer.WaitAsync(TimeSpan.FromSeconds(5)); }
        firstReads.ShouldBeGreaterThanOrEqualTo(100);
        secondReads.ShouldBeGreaterThanOrEqualTo(100);
        invalid.ShouldBeEmpty("a single snapshot must never mix the fence of one tenure with the acquisition time of another");
        fixture.Owners.Count.ShouldBe(2);
    }

    internal static async Task VerifyCompletedDisposalRefusesCrossThreadAdmissionAsync()
    {
        await using var fixture = new Harness();
        await fixture.Sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Run(async () => await fixture.DisposeAsync()).WaitAsync(TimeSpan.FromSeconds(5));
        var owners = fixture.Owners.Count;
        var renewals = fixture.Renewals.Count;
        var releases = fixture.Releases.Count;
        await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(async () =>
        {
            await Should.ThrowAsync<ObjectDisposedException>(() => fixture.Sut.StartAsync(CancellationToken.None));
            await Should.ThrowAsync<ObjectDisposedException>(() => fixture.Sut.StopAsync(CancellationToken.None));
            fixture.Sut.CurrentLeadership.ShouldBeNull();
        }))).WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Owners.Count.ShouldBe(owners);
        fixture.Renewals.Count.ShouldBe(renewals);
        fixture.Releases.Count.ShouldBe(releases);
        owners.ShouldBe(1);
        releases.ShouldBe(1);
        A.CallTo(() => fixture.Redis.Dispose()).MustNotHaveHappened();
        await fixture.DisposeAsync();
    }

    internal sealed class Harness : IAsyncDisposable
    {
        internal IConnectionMultiplexer Redis { get; } = A.Fake<IConnectionMultiplexer>();
        internal IDatabase Database { get; } = A.Fake<IDatabase>();
        internal ConcurrentQueue<string> Owners { get; } = new();
        internal ConcurrentQueue<string> Releases { get; } = new();
        internal ConcurrentQueue<string> Renewals { get; } = new();
        internal TaskCompletionSource SetEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal RedisLeaderElection Sut { get; }

        internal Harness(bool acquire = true, TimeProvider? clock = null, Task<bool>? heldAcquire = null, Func<Task<RedisResult>>? renewal = null, IMessageFailureClassifier? classifier = null, Exception? releaseFailure = null, IFencingTokenIssuer? fencing = null, ILogger<RedisLeaderElection>? logger = null)
        {
            A.CallTo(() => Redis.GetDatabase(A<int>._, A<object>._)).Returns(Database);
            A.CallTo(Database).Where(call => call.Method.Name == nameof(IDatabase.StringSetAsync))
                .WithReturnType<Task<bool>>().ReturnsLazily(call =>
                {
                    Owners.Enqueue(((RedisValue)call.Arguments[1]!).ToString());
                    SetEntered.TrySetResult();
                    return heldAcquire ?? Task.FromResult(acquire);
                });
            A.CallTo(() => Database.StringGetAsync(A<RedisKey>._, A<CommandFlags>._))
                .Returns(Task.FromResult((RedisValue)"other-candidate"));
            A.CallTo(Database).Where(call => call.Method.Name == nameof(IDatabase.ScriptEvaluateAsync))
                .WithReturnType<Task<RedisResult>>().ReturnsLazily(call =>
                {
                    if (call.Arguments[0] is string script && script.Contains("'del'", StringComparison.Ordinal))
                    {
                        Releases.Enqueue(((RedisValue[])call.Arguments[2]!)[0].ToString());
                        if (releaseFailure is not null) { return Task.FromException<RedisResult>(releaseFailure); }
                    }
                    if (call.Arguments[0] is string renew && renew.Contains("'pexpire'", StringComparison.Ordinal))
                    {
                        Renewals.Enqueue(((RedisValue[])call.Arguments[2]!)[0].ToString());
                        if (renewal is not null) { return renewal(); }
                    }
                    return Task.FromResult(RedisResult.Create((RedisValue)1));
                });
            Sut = new RedisLeaderElection(Redis, "tenure-control-key",
                Options.Create(new LeaderElectionOptions { InstanceId = "same-display-id" }),
                logger ?? NullLogger<RedisLeaderElection>.Instance,
                context: new RedisLeaderElectionContext { FailureClassifier = classifier, FencingTokenProvider = fencing }, timeProvider: clock);
        }
        public ValueTask DisposeAsync() => Sut.DisposeAsync();
    }

    private sealed class AdmissionBarrierClock : TimeProvider, IDisposable
    {
        private int _entered;
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal ManualResetEventSlim Release { get; } = new(false);
        public override long GetTimestamp()
        {
            if (Interlocked.Exchange(ref _entered, 1) == 0)
            {
                Entered.TrySetResult();
#pragma warning disable RS0030 // Freeze the synchronous clock read at the actual post-admission/pre-RPC launch site.
                Release.Wait();
#pragma warning restore RS0030
            }
            return base.GetTimestamp();
        }
        public void Dispose() => Release.Dispose();
    }

    private sealed class RenewalBarrierClock : TimeProvider, IDisposable
    {
        private long _ticks;
        private int _armed;
        private int _reads;
        private ManualTimer? _delay;
        internal TaskCompletionSource DelayReady { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal ManualResetEventSlim Release { get; } = new(false);
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch + TimeSpan.FromTicks(Volatile.Read(ref _ticks));
        public override long GetTimestamp()
        {
            // After the delay callback: the scheduler rechecks elapsed time, then the core
            // checks expiry. Read three is the actor's request-start sample AFTER registration.
            if (Volatile.Read(ref _armed) != 0 && Interlocked.Increment(ref _reads) == 3)
            {
                Entered.TrySetResult();
#pragma warning disable RS0030 // Intentional synchronous post-registration/pre-SDK barrier for the real adapter.
                Release.Wait();
#pragma warning restore RS0030
            }
            return Volatile.Read(ref _ticks);
        }
        internal void ArmPostRegistrationBarrier() { _reads = 0; Volatile.Write(ref _armed, 1); }
        internal void FireRenewalDelay()
        {
            Interlocked.Add(ref _ticks, TimeSpan.FromSeconds(5).Ticks);
            _delay!.Fire();
        }
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(callback, state);
            if (dueTime == TimeSpan.FromSeconds(5)) { _delay = timer; DelayReady.TrySetResult(); }
            return timer;
        }
        public void Dispose()
        {
            _delay?.Dispose();
            Release.Dispose();
        }
        private sealed class ManualTimer(TimerCallback callback, object? state) : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
            internal void Fire() => callback(state);
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class ScheduledClock : FakeTimeProvider
    {
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => base.GetUtcNow().UtcTicks;
        private long _utcOffsetTicks;
        internal TimeSpan UtcOffset
        {
            get => TimeSpan.FromTicks(Volatile.Read(ref _utcOffsetTicks));
            set => Volatile.Write(ref _utcOffsetTicks, value.Ticks);
        }
        public override DateTimeOffset GetUtcNow() => base.GetUtcNow() + UtcOffset;
        private readonly ConcurrentDictionary<long, int> _counts = new();
        private readonly ConcurrentDictionary<(long Ticks, int Occurrence), TaskCompletionSource> _scheduled = new();
        private readonly ConcurrentDictionary<int, TaskCompletionSource> _totalScheduled = new();
        private int _totalDelayCount;
        internal Task WaitForTotalDelayCountAsync(int count)
        {
            var completion = _totalScheduled.GetOrAdd(count, static _ => new(TaskCreationOptions.RunContinuationsAsynchronously));
            if (Volatile.Read(ref _totalDelayCount) >= count) { completion.TrySetResult(); }
            return completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        internal Task WaitForDelayAsync(TimeSpan delay, int occurrence)
        {
            var completion = _scheduled.GetOrAdd((delay.Ticks, occurrence), static _ => new(TaskCreationOptions.RunContinuationsAsynchronously));
            if (_counts.TryGetValue(delay.Ticks, out var count) && count >= occurrence) { completion.TrySetResult(); }
            return completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = base.CreateTimer(callback, state, dueTime, period);
            if (dueTime != Timeout.InfiniteTimeSpan)
            {
                var total = Interlocked.Increment(ref _totalDelayCount);
                if (_totalScheduled.TryGetValue(total, out var totalCompletion)) { totalCompletion.TrySetResult(); }
                var count = _counts.AddOrUpdate(dueTime.Ticks, 1, static (_, existing) => existing + 1);
                if (_scheduled.TryGetValue((dueTime.Ticks, count), out var completion)) { completion.TrySetResult(); }
            }
            return timer;
        }
    }

    private sealed class CountingClock : TimeProvider
    {
        private int _kernelTimerCount;
        internal int KernelTimerCount => Volatile.Read(ref _kernelTimerCount);
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            if (dueTime == Timeout.InfiniteTimeSpan && period == Timeout.InfiniteTimeSpan)
            {
                Interlocked.Increment(ref _kernelTimerCount);
            }
            return base.CreateTimer(callback, state, dueTime, period);
        }
    }
}
