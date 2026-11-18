// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using k8s.Models;
namespace Excalibur.LeaderElection.Tests.Kubernetes;

[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
[Trait("Pattern", "Regression")]
public sealed class KubernetesHealthStepDownShould
{
    [Fact]
    public async Task ReleaseLeadershipAfterAnUnhealthyUpdateWithoutReenteringTheHeldLeaseSemaphore()
    {
        await using var f = new KubeTenureFixture();
        await f.StartAsync();
        var acquired = f.Sut.CurrentLeadership;
        acquired.ShouldNotBeNull();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Replace = (lease, _) =>
        {
            if (lease.Spec.HolderIdentity is null) { release.TrySetResult(); }
            return Task.FromResult(f.Persist(lease));
        };
        await f.Sut.UpdateHealthAsync(false, new Dictionary<string, string> { ["reason"] = "test" }, CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(5));
        await f.LostSignal.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await f.Sut.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        await release.Task.WaitAsync(TimeSpan.FromSeconds(5));
        f.Sut.CurrentLeadership.ShouldBeNull();
        f.Lost.ShouldBe(1);
        f.Snapshot().Spec.HolderIdentity.ShouldBeNull();
        await f.Sut.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        f.Lost.ShouldBe(1);
    }
}
