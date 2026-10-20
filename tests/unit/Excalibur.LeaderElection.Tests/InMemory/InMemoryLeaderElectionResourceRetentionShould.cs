// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using System.Reflection;

using Excalibur.Dispatch.LeaderElection.Fencing;

using Microsoft.Extensions.Logging.Abstractions;

using Tests.Shared.Helpers;

namespace Excalibur.LeaderElection.Tests.InMemory;

/// <summary>Verifies resource buckets follow explicit owner lifetimes without resetting fencing history.</summary>
[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
[Trait("Pattern", "Regression")]
public sealed class InMemoryLeaderElectionResourceRetentionShould
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReclaimUniqueResourceBucketsAfterTheirLastOwnerIsDisposed(bool startFirst)
    {
        var state = new InMemoryLeaderElectionSharedState();
        var population = Candidates(state);
        var baseline = population.Count;
        for (var index = 0; index < 128; index++)
        {
            await using var candidate = Create($"resource-{index}", "owner", state);
            if (startFirst)
            {
                await candidate.StartAsync(TestContext.Current.CancellationToken);
                await candidate.StopAsync(TestContext.Current.CancellationToken);
            }
        }

        population.Count.ShouldBe(baseline);
    }

    [Fact]
    public async Task RetainTheOwnerBucketAcrossStopAndRestart()
    {
        var state = new InMemoryLeaderElectionSharedState();
        await using var candidate = Create("restart", "owner", state);
        var bucket = Candidates(state)["restart"];
        await candidate.StartAsync(TestContext.Current.CancellationToken);
        await candidate.StopAsync(TestContext.Current.CancellationToken);
        Candidates(state)["restart"].ShouldBeSameAs(bucket);
        await candidate.StartAsync(TestContext.Current.CancellationToken);
        await candidate.UpdateHealthAsync(isHealthy: true, metadata: null, TestContext.Current.CancellationToken);
        candidate.IsLeader.ShouldBeTrue();
        var health = await candidate.GetCandidateHealthAsync(TestContext.Current.CancellationToken);
        health.ShouldContain(item => string.Equals(item.CandidateId, "owner", StringComparison.Ordinal) && item.IsHealthy);
        await candidate.DisposeAsync();
        Candidates(state).ShouldBeEmpty();
    }

    [Fact]
    public async Task RepublishOnlyWhenStartingAfterTrackingIsRemoved()
    {
        var state = new InMemoryLeaderElectionSharedState();
        await using var candidate = Create("removed-publication", "owner", state);
        var population = Candidates(state);
        var bucket = population["removed-publication"];
        await candidate.StartAsync(TestContext.Current.CancellationToken);
        population.TryRemove("removed-publication", out _).ShouldBeTrue();

        await candidate.UpdateHealthAsync(isHealthy: false, metadata: null, TestContext.Current.CancellationToken);
        population.ShouldBeEmpty();
        candidate.IsLeader.ShouldBeTrue();

        await candidate.StopAsync(TestContext.Current.CancellationToken);
        population.ShouldBeEmpty();
        await candidate.StartAsync(TestContext.Current.CancellationToken);
        population["removed-publication"].ShouldBeSameAs(bucket);
        candidate.IsLeader.ShouldBeTrue();
        await candidate.DisposeAsync();
        population.ShouldBeEmpty();
    }

    [Fact]
    public async Task KeepTheOtherOwnersBucketAndHealthUntilTheFinalDispose()
    {
        var state = new InMemoryLeaderElectionSharedState();
        await using var first = Create("shared", "first", state);
        await using var second = Create("shared", "second", state);
        var bucket = Candidates(state)["shared"];
        await first.StartAsync(TestContext.Current.CancellationToken);
        await second.StartAsync(TestContext.Current.CancellationToken);
        await first.DisposeAsync();
        Candidates(state)["shared"].ShouldBeSameAs(bucket);
        await second.UpdateHealthAsync(isHealthy: true, metadata: null, TestContext.Current.CancellationToken);
        bucket.Keys.ShouldContain("second");
        bucket.Keys.ShouldNotContain("first");
        await second.DisposeAsync();
        Candidates(state).ShouldBeEmpty();
    }

    [Fact]
    public async Task ReleaseOneLifetimeLeaseExactlyOnceAcrossMixedConcurrentDisposeCalls()
    {
        var state = new InMemoryLeaderElectionSharedState();
        await using var disposed = Create("mixed", "disposed", state);
        await using var remaining = Create("mixed", "remaining", state);
        await remaining.StartAsync(TestContext.Current.CancellationToken);
        var tasks = Enumerable.Range(0, 32).Select(index => Task.Run(async () =>
        {
            if (index % 2 == 0)
            {
                disposed.Dispose();
            }
            else
            {
                await disposed.DisposeAsync();
            }
        }, TestContext.Current.CancellationToken));
        await Task.WhenAll(tasks).WaitAsync(Timeout, TestContext.Current.CancellationToken);
        await remaining.UpdateHealthAsync(isHealthy: true, metadata: null, TestContext.Current.CancellationToken);
        Candidates(state)["mixed"].Keys.ShouldContain("remaining");
        await remaining.DisposeAsync();
        Candidates(state).ShouldBeEmpty();
    }

    [Fact]
    public async Task KeepNewOwnersAttachedWhenLastDisposeRacesConstruction()
    {
        var state = new InMemoryLeaderElectionSharedState();
        for (var iteration = 0; iteration < 64; iteration++)
        {
            var oldOwner = Create("racing", "old", state);
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var retiring = Task.Run(async () =>
            {
                await start.Task;
                oldOwner.Dispose();
            }, TestContext.Current.CancellationToken);
            var creating = Task.Run(async () =>
            {
                await start.Task;
                return Create("racing", "new", state);
            }, TestContext.Current.CancellationToken);
            start.SetResult();
            await Task.WhenAll(retiring, creating).WaitAsync(Timeout, TestContext.Current.CancellationToken);
            await using var newOwner = await creating;
            await newOwner.StartAsync(TestContext.Current.CancellationToken);
            await newOwner.UpdateHealthAsync(isHealthy: true, metadata: null, TestContext.Current.CancellationToken);
            Candidates(state)["racing"].Keys.ShouldContain("new");
            newOwner.IsLeader.ShouldBeTrue();
            await newOwner.DisposeAsync();
            Candidates(state).ShouldBeEmpty();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreventDelayedClockReadsFromResurrectingDisposedBuckets(bool healthUpdate)
    {
        var state = new InMemoryLeaderElectionSharedState();
        using var clock = new BlockingClock();
        await using var candidate = Create("delayed", "owner", state, clock);
        if (healthUpdate)
        {
            await candidate.StartAsync(TestContext.Current.CancellationToken);
        }

        clock.Arm();
        var pending = Task.Run(() => healthUpdate
            ? candidate.UpdateHealthAsync(isHealthy: true, metadata: null, TestContext.Current.CancellationToken)
            : candidate.StartAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        try
        {
            clock.WaitUntilBlocked();
            await Task.Run(candidate.Dispose, TestContext.Current.CancellationToken)
                .WaitAsync(Timeout, TestContext.Current.CancellationToken);
            Candidates(state).ShouldBeEmpty();
        }
        finally
        {
            clock.Release();
            try
            {
                await pending.WaitAsync(Timeout, TestContext.Current.CancellationToken);
            }
            catch (ObjectDisposedException)
            {
                // A delayed start may reject disposal; completing without publishing is also valid.
            }
        }

        Candidates(state).ShouldBeEmpty();
        candidate.IsLeader.ShouldBeFalse();
    }

    [Fact]
    public async Task PreserveRealFencingTokenHistoryWhenResourceBucketsAreRecreated()
    {
        var state = new InMemoryLeaderElectionSharedState();
        var provider = new InMemoryFencingTokenProvider(state);
        long firstToken;
        await using (var first = Create("fenced", "first", state, fencing: provider))
        {
            await first.StartAsync(TestContext.Current.CancellationToken);
            firstToken = first.CurrentLeadership!.Value.FencingToken!.Value;
        }

        Candidates(state).ShouldBeEmpty();
        await using var second = Create("fenced", "second", state, fencing: provider);
        await second.StartAsync(TestContext.Current.CancellationToken);
        second.CurrentLeadership!.Value.FencingToken!.Value.ShouldBeGreaterThan(firstToken);
        (await provider.ValidateTokenAsync("fenced", firstToken, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReleaseLeadershipAndRetireTheBucketWhenTimerDisableThrows(bool asynchronous)
    {
        var state = new InMemoryLeaderElectionSharedState();
        using var clock = new ThrowingDisableClock();
        var candidate = Create("disable-failure", "owner", state, clock);
        await candidate.StartAsync(TestContext.Current.CancellationToken);
        candidate.IsLeader.ShouldBeTrue();
        clock.Arm();
        try
        {
            if (asynchronous)
            {
                var failure = await Should.ThrowAsync<InvalidOperationException>(async () =>
                    await candidate.DisposeAsync());
                failure.ShouldBeSameAs(clock.Failure);
            }
            else
            {
                Should.Throw<InvalidOperationException>(candidate.Dispose).ShouldBeSameAs(clock.Failure);
            }

            Candidates(state).ShouldBeEmpty();
            candidate.IsLeader.ShouldBeFalse();
            await using var successor = Create("disable-failure", "successor", state);
            await successor.StartAsync(TestContext.Current.CancellationToken);
            successor.IsLeader.ShouldBeTrue();
        }
        finally
        {
            await candidate.DisposeAsync();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreserveIncumbentWhenAnInactiveSameIdAliasIsDisposedAsynchronously(bool aliasWasStopped)
    {
        var state = new InMemoryLeaderElectionSharedState();
        await using var alias = Create("inactive-alias", "same-id", state);
        if (aliasWasStopped)
        {
            await alias.StartAsync(TestContext.Current.CancellationToken);
            await alias.StopAsync(TestContext.Current.CancellationToken);
        }

        await using var incumbent = Create("inactive-alias", "same-id", state);
        await incumbent.StartAsync(TestContext.Current.CancellationToken);
        await incumbent.UpdateHealthAsync(isHealthy: true,
            metadata: new Dictionary<string, string>(StringComparer.Ordinal) { ["witness"] = "incumbent" },
            TestContext.Current.CancellationToken);
        await alias.DisposeAsync();
        incumbent.IsLeader.ShouldBeTrue();
        var health = await incumbent.GetCandidateHealthAsync(TestContext.Current.CancellationToken);
        var retained = health.ShouldHaveSingleItem();
        retained.CandidateId.ShouldBe("same-id");
        retained.IsHealthy.ShouldBeTrue();
        retained.Metadata["witness"].ShouldBe("incumbent");
        Candidates(state)["inactive-alias"].Keys.ShouldContain("same-id");
    }

    [Fact]
    public async Task LogStoppedWhenARunningCandidateIsDisposedAsynchronously()
    {
        var state = new InMemoryLeaderElectionSharedState();
        var logger = new CapturingLogger<InMemoryLeaderElection>();
        await using var candidate = new InMemoryLeaderElection("stop-log",
            Options.Create(new LeaderElectionOptions { InstanceId = "owner" }), logger, state);
        await candidate.StartAsync(TestContext.Current.CancellationToken);
        logger.Clear();
        await candidate.DisposeAsync();
        logger.Entries.Count(entry => entry.EventId.Id == 180001 && entry.Level == LogLevel.Information).ShouldBe(1);
        await candidate.DisposeAsync();
        logger.Entries.Count(entry => entry.EventId.Id == 180001 && entry.Level == LogLevel.Information).ShouldBe(1);
    }
    private static InMemoryLeaderElection Create(
        string resource,
        string instance,
        InMemoryLeaderElectionSharedState state,
        TimeProvider? clock = null,
        IFencingTokenProvider? fencing = null) => new(
            resource,
            Options.Create(new LeaderElectionOptions
            {
                InstanceId = instance,
                LeaseDuration = TimeSpan.FromMinutes(30),
                RenewInterval = TimeSpan.FromMinutes(5),
                StepDownWhenUnhealthy = false,
            }),
            NullLogger<InMemoryLeaderElection>.Instance,
            state,
            clock,
            fencing);

    private static ConcurrentDictionary<string, ConcurrentDictionary<string, CandidateHealth>> Candidates(
        InMemoryLeaderElectionSharedState state) =>
        (ConcurrentDictionary<string, ConcurrentDictionary<string, CandidateHealth>>)typeof(InMemoryLeaderElectionSharedState)
            .GetProperty("Candidates", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(state)!;

    private sealed class ThrowingDisableClock : TimeProvider, IDisposable
    {
        private ThrowingDisableTimer? _timer;

        public InvalidOperationException Failure { get; } = new("Controlled timer-disable failure.");

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            _timer = new ThrowingDisableTimer(base.CreateTimer(callback, state, dueTime, period), Failure);
            return _timer;
        }

        public void Arm() => _timer.ShouldNotBeNull().Arm();

        public void Dispose() => _timer?.Dispose();
    }

    private sealed class ThrowingDisableTimer(ITimer inner, InvalidOperationException failure) : ITimer
    {
        private int _armed;

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            if (dueTime == System.Threading.Timeout.InfiniteTimeSpan && Interlocked.Exchange(ref _armed, 0) == 1)
            {
                throw failure;
            }

            return inner.Change(dueTime, period);
        }

        public void Arm() => Volatile.Write(ref _armed, 1);

        public void Dispose() => inner.Dispose();

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
    private sealed class BlockingClock : TimeProvider, IDisposable
    {
        private readonly ManualResetEventSlim _entered = new(initialState: false);
        private readonly ManualResetEventSlim _released = new(initialState: false);
        private int _armed;

        public override DateTimeOffset GetUtcNow()
        {
            if (Interlocked.Exchange(ref _armed, 0) == 1)
            {
                _entered.Set();
                if (!_released.Wait(Timeout))
                {
                    throw new TimeoutException("The controlled clock was not released.");
                }
            }

            return base.GetUtcNow();
        }

        public void Arm() => Volatile.Write(ref _armed, 1);

        public void WaitUntilBlocked() => _entered.Wait(Timeout).ShouldBeTrue();

        public void Release() => _released.Set();

        public void Dispose()
        {
            _released.Set();
            _entered.Dispose();
            _released.Dispose();
        }
    }
}
