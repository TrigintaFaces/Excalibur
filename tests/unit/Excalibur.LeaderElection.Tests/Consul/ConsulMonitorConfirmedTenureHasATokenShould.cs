// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using Excalibur.Dispatch.LeaderElection.Fencing;
using global::Consul;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Excalibur.LeaderElection.Tests.Consul;

/// <summary>Read-only ownership discovery cannot create an unfenced tenure or erase a valid fence.</summary>
[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
[Trait("Pattern", "Regression")]
public sealed class ConsulMonitorConfirmedTenureHasATokenShould
{
    [Fact]
    public async Task NotReportLeadershipConfirmedByTheMonitorWhenNoTokenWasEverMinted()
    {
        var provider = A.Fake<IFencingTokenIssuer>();
        _ = A.CallTo(() => provider.IssueTokenAsync(A<string>._, A<CancellationToken>._))
            .ThrowsAsync(new InvalidOperationException("the fencing store is unreachable"));
        await using var h = new Harness(provider, releaseSucceeds: false);
        await h.StartAsync();
        await h.WaitForFailuresAsync(1);
        await h.Clock.WaitForDelayAsync(TimeSpan.FromSeconds(2), 1);
        h.Current.ShouldNotBeNull("the simulated agent refused release and destruction");
        h.Sut.IsLeader.ShouldBeFalse();
        h.Sut.CurrentLeadership.ShouldBeNull();

        // Retry discovers the same public candidate on the stale private session. Discovery
        // is not authority to publish: the new acquisition cannot prove its own session.
        h.Clock.Advance(TimeSpan.FromSeconds(2));
        await h.WaitForFailuresAsync(2);
        await h.Clock.WaitForDelayAsync(TimeSpan.FromSeconds(2), 2);
        Volatile.Read(ref h.Reads).ShouldBeGreaterThanOrEqualTo(2);
        Volatile.Read(ref h.Publications).ShouldBe(0);
        h.Sut.IsLeader.ShouldBeFalse();
        h.Sut.CurrentLeadership.ShouldBeNull();
    }

    [Fact]
    public async Task ReportLeadershipConfirmedByTheMonitorWhenTheTokenWasMinted()
    {
        var provider = A.Fake<IFencingTokenIssuer>();
        _ = A.CallTo(() => provider.IssueTokenAsync(A<string>._, A<CancellationToken>._))
            .ReturnsLazily(() => new ValueTask<long>(11L));
        await using var h = new Harness(provider, releaseSucceeds: true);
        await h.StartAsync();
        var before = h.Sut.CurrentLeadership.ShouldNotBeNull();
        before.FencingToken.ShouldBe(11L);
        await h.Clock.WaitForDelayAsync(TimeSpan.FromSeconds(2), 1);
        h.Clock.Advance(TimeSpan.FromSeconds(2));
        await h.WaitForReadsAsync(2);
        await h.Clock.WaitForDelayAsync(TimeSpan.FromSeconds(2), 2);
        h.Sut.IsLeader.ShouldBeTrue();
        h.Sut.CurrentLeadership.ShouldBe(before);
        Volatile.Read(ref h.Publications).ShouldBe(1);
        _ = A.CallTo(() => provider.IssueTokenAsync(A<string>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task KeepLeadershipAcrossAMonitorPollWhenNoFencingIsConfigured()
    {
        await using var h = new Harness(null, releaseSucceeds: true);
        await h.StartAsync();
        var before = h.Sut.CurrentLeadership.ShouldNotBeNull();
        before.FencingToken.ShouldBeNull();
        await h.Clock.WaitForDelayAsync(TimeSpan.FromSeconds(2), 1);
        h.Clock.Advance(TimeSpan.FromSeconds(2));
        await h.WaitForReadsAsync(2);
        await h.Clock.WaitForDelayAsync(TimeSpan.FromSeconds(2), 2);
        h.Sut.IsLeader.ShouldBeTrue();
        h.Sut.CurrentLeadership.ShouldBe(before);
        Volatile.Read(ref h.Publications).ShouldBe(1);
    }

    private sealed class Harness : IAsyncDisposable
    {
        private int _sessions;
        private int _failures;
        internal int Publications;
        internal int Reads;
        internal KVPair? Current;
        internal TrackingClock Clock { get; } = new();
        internal ConsulLeaderElection Sut { get; }

        internal Harness(IFencingTokenIssuer? provider, bool releaseSucceeds)
        {
            var client = A.Fake<IConsulClient>();
            _ = A.CallTo(() => client.Session.Create(A<SessionEntry>._, A<WriteOptions>._, A<CancellationToken>._))
                .ReturnsLazily(() => Task.FromResult(new WriteResult<string> { Response = "session-" + Interlocked.Increment(ref _sessions) }));
            _ = A.CallTo(() => client.Session.Renew(A<string>._, A<WriteOptions>._, A<CancellationToken>._))
                .ReturnsLazily((string session, WriteOptions _, CancellationToken _) => Task.FromResult(
                    new WriteResult<SessionEntry> { Response = new SessionEntry { ID = session, TTL = TimeSpan.FromSeconds(30) } }));
            _ = A.CallTo(() => client.KV.Acquire(A<KVPair>._, A<WriteOptions>._, A<CancellationToken>._))
                .ReturnsLazily((KVPair pair, WriteOptions _, CancellationToken _) =>
                {
                    var acquired = Interlocked.CompareExchange(ref Current, pair, null) is null;
                    return Task.FromResult(new WriteResult<bool> { Response = acquired });
                });
            _ = A.CallTo(() => client.KV.Get(A<string>._, A<QueryOptions>._, A<CancellationToken>._))
                .ReturnsLazily(() =>
                {
                    Interlocked.Increment(ref Reads);
                    return Task.FromResult(new QueryResult<KVPair> { Response = Volatile.Read(ref Current)! });
                });
            _ = A.CallTo(() => client.KV.Release(A<KVPair>._, A<WriteOptions>._, A<CancellationToken>._))
                .ReturnsLazily((KVPair pair, WriteOptions _, CancellationToken _) =>
                {
                    if (releaseSucceeds && Current?.Session == pair.Session) { Volatile.Write(ref Current, null); }
                    return Task.FromResult(new WriteResult<bool> { Response = releaseSucceeds });
                });
            _ = A.CallTo(() => client.Session.Destroy(A<string>._, A<WriteOptions>._, A<CancellationToken>._))
                .ReturnsLazily((string session, WriteOptions _, CancellationToken _) =>
                {
                    if (releaseSucceeds && Current?.Session == session) { Volatile.Write(ref Current, null); }
                    return Task.FromResult(new WriteResult<bool> { Response = releaseSucceeds });
                });
            _ = A.CallTo(() => client.KV.Put(A<KVPair>._, A<WriteOptions>._, A<CancellationToken>._))
                .Returns(Task.FromResult(new WriteResult<bool> { Response = true }));
            var options = Microsoft.Extensions.Options.Options.Create(new ConsulLeaderElectionOptions
            {
                InstanceId = "candidate-a", MaxRetryAttempts = 0,
                RenewInterval = TimeSpan.FromSeconds(5), GracePeriod = TimeSpan.FromSeconds(5),
                RetryInterval = TimeSpan.FromSeconds(2), SessionTTL = TimeSpan.FromSeconds(30)
            });
            Sut = new ConsulLeaderElection("test-resource", options, client,
                NullLogger<ConsulLeaderElection>.Instance, provider, Clock);
            Sut.BecameLeader += (_, _) => Interlocked.Increment(ref Publications);
            Sut.AcquisitionFailed += (_, _) => Interlocked.Increment(ref _failures);
        }

        internal async Task StartAsync()
        {
            await Sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            if (Sut.IsLeader) { await WaitUntilAsync(() => Volatile.Read(ref Publications) == 1); }
        }
        internal Task WaitForFailuresAsync(int count) => WaitUntilAsync(() => Volatile.Read(ref _failures) >= count);
        internal Task WaitForReadsAsync(int count) => WaitUntilAsync(() => Volatile.Read(ref Reads) >= count);
        internal static async Task WaitUntilAsync(Func<bool> ready)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (!ready())
            {
                await Task.Delay(1, timeout.Token); // delay-ok: Poll actual Consul read/publication/failure counters; the injected clock controls tenure timing.
            }
        }
        public ValueTask DisposeAsync() => Sut.DisposeAsync();
    }

    private sealed class TrackingClock : FakeTimeProvider
    {
        private readonly ConcurrentDictionary<TimeSpan, int> _counts = new();
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = base.CreateTimer(callback, state, dueTime, period);
            _counts.AddOrUpdate(dueTime, 1, static (_, count) => count + 1);
            return timer;
        }
        internal Task WaitForDelayAsync(TimeSpan due, int count) =>
            Harness.WaitUntilAsync(() => _counts.TryGetValue(due, out var seen) && seen >= count);
    }
}
