// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Benchmarks.LeaderElection;

namespace Excalibur.LeaderElection.Tests.Benchmarks;

[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
[Trait("Pattern", "Regression")]
public sealed class LeaderElectionBenchmarkSetupShould
{
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public async Task CoordinateAllCandidatesOnOneElection(int count)
    {
        var candidates = LeaderElectionBenchmarkSetup.CreateCandidates(count, CreateOptions());
        try
        {
            await Task.WhenAll(candidates.Select(candidate => candidate.StartAsync(CancellationToken.None)));
            candidates.Count(candidate => candidate.IsLeader).ShouldBe(1);
            candidates.Select(candidate => candidate.CandidateId).Distinct(StringComparer.Ordinal).Count().ShouldBe(count);
            var leader = candidates.Single(candidate => candidate.IsLeader);
            foreach (var candidate in candidates)
            {
                candidate.CurrentLeaderId.ShouldBe(leader.CandidateId);
                var members = await candidate.GetCandidateHealthAsync(CancellationToken.None);
                members.Select(member => member.CandidateId).Order(StringComparer.Ordinal)
                    .ShouldBe(candidates.Select(member => member.CandidateId).Order(StringComparer.Ordinal));
            }
        }
        finally
        {
            await LeaderElectionBenchmarkSetup.DisposeCandidatesAsync(candidates);
        }
    }

    [Fact]
    public async Task IsolateIterationsWithTheSameCandidateIdentifiers()
    {
        var first = LeaderElectionBenchmarkSetup.CreateCandidates(5, CreateOptions());
        var second = LeaderElectionBenchmarkSetup.CreateCandidates(5, CreateOptions());
        try
        {
            await Task.WhenAll(first.Concat(second).Select(candidate => candidate.StartAsync(CancellationToken.None)));
            first.Count(candidate => candidate.IsLeader).ShouldBe(1);
            second.Count(candidate => candidate.IsLeader).ShouldBe(1);
            await LeaderElectionBenchmarkSetup.DisposeCandidatesAsync(first);
            second.Count(candidate => candidate.IsLeader).ShouldBe(1);
            (await second[0].GetCandidateHealthAsync(CancellationToken.None)).Count().ShouldBe(5);
        }
        finally
        {
            await LeaderElectionBenchmarkSetup.DisposeCandidatesAsync(first);
            await LeaderElectionBenchmarkSetup.DisposeCandidatesAsync(second);
        }
    }

    [Fact]
    public async Task DisposeEveryCandidateEvenWhenLeadershipSubscriberThrows()
    {
        var candidates = LeaderElectionBenchmarkSetup.CreateCandidates(5, CreateOptions());
        await Task.WhenAll(candidates.Select(candidate => candidate.StartAsync(CancellationToken.None)));
        var original = new InvalidOperationException("subscriber failure");
        candidates[0].LostLeadership += (_, _) => throw original;
        try
        {
            // Disposal swallows and logs a cleanup fault rather than propagating it: a throwing Dispose
            // inside a using block would replace the exception that caused the block to exit.
            await Should.NotThrowAsync(
                () => LeaderElectionBenchmarkSetup.DisposeCandidatesAsync(candidates).AsTask());
            foreach (var candidate in candidates)
            {
                await Should.ThrowAsync<ObjectDisposedException>(() => candidate.StartAsync(CancellationToken.None));
            }

            await LeaderElectionBenchmarkSetup.DisposeCandidatesAsync(candidates);
        }
        finally
        {
            await LeaderElectionBenchmarkSetup.DisposeCandidatesAsync(candidates);
        }
    }

    private static LeaderElectionOptions CreateOptions() => new()
    {
        LeaseDuration = TimeSpan.FromSeconds(30),
        RenewInterval = TimeSpan.FromSeconds(10),
        RetryInterval = TimeSpan.FromSeconds(5),
        StepDownWhenUnhealthy = true,
    };
}
