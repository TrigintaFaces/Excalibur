// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using global::Consul;
using Harness = Excalibur.LeaderElection.Tests.Consul.ConsulTenureProtocolShould.Harness;

namespace Excalibur.LeaderElection.Tests.Consul;

[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
[Trait("Pattern", "Regression")]
public sealed class ConsulLeaderElectionShould
{
    [Fact]
    public async Task JoinActualRenewalWorkBeforeStopCompletes()
    {
        var h = new Harness();
        var reply = new TaskCompletionSource<WriteResult<SessionEntry>>(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Renew = (number, session, _) => number == 1 ? Task.FromResult(Harness.Renewed(session)) : reply.Task;
        Task stopping = Task.CompletedTask;
        try
        {
            await h.Sut.StartAsync(CancellationToken.None);
            await h.Clock.WaitForDelayAsync(TimeSpan.FromSeconds(5), 1);
            h.Clock.Advance(TimeSpan.FromSeconds(5));
            await h.WaitForRenewalsAsync(2);
            stopping = h.Sut.StopAsync(CancellationToken.None);
            h.Sut.IsLeader.ShouldBeFalse();
            stopping.IsCompleted.ShouldBeFalse();
            h.Released.ShouldBeEmpty(); h.Destroyed.ShouldBeEmpty();
        }
        finally
        {
            reply.TrySetResult(Harness.Renewed("session-1"));
            await stopping.WaitAsync(TimeSpan.FromSeconds(5));
            await h.DisposeAsync();
        }
        h.Released.ShouldHaveSingleItem().ShouldBe("session-1");
        h.Destroyed.ShouldHaveSingleItem().ShouldBe("session-1");
    }

    [Fact]
    public async Task RetainShutdownOwnershipWhenTheCallerCancelsItsWait()
    {
        var h = new Harness();
        var reply = new TaskCompletionSource<WriteResult<SessionEntry>>(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Renew = (number, session, _) => number == 1 ? Task.FromResult(Harness.Renewed(session)) : reply.Task;
        try
        {
            await h.Sut.StartAsync(CancellationToken.None);
            await h.Clock.WaitForDelayAsync(TimeSpan.FromSeconds(5), 1);
            h.Clock.Advance(TimeSpan.FromSeconds(5)); await h.WaitForRenewalsAsync(2);
            using var canceled = new CancellationTokenSource(); canceled.Cancel();
            await Should.ThrowAsync<OperationCanceledException>(() => h.Sut.StopAsync(canceled.Token));
            h.Sut.IsLeader.ShouldBeFalse();
            h.Destroyed.ShouldBeEmpty();
            await Should.ThrowAsync<InvalidOperationException>(() => h.Sut.StartAsync(CancellationToken.None));
        }
        finally
        {
            reply.TrySetResult(Harness.Renewed("session-1"));
            await h.Sut.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            await h.DisposeAsync();
        }
        h.Destroyed.ShouldHaveSingleItem().ShouldBe("session-1");
    }

    [Fact]
    public async Task ObserveRenewalFaultsWithoutUntrackedAsyncVoidWork()
    {
        await using var h = new Harness();
        var failed = new IOException("renewal unavailable");
        h.Renew = (number, session, _) => number == 1 ? Task.FromResult(Harness.Renewed(session)) : Task.FromException<WriteResult<SessionEntry>>(failed);
        await h.Sut.StartAsync(CancellationToken.None);
        await h.Clock.WaitForDelayAsync(TimeSpan.FromSeconds(5), 1);
        h.Clock.Advance(TimeSpan.FromSeconds(5)); await h.WaitForRenewalsAsync(2);
        await h.Clock.WaitForDelayAsync(TimeSpan.FromSeconds(2), 2);
        h.Sut.IsLeader.ShouldBeTrue();
        await h.Sut.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        h.Destroyed.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task RefuseNewAdmissionFromAnotherThreadAfterCompletedDisposal()
    {
        var h = new Harness();
        await h.Sut.StartAsync(CancellationToken.None);
        await h.DisposeAsync();
        await Task.Run(async () =>
        {
            h.Sut.IsLeader.ShouldBeFalse(); h.Sut.CurrentLeadership.ShouldBeNull(); h.Sut.CurrentLeaderId.ShouldBeNull();
            await Should.ThrowAsync<ObjectDisposedException>(() => h.Sut.StartAsync(CancellationToken.None));
            await h.Sut.StopAsync(CancellationToken.None);
        });
        h.Created.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task ReadCurrentLeaderWithoutWaitingForAnOutstandingMonitorRpc()
    {
        var h = new Harness();
        var reply = new TaskCompletionSource<QueryResult<KVPair>>(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Read = (number, _, _) => number == 2 ? reply.Task : Task.FromResult(new QueryResult<KVPair> { Response = h.Current! });
        try
        {
            await h.Sut.StartAsync(CancellationToken.None);
            await h.Clock.WaitForDelayAsync(TimeSpan.FromSeconds(2), 1);
            h.Clock.Advance(TimeSpan.FromSeconds(2)); await h.WaitForReadsAsync(2);
            await Task.Run(() =>
            {
                for (var i = 0; i < 1000; i++) { h.Sut.CurrentLeaderId.ShouldBe(h.Sut.CandidateId); }
            }).WaitAsync(TimeSpan.FromSeconds(5));
            A.CallTo(() => h.Kv.Get(A<string>._, A<QueryOptions>._, A<CancellationToken>._)).MustHaveHappened(2, Times.Exactly);
        }
        finally { reply.TrySetResult(new QueryResult<KVPair> { Response = h.Current! }); await h.DisposeAsync(); }
    }

    [Fact]
    public void ImplementIAsyncDisposable() => typeof(IAsyncDisposable).IsAssignableFrom(typeof(ConsulLeaderElection)).ShouldBeTrue();

    [Fact]
    public async Task ReturnNullFromUnstartedCurrentLeaderWithoutContactingTheAgent()
    {
        await using var h = new Harness();
        for (var i = 0; i < 1000; i++) { h.Sut.CurrentLeaderId.ShouldBeNull(); }
        A.CallTo(() => h.Kv.Get(A<string>._, A<QueryOptions>._, A<CancellationToken>._)).MustNotHaveHappened();
        h.Created.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReturnNullFromCompletedStopWithoutIssuingAnotherRead()
    {
        await using var h = new Harness();
        await h.Sut.StartAsync(CancellationToken.None);
        await h.Sut.StopAsync(CancellationToken.None);
        A.CallTo(() => h.Kv.Get(A<string>._, A<QueryOptions>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        for (var i = 0; i < 1000; i++) { h.Sut.CurrentLeaderId.ShouldBeNull(); }
        A.CallTo(() => h.Kv.Get(A<string>._, A<QueryOptions>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }
}
