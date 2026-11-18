// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using global::Consul;
using Harness = Excalibur.LeaderElection.Tests.Consul.ConsulTenureProtocolShould.Harness;

namespace Excalibur.LeaderElection.Tests.Consul;

[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
[Trait("Pattern", "Regression")]
/// <summary>Local authority ends before cleanup; cleanup failures remain observable and memoized.</summary>
public sealed class ConsulStoppedLeaderRelinquishesShould
{
    [Fact]
    public async Task ReportNotLeaderAfterStopWhenTheReleaseThrows()
    {
        var h = await StartAsync(); var failure = new IOException("release unavailable"); h.ReleaseFailure = failure;
        (await Should.ThrowAsync<IOException>(() => h.Sut.StopAsync(CancellationToken.None))).ShouldBeSameAs(failure);
        AssertRetired(h); AssertCleanup(h);
        (await Should.ThrowAsync<IOException>(() => h.DisposeAsync().AsTask())).ShouldBeSameAs(failure);
    }

    [Fact]
    public async Task ReportNotLeaderAfterStopWhenTheReleaseIsRefusedWithoutThrowing()
    {
        await using var h = await StartAsync();
        A.CallTo(() => h.Kv.Release(A<KVPair>._, A<WriteOptions>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new WriteResult<bool> { Response = false }));
        await h.Sut.StopAsync(CancellationToken.None);
        AssertRetired(h);
        A.CallTo(() => h.Kv.Release(A<KVPair>._, A<WriteOptions>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        h.Destroyed.ShouldHaveSingleItem().ShouldBe("session-1");
    }

    [Fact]
    public async Task ReportNotLeaderAfterAsyncDisposeWhenTheReleaseThrows()
    {
        var h = await StartAsync(); var failure = new IOException("release unavailable"); h.ReleaseFailure = failure;
        (await Should.ThrowAsync<IOException>(() => h.DisposeAsync().AsTask())).ShouldBeSameAs(failure);
        AssertRetired(h); AssertCleanup(h);
    }

    [Fact]
    public async Task ReportNotLeaderAfterSyncDisposeWhenTheReleaseThrows()
    {
        var h = await StartAsync(); var failure = new IOException("release unavailable"); h.ReleaseFailure = failure;
        Should.Throw<IOException>(h.Sut.Dispose).ShouldBeSameAs(failure);
        AssertRetired(h); AssertCleanup(h);
    }

    [Fact]
    public async Task ReportNotLeaderAfterAsyncDisposeWhenTheReleaseSucceeds()
    {
        var h = await StartAsync(); await h.DisposeAsync(); AssertRetired(h); AssertCleanup(h);
    }

    [Fact]
    public async Task RaiseLostLeadershipExactlyOnceWhenTheReleaseSucceeds()
    {
        var h = await StartAsync();
        var lost = ObserveLoss(h, out var count);
        await h.Sut.StopAsync(CancellationToken.None);
        await lost.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await h.DisposeAsync();
        count().ShouldBe(1); AssertCleanup(h);
    }

    [Fact]
    public async Task RaiseLostLeadershipExactlyOnceAcrossStopThenDispose()
    {
        var h = await StartAsync(); var failure = new IOException("release unavailable"); h.ReleaseFailure = failure;
        var lost = ObserveLoss(h, out var count);
        await Should.ThrowAsync<IOException>(() => h.Sut.StopAsync(CancellationToken.None));
        await lost.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Should.ThrowAsync<IOException>(() => h.DisposeAsync().AsTask());
        count().ShouldBe(1); AssertCleanup(h);
    }

    [Fact]
    public async Task PublishOneLossWhenDisposalEndsAPublishedTenure()
    {
        var h = new Harness();
        var changes = new System.Collections.Concurrent.ConcurrentQueue<string?>();
        var acquired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var changedToLost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Sut.LeaderChanged += (_, change) =>
        {
            changes.Enqueue(change.NewLeaderId);
            if (change.NewLeaderId is null) { changedToLost.TrySetResult(); }
            else { acquired.TrySetResult(); }
        };
        var lost = ObserveLoss(h, out var count);
        await h.Sut.StartAsync(CancellationToken.None);
        await acquired.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await h.DisposeAsync();
        await lost.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await changedToLost.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await h.DisposeAsync();
        count().ShouldBe(1); AssertRetired(h); AssertCleanup(h);
        changes.Count.ShouldBe(2);
        changes.TryDequeue(out var first).ShouldBeTrue(); first.ShouldBe(h.Sut.CandidateId);
        changes.TryDequeue(out var second).ShouldBeTrue(); second.ShouldBeNull();
    }

    [Fact]
    public async Task PreserveTheSameFailureAcrossBothDisposePathsWithoutRepeatingCleanup()
    {
        var h = await StartAsync(); var failure = new IOException("release unavailable"); h.ReleaseFailure = failure;
        Should.Throw<IOException>(h.Sut.Dispose).ShouldBeSameAs(failure);
        (await Should.ThrowAsync<IOException>(() => h.DisposeAsync().AsTask())).ShouldBeSameAs(failure);
        Should.Throw<IOException>(h.Sut.Dispose).ShouldBeSameAs(failure);
        AssertRetired(h); AssertCleanup(h);
    }

    private static async Task<Harness> StartAsync()
    {
        var h = new Harness(); await h.Sut.StartAsync(CancellationToken.None);
        h.Sut.IsLeader.ShouldBeTrue("loss controls require an actually published tenure");
        return h;
    }
    private static void AssertRetired(Harness h)
    {
        h.Sut.IsLeader.ShouldBeFalse(); h.Sut.CurrentLeadership.ShouldBeNull(); h.Sut.CurrentLeaderId.ShouldBeNull();
    }
    private static void AssertCleanup(Harness h)
    {
        h.Released.ShouldHaveSingleItem().ShouldBe("session-1"); h.Destroyed.ShouldHaveSingleItem().ShouldBe("session-1");
        A.CallTo(() => h.Client.Dispose()).MustNotHaveHappened();
    }
    private static TaskCompletionSource ObserveLoss(Harness h, out Func<int> count)
    {
        var total = 0;
        var lost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Sut.LostLeadership += (_, _) => { Interlocked.Increment(ref total); lost.TrySetResult(); };
        count = () => Volatile.Read(ref total);
        return lost;
    }
}
