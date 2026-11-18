// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Shouldly;
using Xunit;

namespace Excalibur.LeaderElection.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
[Trait("Pattern", "Regression")]
public sealed class RenewalTenureDeadlineShould
{
    private static readonly bool[] ExpectedTransitions = [true, false];
    [Fact]
    public async Task KeepOneFailedProbeAtRAndExpireAtRPlusGWithoutAReply()
    {
        var clock = new ManualClock();
        var retired = false;
        await using var kernel = new RenewalTenureDeadline(clock, TimeSpan.FromSeconds(10), _ => { retired = true; return Task.CompletedTask; });
        var generation = kernel.BeginAcquisition(new object());
        kernel.Publish(generation, clock.GetTimestamp(), new object(), 7, clock.GetUtcNow()).ShouldBeTrue();
        clock.Advance(TimeSpan.FromSeconds(5));
        kernel.Current.ShouldNotBeNull();
        clock.Advance(TimeSpan.FromSeconds(5)); clock.Timer.Fire();
        await kernel.RetirementTask;
        retired.ShouldBeTrue();
        kernel.Current.ShouldBeNull();
        kernel.Confirm(generation, 0).ShouldBeFalse();
    }

    [Fact]
    public async Task AnchorRenewalAtRequestStartAndRejectReplyAtOldDeadline()
    {
        var clock = new ManualClock();
        await using var kernel = new RenewalTenureDeadline(clock, TimeSpan.FromSeconds(10), static _ => Task.CompletedTask);
        var generation = kernel.BeginAcquisition(new object());
        kernel.Publish(generation, clock.GetTimestamp(), new object(), 7, clock.GetUtcNow()).ShouldBeTrue();
        clock.Advance(TimeSpan.FromSeconds(8)); var renewalStart = clock.GetTimestamp();
        clock.Advance(TimeSpan.FromSeconds(1)); kernel.Confirm(generation, renewalStart).ShouldBeTrue();
        clock.Advance(TimeSpan.FromSeconds(8)); kernel.Current.ShouldNotBeNull();
        var oldDeadlineReplyStart = clock.GetTimestamp();
        clock.Advance(TimeSpan.FromSeconds(1));
        kernel.Confirm(generation, oldDeadlineReplyStart).ShouldBeFalse();
        kernel.Current.ShouldBeNull();
    }

    [Fact]
    public async Task RetireAnExpiredAcquisitionAndDeliverItsOwnershipProof()
    {
        var clock = new ManualClock();
        RenewalTenureDeadline.Retirement? notice = null;
        await using var kernel = new RenewalTenureDeadline(clock, TimeSpan.FromSeconds(10), value => { notice = value; return Task.CompletedTask; });
        var owner = new object(); var proof = new object();
        var generation = kernel.BeginAcquisition(owner);
        var start = clock.GetTimestamp();
        clock.Advance(TimeSpan.FromSeconds(10));
        kernel.Publish(generation, start, proof, 7, clock.GetUtcNow()).ShouldBeFalse();
        await kernel.RetirementTask;
        notice.ShouldNotBeNull();
        notice.AcquisitionOwner.ShouldBeSameAs(owner);
        notice.Tenure!.OwnershipProof.ShouldBeSameAs(proof);
        notice.WasHolding.ShouldBeFalse();
        kernel.Current.ShouldBeNull();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RetireWhenTimerArmingThrowsOrReturnsFalse(bool throws, bool duringRenewal)
    {
        var clock = new ManualClock();
        RenewalTenureDeadline.Retirement? notice = null;
        await using var kernel = new RenewalTenureDeadline(clock, TimeSpan.FromSeconds(10), value => { notice = value; return Task.CompletedTask; });
        var generation = kernel.BeginAcquisition(new object());
        if (duringRenewal) { kernel.Publish(generation, clock.GetTimestamp(), new object(), 7, clock.GetUtcNow()).ShouldBeTrue(); }
        clock.Timer.ThrowOnChange = throws;
        clock.Timer.RefuseChange = !throws;
        var accepted = duringRenewal ? kernel.Confirm(generation, clock.GetTimestamp()) :
            kernel.Publish(generation, clock.GetTimestamp(), new object(), 7, clock.GetUtcNow());
        accepted.ShouldBeFalse();
        kernel.Current.ShouldBeNull();
        await kernel.RetirementTask;
        notice.ShouldNotBeNull();
        notice.WasHolding.ShouldBe(duringRenewal);
        notice.Generation.ShouldBe(generation);
        notice.Tenure.ShouldNotBeNull();
        notice.Tenure.FencingToken.ShouldBe(7);
        notice.Tenure.OwnershipProof.ShouldNotBeNull();
        notice.Cause.ShouldNotBeNull();
        clock.Timer.ThrowOnChange = false; clock.Timer.RefuseChange = false;
    }

    [Fact]
    public async Task RefusePublicationWhenTimerPreparationConsumesTheRemainingBudget()
    {
        var clock = new ManualClock();
        RenewalTenureDeadline.Retirement? notice = null;
        await using var kernel = new RenewalTenureDeadline(clock, TimeSpan.FromSeconds(10), value => { notice = value; return Task.CompletedTask; });
        var proof = new object();
        var generation = kernel.BeginAcquisition(new object());
        clock.Timer.BeforeChange = due => { if (due > TimeSpan.Zero) { clock.Advance(TimeSpan.FromSeconds(10)); } };
        kernel.Publish(generation, clock.GetTimestamp(), proof, 7, clock.GetUtcNow()).ShouldBeFalse();
        kernel.Current.ShouldBeNull();
        await kernel.RetirementTask;
        notice.ShouldNotBeNull();
        notice.WasHolding.ShouldBeFalse();
        notice.Tenure!.OwnershipProof.ShouldBeSameAs(proof);
    }

    [Fact]
    public async Task RearmAnEarlyOneShotCallbackAndEventuallyExpire()
    {
        var clock = new ManualClock();
        await using var kernel = new RenewalTenureDeadline(clock, TimeSpan.FromSeconds(10), static _ => Task.CompletedTask);
        var generation = kernel.BeginAcquisition(new object());
        kernel.Publish(generation, clock.GetTimestamp(), new object(), 9, clock.GetUtcNow()).ShouldBeTrue();
        clock.Advance(TimeSpan.FromSeconds(1)); clock.Timer.Fire();
        kernel.Current.ShouldNotBeNull();
        clock.Timer.LastDue.ShouldBe(TimeSpan.FromSeconds(9));
        clock.Advance(TimeSpan.FromSeconds(9)); clock.Timer.Fire();
        kernel.Current.ShouldBeNull();
        await kernel.RetirementTask;
    }

    [Fact]
    public async Task RoundPositiveSubmillisecondDueUpInsteadOfTruncatingToZero()
    {
        var clock = new ManualClock();
        await using var kernel = new RenewalTenureDeadline(clock, TimeSpan.FromTicks(100), static _ => Task.CompletedTask);
        var generation = kernel.BeginAcquisition(new object());
        kernel.Publish(generation, clock.GetTimestamp(), new object(), null, clock.GetUtcNow()).ShouldBeTrue();
        clock.Timer.LastDue.ShouldBe(TimeSpan.FromMilliseconds(1));
        clock.Advance(TimeSpan.FromTicks(99)); clock.Timer.Fire();
        kernel.Current.ShouldNotBeNull();
        clock.Timer.LastDue.ShouldBe(TimeSpan.FromMilliseconds(1));
        clock.Advance(TimeSpan.FromTicks(1));
        kernel.Current.ShouldBeNull();
    }

    [Fact]
    public async Task ChunkAnOversizedDueAndRearmUntilTheActualDeadline()
    {
        var clock = new ManualClock();
        var maxDue = TimeSpan.FromMilliseconds(uint.MaxValue - 1d);
        await using var kernel = new RenewalTenureDeadline(clock, TimeSpan.FromDays(60), static _ => Task.CompletedTask);
        var generation = kernel.BeginAcquisition(new object());
        kernel.Publish(generation, clock.GetTimestamp(), new object(), null, clock.GetUtcNow()).ShouldBeTrue();
        clock.Timer.LastDue.ShouldBe(maxDue);
        clock.Advance(maxDue); clock.Timer.Fire();
        kernel.Current.ShouldNotBeNull();
        clock.Timer.LastDue.ShouldBeGreaterThan(TimeSpan.Zero);
        clock.Timer.LastDue.ShouldBeLessThan(maxDue);
        clock.Advance(TimeSpan.FromDays(60) - maxDue); clock.Timer.Fire();
        kernel.Current.ShouldBeNull();
    }

    [Fact]
    public async Task PublishProofFenceAndTimeAsOneAuthoritativeImmutableRecord()
    {
        var clock = new ManualClock();
        await using var kernel = new RenewalTenureDeadline(clock, TimeSpan.FromSeconds(10), static _ => Task.CompletedTask);
        var proof = new object(); var acquired = clock.GetUtcNow();
        var generation = kernel.BeginAcquisition(new object()); var start = clock.GetTimestamp();
        kernel.Current.ShouldBeNull();
        kernel.Publish(generation, start, proof, 42, acquired).ShouldBeTrue();
        var published = kernel.Current.ShouldNotBeNull();
        published.Generation.ShouldBe(generation);
        published.OwnershipProof.ShouldBeSameAs(proof);
        published.FencingToken.ShouldBe(42);
        published.AcquiredAt.ShouldBe(acquired);
        published.ProofRequestStart.ShouldBe(start);
        kernel.Retire(generation, "definitive loss");
        kernel.Current.ShouldBeNull();
        await kernel.RetirementTask;
        kernel.CompleteRetirement(generation).ShouldBeTrue();
        var next = kernel.BeginAcquisition(new object());
        kernel.Publish(next, clock.GetTimestamp(), new object(), 43, clock.GetUtcNow()).ShouldBeTrue();
        kernel.Confirm(generation, clock.GetTimestamp()).ShouldBeFalse();
        kernel.Current!.Generation.ShouldBe(next);
    }

    [Fact]
    public async Task ReportCanceledStopWhileRetainingUnfinishedBorrowedClientWork()
    {
        var clock = new ManualClock();
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var kernel = new RenewalTenureDeadline(clock, TimeSpan.FromSeconds(10), _ => pending.Task);
        var generation = kernel.BeginAcquisition(new object());
        kernel.Publish(generation, clock.GetTimestamp(), new object(), 7, clock.GetUtcNow()).ShouldBeTrue();
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        try
        {
            await Should.ThrowAsync<OperationCanceledException>(() => kernel.StopAsync(canceled.Token));
            kernel.Current.ShouldBeNull();
            kernel.RetirementTask.IsCompleted.ShouldBeFalse();
            Should.Throw<InvalidOperationException>(() => kernel.BeginAcquisition(new object()));
        }
        finally { pending.TrySetResult(); }
        await kernel.StopAsync(CancellationToken.None);
        await kernel.DisposeAsync();
    }

    [Fact]
    public async Task RefuseSuccessfulDisposalWhenRetirementFailed()
    {
        var clock = new ManualClock();
        var failure = new InvalidOperationException("borrowed client retirement failed");
        var kernel = new RenewalTenureDeadline(clock, TimeSpan.FromSeconds(10), _ => Task.FromException(failure));
        var generation = kernel.BeginAcquisition(new object());
        kernel.Publish(generation, clock.GetTimestamp(), new object(), 7, clock.GetUtcNow()).ShouldBeTrue();
        var actual = await Should.ThrowAsync<InvalidOperationException>(() => kernel.DisposeAsync().AsTask());
        actual.ShouldBeSameAs(failure);
        clock.Timer.WasDisposed.ShouldBeTrue();
        kernel.Current.ShouldBeNull();
        kernel.CompleteRetirement(generation).ShouldBeFalse();
    }

    [Fact]
    public async Task AggregateRetirementAndOwnedTimerDisposalFailuresInCauseOrder()
    {
        var clock = new ManualClock();
        var retirementFailure = new InvalidOperationException("retirement failure");
        var timerFailure = new InvalidOperationException("timer disposal failure");
        var kernel = new RenewalTenureDeadline(clock, TimeSpan.FromSeconds(10), _ => Task.FromException(retirementFailure));
        var generation = kernel.BeginAcquisition(new object());
        kernel.Publish(generation, clock.GetTimestamp(), new object(), 7, clock.GetUtcNow()).ShouldBeTrue();
        clock.Timer.DisposeFailure = timerFailure;
        var failure = await Should.ThrowAsync<AggregateException>(() => kernel.DisposeAsync().AsTask());
        failure.InnerExceptions.Count.ShouldBe(2);
        failure.InnerExceptions[0].ShouldBeSameAs(retirementFailure);
        failure.InnerExceptions[1].ShouldBeSameAs(timerFailure);
        clock.Timer.WasDisposed.ShouldBeTrue();
        kernel.Current.ShouldBeNull();
    }

    [Fact]
    public async Task PropagateAnOwnedTimerDisposalFailureWhenRetirementSucceeded()
    {
        var clock = new ManualClock();
        var failure = new InvalidOperationException("timer disposal failure");
        var kernel = new RenewalTenureDeadline(clock, TimeSpan.FromSeconds(10), static _ => Task.CompletedTask);
        var generation = kernel.BeginAcquisition(new object());
        kernel.Publish(generation, clock.GetTimestamp(), new object(), 7, clock.GetUtcNow()).ShouldBeTrue();
        clock.Timer.DisposeFailure = failure;
        var actual = await Should.ThrowAsync<InvalidOperationException>(() => kernel.DisposeAsync().AsTask());
        actual.ShouldBeSameAs(failure);
        clock.Timer.WasDisposed.ShouldBeTrue();
    }

    [Fact]
    public async Task QueuePublicationBeforeLossEvenWhenThePublicationSubscriberIsBlocked()
    {
        var clock = new ManualClock();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var seen = new List<bool>(); var cleaned = false;
        var kernel = new RenewalTenureDeadline(clock, TimeSpan.FromSeconds(10), _ => { cleaned = true; return Task.CompletedTask; }, transition =>
        {
            seen.Add(transition.Acquired);
#pragma warning disable RS0030 // Intentional synchronous subscriber adversary proves blocked delivery and callback-reentrant shutdown cannot hold protocol retirement.
            if (transition.Acquired) { entered.TrySetResult(); release.Task.GetAwaiter().GetResult(); }
#pragma warning restore RS0030
        });
        try
        {
            var generation = kernel.BeginAcquisition(new object());
            kernel.Publish(generation, clock.GetTimestamp(), new object(), 7, clock.GetUtcNow()).ShouldBeTrue();
            await entered.Task;
            clock.Advance(TimeSpan.FromSeconds(10)); clock.Timer.Fire();
            await kernel.StopAsync(CancellationToken.None);
            cleaned.ShouldBeTrue();
            kernel.Current.ShouldBeNull();
            clock.Timer.WasDisposed.ShouldBeTrue();
            kernel.NotificationDrainTask.IsCompleted.ShouldBeFalse();
        }
        finally { release.TrySetResult(); }
        await kernel.NotificationDrainTask;
        seen.ShouldBe(ExpectedTransitions);
        await kernel.DisposeAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AllowSubscriberStopOrDisposeWithoutJoiningItsOwnDelivery(bool dispose)
    {
        var clock = new ManualClock();
        var callback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        RenewalTenureDeadline kernel = null!;
        kernel = new RenewalTenureDeadline(clock, TimeSpan.FromSeconds(10), static _ => Task.CompletedTask, transition =>
        {
            if (!transition.Acquired) { return; }
#pragma warning disable RS0030 // Intentional synchronous subscriber adversary proves blocked delivery and callback-reentrant shutdown cannot hold protocol retirement.
            if (dispose) { kernel.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
#pragma warning restore RS0030
#pragma warning disable RS0030 // Intentional synchronous subscriber adversary proves blocked delivery and callback-reentrant shutdown cannot hold protocol retirement.
            else { kernel.StopAsync(CancellationToken.None).GetAwaiter().GetResult(); }
#pragma warning restore RS0030
            callback.TrySetResult();
        });
        var generation = kernel.BeginAcquisition(new object());
        kernel.Publish(generation, clock.GetTimestamp(), new object(), 7, clock.GetUtcNow()).ShouldBeTrue();
        await callback.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await kernel.NotificationDrainTask;
        kernel.Current.ShouldBeNull();
        clock.Timer.DisposeCalls.ShouldBe(1);
        await kernel.DisposeAsync();
        clock.Timer.DisposeCalls.ShouldBe(1);
    }

    [Fact]
    public async Task RefuseNotificationReentrantAcquisitionWithoutDeadlocking()
    {
        var clock = new ManualClock();
        var refused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        RenewalTenureDeadline kernel = null!;
        kernel = new RenewalTenureDeadline(clock, TimeSpan.FromSeconds(10), static _ => Task.CompletedTask, transition =>
        {
            if (!transition.Acquired) { return; }
            Should.Throw<InvalidOperationException>(() => kernel.BeginAcquisition(new object()));
#pragma warning disable RS0030 // Intentional synchronous subscriber adversary proves blocked delivery and callback-reentrant shutdown cannot hold protocol retirement.
            Should.Throw<InvalidOperationException>(() => kernel.WaitForNotificationDrainAsync(CancellationToken.None).GetAwaiter().GetResult());
#pragma warning restore RS0030
            refused.TrySetResult();
        });
        var generation = kernel.BeginAcquisition(new object());
        kernel.Publish(generation, clock.GetTimestamp(), new object(), 7, clock.GetUtcNow()).ShouldBeTrue();
        await refused.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await kernel.DisposeAsync();
        await kernel.NotificationDrainTask;
    }

    [Fact]
    public async Task PermitADeferredSubscriberDescendantAfterItsCallbackAndDrainComplete()
    {
        var clock = new ManualClock();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var created = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? descendant = null;
        RenewalTenureDeadline old = null!;
        old = new RenewalTenureDeadline(clock, TimeSpan.FromSeconds(10), static _ => Task.CompletedTask, transition =>
        {
            if (!transition.Acquired) { return; }
            Should.Throw<InvalidOperationException>(() => old.BeginAcquisition(new object()));
            descendant = Task.Run(async () =>
            {
                await release.Task;
                var nextClock = new ManualClock();
                await using var next = new RenewalTenureDeadline(nextClock, TimeSpan.FromSeconds(10), static _ => Task.CompletedTask,
                    previousNotificationDrain: old.NotificationDrainTask);
                await next.WaitForNotificationDrainAsync(CancellationToken.None);
                var generation = next.BeginAcquisition(new object());
                next.Publish(generation, nextClock.GetTimestamp(), new object(), 9, nextClock.GetUtcNow()).ShouldBeTrue();
            });
            created.TrySetResult();
        });
        var generation = old.BeginAcquisition(new object());
        old.Publish(generation, clock.GetTimestamp(), new object(), 7, clock.GetUtcNow()).ShouldBeTrue();
        await created.Task;
        await old.StopAsync(CancellationToken.None);
        await old.NotificationDrainTask;
        release.TrySetResult();
        await descendant!.WaitAsync(TimeSpan.FromSeconds(5));
        await old.DisposeAsync();
    }

    [Fact]
    public async Task RestartExternallyOnlyAfterPreviousRunNotificationsDrain()
    {
        var oldClock = new ManualClock();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var old = new RenewalTenureDeadline(oldClock, TimeSpan.FromSeconds(10), static _ => Task.CompletedTask, transition =>
        {
#pragma warning disable RS0030 // Intentional synchronous subscriber adversary proves blocked delivery and callback-reentrant shutdown cannot hold protocol retirement.
            if (transition.Acquired) { entered.TrySetResult(); release.Task.GetAwaiter().GetResult(); }
#pragma warning restore RS0030
        });
        var generation = old.BeginAcquisition(new object());
        old.Publish(generation, oldClock.GetTimestamp(), new object(), 7, oldClock.GetUtcNow()).ShouldBeTrue();
        await entered.Task;
        await old.StopAsync(CancellationToken.None);
        var clock = new ManualClock();
        await using var next = new RenewalTenureDeadline(clock, TimeSpan.FromSeconds(10), static _ => Task.CompletedTask,
            previousNotificationDrain: old.NotificationDrainTask);
        try { Should.Throw<InvalidOperationException>(() => next.BeginAcquisition(new object())); }
        finally { release.TrySetResult(); }
        await next.WaitForNotificationDrainAsync(CancellationToken.None);
        var nextGeneration = next.BeginAcquisition(new object());
        next.Publish(nextGeneration, clock.GetTimestamp(), new object(), 8, clock.GetUtcNow()).ShouldBeTrue();
        await old.DisposeAsync();
    }

    [Fact]
    public async Task ContinueRemainingSubscribersAfterOneThrows()
    {
        var clock = new ManualClock();
        var seen = new List<bool>();
        var failure = new InvalidOperationException("subscriber failure");
        Action<RenewalTenureDeadline.Transition> throwing = _ => throw failure;
        Action<RenewalTenureDeadline.Transition> recording = transition => seen.Add(transition.Acquired);
        var kernel = new RenewalTenureDeadline(clock, TimeSpan.FromSeconds(10), static _ => Task.CompletedTask, throwing + recording);
        var generation = kernel.BeginAcquisition(new object());
        kernel.Publish(generation, clock.GetTimestamp(), new object(), 7, clock.GetUtcNow()).ShouldBeTrue();
        await kernel.StopAsync(CancellationToken.None);
        await kernel.NotificationDrainTask;
        seen.ShouldBe(ExpectedTransitions);
        kernel.NotificationFailure.ShouldBeSameAs(failure);
        await kernel.DisposeAsync();
    }

    [Fact]
    public async Task DisposeTheOwnedTimerOnceAcrossConcurrentCalls()
    {
        var clock = new ManualClock();
        var kernel = new RenewalTenureDeadline(clock, TimeSpan.FromSeconds(10), static _ => Task.CompletedTask);
        var generation = kernel.BeginAcquisition(new object());
        kernel.Publish(generation, clock.GetTimestamp(), new object(), 7, clock.GetUtcNow()).ShouldBeTrue();
        await Task.WhenAll(kernel.DisposeAsync().AsTask(), kernel.DisposeAsync().AsTask(), kernel.StopAsync(CancellationToken.None));
        clock.Timer.DisposeCalls.ShouldBe(1);
    }

    [Fact]
    public async Task SealTheRegisteredOperationBeforeLateLaunchAfterAdmission()
    {
        var clock = new ManualClock();
        var owner = new OwnedOperationLatch();
        var started = false;
        await using var kernel = new RenewalTenureDeadline(clock, TimeSpan.FromSeconds(10), _ => owner.SealAndJoinAsync());
        kernel.BeginAcquisition(owner);
        // Exact admission/launch gap: shutdown wins before the adapter attempts launch.
        await kernel.StopAsync(CancellationToken.None);
        owner.TryLaunch(() => { started = true; return Task.CompletedTask; }).ShouldBeFalse();
        started.ShouldBeFalse();
        owner.Completion.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Fact]
    public async Task JoinAnAlreadyRegisteredOperationBeforeSuccessfulStop()
    {
        var clock = new ManualClock();
        var owner = new OwnedOperationLatch();
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var kernel = new RenewalTenureDeadline(clock, TimeSpan.FromSeconds(10), _ => owner.SealAndJoinAsync());
        kernel.BeginAcquisition(owner);
        owner.TryLaunch(() => pending.Task).ShouldBeTrue();
        var stop = kernel.StopAsync(CancellationToken.None);
        stop.IsCompleted.ShouldBeFalse();
        pending.TrySetResult();
        await stop;
        clock.Timer.WasDisposed.ShouldBeTrue();
    }

    [Fact]
    public async Task SnapshotARegisteredRenewalAndRejectRegistrationAfterRetirement()
    {
        var clock = new ManualClock();
        RenewalTenureDeadline.Retirement? observed = null;
        var acquire = new OwnedOperationLatch();
        acquire.TryLaunch(static () => Task.CompletedTask).ShouldBeTrue();
        await acquire.Completion;
        await using var kernel = new RenewalTenureDeadline(clock, TimeSpan.FromSeconds(10), value => { observed = value; return value.Operation!.SealAndJoinAsync(); });
        var generation = kernel.BeginAcquisition(new object(), acquire);
        kernel.Publish(generation, clock.GetTimestamp(), new object(), 7, clock.GetUtcNow()).ShouldBeTrue();
        var renewal = new OwnedOperationLatch();
        kernel.TryRegisterOperation(generation, renewal).ShouldBeTrue();
        clock.Advance(TimeSpan.FromSeconds(10)); clock.Timer.Fire();
        kernel.TryRegisterOperation(generation, new OwnedOperationLatch()).ShouldBeFalse();
        await kernel.RetirementTask;
        observed.ShouldNotBeNull();
        observed.Operation.ShouldBeSameAs(renewal);
        renewal.TryLaunch(static () => Task.CompletedTask).ShouldBeFalse();
    }

    [Fact]
    public async Task CoalesceRemoteHolderAndAcquisitionFailureObservationsBehindABlockedSubscriber()
    {
        var clock = new ManualClock();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var seen = new List<RenewalTenureDeadline.Observation>();
        var kernel = new RenewalTenureDeadline(clock, TimeSpan.FromSeconds(10), static _ => Task.CompletedTask,
            observe: observation =>
            {
                seen.Add(observation);
#pragma warning disable RS0030 // Intentional synchronous subscriber adversary proves blocked delivery and callback-reentrant shutdown cannot hold protocol retirement.
                if (seen.Count == 1) { entered.TrySetResult(); release.Task.GetAwaiter().GetResult(); }
#pragma warning restore RS0030
            });
        try
        {
            var generation = kernel.BeginAcquisition(new object());
            kernel.ReportObservation(generation, true, "first", "race lost", null, clock.GetUtcNow());
            await entered.Task;
            for (var i = 0; i < 100; i++)
            {
                kernel.ReportObservation(generation, true, "holder-" + i, "failure-" + i, null, clock.GetUtcNow());
            }
            kernel.Current.ShouldBeNull();
            kernel.ReadStatus().ObservedHolder.ShouldBe("holder-99");
        }
        finally { release.TrySetResult(); }
        await kernel.NotificationDrainTask;
        seen.Count.ShouldBe(2);
        seen[1].PreviousHolder.ShouldBe("first");
        seen[1].Holder.ShouldBe("holder-99");
        seen[1].FailureReason.ShouldBe("failure-99");
        await kernel.DisposeAsync();
    }

    [Fact]
    public async Task KeepRemoteDisplayObservationsSeparateFromAuthoritativeLocalOwnership()
    {
        var clock = new ManualClock();
        await using var kernel = new RenewalTenureDeadline(clock, TimeSpan.FromSeconds(10), static _ => Task.CompletedTask);
        var generation = kernel.BeginAcquisition(new object());
        kernel.ReportObservation(generation, true, "remote", "race", null, clock.GetUtcNow());
        kernel.ReadStatus().Tenure.ShouldBeNull();
        kernel.ReadStatus().ObservedHolder.ShouldBe("remote");
        kernel.Publish(generation, clock.GetTimestamp(), new object(), 42, clock.GetUtcNow()).ShouldBeTrue();
        var status = kernel.ReadStatus();
        status.Tenure!.FencingToken.ShouldBe(42);
        status.ObservedHolder.ShouldBeNull();
        kernel.RequestStop();
        kernel.ReadStatus().Tenure.ShouldBeNull();
        kernel.ReadStatus().ObservedHolder.ShouldBeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SealTheRegisteredLaunchAtRetirementBeforeItsCleanupWorkerRuns(bool renewal)
    {
        var clock = new ManualClock();
        var workerEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseWorker = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var initial = new OwnedOperationLatch();
        var operation = renewal ? new OwnedOperationLatch() : initial;
        if (renewal) { initial.TryLaunch(static () => Task.CompletedTask); await initial.Completion; }
        var kernel = new RenewalTenureDeadline(clock, TimeSpan.FromSeconds(10), async retirement =>
        {
            workerEntered.TrySetResult();
            await releaseWorker.Task;
            await retirement.OperationCompletion!;
        });
        try
        {
            var generation = kernel.BeginAcquisition(new object(), initial);
            if (renewal)
            {
                kernel.Publish(generation, clock.GetTimestamp(), new object(), 7, clock.GetUtcNow()).ShouldBeTrue();
                kernel.TryRegisterOperation(generation, operation).ShouldBeTrue();
            }
            kernel.Retire(generation, "stop at launch barrier");
            await workerEntered.Task;
            operation.Completion.IsCompletedSuccessfully.ShouldBeTrue();
            operation.TryLaunch(static () => throw new InvalidOperationException("late SDK launch must be impossible")).ShouldBeFalse();
            kernel.Current.ShouldBeNull();
        }
        finally { releaseWorker.TrySetResult(); }
        await kernel.DisposeAsync();
    }

    private sealed class ManualClock : TimeProvider
    {
        private long _ticks;
        internal ManualTimer Timer { get; private set; } = null!;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _ticks;
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch + TimeSpan.FromTicks(_ticks);
        internal void Advance(TimeSpan elapsed) => _ticks += elapsed.Ticks;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            Timer = new ManualTimer(callback, state);
    }

    private sealed class ManualTimer(TimerCallback callback, object? state) : ITimer
    {
        internal bool WasDisposed { get; private set; }
        internal int DisposeCalls { get; private set; }
        internal Exception? DisposeFailure { get; set; }
        internal Action<TimeSpan>? BeforeChange { get; set; }
        internal bool ThrowOnChange { get; set; }
        internal bool RefuseChange { get; set; }
        internal TimeSpan LastDue { get; private set; }
        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            BeforeChange?.Invoke(dueTime);
            if (ThrowOnChange) { throw new InvalidOperationException("injected timer failure"); }
            if (RefuseChange) { return false; }
            LastDue = dueTime; return true;
        }
        internal void Fire() => callback(state);
        public void Dispose() { WasDisposed = true; }
        public ValueTask DisposeAsync()
        {
            WasDisposed = true;
            DisposeCalls++;
            return DisposeFailure is null ? ValueTask.CompletedTask : ValueTask.FromException(DisposeFailure);
        }
    }
}
