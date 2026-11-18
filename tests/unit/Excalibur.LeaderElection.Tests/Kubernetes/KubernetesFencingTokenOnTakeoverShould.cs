// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Net;
using k8s.Models;
namespace Excalibur.LeaderElection.Tests.Kubernetes;
[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
[Trait("Pattern", "Regression")]
public sealed class KubernetesFencingTokenOnTakeoverShould
{
    [Fact]
    public async Task AdvanceTheFencingTokenWhenItTakesTheLeaseFromAnotherHolder()
    {
        await using var f = new KubeTenureFixture();
        var foreign = f.Snapshot(); foreign.Spec.HolderIdentity = "other"; foreign.Spec.LeaseTransitions = 40;
        foreign.Spec.RenewTime = f.Clock.GetUtcNow().AddHours(-1).UtcDateTime;
        foreign.Metadata.Annotations[KubernetesLeaseProtocol.OwnerAnnotation] = "other-private-owner";
        f.Persist(foreign);
        await f.StartAsync();
        var current = f.Snapshot(); current.Spec.HolderIdentity.ShouldBe("candidate");
        current.Spec.LeaseTransitions.ShouldBe(41);
        current.Metadata.Annotations[KubernetesLeaseProtocol.OwnerAnnotation].ShouldNotBe("other-private-owner");
        f.Sut.CurrentLeadership.ShouldNotBeNull().FencingToken.ShouldBe(41);
    }

    [Fact]
    public async Task NotAdvanceTheFencingTokenWhenItRenewsItsOwnLease()
    {
        await using var f = new KubeTenureFixture(); await f.StartAsync(); var first = f.Snapshot();
        var renewed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Replace = (lease, _) => { var result = f.Persist(lease); renewed.TrySetResult(); return Task.FromResult(result); };
        await f.Clock.WaitForDueAsync(TimeSpan.FromSeconds(5), 1); f.Clock.Advance(TimeSpan.FromSeconds(5));
        await renewed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var current = f.Snapshot(); current.Spec.LeaseTransitions.ShouldBe(first.Spec.LeaseTransitions);
        current.Metadata.Annotations[KubernetesLeaseProtocol.OwnerAnnotation].ShouldBe(first.Metadata.Annotations[KubernetesLeaseProtocol.OwnerAnnotation]);
        f.Acquired.ShouldBe(1);
    }
}
