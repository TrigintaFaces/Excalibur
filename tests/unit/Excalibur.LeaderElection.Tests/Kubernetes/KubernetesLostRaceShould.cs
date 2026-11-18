// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Net;
using k8s.Models;
namespace Excalibur.LeaderElection.Tests.Kubernetes;
[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
[Trait("Pattern", "Regression")]
public sealed class KubernetesLostRaceShould
{
    [Fact]
    public async Task NotClaimLeadershipWhenTheWriteConflicts()
    {
        await using var f = new KubeTenureFixture();
        var failed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Replace = (_, _) => Task.FromException<V1Lease>(KubeTenureFixture.Status(HttpStatusCode.Conflict));
        f.Sut.AcquisitionFailed += (_, _) => failed.TrySetResult();
        await f.Sut.StartAsync(CancellationToken.None); await failed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        f.Sut.CurrentLeadership.ShouldBeNull(); f.Sut.IsLeader.ShouldBeFalse();
        await f.Sut.DisposeAsync();
        await f.Sut.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(5));
        f.OwnershipWrites.ShouldBe(1); f.Acquired.ShouldBe(0);
    }

    [Fact]
    public async Task FailClosedWhenTheHolderCannotBeConfirmed()
    {
        await using var f = new KubeTenureFixture();
        var failed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Replace = (lease, _) => { var written = f.Persist(lease); return lease.Spec.HolderIdentity is null ? Task.FromResult(written) : Task.FromException<V1Lease>(new IOException("applied write response lost")); };
        f.Sut.AcquisitionFailed += (_, _) => failed.TrySetResult();
        await f.Sut.StartAsync(CancellationToken.None); await failed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        f.Sut.CurrentLeadership.ShouldBeNull(); f.Sut.IsLeader.ShouldBeFalse();
        // Actual cleanup must confirm and clear only this generation's persisted private owner.
        f.Replace = (lease, _) => Task.FromResult(f.Persist(lease));
        await f.Sut.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        await f.Sut.DisposeAsync();
        await f.Sut.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(5));
        f.Acquired.ShouldBe(0);
        f.Snapshot().Spec.HolderIdentity.ShouldBeNull();
    }

    [Fact]
    public async Task StillAcquireWhenTheWriteSucceeds()
    {
        await using var f = new KubeTenureFixture(); await f.StartAsync();
        f.Acquired.ShouldBe(1); f.OwnershipWrites.ShouldBe(1);
        f.Sut.CurrentLeadership.ShouldNotBeNull().FencingToken.GetValueOrDefault().ShouldBeGreaterThan(0L);
    }
}
