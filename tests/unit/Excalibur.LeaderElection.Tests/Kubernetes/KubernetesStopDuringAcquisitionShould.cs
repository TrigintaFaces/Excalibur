// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Net;
using k8s.Models;
namespace Excalibur.LeaderElection.Tests.Kubernetes;

/// <summary>
/// CHARACTERIZATION, NOT A REGRESSION ARM. Unlike Postgres and Redis, this provider emits no
/// contention diagnostic in its not-acquired branch, and its two remaining effects are both
/// suppressed once a stop has been requested -- ReportObservation returns early on the kernel's
/// stopped flag, and Retire is a no-op because RequestStop already moved the phase to Retiring.
/// So a stop during acquisition produces no wrong observable output here and there is nothing for
/// a RED-proven arm to lock. These arms pin that, so that adding one later cannot pass unnoticed.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
[Trait("Pattern", "Regression")]
public sealed class KubernetesStopDuringAcquisitionShould
{
    /// <summary>GREEN before and after the fix: the stop path is already silent.</summary>
    [Fact]
    public async Task ReportNoContentionWhenTheStopSealedTheLatchBeforeItLaunched()
    {
        await using var fixture = new KubeTenureFixture();
        var contention = 0;
        fixture.Sut.AcquisitionFailed += (_, _) => Interlocked.Increment(ref contention);
        // Park the actor on its request-start capture: the one statement between
        // Kernel.BeginAcquisition and Acquire.TryLaunch.
        fixture.Clock.ArmTimestampBarrier(1, actorOnly: true);
        var starting = fixture.Sut.StartAsync(CancellationToken.None);
        await fixture.Clock.BarrierEntered.Task.WaitAsync(Backstop);
        var stopping = fixture.Sut.StopAsync(CancellationToken.None);
        fixture.Clock.ReleaseBarrier();
        _ = await Record.ExceptionAsync(() => starting.WaitAsync(Backstop));
        await stopping.WaitAsync(Backstop);
        await fixture.Sut.DisposeAsync();
        await fixture.Sut.NotificationsCompleted.WaitAsync(Backstop);

        // NON-VACUITY GUARD. Proves the sealed-unlaunched state was constructed rather than the
        // never-begun state: a launched attempt always reads the Lease before anything else.
        fixture.Calls.ShouldBe(0, "the sealed latch must refuse the launch, so the API is never called");
        fixture.Acquired.ShouldBe(0);
        Volatile.Read(ref contention).ShouldBe(0, "a stop that sealed the latch is not a lost race");
    }

    /// <summary>
    /// POSITIVE CONTROL for the arm above: the AcquisitionFailed event does fire on a genuine
    /// race loss, so the zero asserted there is a real silence and not a dead subscription.
    /// </summary>
    [Fact]
    public async Task StillReportContentionWhenTheLeaseWriteConflicts()
    {
        await using var fixture = new KubeTenureFixture();
        var contention = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Replace = (_, _) => Task.FromException<V1Lease>(KubeTenureFixture.Status(HttpStatusCode.Conflict));
        fixture.Sut.AcquisitionFailed += (_, _) => contention.TrySetResult();
        await fixture.Sut.StartAsync(CancellationToken.None).WaitAsync(Backstop);
        await contention.Task.WaitAsync(Backstop);
        fixture.Calls.ShouldBeGreaterThan(0, "the attempt must actually have asked Kubernetes");
        fixture.Acquired.ShouldBe(0);
    }

    // Generous on purpose. This suite already carries 5s real-clock watchdogs that flake on a
    // loaded 2-CPU container; every wait here is on a signal, never on the clock.
    private static TimeSpan Backstop => TimeSpan.FromSeconds(30);
}
