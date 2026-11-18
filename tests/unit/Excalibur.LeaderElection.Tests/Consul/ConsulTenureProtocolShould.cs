// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using global::Consul;
using Excalibur.Dispatch;
using Excalibur.Dispatch.LeaderElection;
using Excalibur.Dispatch.LeaderElection.Fencing;
using Excalibur.LeaderElection.Consul;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using Xunit;

namespace Excalibur.LeaderElection.Tests.Consul;

[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
[Trait("Pattern", "Regression")]
public sealed class ConsulTenureProtocolShould
{
    public static TheoryData<bool, bool, bool, bool> OptionalRegistrationMatrix()
    {
        var data = new TheoryData<bool, bool, bool, bool>();
        foreach (var builder in new[] { false, true })
        foreach (var logging in new[] { false, true })
        foreach (var fencing in new[] { false, true })
        foreach (var classifier in new[] { false, true })
            data.Add(builder, logging, fencing, classifier);
        return data;
    }

    [Theory]
    [MemberData(nameof(OptionalRegistrationMatrix))]
    public async Task ResolveOptionalDependenciesWithoutDroppingClassifier(bool builder, bool logging, bool fencing, bool withClassifier)
    {
        await using var h = new Harness();
        var services = new ServiceCollection();
        services.AddSingleton(h.Client);
        if (logging) { services.AddLogging(); }
        var fault = new IOException("classified renewal failure through actual DI factory");
        var classified = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var renewed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var classifier = A.Fake<IMessageFailureClassifier>();
        A.CallTo(() => classifier.Classify(fault)).ReturnsLazily(() =>
        {
            classified.TrySetResult(); return MessageFailureKind.Permanent;
        });
        if (withClassifier) { services.AddSingleton(classifier); }
        if (fencing)
        {
            var fence = A.Fake<IFencingTokenIssuer>();
            A.CallTo(() => fence.IssueTokenAsync(A<string>._, A<CancellationToken>._)).ReturnsLazily(() => new ValueTask<long>(42L));
            services.AddSingleton(fence);
            services.AddSingleton<IFencingTokenProvider>(fence);
        }
        if (builder)
        {
            services.AddExcaliburLeaderElection(le =>
            {
                le.UseConsul(c => c.Address("http://localhost:8500"));
                if (!fencing) { le.WithoutFencingTokens(); }
            });
        }
        else
        {
            services.AddConsulLeaderElection(c => c.Address("http://localhost:8500"));
            if (!fencing) { services.WithoutFencingTokens(); }
        }
        services.Configure<ConsulLeaderElectionOptions>(options =>
        {
            options.MaxRetryAttempts = 0; options.RenewInterval = TimeSpan.FromMilliseconds(50);
            options.GracePeriod = TimeSpan.FromSeconds(5);
        });
        h.Renew = (number, session, _) =>
        {
            if (number == 1) { return Task.FromResult(Harness.Renewed(session)); }
            renewed.TrySetResult(); return Task.FromException<WriteResult<SessionEntry>>(fault);
        };
        using var provider = services.BuildServiceProvider();
        if (!logging) { provider.GetService<Microsoft.Extensions.Logging.ILoggerFactory>().ShouldBeNull(); }
        if (!fencing) { provider.GetService<IFencingTokenIssuer>().ShouldBeNull(); }
        if (!withClassifier) { provider.GetService<IMessageFailureClassifier>().ShouldBeNull(); }
        var election = provider.GetRequiredService<ILeaderElectionFactory>().CreateElection("di-matrix", "same-public-id");
        var lost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        election.LostLeadership += (_, _) => lost.TrySetResult();
        try
        {
            await election.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            await renewed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (withClassifier)
            {
                await classified.Task.WaitAsync(TimeSpan.FromSeconds(2));
                await lost.Task.WaitAsync(TimeSpan.FromSeconds(2));
                A.CallTo(() => classifier.Classify(fault)).MustHaveHappened();
            }
        }
        finally
        {
            await election.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            if (election is IAsyncDisposable asyncDisposable) { await asyncDisposable.DisposeAsync(); }
            else if (election is IDisposable disposable) { disposable.Dispose(); }
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RefuseDisplayIdentityWithoutBothSessionAndNonce(bool wrongSession)
    {
        await using var h = new Harness();
        h.Read = (_, _, _) =>
        {
            var pair = h.Current!;
            var json = JsonNode.Parse(pair.Value)!.AsObject();
            if (!wrongSession) { json["OwnerNonce"] = "not-this-tenure"; }
            return Task.FromResult(new QueryResult<KVPair>
            {
                Response = new KVPair(pair.Key) { Session = wrongSession ? "another-session" : pair.Session,
                    Value = System.Text.Encoding.UTF8.GetBytes(json.ToJsonString()) }
            });
        };
        await h.Sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        h.Sut.IsLeader.ShouldBeFalse();
        h.Sut.CurrentLeadership.ShouldBeNull();
        await h.Sut.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        h.Released.ShouldHaveSingleItem().ShouldBe("session-1");
        h.Destroyed.ShouldHaveSingleItem().ShouldBe("session-1");
    }

    [Fact]
    public async Task ExpireIndependentlyWhenRenewHangsDespiteReadableOwnedKv()
    {
        await using var h = new Harness();
        var pending = new TaskCompletionSource<WriteResult<SessionEntry>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Renew = (number, session, _) =>
        {
            if (number == 1) { return Task.FromResult(Harness.Renewed(session)); }
            entered.TrySetResult(); return pending.Task;
        };
        var lost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Sut.LostLeadership += (_, _) => lost.TrySetResult();
        try
        {
            await h.Sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            await h.Clock.WaitForDelayAsync(TimeSpan.FromSeconds(2), 1);
            h.Clock.Advance(TimeSpan.FromSeconds(2));
            await h.WaitForReadsAsync(2);
            await h.Clock.WaitForDelayAsync(TimeSpan.FromSeconds(2), 2);
            h.Clock.Advance(TimeSpan.FromSeconds(2));
            await h.WaitForReadsAsync(3);
            h.Clock.Advance(TimeSpan.FromSeconds(1));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            h.Clock.Advance(TimeSpan.FromSeconds(5));
            await lost.Task.WaitAsync(TimeSpan.FromSeconds(5)); // No getter drove expiry.
            h.Sut.IsLeader.ShouldBeFalse();
            h.Released.ShouldBeEmpty();
            var stopping = h.Sut.StopAsync(CancellationToken.None);
            stopping.IsCompleted.ShouldBeFalse();
            pending.TrySetResult(Harness.Renewed("session-1"));
            await stopping.WaitAsync(TimeSpan.FromSeconds(5));
            h.Sut.IsLeader.ShouldBeFalse();
            h.Created.Count.ShouldBe(1);
        }
        finally { pending.TrySetResult(Harness.Renewed("session-1")); }
    }

    [Fact]
    public async Task KeepRenewalIndependentOfASlowMonitorAndDiscardItsStaleLoss()
    {
        await using var h = new Harness();
        var slowRead = new TaskCompletionSource<QueryResult<KVPair>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var monitorEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Read = (number, _, _) =>
        {
            if (number == 2) { monitorEntered.TrySetResult(); return slowRead.Task; }
            return Task.FromResult(new QueryResult<KVPair> { Response = h.Current! });
        };
        try
        {
            await h.Sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            await h.Clock.WaitForDelayAsync(TimeSpan.FromSeconds(2), 1);
            h.Clock.Advance(TimeSpan.FromSeconds(2));
            await monitorEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            h.Clock.Advance(TimeSpan.FromSeconds(3));
            await h.WaitForRenewalsAsync(2);
            await h.WaitForReadsAsync(3); // Combined proof passed while monitor still held.
            await h.Clock.WaitForDelayAsync(TimeSpan.FromSeconds(5), 2);
            slowRead.TrySetResult(new QueryResult<KVPair> { Response = null! });
            await h.Clock.WaitForDelayAsync(TimeSpan.FromSeconds(2), 2);
            h.Sut.IsLeader.ShouldBeTrue(); // Old monitor's pre-confirm record cannot retire the fresh proof.
            h.Sut.CurrentLeadership.ShouldNotBeNull();
        }
        finally { slowRead.TrySetResult(new QueryResult<KVPair> { Response = h.Current! }); }
    }

    [Fact]
    public async Task JoinBothIgnoredCancellationRequestsBeforeCapturedSessionCleanup()
    {
        await using var h = new Harness();
        var monitor = new TaskCompletionSource<QueryResult<KVPair>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var renewal = new TaskCompletionSource<WriteResult<SessionEntry>>(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Read = (number, _, _) => number == 2 ? monitor.Task : Task.FromResult(new QueryResult<KVPair> { Response = h.Current! });
        h.Renew = (number, session, _) => number == 1 ? Task.FromResult(Harness.Renewed(session)) : renewal.Task;
        try
        {
            await h.Sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            await h.Clock.WaitForDelayAsync(TimeSpan.FromSeconds(2), 1);
            h.Clock.Advance(TimeSpan.FromSeconds(2)); await h.WaitForReadsAsync(2);
            h.Clock.Advance(TimeSpan.FromSeconds(3)); await h.WaitForRenewalsAsync(2);
            var stopping = h.Sut.StopAsync(CancellationToken.None);
            h.Sut.IsLeader.ShouldBeFalse();
            stopping.IsCompleted.ShouldBeFalse();
            h.Released.ShouldBeEmpty(); h.Destroyed.ShouldBeEmpty();
            monitor.TrySetResult(new QueryResult<KVPair> { Response = h.Current! });
            renewal.TrySetResult(Harness.Renewed("session-1"));
            await stopping.WaitAsync(TimeSpan.FromSeconds(5));
            h.Released.ShouldHaveSingleItem().ShouldBe("session-1");
            h.Destroyed.ShouldHaveSingleItem().ShouldBe("session-1");
            A.CallTo(() => h.Client.Dispose()).MustNotHaveHappened();
        }
        finally { monitor.TrySetResult(new QueryResult<KVPair> { Response = h.Current! }); renewal.TrySetResult(Harness.Renewed("session-1")); }
    }

    [Fact]
    public async Task PreserveFencedNotificationAndServerAndHealthSettings()
    {
        var fence = A.Fake<IFencingTokenIssuer>();
        A.CallTo(() => fence.IssueTokenAsync(A<string>._, A<CancellationToken>._)).ReturnsLazily(() => new ValueTask<long>(42L));
        await using var h = new Harness(fence: fence);
        var notification = new TaskCompletionSource<LeaderChangedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Sut.LeaderChanged += (_, change) => { if (change.NewLeaderId == h.Sut.CandidateId) { notification.TrySetResult(change); } };
        await h.Sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        (await notification.Task.WaitAsync(TimeSpan.FromSeconds(5))).FencingToken.ShouldBe(42);
        var leadership = h.Sut.CurrentLeadership;
        leadership.ShouldNotBeNull(); leadership.GetValueOrDefault().FencingToken.ShouldBe(42);
        var request = h.Created.ShouldHaveSingleItem();
        request.Behavior.ShouldBe(SessionBehavior.Delete);
        request.Checks.ShouldContain("custom-health"); request.Checks.ShouldContain("serfHealth");
        request.TTL.ShouldBe(TimeSpan.FromSeconds(30)); request.LockDelay.ShouldBe(TimeSpan.FromSeconds(15));
        h.Queries.ShouldAllBe(q => q.Consistency == ConsistencyMode.Consistent && q.Datacenter == "dc-test" && q.Token == "test-token");
        h.Writes.ShouldAllBe(q => q.Datacenter == "dc-test" && q.Token == "test-token");
        await h.Sut.UpdateHealthAsync(false, new Dictionary<string, string> { ["note"] = "offline" }, CancellationToken.None);
        h.Sut.IsLeader.ShouldBeFalse();
        var health = (await h.Sut.GetCandidateHealthAsync(CancellationToken.None)).Single(item => item.CandidateId == h.Sut.CandidateId);
        health.IsHealthy.ShouldBeFalse(); health.Metadata["note"].ShouldBe("offline");
    }

    [Fact]
    public void ValidateTimingAgainstActualSessionTtlInsteadOfNominalLeaseDuration()
    {
        var options = Harness.Settings(); options.LeaseDuration = TimeSpan.FromSeconds(1);
        new ConsulLeaderElectionOptionsValidator().Validate(null, options).Succeeded.ShouldBeTrue();
        options.SessionTTL = TimeSpan.FromSeconds(10); options.LockDelay = TimeSpan.Zero;
        new ConsulLeaderElectionOptionsValidator().Validate(null, options).Failed.ShouldBeTrue();
        options.SessionTTL = TimeSpan.FromSeconds(30); options.RenewInterval = TimeSpan.MaxValue;
        new ConsulLeaderElectionOptionsValidator().Validate(null, options).Failed.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(10)]
    public async Task RefuseMissingOrUnsafeReturnedTtl(int? seconds)
    {
        await using var h = new Harness();
        h.Renew = (_, session, _) => Task.FromResult(new WriteResult<SessionEntry>
        { Response = new SessionEntry { ID = session, TTL = seconds.HasValue ? TimeSpan.FromSeconds(seconds.Value) : null } });
        await h.Sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        h.Sut.IsLeader.ShouldBeFalse();
        h.Sut.CurrentLeadership.ShouldBeNull();
        await h.Sut.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        h.Destroyed.ShouldHaveSingleItem().ShouldBe("session-1");
    }

    [Fact]
    public async Task AllowPublicationSubscriberToStopWithoutJoiningItself()
    {
        await using var h = new Harness();
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Sut.BecameLeader += (_, _) =>
        {
#pragma warning disable RS0030 // Synchronous subscriber is the intentional callback/self-join adversary.
            h.Sut.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
#pragma warning restore RS0030
            stopped.TrySetResult();
        };
        await h.Sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        await stopped.Task.WaitAsync(TimeSpan.FromSeconds(5));
        h.Sut.IsLeader.ShouldBeFalse(); h.Destroyed.Count.ShouldBe(1);
    }

    [Fact]
    public async Task AcceleratePermanentRenewalFailureBeforeTheLocalDeadline()
    {
        var error = new IOException("permanent Consul renewal fault");
        var classifier = A.Fake<IMessageFailureClassifier>();
        A.CallTo(() => classifier.Classify(error)).Returns(MessageFailureKind.Permanent);
        await using var h = new Harness(classifier: classifier);
        h.Renew = (number, session, _) => number == 1 ? Task.FromResult(Harness.Renewed(session)) : Task.FromException<WriteResult<SessionEntry>>(error);
        var lost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Sut.LostLeadership += (_, _) => lost.TrySetResult();
        await h.Sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        await h.Clock.WaitForDelayAsync(TimeSpan.FromSeconds(5), 1);
        h.Clock.Advance(TimeSpan.FromSeconds(5));
        await lost.Task.WaitAsync(TimeSpan.FromSeconds(5));
        h.Sut.IsLeader.ShouldBeFalse();
        A.CallTo(() => classifier.Classify(error)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task RetireExpiredSessionWithoutClassifierWhileMonitorIsHeldBeforeDeadline()
    {
        await using var h = new Harness();
        var monitorEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseMonitor = new TaskCompletionSource<QueryResult<KVPair>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var lost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Read = (number, _, _) =>
        {
            if (number == 1) { return Task.FromResult(new QueryResult<KVPair> { Response = h.Current! }); }
            monitorEntered.TrySetResult();
            return releaseMonitor.Task; // Deliberately ignores cancellation.
        };
        h.Renew = (number, session, _) => number == 1
            ? Task.FromResult(Harness.Renewed(session))
            : Task.FromException<WriteResult<SessionEntry>>(new SessionExpiredException("session no longer exists"));
        h.Sut.LostLeadership += (_, _) => lost.TrySetResult();
        try
        {
            await h.Sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            await h.Clock.WaitForDelayAsync(TimeSpan.FromSeconds(2), 1);
            await h.Clock.WaitForDelayAsync(TimeSpan.FromSeconds(5), 1);
            h.Clock.Advance(TimeSpan.FromSeconds(2));
            await monitorEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            h.Clock.Advance(TimeSpan.FromSeconds(3));
            // The fake clock remains at t=5; the local deadline is t=10. No getter
            // triggers this transition, and the blocked monitor cannot discover it.
            await lost.Task.WaitAsync(TimeSpan.FromSeconds(5));
            h.Sut.IsLeader.ShouldBeFalse();
            h.Released.ShouldBeEmpty();
            h.Destroyed.ShouldBeEmpty();
        }
        finally { releaseMonitor.TrySetResult(new QueryResult<KVPair> { Response = h.Current! }); }
    }

    [Fact]
    public async Task PreserveCleanupFailureAndRefuseRestartWithoutFalseQuiescence()
    {
        var h = new Harness();
        var failure = new IOException("release failed"); h.ReleaseFailure = failure;
        await h.Sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        var stopped = await Should.ThrowAsync<IOException>(() => h.Sut.StopAsync(CancellationToken.None));
        stopped.ShouldBeSameAs(failure);
        h.Sut.IsLeader.ShouldBeFalse();
        h.Destroyed.ShouldHaveSingleItem().ShouldBe("session-1");
        await Should.ThrowAsync<InvalidOperationException>(() => h.Sut.StartAsync(CancellationToken.None));
        var disposed = await Should.ThrowAsync<IOException>(() => h.Sut.DisposeAsync().AsTask());
        disposed.ShouldBeSameAs(failure);
    }

    internal sealed class Harness : IAsyncDisposable
    {
        private int _sessionNumber;
        private int _renewals;
        private int _reads;
        internal IConsulClient Client { get; } = A.Fake<IConsulClient>();
        internal ISessionEndpoint Session { get; } = A.Fake<ISessionEndpoint>();
        internal IKVEndpoint Kv { get; } = A.Fake<IKVEndpoint>();
        internal ScheduledClock Clock { get; } = new();
        internal ConcurrentQueue<SessionEntry> Created { get; } = new();
        internal ConcurrentQueue<string> Released { get; } = new();
        internal ConcurrentQueue<string> Destroyed { get; } = new();
        internal ConcurrentQueue<QueryOptions> Queries { get; } = new();
        internal ConcurrentQueue<WriteOptions> Writes { get; } = new();
        internal Func<int, string, CancellationToken, Task<WriteResult<SessionEntry>>>? Renew;
        internal Func<int, QueryOptions, CancellationToken, Task<QueryResult<KVPair>>>? Read;
        internal Exception? ReleaseFailure;
        internal KVPair? Current;
        internal ConsulLeaderElection Sut { get; }
        internal static ConsulLeaderElectionOptions Settings() => new()
        {
            InstanceId = "same-public-id", Datacenter = "dc-test", Token = "test-token",
            HealthCheckId = "custom-health", MaxRetryAttempts = 0
        };
        internal Harness(IFencingTokenIssuer? fence = null, IMessageFailureClassifier? classifier = null, ILogger<ConsulLeaderElection>? logger = null)
        {
            A.CallTo(() => Client.Session).Returns(Session); A.CallTo(() => Client.KV).Returns(Kv);
            A.CallTo(() => Session.Create(A<SessionEntry>._, A<WriteOptions>._, A<CancellationToken>._)).ReturnsLazily((SessionEntry entry, WriteOptions write, CancellationToken _) =>
            {
                Created.Enqueue(entry); Writes.Enqueue(write);
                return Task.FromResult(new WriteResult<string> { Response = "session-" + Interlocked.Increment(ref _sessionNumber) });
            });
            A.CallTo(() => Session.Renew(A<string>._, A<WriteOptions>._, A<CancellationToken>._)).ReturnsLazily((string session, WriteOptions write, CancellationToken token) =>
            {
                Writes.Enqueue(write); var number = Interlocked.Increment(ref _renewals);
                return Renew?.Invoke(number, session, token) ?? Task.FromResult(Renewed(session));
            });
            A.CallTo(() => Kv.Acquire(A<KVPair>._, A<WriteOptions>._, A<CancellationToken>._)).ReturnsLazily((KVPair pair, WriteOptions write, CancellationToken _) =>
            {
                Writes.Enqueue(write); Volatile.Write(ref Current, pair);
                return Task.FromResult(new WriteResult<bool> { Response = true });
            });
            A.CallTo(() => Kv.Get(A<string>._, A<QueryOptions>._, A<CancellationToken>._)).ReturnsLazily((string _, QueryOptions query, CancellationToken token) =>
            {
                Queries.Enqueue(query); var number = Interlocked.Increment(ref _reads);
                return Read?.Invoke(number, query, token) ?? Task.FromResult(new QueryResult<KVPair> { Response = Volatile.Read(ref Current)! });
            });
            A.CallTo(() => Kv.Release(A<KVPair>._, A<WriteOptions>._, A<CancellationToken>._)).ReturnsLazily((KVPair pair, WriteOptions write, CancellationToken _) =>
            {
                Writes.Enqueue(write); Released.Enqueue(pair.Session);
                if (ReleaseFailure is not null) { return Task.FromException<WriteResult<bool>>(ReleaseFailure); }
                var matches = string.Equals(Current?.Session, pair.Session, StringComparison.Ordinal);
                if (matches) { Volatile.Write(ref Current, null); }
                return Task.FromResult(new WriteResult<bool> { Response = matches });
            });
            A.CallTo(() => Session.Destroy(A<string>._, A<WriteOptions>._, A<CancellationToken>._)).ReturnsLazily((string session, WriteOptions write, CancellationToken _) =>
            {
                Writes.Enqueue(write); Destroyed.Enqueue(session);
                return Task.FromResult(new WriteResult<bool> { Response = true });
            });
            A.CallTo(() => Kv.Put(A<KVPair>._, A<WriteOptions>._, A<CancellationToken>._)).ReturnsLazily((KVPair _, WriteOptions write, CancellationToken token) =>
            { Writes.Enqueue(write); return Task.FromResult(new WriteResult<bool> { Response = true }); });
            A.CallTo(() => Kv.List(A<string>._, A<QueryOptions>._, A<CancellationToken>._)).Returns(Task.FromResult(new QueryResult<KVPair[]> { Response = null! }));
            Sut = new ConsulLeaderElection("protocol-control", Options.Create(Settings()), Client,
                logger ?? NullLogger<ConsulLeaderElection>.Instance, fence, Clock, classifier);
        }
        internal static WriteResult<SessionEntry> Renewed(string session) => new() { Response = new SessionEntry { ID = session, TTL = TimeSpan.FromSeconds(30) } };
        internal Task WaitForReadsAsync(int count) => WaitUntilAsync(() => Volatile.Read(ref _reads) >= count);
        internal Task WaitForRenewalsAsync(int count) => WaitUntilAsync(() => Volatile.Read(ref _renewals) >= count);
        internal static async Task WaitUntilAsync(Func<bool> ready)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (!ready())
            {
                await Task.Delay(1, timeout.Token); // delay-ok: Poll actual Consul read/renewal counters; the injected clock controls tenure timing.
            }
        }
        public ValueTask DisposeAsync() => Sut.DisposeAsync();
    }
    internal sealed class ScheduledClock : FakeTimeProvider
    {
        private readonly ConcurrentDictionary<TimeSpan, int> _timers = new();
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = base.CreateTimer(callback, state, dueTime, period);
            _timers.AddOrUpdate(dueTime, 1, static (_, count) => count + 1);
            return timer;
        }
        internal Task WaitForDelayAsync(TimeSpan due, int count) => Harness.WaitUntilAsync(() => _timers.TryGetValue(due, out var seen) && seen >= count);
    }
}
