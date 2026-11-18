// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Runtime.ExceptionServices;
using global::Consul;
using Excalibur.Dispatch.LeaderElection;
using Excalibur.LeaderElection.Consul;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using Xunit;

namespace Excalibur.Integration.Tests.LeaderElection;

[Collection(ConsulLeaderElectionTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "LeaderElection")]
[Trait("Database", "Consul")]
[Trait("Pattern", "Regression")]
public sealed class ConsulLateSessionRuntimeShould(ConsulContainerFixture fixture)
{
    [Fact]
    public async Task PreserveSameIdSuccessorAgainstActualLateOldRenewalAndRelease()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using var client = new ConsulClient(config => config.Address = new Uri(fixture.ConsulAddress));
        var prefix = "excalibur/tests/tenure/" + Guid.NewGuid().ToString("N");
        var resource = "isolated-resource";
        var key = prefix + "/leader/" + resource;
        var options = Options.Create(new ConsulLeaderElectionOptions
        {
            ConsulAddress = fixture.ConsulAddress, KeyPrefix = prefix, InstanceId = "same-public-id",
            LockDelay = TimeSpan.Zero, MaxRetryAttempts = 0
        });
        var clock = new RuntimeClock();
        var wrapped = A.Fake<IConsulClient>();
        var sessions = A.Fake<ISessionEndpoint>();
        var kv = A.Fake<IKVEndpoint>();
        A.CallTo(() => wrapped.Session).Returns(sessions); A.CallTo(() => wrapped.KV).Returns(kv);
        var releaseRenew = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseMonitor = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var renewalEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var monitorEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var actualOldRenewWasValid = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var actualOldRelease = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var lost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        string? oldSession = null;
        var renewalCount = 0; var readCount = 0;
        A.CallTo(() => sessions.Create(A<SessionEntry>._, A<WriteOptions>._, A<CancellationToken>._))
            .ReturnsLazily(async (SessionEntry entry, WriteOptions write, CancellationToken token) =>
            {
                var created = await client.Session.Create(entry, write, token);
                oldSession = created.Response; return created;
            });
        A.CallTo(() => sessions.Renew(A<string>._, A<WriteOptions>._, A<CancellationToken>._))
            .ReturnsLazily(async (string session, WriteOptions write, CancellationToken token) =>
            {
                if (Interlocked.Increment(ref renewalCount) == 1) { return await client.Session.Renew(session, write, token); }
                renewalEntered.TrySetResult(); await releaseRenew.Task;
                try
                {
                    // Forward the exact captured OLD session, ignoring cancellation as the adversary.
                    var result = await client.Session.Renew(session, write, CancellationToken.None);
                    actualOldRenewWasValid.TrySetResult(result.Response is not null);
                    return result;
                }
                catch (SessionExpiredException) { actualOldRenewWasValid.TrySetResult(false); throw; }
            });
        A.CallTo(() => sessions.Destroy(A<string>._, A<WriteOptions>._, A<CancellationToken>._))
            .ReturnsLazily((string session, WriteOptions write, CancellationToken token) => client.Session.Destroy(session, write, token));
        A.CallTo(() => kv.Acquire(A<KVPair>._, A<WriteOptions>._, A<CancellationToken>._))
            .ReturnsLazily((KVPair pair, WriteOptions write, CancellationToken token) => client.KV.Acquire(pair, write, token));
        A.CallTo(() => kv.Put(A<KVPair>._, A<WriteOptions>._, A<CancellationToken>._))
            .ReturnsLazily((KVPair pair, WriteOptions write, CancellationToken token) => client.KV.Put(pair, write, token));
        A.CallTo(() => kv.Get(A<string>._, A<QueryOptions>._, A<CancellationToken>._))
            .ReturnsLazily(async (string name, QueryOptions query, CancellationToken token) =>
            {
                if (Interlocked.Increment(ref readCount) == 1) { return await client.KV.Get(name, query, token); }
                monitorEntered.TrySetResult(); await releaseMonitor.Task;
                return await client.KV.Get(name, query, CancellationToken.None);
            });
        A.CallTo(() => kv.Release(A<KVPair>._, A<WriteOptions>._, A<CancellationToken>._))
            .ReturnsLazily(async (KVPair pair, WriteOptions write, CancellationToken token) =>
            {
                var released = await client.KV.Release(pair, write, token);
                actualOldRelease.TrySetResult(released.Response); return released;
            });
        var old = new ConsulLeaderElection(resource, options, wrapped, NullLogger<ConsulLeaderElection>.Instance, timeProvider: clock);
        var successor = new ConsulLeaderElection(resource, options, client, NullLogger<ConsulLeaderElection>.Instance);
        old.LostLeadership += (_, _) => lost.TrySetResult();
        var failures = new List<Exception>();
        try
        {
            await old.StartAsync(deadline.Token);
            old.IsLeader.ShouldBeTrue();
            await clock.WaitForRenewalAsync(deadline.Token);
            clock.Advance(TimeSpan.FromSeconds(5));
            await renewalEntered.Task.WaitAsync(deadline.Token);
            await monitorEntered.Task.WaitAsync(deadline.Token);
            oldSession.ShouldNotBeNullOrWhiteSpace();
            // Explicit session destruction is a fault injection, not a TTL-expiry timing claim.
            _ = await client.Session.Destroy(oldSession!, deadline.Token);
            await successor.StartAsync(deadline.Token);
            successor.IsLeader.ShouldBeTrue();
            var successorPair = (await client.KV.Get(key, new QueryOptions { Consistency = ConsistencyMode.Consistent }, deadline.Token)).Response;
            successorPair.ShouldNotBeNull(); successorPair.Session.ShouldNotBe(oldSession);
            clock.Advance(TimeSpan.FromSeconds(5));
            await lost.Task.WaitAsync(deadline.Token); // Both old SDK lanes remain blocked.
            actualOldRelease.Task.IsCompleted.ShouldBeFalse();
            releaseMonitor.TrySetResult(); releaseRenew.TrySetResult();
            (await actualOldRenewWasValid.Task.WaitAsync(deadline.Token)).ShouldBeFalse();
            await old.StopAsync(deadline.Token);
            (await actualOldRelease.Task.WaitAsync(deadline.Token)).ShouldBeFalse();
            var retained = (await client.KV.Get(key, new QueryOptions { Consistency = ConsistencyMode.Consistent }, deadline.Token)).Response;
            retained.ShouldNotBeNull(); retained.Session.ShouldBe(successorPair.Session); retained.Value.ShouldBe(successorPair.Value);
            old.IsLeader.ShouldBeFalse(); successor.IsLeader.ShouldBeTrue();
        }
        catch (Exception error) { failures.Add(error); }
        finally
        {
            releaseMonitor.TrySetResult(); releaseRenew.TrySetResult();
            await CaptureAsync(() => old.DisposeAsync().AsTask(), failures);
            await CaptureAsync(() => successor.DisposeAsync().AsTask(), failures);
            await CaptureAsync(async () => { _ = await client.KV.Delete(key); }, failures);
        }
        if (failures.Count == 1) { ExceptionDispatchInfo.Capture(failures[0]).Throw(); }
        if (failures.Count > 1) { throw new AggregateException(failures); }
    }
    private static async Task CaptureAsync(Func<Task> action, List<Exception> failures)
    {
        try { await action(); }
        catch (Exception error) { failures.Add(error); }
    }
    private sealed class RuntimeClock : FakeTimeProvider
    {
        private readonly TaskCompletionSource _renewalReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Task WaitForRenewalAsync(CancellationToken token) => _renewalReady.Task.WaitAsync(token);
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = base.CreateTimer(callback, state, dueTime, period);
            if (dueTime == TimeSpan.FromSeconds(5)) { _renewalReady.TrySetResult(); }
            return timer;
        }
    }
}
