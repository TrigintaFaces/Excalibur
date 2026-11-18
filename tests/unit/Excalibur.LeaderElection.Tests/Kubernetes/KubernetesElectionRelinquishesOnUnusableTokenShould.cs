// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Net;
using k8s.Models;
namespace Excalibur.LeaderElection.Tests.Kubernetes;
[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
[Trait("Pattern", "Regression")]
public sealed class KubernetesElectionRelinquishesOnUnusableTokenShould
{
    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task RefuseAnUnusableConfirmedWriteAndReleaseOnlyItsActualOwner(int? returnedToken)
    {
        await using var f = new KubeTenureFixture();
        f.Replace = (body, _) => { var result = f.Persist(body); if (body.Spec.HolderIdentity is not null) { result.Spec.LeaseTransitions = returnedToken; } return Task.FromResult(result); };
        var failed = new TaskCompletionSource<LeaderElectionAcquisitionFailedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Sut.AcquisitionFailed += (_, args) => failed.TrySetResult(args);
        await f.Sut.StartAsync(CancellationToken.None);
        (await failed.Task.WaitAsync(TimeSpan.FromSeconds(5))).Reason.ShouldContain("usable fencing");
        f.Sut.CurrentLeadership.ShouldBeNull(); f.Sut.IsLeader.ShouldBeFalse();
        await f.Sut.StopAsync(CancellationToken.None);
        await f.Sut.DisposeAsync();
        await f.Sut.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(5));
        f.Acquired.ShouldBe(0);
        f.Snapshot().Spec.HolderIdentity.ShouldBeNull();
    }

    [Fact]
    public async Task LeadWhenTheLeaseCarriesAPositiveTransitionCount()
    {
        await using var f = new KubeTenureFixture(); await f.StartAsync();
        f.Sut.CurrentLeadership.ShouldNotBeNull().FencingToken.GetValueOrDefault().ShouldBeGreaterThan(0L);
        f.Acquired.ShouldBe(1);
    }
}
