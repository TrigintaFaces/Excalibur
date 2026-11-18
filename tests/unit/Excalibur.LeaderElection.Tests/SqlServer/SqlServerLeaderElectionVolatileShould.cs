// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using Excalibur.Dispatch.LeaderElection.Fencing;

namespace Excalibur.LeaderElection.Tests.SqlServer;

/// <summary>Preserves atomic reader guarantees without requiring obsolete provider shadow fields.</summary>
[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
[Trait("Pattern", "Regression")]
public sealed class SqlServerLeaderElectionVolatileShould
{
    [Fact]
    public async Task ReturnFalseForIsLeaderByDefault()
    {
        await using var fixture = new SqlServerTenureHarness();
        fixture.Sut.IsLeader.ShouldBeFalse();
        fixture.Sut.CurrentLeadership.ShouldBeNull();
        fixture.Sut.CurrentLeaderId.ShouldBeNull();
        fixture.Sessions.ShouldBeEmpty();
    }

    [Fact]
    public async Task PublishAndRetireThroughTheSameAuthoritativeTenure()
    {
        await using var fixture = new SqlServerTenureHarness();
        await fixture.StartAsync();
        fixture.Sut.IsLeader.ShouldBeTrue();
        fixture.Sut.CurrentLeaderId.ShouldBe(fixture.Sut.CandidateId);
        fixture.Sut.CurrentLeadership.ShouldNotBeNull();
        await fixture.Sut.StopAsync(CancellationToken.None);
        fixture.Sut.IsLeader.ShouldBeFalse();
        fixture.Sut.CurrentLeaderId.ShouldBeNull();
        fixture.Sut.CurrentLeadership.ShouldBeNull();
    }

    [Fact]
    public async Task ReadConsistentFenceAndAcquisitionTimeWhileRetirementRacesReaders()
    {
        var fence = A.Fake<IFencingTokenIssuer>();
        A.CallTo(() => fence.IssueTokenAsync(A<string>._, A<CancellationToken>._)).Returns(new ValueTask<long>(42L));
        await using var fixture = new SqlServerTenureHarness(fence: fence);
        await fixture.StartAsync();
        var expected = fixture.Sut.CurrentLeadership.ShouldNotBeNull();
        var snapshots = new ConcurrentQueue<Leadership>();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = 0;
        var readers = Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
        {
            snapshots.Enqueue(fixture.Sut.CurrentLeadership.ShouldNotBeNull());
            if (Interlocked.Increment(ref entered) == 8) { ready.TrySetResult(); }
            await release.Task;
            for (var i = 0; i < 1000; i++)
            {
                if (fixture.Sut.CurrentLeadership is { } held) { snapshots.Enqueue(held); }
            }
        })).ToArray();
        try
        {
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
            release.TrySetResult();
            await fixture.Sut.StopAsync(CancellationToken.None);
        }
        finally { release.TrySetResult(); await Task.WhenAll(readers); }
        snapshots.Count.ShouldBeGreaterThanOrEqualTo(8);
        snapshots.ShouldAllBe(snapshot => snapshot == expected);
        fixture.Sut.CurrentLeadership.ShouldBeNull();
    }

    [Fact]
    public async Task ReadIsLeaderWithoutDeadlockUnderConcurrentAccess()
    {
        await using var fixture = new SqlServerTenureHarness();
        var results = new bool[1000];
        Parallel.For(0, results.Length, index => results[index] = fixture.Sut.IsLeader);
        results.ShouldAllBe(value => !value);
    }
}
