// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Collections.Concurrent;
using System.Net;
using Excalibur.LeaderElection.Kubernetes;
using k8s;
using k8s.Autorest;
using k8s.Models;
using Microsoft.Extensions.Time.Testing;

namespace Excalibur.LeaderElection.Tests.Kubernetes;

[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
[Trait("Pattern", "Regression")]
public sealed class KubernetesTenureProtocolShould(ITestOutputHelper output)
{
    private static readonly string[] ExpectedCallbackOrder = ["acquired", "lost"];
    [Fact]
    public async Task LoseAtTenWhileTheActualSdkCanRemainPendingForOneHundredSeconds()
    {
        await using var fixture = new KubeTenureFixture();
        var held = new TaskCompletionSource<V1Lease>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = Signal(); var lost = new TaskCompletionSource<LeaderElectionEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Sut.LostLeadership += (_, args) => lost.TrySetResult(args);
        try
        {
        await fixture.StartAsync();
        V1Lease? delayed = null;
        fixture.Replace = (lease, _) => lease.Spec.HolderIdentity is not null ? HoldAsync(lease) : Task.FromResult(fixture.Persist(lease));
        Task<V1Lease> HoldAsync(V1Lease lease) { delayed = lease; entered.TrySetResult(); return held.Task; }
        await fixture.Clock.WaitForDueAsync(TimeSpan.FromSeconds(5), 1);
        fixture.Clock.Advance(TimeSpan.FromSeconds(5)); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Clock.Advance(TimeSpan.FromSeconds(5));
        var loss = await lost.Task.WaitAsync(TimeSpan.FromSeconds(5)); // Notification, not getter-triggered expiry.
        loss.Timestamp.ShouldBe(fixture.Origin + TimeSpan.FromSeconds(10));
        fixture.Sut.IsLeader.ShouldBeFalse(); fixture.Sut.CurrentLeadership.ShouldBeNull();
        var stopping = fixture.Sut.StopAsync(CancellationToken.None); stopping.IsCompleted.ShouldBeFalse();
        fixture.Clock.Advance(TimeSpan.FromSeconds(90));
        await AssertCallerTimeoutAsync(stopping);
        var disposing = fixture.Sut.DisposeAsync().AsTask(); disposing.IsCompleted.ShouldBeFalse();
        // A successful late native reply still cannot publish or confirm the retired generation.
        held.TrySetResult(fixture.Persist(delayed!));
        await disposing.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Acquired.ShouldBe(1); fixture.Lost.ShouldBe(1); fixture.Sut.IsLeader.ShouldBeFalse();
        A.CallTo(() => fixture.Client.Dispose()).MustNotHaveHappened();
        }
        finally
        {
            held.TrySetResult(fixture.Snapshot());
        }
    }

    [Fact]
    public async Task TimeOutTheInitialHeldReadWithoutPublishingOrAdmittingItsLateWrite()
    {
        var fixture = new KubeTenureFixture();
        var reply = new TaskCompletionSource<V1Lease>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = Signal();
        try
        {
            fixture.Clock.Advance(TimeSpan.FromDays(30));
            fixture.Read = _ => { entered.TrySetResult(); return reply.Task; };
            var starting = fixture.Sut.StartAsync(CancellationToken.None);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            fixture.Clock.Advance(TimeSpan.FromSeconds(10));
            await AssertCallerTimeoutAsync(starting);
            fixture.Sut.CurrentLeadership.ShouldBeNull(); fixture.Acquired.ShouldBe(0);
            fixture.Calls.ShouldBe(1); fixture.OwnershipWrites.ShouldBe(0);
            var disposing = fixture.Sut.DisposeAsync().AsTask(); disposing.IsCompleted.ShouldBeFalse();
            reply.TrySetResult(fixture.Snapshot());
            await disposing.WaitAsync(TimeSpan.FromSeconds(5));
            fixture.Calls.ShouldBeGreaterThanOrEqualTo(1); fixture.Calls.ShouldBeLessThanOrEqualTo(2);
            fixture.OwnershipWrites.ShouldBe(0); fixture.Lost.ShouldBe(0); // At most one separately authorized cleanup read.
            A.CallTo(() => fixture.Client.Dispose()).MustNotHaveHappened();
        }
        finally
        {
            fixture.Read = _ => Task.FromResult(fixture.Snapshot()); reply.TrySetResult(fixture.Snapshot());
            await fixture.DisposeAsync();
        }
    }

    [Fact]
    public async Task TimeOutStopWithoutAbandoningTheActualHeldRenewalOrStartingAnotherRequest()
    {
        var fixture = new KubeTenureFixture(); await fixture.StartAsync();
        var reply = new TaskCompletionSource<V1Lease>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = Signal();
        try
        {
            fixture.Read = _ => { entered.TrySetResult(); return reply.Task; };
            fixture.Clock.Advance(TimeSpan.FromSeconds(5)); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var count = fixture.Calls;
            var stopping = fixture.Sut.StopAsync(CancellationToken.None);
            fixture.Sut.IsLeader.ShouldBeFalse(); fixture.Clock.Advance(TimeSpan.FromSeconds(10));
            await AssertCallerTimeoutAsync(stopping);
            fixture.Calls.ShouldBe(count);
            await Should.ThrowAsync<InvalidOperationException>(() => fixture.Sut.StartAsync(CancellationToken.None));
            var disposing = fixture.Sut.DisposeAsync().AsTask(); disposing.IsCompleted.ShouldBeFalse();
            fixture.Read = _ => Task.FromResult(fixture.Snapshot()); reply.TrySetResult(fixture.Snapshot());
            await disposing.WaitAsync(TimeSpan.FromSeconds(5));
            fixture.Acquired.ShouldBe(1); fixture.Lost.ShouldBe(1);
        }
        finally
        {
            fixture.Read = _ => Task.FromResult(fixture.Snapshot()); reply.TrySetResult(fixture.Snapshot());
            await fixture.DisposeAsync();
        }
    }

    [Fact]
    public async Task RejectALateInitialReplyAtTheBudgetBoundaryInsteadOfReportingSuccessfulStart()
    {
        var fixture = new KubeTenureFixture();
        var reply = new TaskCompletionSource<V1Lease>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = Signal();
        try
        {
            fixture.Read = _ => { entered.TrySetResult(); return reply.Task; };
            var starting = fixture.Sut.StartAsync(CancellationToken.None);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            fixture.Clock.Advance(TimeSpan.FromSeconds(10));
            reply.TrySetResult(fixture.Snapshot()); // Before observing caller completion, but after the deadline.
            await AssertCallerTimeoutAsync(starting);
            await fixture.Sut.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            fixture.Acquired.ShouldBe(0); fixture.Lost.ShouldBe(0); fixture.OwnershipWrites.ShouldBe(0);
        }
        finally
        {
            fixture.Read = _ => Task.FromResult(fixture.Snapshot()); reply.TrySetResult(fixture.Snapshot());
            await fixture.DisposeAsync();
        }
    }

    [Fact]
    public async Task KeepRequestStartCadenceWithHealthyFourPointNineSecondRpcReplies()
    {
        await using var fixture = new KubeTenureFixture();
        fixture.Replace = (lease, _) => { if (lease.Spec.HolderIdentity is not null) { fixture.Clock.Advance(TimeSpan.FromMilliseconds(4900)); } return Task.FromResult(fixture.Persist(lease)); };
        await fixture.StartAsync();
        await fixture.Clock.WaitForDueAsync(TimeSpan.FromMilliseconds(100), 1);
        fixture.Clock.Advance(TimeSpan.FromMilliseconds(100));
        await fixture.Clock.WaitForDueAsync(TimeSpan.FromMilliseconds(100), 2);
        fixture.Clock.Advance(TimeSpan.FromMilliseconds(100));
        await fixture.Clock.WaitForDueAsync(TimeSpan.FromMilliseconds(100), 3);
        fixture.ReadStarts.Take(3).ToArray().ShouldBe(new long[] { 0, TimeSpan.FromSeconds(5).Ticks, TimeSpan.FromSeconds(10).Ticks });
        fixture.Sut.IsLeader.ShouldBeTrue(); fixture.Acquired.ShouldBe(1);
    }

    [Fact]
    public async Task RecoverAmbiguityAtSevenWithoutExtendingTheOldProofOnTheFailure()
    {
        var fixture = new KubeTenureFixture();
        var diagnostics = new KubeAmbiguityDiagnostics(message => output.WriteLine(message));
        Exception? primary = null;
        try
        {
            var purposes = await diagnostics.CalibrateAsync();
            await fixture.StartAsync(); var fail = true;
            var initialProof = diagnostics.CaptureProof(fixture.Sut, fixture.Clock, "initial");
            initialProof.ProofRequestStart.ShouldBe(0);
            fixture.Read = _ =>
            {
                if (fail) { fail = false; fixture.Clock.Advance(TimeSpan.FromMilliseconds(100)); return Task.FromException<V1Lease>(new IOException("ambiguous transport")); }
                return Task.FromResult(fixture.Snapshot());
            };
            var initialCadence = await fixture.Clock.WaitForSourceDueAsync(purposes.Delay, TimeSpan.FromSeconds(5), 0);
            fixture.Clock.AssertUniqueSourceMilestone(initialCadence, purposes.Delay, TimeSpan.FromSeconds(5), 0);
            initialCadence.Now.ShouldBe(0);
            fixture.Clock.Advance(TimeSpan.FromSeconds(5));
            var retryCadence = await fixture.Clock.WaitForSourceDueAsync(purposes.Delay, TimeSpan.FromMilliseconds(1900), initialCadence.Sequence);
            fixture.Clock.AssertUniqueSourceMilestone(retryCadence, purposes.Delay, TimeSpan.FromMilliseconds(1900), initialCadence.Sequence);
            retryCadence.Now.ShouldBe(TimeSpan.FromMilliseconds(5100).Ticks);
            var failedProof = diagnostics.CaptureProof(fixture.Sut, fixture.Clock, "failed-renewal-at-5.1");
            failedProof.Generation.ShouldBe(initialProof.Generation); failedProof.KernelIdentity.ShouldBe(initialProof.KernelIdentity);
            failedProof.ProofRequestStart.ShouldBe(0); // A failed request cannot extend the old proof.
            diagnostics.ObserveLegacyGate(fixture.Clock, purposes); // Diagnostic only; NEVER the clock-advance gate.
            fixture.Clock.Advance(TimeSpan.FromMilliseconds(1900));
            var recoveredCadence = await fixture.Clock.WaitForSourceDueAsync(purposes.Delay, TimeSpan.FromSeconds(5), retryCadence.Sequence);
            fixture.Clock.AssertUniqueSourceMilestone(recoveredCadence, purposes.Delay, TimeSpan.FromSeconds(5), retryCadence.Sequence);
            recoveredCadence.Now.ShouldBe(TimeSpan.FromSeconds(7).Ticks);
            var recoveredProof = diagnostics.CaptureProof(fixture.Sut, fixture.Clock, "confirmed-renewal-at-7-before-final-advance");
            recoveredProof.Generation.ShouldBe(initialProof.Generation); recoveredProof.KernelIdentity.ShouldBe(initialProof.KernelIdentity);
            recoveredProof.OwnershipIdentity.ShouldBe(initialProof.OwnershipIdentity); recoveredProof.ProofRequestStart.ShouldBe(TimeSpan.FromSeconds(7).Ticks);
            fixture.ReadStarts.ToArray().ShouldBe(new long[] { 0, TimeSpan.FromSeconds(5).Ticks, TimeSpan.FromSeconds(7).Ticks });
            fixture.Clock.Advance(TimeSpan.FromMilliseconds(3100));
            fixture.Clock.DiagnosticNow.ShouldBe(TimeSpan.FromMilliseconds(10100).Ticks);
            diagnostics.CaptureProof(fixture.Sut, fixture.Clock, "at-10.1-before-IsLeader-getter").ProofRequestStart.ShouldBe(TimeSpan.FromSeconds(7).Ticks);
            fixture.Sut.IsLeader.ShouldBeTrue(); fixture.Acquired.ShouldBe(1);
            fixture.ReadStarts.ToArray().ShouldBe(new long[] { 0, TimeSpan.FromSeconds(5).Ticks, TimeSpan.FromSeconds(7).Ticks });
        }
        catch (Exception error) { primary = error; }
        finally
        {
            try { output.WriteLine("BEFORE_PHYSICAL_TEARDOWN"); diagnostics.Dump(fixture); }
            catch (Exception error) { if (primary is null) { primary = error; } else { primary.Data["SecondaryDiagnosticFailure"] = error; } }
            fixture.Clock.ReleaseBarrier(); fixture.Read = _ => Task.FromResult(fixture.Snapshot());
            // All SDK replies in this workload are settled; directly join actual provider teardown.
            try { await fixture.DisposeAsync(); }
            catch (Exception error) { if (primary is null) { primary = error; } else { primary.Data["SecondaryPhysicalTeardownFailure"] = error; } }
            try { output.WriteLine("AFTER_PHYSICAL_TEARDOWN"); diagnostics.Dump(fixture); }
            catch (Exception error) { if (primary is null) { primary = error; } else { primary.Data["SecondaryFinalDiagnosticFailure"] = error; } }
        }
        if (primary is not null) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(primary).Throw(); }
    }

    [Fact]
    public async Task RearmEarlyTimerAndIgnoreBackwardsUtcForLocalExpiry()
    {
        await using var fixture = new KubeTenureFixture();
        var entered = Signal(); var reply = new TaskCompletionSource<V1Lease>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
        await fixture.StartAsync();
        fixture.Clock.FireDeadlineEarly(); await fixture.Clock.WaitForDueAsync(TimeSpan.FromSeconds(10), 2); fixture.Lost.ShouldBe(0); fixture.Sut.IsLeader.ShouldBeTrue();
        fixture.Clock.UtcOffset = TimeSpan.FromHours(-24);
        fixture.Read = _ => { entered.TrySetResult(); return reply.Task; };
        fixture.Clock.Advance(TimeSpan.FromSeconds(5)); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Clock.Advance(TimeSpan.FromSeconds(5)); await fixture.LostSignal.Task.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Sut.IsLeader.ShouldBeFalse(); fixture.Sut.CurrentLeadership.ShouldBeNull();
        fixture.Read = _ => Task.FromResult(fixture.Snapshot()); reply.TrySetResult(fixture.Snapshot());
        await fixture.Sut.StopAsync(CancellationToken.None);
        fixture.Lost.ShouldBe(1);
        }
        finally
        {
            fixture.Read = _ => Task.FromResult(fixture.Snapshot()); reply.TrySetResult(fixture.Snapshot());
        }
    }

    [Fact]
    public async Task RetireImmediatelyWhenAReadFindsARecreatedUidWithoutClearingIt()
    {
        await using var fixture = new KubeTenureFixture(); await fixture.StartAsync();
        fixture.RecreateWithForeignOwner();
        fixture.Clock.Advance(TimeSpan.FromSeconds(5));
        await fixture.LostSignal.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await fixture.Sut.StopAsync(CancellationToken.None);
        fixture.Snapshot().Metadata.Uid.ShouldBe("replacement-uid");
        fixture.Snapshot().Spec.HolderIdentity.ShouldBe("candidate"); fixture.Sut.IsLeader.ShouldBeFalse();
    }

    [Fact]
    public async Task StopCanCancelItsWaitButMustJoinTheActualHeldSdkBeforeDisposalFinishes()
    {
        var fixture = new KubeTenureFixture(); await fixture.StartAsync();
        var reply = new TaskCompletionSource<V1Lease>(TaskCreationOptions.RunContinuationsAsynchronously); var entered = Signal();
        try
        {
        fixture.Read = _ => { entered.TrySetResult(); return reply.Task; };
        fixture.Clock.Advance(TimeSpan.FromSeconds(5)); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var caller = new CancellationTokenSource(); var stopping = fixture.Sut.StopAsync(caller.Token);
        caller.Cancel(); await Should.ThrowAsync<OperationCanceledException>(() => stopping);
        var disposing = fixture.Sut.DisposeAsync().AsTask(); disposing.IsCompleted.ShouldBeFalse();
        fixture.Read = _ => Task.FromResult(fixture.Snapshot()); reply.TrySetResult(fixture.Snapshot());
        await disposing.WaitAsync(TimeSpan.FromSeconds(5));
        var count = fixture.Calls;
        await Should.ThrowAsync<ObjectDisposedException>(() => fixture.Sut.StartAsync(CancellationToken.None));
        await Should.ThrowAsync<ObjectDisposedException>(() => fixture.Sut.GetCandidateHealthAsync(CancellationToken.None));
        fixture.Calls.ShouldBe(count); A.CallTo(() => fixture.Client.Dispose()).MustNotHaveHappened();
        }
        finally
        {
            fixture.Read = _ => Task.FromResult(fixture.Snapshot()); reply.TrySetResult(fixture.Snapshot()); await fixture.DisposeAsync();
        }
    }

    [Fact]
    public async Task StopSealsTheActualInitialAdmissionToLaunchGap()
    {
        var fixture = new KubeTenureFixture();
        var probe = new KubePublicLaunchProbe(fixture.Sut, fixture.Clock, renewal: false, message => output.WriteLine(message));
        ObserveActualLaunchSdk(fixture, probe);
        Task? starting = null; Task? stopping = null; var failures = new List<Exception>();
        try
        {
            fixture.Clock.ArmReadyLaunchBarrier(probe);
            var actualStart = fixture.Sut.StartAsync(CancellationToken.None); starting = actualStart;
            await fixture.Clock.BarrierEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var selected = probe.RequireSelected();
            selected.Generation.ShouldBeGreaterThan(0); selected.Operation.ShouldBe(selected.Acquire);
            selected.State.ShouldBe(0); selected.Completed.ShouldBeFalse(); selected.Published.ShouldBeFalse();
            selected.KernelGateHeldAtSelector.ShouldBeFalse(); selected.RunGateHeldAtSelector.ShouldBeFalse();
            fixture.Calls.ShouldBe(0); probe.RequireNoSdkAfter(0);
            stopping = fixture.Sut.StopAsync(CancellationToken.None);
            var sealedLatch = probe.RequireSameLatchSealedBySynchronousStop();
            sealedLatch.Operation.ShouldBe(selected.Operation); sealedLatch.Generation.ShouldBe(selected.Generation);
            sealedLatch.State.ShouldBe(2); sealedLatch.Completed.ShouldBeTrue(); stopping.IsCompleted.ShouldBeFalse();
            fixture.Calls.ShouldBe(0); probe.RequireNoSdkAfter(0);
            fixture.Clock.ReleaseBarrier();
            var terminal = actualStart.ContinueWith(static _ => { }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            await terminal.WaitAsync(TimeSpan.FromSeconds(5));
            await Should.ThrowAsync<OperationCanceledException>(() => actualStart);
            await stopping;
            fixture.Calls.ShouldBe(0); fixture.OwnershipWrites.ShouldBe(0); fixture.Creates.ShouldBe(0); fixture.Acquired.ShouldBe(0);
            fixture.Sut.IsLeader.ShouldBeFalse(); fixture.Sut.CurrentLeadership.ShouldBeNull(); probe.RequireNoSdkAfter(0);
        }
        catch (Exception error) { failures.Add(error); }
        finally
        {
            try { stopping ??= fixture.Sut.StopAsync(CancellationToken.None); } catch (Exception error) { failures.Add(error); }
            fixture.Clock.ReleaseBarrier();
            if (starting is not null)
            {
                try { await starting; } catch (OperationCanceledException) when (starting.IsCanceled) { } catch (Exception error) { failures.Add(error); }
            }
            if (stopping is not null) { try { await stopping; } catch (Exception error) { failures.Add(error); } }
            try { probe.Dump(); } catch (Exception error) { failures.Add(error); }
            try { await fixture.DisposeAsync(); } catch (Exception error) { failures.Add(error); }
        }
        KubernetesElectionFailures.Throw(failures, "Actual initial Ready-latch proof or physical teardown failed.");
    }

    [Fact]
    public async Task StopSealsTheActualPostRegistrationRenewalLaunchGap()
    {
        var fixture = new KubeTenureFixture();
        var probe = new KubePublicLaunchProbe(fixture.Sut, fixture.Clock, renewal: true, message => output.WriteLine(message));
        var timers = new KubeAmbiguityDiagnostics(message => output.WriteLine(message));
        ObserveActualLaunchSdk(fixture, probe);
        Task? advancing = null; Task? stopping = null; var failures = new List<Exception>(); var checkpoint = 0;
        try
        {
            var purposes = await timers.CalibrateAsync(); await fixture.StartAsync();
            var cadence = await fixture.Clock.WaitForSourceDueAsync(purposes.Delay, TimeSpan.FromSeconds(5), 0);
            fixture.Clock.AssertUniqueSourceMilestone(cadence, purposes.Delay, TimeSpan.FromSeconds(5), 0);
            var baseline = probe.CaptureRenewalBaseline(); baseline.ProofStart.ShouldBe(0); baseline.Operation.ShouldBe(baseline.Acquire);
            checkpoint = probe.SdkCheckpoint; checkpoint.ShouldBe(2); fixture.Calls.ShouldBe(2);
            fixture.Clock.ArmReadyLaunchBarrier(probe);
            advancing = Task.Run(() => fixture.Clock.Advance(TimeSpan.FromSeconds(5)));
            await fixture.Clock.BarrierEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var selected = probe.RequireSelected();
            selected.Kernel.ShouldBe(baseline.Kernel); selected.Generation.ShouldBe(baseline.Generation);
            selected.Acquire.ShouldBe(baseline.Acquire); selected.Operation.ShouldNotBe(selected.Acquire);
            selected.State.ShouldBe(0); selected.Completed.ShouldBeFalse(); selected.Published.ShouldBeTrue(); selected.ProofStart.ShouldBe(0);
            selected.KernelGateHeldAtSelector.ShouldBeFalse(); selected.RunGateHeldAtSelector.ShouldBeFalse();
            fixture.OwnershipWrites.ShouldBe(1); fixture.Calls.ShouldBe(2); probe.RequireNoSdkAfter(checkpoint);
            // The actual barrier is outside both product gates; public Stop must synchronously seal the selected latch.
            stopping = fixture.Sut.StopAsync(CancellationToken.None);
            var sealedLatch = probe.RequireSameLatchSealedBySynchronousStop();
            sealedLatch.Kernel.ShouldBe(selected.Kernel); sealedLatch.Operation.ShouldBe(selected.Operation); sealedLatch.Generation.ShouldBe(selected.Generation);
            sealedLatch.State.ShouldBe(2); sealedLatch.Completed.ShouldBeTrue(); stopping.IsCompleted.ShouldBeFalse();
            fixture.Sut.IsLeader.ShouldBeFalse(); probe.RequireOnlyAuthorizedCleanupAfter(checkpoint);
        }
        catch (Exception error) { failures.Add(error); }
        finally
        {
            try { stopping ??= fixture.Sut.StopAsync(CancellationToken.None); } catch (Exception error) { failures.Add(error); }
            fixture.Clock.ReleaseBarrier();
            if (advancing is not null) { try { await advancing; } catch (Exception error) { failures.Add(error); } }
            if (stopping is not null) { try { await stopping; } catch (Exception error) { failures.Add(error); } }
            try
            {
                if (failures.Count == 0)
                {
                    await fixture.Sut.DisposeAsync(); await fixture.Sut.NotificationsCompleted;
                    probe.RequireOnlyAuthorizedCleanupAfter(checkpoint);
                    fixture.OwnershipWrites.ShouldBe(1); fixture.Acquired.ShouldBe(1); fixture.Lost.ShouldBe(1); fixture.Creates.ShouldBe(0);
                    fixture.Sut.IsLeader.ShouldBeFalse(); fixture.Sut.CurrentLeadership.ShouldBeNull(); fixture.Snapshot().Spec.HolderIdentity.ShouldBeNull();
                }
            }
            catch (Exception error) { failures.Add(error); }
            try { probe.Dump(); timers.Dump(fixture); } catch (Exception error) { failures.Add(error); }
            try { await fixture.DisposeAsync(); } catch (Exception error) { failures.Add(error); }
        }
        KubernetesElectionFailures.Throw(failures, "Actual distinct renewal Ready-latch proof or physical teardown failed.");
    }

    private static void ObserveActualLaunchSdk(KubeTenureFixture fixture, KubePublicLaunchProbe probe)
    {
        fixture.Read = token => { probe.ObserveSdk("Read", null, token); return Task.FromResult(fixture.Snapshot()); };
        fixture.Replace = (lease, token) => { probe.ObserveSdk("Replace", lease.Spec.HolderIdentity, token); return Task.FromResult(fixture.Persist(lease)); };
    }

    [Fact]
    public async Task UnhealthyIntentRetiresLocallyBeforeItsHeldAnnotationAndJoinsThatIntent()
    {
        await using var fixture = new KubeTenureFixture(); await fixture.StartAsync();
        var reply = new TaskCompletionSource<V1Lease>(TaskCreationOptions.RunContinuationsAsynchronously); var entered = Signal(); V1Lease? annotationBody = null;
        try
        {
        fixture.Replace = (lease, _) =>
        {
            if (lease.Metadata.Annotations.Keys.Any(key => key.StartsWith("leader-election.excalibur.io/health-", StringComparison.Ordinal)) && lease.Spec.HolderIdentity is not null)
            { annotationBody = lease; entered.TrySetResult(); return reply.Task; }
            return Task.FromResult(fixture.Persist(lease));
        };
        var updating = fixture.Sut.UpdateHealthAsync(false, new Dictionary<string, string> { ["reason"] = "test" }, CancellationToken.None);
        await fixture.LostSignal.Task.WaitAsync(TimeSpan.FromSeconds(5)); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Sut.IsLeader.ShouldBeFalse(); updating.IsCompleted.ShouldBeFalse();
        var stopping = fixture.Sut.StopAsync(CancellationToken.None); stopping.IsCompleted.ShouldBeFalse();
        reply.TrySetResult(fixture.Persist(annotationBody!)); await updating; await stopping;
        var health = KubernetesLeaseHealthCodec.Deserialize(fixture.Snapshot().Metadata.Annotations["leader-election.excalibur.io/health-candidate"]).ShouldNotBeNull();
        health.IsHealthy.ShouldBeFalse(); health.Metadata!["reason"].ShouldBe("test");
        fixture.Acquired.ShouldBe(1); fixture.Lost.ShouldBe(1);
        }
        finally
        {
            reply.TrySetResult(fixture.Snapshot());
        }
    }

    [Fact]
    public async Task HeldHealthDiscoveryCannotBlockTheOwnershipDeadlineAndIsJoinedOnDispose()
    {
        var fixture = new KubeTenureFixture(); await fixture.StartAsync();
        var healthReply = new TaskCompletionSource<V1Lease>(TaskCreationOptions.RunContinuationsAsynchronously);
        var renewalReply = new TaskCompletionSource<V1Lease>(TaskCreationOptions.RunContinuationsAsynchronously);
        var healthEntered = Signal(); var renewalEntered = Signal(); var cleanupEntered = Signal(); var calls = 0;
        var unavailable = new IOException("renew unavailable");
        try
        {
            fixture.Read = _ =>
            {
                switch (Interlocked.Increment(ref calls))
                {
                    case 1: healthEntered.TrySetResult(); return healthReply.Task;
                    case 2: renewalEntered.TrySetResult(); return renewalReply.Task;
                    default: cleanupEntered.TrySetResult(); return Task.FromResult(fixture.Snapshot());
                }
            };
            var health = fixture.Sut.GetCandidateHealthAsync(CancellationToken.None);
            await healthEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            fixture.Clock.Advance(TimeSpan.FromSeconds(5));
            await renewalEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            fixture.Clock.Advance(TimeSpan.FromSeconds(5));
            await fixture.LostSignal.Task.WaitAsync(TimeSpan.FromSeconds(5));
            fixture.Sut.IsLeader.ShouldBeFalse();
            // Both admitted SDK reads deliberately ignore cancellation until their own replies settle.
            var disposing = fixture.Sut.DisposeAsync().AsTask();
            disposing.IsCompleted.ShouldBeFalse();
            health.IsCompleted.ShouldBeFalse();
            renewalReply.TrySetException(unavailable);
            // A separate fresh-read milestone proves renewal settlement did not revive authority
            // and that cleanup can proceed while discovery is still held.
            await cleanupEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            fixture.Sut.IsLeader.ShouldBeFalse();
            disposing.IsCompleted.ShouldBeFalse();
            health.IsCompleted.ShouldBeFalse();
            healthReply.TrySetResult(fixture.Snapshot());
            await health;
            await disposing.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            fixture.Read = _ => Task.FromResult(fixture.Snapshot());
            renewalReply.TrySetException(unavailable);
            healthReply.TrySetResult(fixture.Snapshot());
            await fixture.DisposeAsync();
        }
    }

    [Fact]
    public async Task ASecondLiveCandidateWithTheSameDisplayIdCannotStealThePrivateTenure()
    {
        await using var fixture = new KubeTenureFixture(); await fixture.StartAsync();
        var original = fixture.Snapshot();
        await using var second = fixture.CreateProvider();
        await second.StartAsync(CancellationToken.None);
        second.IsLeader.ShouldBeFalse(); fixture.Sut.IsLeader.ShouldBeTrue();
        fixture.Snapshot().Metadata.Annotations[KubernetesLeaseProtocol.OwnerAnnotation].ShouldBe(original.Metadata.Annotations[KubernetesLeaseProtocol.OwnerAnnotation]);
        fixture.Snapshot().Spec.LeaseTransitions.ShouldBe(original.Spec.LeaseTransitions);
    }

    [Fact]
    public async Task LateOldReplaceAndCleanupCannotClearASameDisplaySuccessor()
    {
        await using var fixture = new KubeTenureFixture(); await fixture.StartAsync();
        var oldOwner = fixture.Snapshot().Metadata.Annotations[KubernetesLeaseProtocol.OwnerAnnotation];
        V1Lease? delayed = null; var entered = Signal();
        var response = new TaskCompletionSource<V1Lease>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Replace = (body, _) =>
        {
            if (body.Spec.HolderIdentity is not null && body.Metadata.Annotations.TryGetValue(KubernetesLeaseProtocol.OwnerAnnotation, out var owner) && owner == oldOwner)
            { delayed = body; entered.TrySetResult(); return response.Task; }
            return Task.FromResult(fixture.Persist(body));
        };
        try
        {
            await fixture.Clock.WaitForDueAsync(TimeSpan.FromSeconds(5), 1);
            fixture.Clock.Advance(TimeSpan.FromSeconds(5)); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            fixture.Clock.Advance(TimeSpan.FromSeconds(16)); await fixture.LostSignal.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var stopping = fixture.Sut.StopAsync(CancellationToken.None);
            await using var successor = fixture.CreateProvider();
            await successor.StartAsync(CancellationToken.None);
            var successorProof = successor.CurrentLeadership.ShouldNotBeNull();
            successorProof.FencingToken.ShouldBe(2);
            try { response.TrySetResult(fixture.Persist(delayed!)); }
            catch (Exception error) { response.TrySetException(error); }
            await stopping.WaitAsync(TimeSpan.FromSeconds(5));
            successor.IsLeader.ShouldBeTrue();
            fixture.Snapshot().Spec.LeaseTransitions.ShouldBe(2);
            fixture.Snapshot().Metadata.Annotations[KubernetesLeaseProtocol.OwnerAnnotation].ShouldNotBe(oldOwner);
        }
        finally
        {
            fixture.Replace = (body, _) => Task.FromResult(fixture.Persist(body));
            response.TrySetException(KubeTenureFixture.Status(HttpStatusCode.Conflict));
        }
    }

    [Fact]
    public async Task CallbackStopAndDisposeRemainAcyclicAndTerminalNotificationsJoinQueuedLoss()
    {
        await using var fixture = new KubeTenureFixture();
        var transcript = new ConcurrentQueue<string>(); var callback = Signal();
        fixture.Sut.BecameLeader += (_, _) =>
        {
            transcript.Enqueue("acquired");
#pragma warning disable RS0030 // Synchronous consumer callback is the actual self-join adversary.
            fixture.Sut.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
#pragma warning restore RS0030
        };
        fixture.Sut.LostLeadership += (_, _) =>
        {
            transcript.Enqueue("lost");
#pragma warning disable RS0030 // Resource disposal may complete inside callback; notification completion must wait for callback return.
            fixture.Sut.Dispose();
#pragma warning restore RS0030
            fixture.Sut.NotificationsCompleted.IsCompleted.ShouldBeFalse(); callback.TrySetResult();
        };
        await fixture.Sut.StartAsync(CancellationToken.None);
        await callback.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await fixture.Sut.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(5));
        transcript.ToArray().ShouldBe(ExpectedCallbackOrder);
    }

    [Fact]
    public async Task PreserveAFailedReleaseAcrossRepeatedDisposalAndRefuseUnsafeRestart()
    {
        var fixture = new KubeTenureFixture(); await fixture.StartAsync();
        var failure = new IOException("release cannot be confirmed");
        fixture.Replace = (lease, _) => lease.Spec.HolderIdentity is null ? Task.FromException<V1Lease>(failure) : Task.FromResult(fixture.Persist(lease));
        await Should.ThrowAsync<IOException>(() => fixture.Sut.StopAsync(CancellationToken.None));
        fixture.Sut.IsLeader.ShouldBeFalse();
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.Sut.StartAsync(CancellationToken.None));
        var first = fixture.Sut.DisposeAsync().AsTask(); var second = fixture.Sut.DisposeAsync().AsTask();
        first.ShouldBeSameAs(second);
        await Should.ThrowAsync<IOException>(() => first);
        await Should.ThrowAsync<IOException>(() => second);
        fixture.Clock.Dispose();
    }

    [Fact]
    public async Task FailClosedOnExhaustedNativeCounterWithoutPublishingOrLosingAnUnpublishedTenure()
    {
        await using var fixture = new KubeTenureFixture(); fixture.SetUnheldTransitions(int.MaxValue);
        var failed = new TaskCompletionSource<Excalibur.Dispatch.LeaderElection.LeaderElectionAcquisitionFailedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Sut.AcquisitionFailed += (_, args) => failed.TrySetResult(args);
        await fixture.Sut.StartAsync(CancellationToken.None);
        (await failed.Task.WaitAsync(TimeSpan.FromSeconds(5))).Reason.ShouldContain("exhausted");
        await fixture.Sut.StopAsync(CancellationToken.None);
        fixture.Acquired.ShouldBe(0); fixture.Lost.ShouldBe(0); fixture.OwnershipWrites.ShouldBe(0);
    }

    [Fact]
    public async Task KeepReadOnlyDiscoveryAvailableAfterJoinedStopWithoutReusingADisposedRunToken()
    {
        await using var fixture = new KubeTenureFixture(); await fixture.StartAsync();
        await fixture.Sut.StopAsync(CancellationToken.None);
        var before = fixture.Calls;
        (await fixture.Sut.GetCandidateHealthAsync(CancellationToken.None)).ShouldBeEmpty();
        fixture.Calls.ShouldBe(before + 1); fixture.Sut.IsLeader.ShouldBeFalse();
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict, 1)][InlineData(HttpStatusCode.NotFound, 1)]
    [InlineData(HttpStatusCode.Unauthorized, 1)][InlineData(HttpStatusCode.Forbidden, 1)]
    [InlineData(HttpStatusCode.RequestTimeout, 4)][InlineData(HttpStatusCode.TooManyRequests, 4)]
    [InlineData(HttpStatusCode.InternalServerError, 4)][InlineData(HttpStatusCode.ServiceUnavailable, 4)]
    public async Task PreserveConfiguredRetriesOnlyForNativeTransientStatuses(HttpStatusCode status, int attempts)
    {
        await using var fixture = new KubeTenureFixture();
        fixture.Replace = (_, _) => Task.FromException<V1Lease>(KubeTenureFixture.Status(status));
        await using var provider = fixture.CreateProvider(maxRetries: 3);
        await provider.StartAsync(CancellationToken.None);
        await provider.StopAsync(CancellationToken.None);
        fixture.OwnershipWrites.ShouldBe(attempts); provider.IsLeader.ShouldBeFalse();
    }

    [Fact]
    public async Task RecoverTheColdCreateConflictByReadingTheOtherCreatorsNativeRecord()
    {
        await using var fixture = new KubeTenureFixture(); var reads = 0;
        fixture.Read = _ => Interlocked.Increment(ref reads) == 1
            ? Task.FromException<V1Lease>(KubeTenureFixture.Status(HttpStatusCode.NotFound))
            : Task.FromResult(fixture.Snapshot());
        await fixture.StartAsync();
        fixture.Creates.ShouldBe(1); fixture.OwnershipWrites.ShouldBe(1); fixture.Sut.IsLeader.ShouldBeTrue();
    }

    [Fact]
    public async Task GiveBackAnUnusableNativeWriteWithoutPublishingOrEmittingAnUnpublishedLoss()
    {
        await using var fixture = new KubeTenureFixture();
        fixture.Replace = (body, _) =>
        {
            var persisted = fixture.Persist(body);
            if (body.Spec.HolderIdentity is not null) { persisted.Spec.LeaseTransitions = 0; }
            return Task.FromResult(persisted);
        };
        var failed = new TaskCompletionSource<Excalibur.Dispatch.LeaderElection.LeaderElectionAcquisitionFailedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Sut.AcquisitionFailed += (_, args) => failed.TrySetResult(args);
        await fixture.Sut.StartAsync(CancellationToken.None);
        (await failed.Task.WaitAsync(TimeSpan.FromSeconds(5))).Reason.ShouldContain("usable fencing");
        await fixture.Sut.StopAsync(CancellationToken.None);
        fixture.Acquired.ShouldBe(0); fixture.Lost.ShouldBe(0); fixture.Snapshot().Spec.HolderIdentity.ShouldBeNull();
    }

    [Fact]
    public async Task RejectSameElectionLifecycleReentryInsideActualNativeHealthSdkBeforeSealing()
    {
        await using var fixture = new KubeTenureFixture(); await fixture.StartAsync(); var calls = 0;
        var outcome = Signal();
        fixture.Read = async nativeToken =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                try
                {
                    await Should.ThrowAsync<InvalidOperationException>(() => fixture.Sut.StartAsync(CancellationToken.None));
                    Should.Throw<InvalidOperationException>(() => { _ = fixture.Sut.StopAsync(CancellationToken.None); });
                    Should.Throw<InvalidOperationException>(() => { _ = fixture.Sut.DisposeAsync().AsTask(); });
                    Should.Throw<InvalidOperationException>(() => fixture.Sut.Dispose());
                    fixture.Sut.IsLeader.ShouldBeTrue(); outcome.TrySetResult();
                }
                catch (Exception error) { outcome.TrySetException(error); }
            }
            return fixture.Snapshot();
        };
        await fixture.Sut.GetCandidateHealthAsync(CancellationToken.None);
        await outcome.Task.WaitAsync(TimeSpan.FromSeconds(5)); // Discovery intentionally swallows SDK faults; retain this assertion independently.
        fixture.Sut.IsLeader.ShouldBeTrue();
    }

    [Fact]
    public async Task KeepSdkCancellationCallbackGuardActiveThroughActualCancellationJoin()
    {
        await using var fixture = new KubeTenureFixture(); await fixture.StartAsync(); var calls = 0;
        var entered = Signal(); var outcome = Signal();
        var reply = new TaskCompletionSource<V1Lease>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationTokenRegistration registration = default;
        Task? attemptedGet = null; Task? attemptedUpdate = null;
        fixture.Read = token =>
        {
            if (Interlocked.Increment(ref calls) != 1) { return Task.FromResult(fixture.Snapshot()); }
            registration = token.Register(() =>
            {
                try
                {
                    Should.Throw<InvalidOperationException>(() => { _ = fixture.Sut.StopAsync(CancellationToken.None); });
                    attemptedGet = fixture.Sut.GetCandidateHealthAsync(CancellationToken.None);
                    attemptedUpdate = fixture.Sut.UpdateHealthAsync(true, null, CancellationToken.None);
#pragma warning disable RS0030 // Actual SDK cancellation callback probes a bounded synchronous wait; missing guard fails with Timeout rather than hanging the suite.
                    Should.Throw<InvalidOperationException>(() => attemptedGet.WaitAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult());
                    Should.Throw<InvalidOperationException>(() => attemptedUpdate.WaitAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult());
#pragma warning restore RS0030
                    Should.Throw<InvalidOperationException>(() => { _ = fixture.Sut.DisposeAsync().AsTask(); });
                    Should.Throw<InvalidOperationException>(() => fixture.Sut.Dispose());
                    outcome.TrySetResult();
                }
                catch (Exception error) { outcome.TrySetException(error); }
                finally { reply.TrySetCanceled(token); }
            });
            entered.TrySetResult(); return reply.Task;
        };
        var reading = fixture.Sut.GetCandidateHealthAsync(CancellationToken.None);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await fixture.Sut.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            await outcome.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Should.ThrowAsync<OperationCanceledException>(() => reading);
            fixture.Read = _ => Task.FromResult(fixture.Snapshot());
            await fixture.Sut.StartAsync(CancellationToken.None); fixture.Sut.IsLeader.ShouldBeTrue();
        }
        finally
        {
            reply.TrySetCanceled(); await registration.DisposeAsync(); fixture.Read = _ => Task.FromResult(fixture.Snapshot());
            foreach (var attempted in new[] { attemptedGet, attemptedUpdate })
            {
                if (attempted is null) { continue; }
                try { await attempted.WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (InvalidOperationException) { } // Exact expected refusal already independently asserted above.
            }
        }
    }

    [Fact]
    public async Task AllowIndependentElectionStopFromAnotherElectionsActualSdkCallback()
    {
        await using var first = new KubeTenureFixture(); await using var second = new KubeTenureFixture();
        await first.StartAsync(); await second.StartAsync(); var calls = 0;
        first.Read = async _ =>
        {
            if (Interlocked.Increment(ref calls) == 1) { await second.Sut.StopAsync(CancellationToken.None); }
            return first.Snapshot();
        };
        await first.Sut.GetCandidateHealthAsync(CancellationToken.None);
        first.Sut.IsLeader.ShouldBeTrue(); second.Sut.IsLeader.ShouldBeFalse();
    }

    [Fact]
    public async Task RejectAnActiveAncestorElectionEvenWhenAnotherSdkOwnerIsTheTopScope()
    {
        await using var first = new KubeTenureFixture(); await using var second = new KubeTenureFixture();
        await first.StartAsync(); await second.StartAsync(); var firstCalls = 0; var secondCalls = 0; var outcome = Signal();
        second.Read = nativeToken =>
        {
            if (Interlocked.Increment(ref secondCalls) == 1)
            {
                try { Should.Throw<InvalidOperationException>(() => { _ = first.Sut.StopAsync(CancellationToken.None); }); outcome.TrySetResult(); }
                catch (Exception error) { outcome.TrySetException(error); }
            }
            return Task.FromResult(second.Snapshot());
        };
        first.Read = async nativeToken =>
        {
            if (Interlocked.Increment(ref firstCalls) == 1) { _ = await second.Sut.GetCandidateHealthAsync(CancellationToken.None); }
            return first.Snapshot();
        };
        await first.Sut.GetCandidateHealthAsync(CancellationToken.None);
        await outcome.Task.WaitAsync(TimeSpan.FromSeconds(5));
        first.Sut.IsLeader.ShouldBeTrue(); second.Sut.IsLeader.ShouldBeTrue();
    }

    [Fact]
    public async Task ExpireSdkScopeForEscapedContextAfterActualSdkAndCancellationSourceCleanup()
    {
        await using var fixture = new KubeTenureFixture(); await fixture.StartAsync();
        var release = Signal(); Task escaped = Task.CompletedTask; var calls = 0;
        fixture.Read = _ =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
#pragma warning disable RS0030 // Deliberately retain SDK ExecutionContext beyond native completion to test guard expiration.
                escaped = Task.Run(async () => { await release.Task; await fixture.Sut.StopAsync(CancellationToken.None); });
#pragma warning restore RS0030
            }
            return Task.FromResult(fixture.Snapshot());
        };
        await fixture.Sut.GetCandidateHealthAsync(CancellationToken.None);
        release.TrySetResult(); await escaped.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Sut.IsLeader.ShouldBeFalse();
    }

    [Theory][InlineData(1L, 1)][InlineData(10000001L, 2)]
    public void CeilNativeLeaseSecondsWithoutFloatingPointTruncation(long ticks, int expected) =>
        KubernetesLeaseProtocol.ToNativeSeconds(TimeSpan.FromTicks(ticks)).ShouldBe(expected);

    [Fact]
    public void RejectOneTickPastTheNativeInt32SecondDomain() =>
        Should.Throw<ArgumentOutOfRangeException>(() => KubernetesLeaseProtocol.ToNativeSeconds(TimeSpan.FromTicks((long)int.MaxValue * TimeSpan.TicksPerSecond + 1)));

    private static async Task AssertCallerTimeoutAsync(Task actualCaller)
    {
        // Wait for terminal status independently; an outer watchdog timeout is not the expected error.
        var terminal = actualCaller.ContinueWith(static _ => { }, CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        await terminal.WaitAsync(TimeSpan.FromSeconds(5));
        actualCaller.IsCompleted.ShouldBeTrue();
        await Should.ThrowAsync<TimeoutException>(() => actualCaller);
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
