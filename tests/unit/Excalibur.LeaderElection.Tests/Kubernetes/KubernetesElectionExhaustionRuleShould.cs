// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Net;
using k8s.Models;
namespace Excalibur.LeaderElection.Tests.Kubernetes;
[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
[Trait("Pattern", "Regression")]
public sealed class KubernetesElectionExhaustionRuleShould
{
    [Fact]
    public async Task RelinquishWithoutWritingWhenTheTransitionCounterIsAtInt32Max()
    {
        await using var f = new KubeTenureFixture(); f.SetUnheldTransitions(int.MaxValue);
        var failed = new TaskCompletionSource<LeaderElectionAcquisitionFailedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Sut.AcquisitionFailed += (_, args) => failed.TrySetResult(args);
        await f.Sut.StartAsync(CancellationToken.None);
        (await failed.Task.WaitAsync(TimeSpan.FromSeconds(5))).Reason.ShouldContain("exhausted");
        f.Sut.CurrentLeadership.ShouldBeNull(); f.Sut.IsLeader.ShouldBeFalse();
        await f.Sut.DisposeAsync();
        await f.Sut.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(5));
        f.OwnershipWrites.ShouldBe(0); f.Acquired.ShouldBe(0);
    }

    [Fact]
    public async Task TakeTheLeaseAndAdvanceTheCounterWhenOneValueRemains()
    {
        await using var f = new KubeTenureFixture(); f.SetUnheldTransitions(int.MaxValue - 1);
        await f.StartAsync();
        f.Snapshot().Spec.LeaseTransitions.ShouldBe(int.MaxValue);
        f.Sut.CurrentLeadership.ShouldNotBeNull().FencingToken.ShouldBe(int.MaxValue);
    }

    [Fact]
    public async Task AgreeWithTheProviderAtTheBoundaryWhereOneValueRemains()
    {
        await using var actorStore = new KubeTenureFixture();
        await using var adapterStore = new KubeTenureFixture();
        actorStore.SetUnheldTransitions(int.MaxValue - 1);
        adapterStore.SetUnheldTransitions(int.MaxValue - 1);
        var provider = new KubernetesFencingTokenProvider(adapterStore.Client, Options.Create(new KubernetesLeaderElectionOptions
        { Namespace = "test", LeaseName = "orders", InstanceId = "candidate" }));
        // The actor advances the native counter in its holder CAS; this adapter reads the native count.
        var observed = await provider.GetTokenAsync("orders", CancellationToken.None);
        observed.ShouldBe(int.MaxValue - 1);
        adapterStore.Snapshot().Spec.LeaseTransitions.ShouldBe(int.MaxValue - 1);
        adapterStore.OwnershipWrites.ShouldBe(0);
        await actorStore.StartAsync();
        actorStore.Snapshot().Spec.LeaseTransitions.ShouldBe(int.MaxValue);
        actorStore.Sut.CurrentLeadership.ShouldNotBeNull().FencingToken.ShouldBe(int.MaxValue);
        adapterStore.SetUnheldTransitions(int.MaxValue);
        (await provider.GetTokenAsync("orders", CancellationToken.None)).ShouldBe((long)int.MaxValue);
        (await provider.ValidateTokenAsync("orders", int.MaxValue, CancellationToken.None)).ShouldBeTrue();
        KubernetesFencingTokenProvider.IsTokenDomainExhausted(int.MaxValue).ShouldBeTrue();
        ((object)provider is Excalibur.Dispatch.LeaderElection.Fencing.IFencingTokenIssuer).ShouldBeFalse();
    }
}
