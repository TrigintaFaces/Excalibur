// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Net;
using k8s.Models;
namespace Excalibur.LeaderElection.Tests.Kubernetes;
[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
[Trait("Pattern", "Regression")]
public sealed class KubernetesRecoveredTenureHasAUsableTokenShould
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task NeverAdoptASameDisplayOwnersUnconfirmedPrivateTenure(int token)
    {
        await using var f = new KubeTenureFixture();
        var foreign = f.Snapshot(); foreign.Spec.HolderIdentity = "candidate";
        foreign.Spec.LeaseTransitions = token; foreign.Spec.RenewTime = f.Clock.GetUtcNow().UtcDateTime;
        foreign.Metadata.Annotations[KubernetesLeaseProtocol.OwnerAnnotation] = "someone-elses-private-owner";
        f.Persist(foreign);
        var failed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Sut.AcquisitionFailed += (_, _) => failed.TrySetResult();
        await f.Sut.StartAsync(CancellationToken.None); await failed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        f.Sut.CurrentLeadership.ShouldBeNull(); f.Sut.IsLeader.ShouldBeFalse();
        await f.Sut.StopAsync(CancellationToken.None);
        await f.Sut.DisposeAsync();
        await f.Sut.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(5));
        f.Acquired.ShouldBe(0); f.OwnershipWrites.ShouldBe(0);
        f.Snapshot().Metadata.Annotations[KubernetesLeaseProtocol.OwnerAnnotation].ShouldBe("someone-elses-private-owner");
    }

    [Fact]
    public async Task MintANewTenureAfterTheSameDisplayForeignOwnerExpires()
    {
        await using var f = new KubeTenureFixture();
        var foreign = f.Snapshot(); foreign.Spec.HolderIdentity = "candidate"; foreign.Spec.LeaseTransitions = 41;
        foreign.Spec.RenewTime = f.Clock.GetUtcNow().AddHours(-1).UtcDateTime;
        foreign.Metadata.Annotations[KubernetesLeaseProtocol.OwnerAnnotation] = "old-private-owner";
        f.Persist(foreign); await f.StartAsync();
        f.Sut.CurrentLeadership.ShouldNotBeNull().FencingToken.ShouldBe(42);
        f.Snapshot().Metadata.Annotations[KubernetesLeaseProtocol.OwnerAnnotation].ShouldNotBe("old-private-owner");
    }
}
