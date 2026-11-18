// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Excalibur.Dispatch.LeaderElection.Fencing;
using Excalibur.LeaderElection.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Excalibur.LeaderElection.Tests.Diagnostics;

[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
[Trait("Pattern", "Regression")]
public sealed class TelemetryQueuedNotificationShould
{
    [Fact]
    public async Task ForwardTheOriginalQueuedLossAfterResourceDisposalReturns()
    {
        var redis = A.Fake<IConnectionMultiplexer>();
        var database = A.Fake<IDatabase>();
        A.CallTo(() => redis.GetDatabase(A<int>._, A<object>._)).Returns(database);
        A.CallTo(database).Where(call => call.Method.Name == nameof(IDatabase.StringSetAsync))
            .WithReturnType<Task<bool>>().Returns(Task.FromResult(true));
        A.CallTo(database).Where(call => call.Method.Name == nameof(IDatabase.ScriptEvaluateAsync))
            .WithReturnType<Task<RedisResult>>().Returns(Task.FromResult(RedisResult.Create((RedisValue)1)));
        var fence = A.Fake<IFencingTokenIssuer>();
        A.CallTo(() => fence.IssueTokenAsync(A<string>._, A<CancellationToken>._)).ReturnsLazily(() => ValueTask.FromResult(42L));
        var clock = new FakeTimeProvider();
        await using var inner = new RedisLeaderElection(redis, "queued-disposal",
            Options.Create(new LeaderElectionOptions { InstanceId = "queued-candidate" }),
            NullLogger<RedisLeaderElection>.Instance,
            new RedisLeaderElectionContext { FencingTokenProvider = fence }, clock);
        using var meter = new Meter("Excalibur.LeaderElection.QueuedDisposal." + Guid.NewGuid().ToString("N"));
        using var activity = new ActivitySource("Excalibur.LeaderElection.QueuedDisposal");
        await using var sut = new TelemetryLeaderElection(inner, meter, activity, "Redis", clock);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var returned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sourceLost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sourceFinalChange = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var changes = new ConcurrentQueue<LeaderChangedEventArgs>();
        var losses = 0;
        sut.BecameLeader += (_, _) =>
        {
            entered.TrySetResult();
            try
            {
#pragma warning disable RS0030 // Intentional synchronous consumer barrier; resource disposal must not join this callback.
                release.Task.GetAwaiter().GetResult();
#pragma warning restore RS0030
            }
            finally { returned.TrySetResult(); }
        };
        sut.LostLeadership += (_, _) => Interlocked.Increment(ref losses);
        sut.LeaderChanged += (_, change) => changes.Enqueue(change);
        inner.LostLeadership += (_, _) => sourceLost.TrySetResult();
        inner.LeaderChanged += (_, change) => { if (change.NewLeaderId is null) { sourceFinalChange.TrySetResult(); } };
        try
        {
            await sut.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            clock.Advance(TimeSpan.FromSeconds(1));
            await sut.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            inner.IsLeader.ShouldBeFalse();
            sourceLost.Task.IsCompleted.ShouldBeFalse("the original source notification remains behind the held callback");
            returned.Task.IsCompleted.ShouldBeFalse();
            release.TrySetResult();
            await returned.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await sourceLost.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await sourceFinalChange.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Volatile.Read(ref losses).ShouldBe(1, "resource completion must not detach before the original queued loss is forwarded");
            var forwarded = changes.ToArray();
            forwarded.Length.ShouldBe(2);
            forwarded[0].FencingToken.ShouldBe(42);
            forwarded[0].NewLeaderId.ShouldBe(inner.CandidateId);
            forwarded[1].NewLeaderId.ShouldBeNull();
            forwarded[1].FencingToken.ShouldBeNull();
            A.CallTo(() => redis.Dispose()).MustNotHaveHappened();
        }
        finally
        {
            release.TrySetResult();
            await inner.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            if (entered.Task.IsCompleted) { await returned.Task.WaitAsync(TimeSpan.FromSeconds(5)); }
        }
    }
}
