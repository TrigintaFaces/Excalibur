// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using k8s.Models;
namespace Excalibur.LeaderElection.Tests.Kubernetes;

[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
[Trait("Pattern", "Regression")]
public sealed class KubernetesLeaderElectionShould
{
    [Fact]
    public async Task JoinAnActualPendingNativeRenewalBeforeDisposalCompletes()
    {
        await using var f = new KubeTenureFixture();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reply = new TaskCompletionSource<V1Lease>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            await f.StartAsync();
            f.Read = _ => { entered.TrySetResult(); return reply.Task; };
            await f.Clock.WaitForDueAsync(TimeSpan.FromSeconds(5), 1);
            f.Clock.Advance(TimeSpan.FromSeconds(5));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var disposing = f.Sut.DisposeAsync().AsTask();
            disposing.IsCompleted.ShouldBeFalse();
            f.Sut.CurrentLeadership.ShouldBeNull();
            f.Read = _ => Task.FromResult(f.Snapshot());
            reply.TrySetResult(f.Snapshot());
            await disposing.WaitAsync(TimeSpan.FromSeconds(5));
            await f.Sut.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally { f.Read = _ => Task.FromResult(f.Snapshot()); reply.TrySetResult(f.Snapshot()); }
    }

    [Fact]
    public async Task PublishCoherentImmutableSnapshotsToConcurrentReaders()
    {
        await using var f = new KubeTenureFixture();
        await f.StartAsync();
        var original = f.Sut.CurrentLeadership;
        original.ShouldNotBeNull();
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            for (var index = 0; index < 1000; index++)
            {
                var snapshot = f.Sut.CurrentLeadership;
                if (snapshot is null) { continue; }
                snapshot.ShouldBe(original);
                snapshot.Value.FencingToken.ShouldNotBeNull().ShouldBeGreaterThan(0);
            }
        })));
        await f.Sut.StopAsync(CancellationToken.None);
        f.Sut.CurrentLeadership.ShouldBeNull();
        original.Value.FencingToken.ShouldNotBeNull().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task RefuseNewAdmissionAfterCompletedDisposalWithoutOwningTheBorrowedClient()
    {
        await using var f = new KubeTenureFixture();
        await f.StartAsync();
        await f.Sut.DisposeAsync();
        await f.Sut.NotificationsCompleted.WaitAsync(TimeSpan.FromSeconds(5));
        var count = f.Calls;
        await Should.ThrowAsync<ObjectDisposedException>(() => f.Sut.StartAsync(CancellationToken.None));
        await Should.ThrowAsync<ObjectDisposedException>(() => f.Sut.GetCandidateHealthAsync(CancellationToken.None));
        f.Calls.ShouldBe(count);
        A.CallTo(() => f.Client.Dispose()).MustNotHaveHappened();
    }
}
