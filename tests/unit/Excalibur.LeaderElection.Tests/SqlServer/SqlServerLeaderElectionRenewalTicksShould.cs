// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.LeaderElection.SqlServer;

namespace Excalibur.LeaderElection.Tests.SqlServer;

/// <summary>Monotonic proof behavior replaces assertions about removed timestamp storage fields.</summary>
[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
[Trait("Pattern", "Regression")]
public sealed class SqlServerLeaderElectionRenewalTicksShould
{
    [Fact]
    public async Task KeepTheProofBudgetIndependentOfUtcClockSteps()
    {
        await using var fixture = new SqlServerTenureHarness();
        fixture.Verify = (_, _) => Task.FromResult(SqlServerSessionProof.Indeterminate);
        var lost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Sut.LostLeadership += (_, _) => lost.TrySetResult();
        await fixture.StartAsync();
        await fixture.Clock.WaitForDelayAsync(TimeSpan.FromSeconds(5), 1);
        fixture.Clock.UtcOffset = TimeSpan.FromHours(-12);
        fixture.Clock.Advance(TimeSpan.FromSeconds(5));
        await fixture.Clock.WaitForDelayAsync(TimeSpan.FromSeconds(2), 1);
        fixture.Sut.IsLeader.ShouldBeTrue();
        fixture.Clock.Advance(TimeSpan.FromSeconds(5));
        await lost.Task.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Sut.IsLeader.ShouldBeFalse();
        fixture.Sut.CurrentLeadership.ShouldBeNull();
    }

    [Fact]
    public async Task RefreshOnlyTheMonotonicProofWhileKeepingTheOriginalAcquiredSnapshot()
    {
        await using var fixture = new SqlServerTenureHarness();
        await fixture.StartAsync();
        var original = fixture.Sut.CurrentLeadership.ShouldNotBeNull();
        await fixture.Clock.WaitForDelayAsync(TimeSpan.FromSeconds(5), 1);
        fixture.Clock.UtcOffset = TimeSpan.FromHours(12);
        fixture.Clock.Advance(TimeSpan.FromSeconds(5));
        await fixture.Clock.WaitForDelayAsync(TimeSpan.FromSeconds(5), 2);
        fixture.Sut.CurrentLeadership.ShouldNotBeNull().ShouldBe(original);
        fixture.Sessions.Single().VerificationStarts.Count.ShouldBe(1);
    }
}
