// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Net;
using k8s.Models;
namespace Excalibur.LeaderElection.Tests.Kubernetes;
[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
[Trait("Pattern", "Regression")]
public sealed class KubernetesLeaderNamesItsFencingTokenShould
{
    [Fact]
    public async Task NotReportLeadershipWithoutAFencingTokenAfterItsAppliedWriteIsReportedAsAConflict()
    {
        await using var f = new KubeTenureFixture();
        f.Replace = (body, _) => { var written = f.Persist(body); return body.Spec.HolderIdentity is null ? Task.FromResult(written) : Task.FromException<V1Lease>(KubeTenureFixture.Status(HttpStatusCode.Conflict)); };
        var failed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Sut.AcquisitionFailed += (_, _) => failed.TrySetResult();
        await f.Sut.StartAsync(CancellationToken.None); await failed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        f.Sut.CurrentLeadership.ShouldBeNull(); f.Sut.IsLeader.ShouldBeFalse();
        f.Replace = (body, _) => Task.FromResult(f.Persist(body));
        await f.Sut.StopAsync(CancellationToken.None);
        await f.Sut.DisposeAsync();
        await f.Sut.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(5));
        f.Acquired.ShouldBe(0);
        f.Snapshot().Spec.HolderIdentity.ShouldBeNull();
    }

    [Fact]
    public async Task ReportANonNullFencingTokenForANormallyAcquiredLeadership()
    {
        await using var f = new KubeTenureFixture(); await f.StartAsync();
        f.Sut.CurrentLeadership.ShouldNotBeNull().FencingToken.ShouldBe(f.Snapshot().Spec.LeaseTransitions!.Value);
        f.Acquired.ShouldBe(1);
    }
}
