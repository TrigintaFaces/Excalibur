// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using global::Consul;
using Harness = Excalibur.LeaderElection.Tests.Consul.ConsulTenureProtocolShould.Harness;

namespace Excalibur.LeaderElection.Tests.Consul;

[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
[Trait("Pattern", "Regression")]
/// <summary>Only successful session renewal plus exact ownership proof refreshes the R+G deadline.</summary>
public sealed class ConsulGracePeriodShould
{
    [Fact]
    public async Task KeepLeadership_WhenAReadFailsAndTheGraceBoundHasNotElapsed()
    {
        await using var h = new Harness(); await h.Sut.StartAsync(CancellationToken.None);
        h.Read = (_, _, _) => Task.FromException<QueryResult<KVPair>>(new IOException("read unavailable"));
        await h.Clock.WaitForDelayAsync(TimeSpan.FromSeconds(2), 1);
        h.Clock.Advance(TimeSpan.FromSeconds(2)); await h.WaitForReadsAsync(2);
        h.Sut.IsLeader.ShouldBeTrue();
    }

    [Fact]
    public async Task RelinquishLeadership_WhenReadsKeepFailingPastTheGraceBound()
    {
        await using var h = new Harness(); await h.Sut.StartAsync(CancellationToken.None);
        var lost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task stopping = Task.CompletedTask;
        h.Sut.LostLeadership += (_, _) =>
        {
            // Seal admission before waking the test: expiry must not race a new tenure.
            stopping = h.Sut.StopAsync(CancellationToken.None);
            lost.TrySetResult();
        };
        h.Read = (_, _, _) => Task.FromException<QueryResult<KVPair>>(new IOException("read unavailable"));
        await h.Clock.WaitForDelayAsync(TimeSpan.FromSeconds(2), 1);
        h.Clock.Advance(TimeSpan.FromSeconds(2)); await h.WaitForReadsAsync(2);
        h.Clock.Advance(TimeSpan.FromSeconds(3)); await h.WaitForRenewalsAsync(2); await h.WaitForReadsAsync(3);
        h.Sut.IsLeader.ShouldBeTrue();
        h.Clock.Advance(TimeSpan.FromSeconds(5));
        await lost.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await stopping.WaitAsync(TimeSpan.FromSeconds(5));
        h.Sut.IsLeader.ShouldBeFalse();
    }

    [Fact]
    public async Task RelinquishLeadership_WhenRenewalsKeepFailingPastTheGraceBound()
    {
        await using var h = new Harness(); await h.Sut.StartAsync(CancellationToken.None);
        var lost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task stopping = Task.CompletedTask;
        h.Sut.LostLeadership += (_, _) =>
        {
            // Seal admission before waking the test: expiry must not race a new tenure.
            stopping = h.Sut.StopAsync(CancellationToken.None);
            lost.TrySetResult();
        };
        h.Renew = (_, _, _) => Task.FromException<WriteResult<SessionEntry>>(new IOException("renewal unavailable"));
        // Successful read-only monitoring must not refresh the session proof.
        await h.Clock.WaitForDelayAsync(TimeSpan.FromSeconds(2), 1);
        h.Clock.Advance(TimeSpan.FromSeconds(2)); await h.WaitForReadsAsync(2);
        h.Clock.Advance(TimeSpan.FromSeconds(3)); await h.WaitForRenewalsAsync(2);
        h.Sut.IsLeader.ShouldBeTrue();
        h.Clock.Advance(TimeSpan.FromSeconds(5));
        await lost.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await stopping.WaitAsync(TimeSpan.FromSeconds(5)); h.Sut.IsLeader.ShouldBeFalse();
    }

    [Fact]
    public async Task RestartTheGraceBound_WhenARenewalSucceeds()
    {
        await using var h = new Harness(); await h.Sut.StartAsync(CancellationToken.None);
        var lost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task stopping = Task.CompletedTask;
        h.Sut.LostLeadership += (_, _) =>
        {
            // Seal admission before waking the test: expiry must not race a new tenure.
            stopping = h.Sut.StopAsync(CancellationToken.None);
            lost.TrySetResult();
        };
        await h.Clock.WaitForDelayAsync(TimeSpan.FromSeconds(5), 1);
        h.Clock.Advance(TimeSpan.FromSeconds(5)); await h.WaitForRenewalsAsync(2);
        await h.Clock.WaitForDelayAsync(TimeSpan.FromSeconds(5), 2); // confirmation completed at5; deadline15.
        h.Renew = (_, _, _) => Task.FromException<WriteResult<SessionEntry>>(new IOException("renewal unavailable"));
        h.Clock.Advance(TimeSpan.FromSeconds(5)); await h.WaitForRenewalsAsync(3);
        h.Sut.IsLeader.ShouldBeTrue("the successful proof at5 replaced the original deadline10");
        h.Clock.Advance(TimeSpan.FromSeconds(4)); h.Sut.IsLeader.ShouldBeTrue();
        h.Clock.Advance(TimeSpan.FromSeconds(1));
        await lost.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await stopping.WaitAsync(TimeSpan.FromSeconds(5)); h.Sut.IsLeader.ShouldBeFalse();
    }

    [Fact]
    public async Task RelinquishLeadershipImmediately_WhenConsulReportsTheSessionGone()
    {
        await using var h = new Harness(); await h.Sut.StartAsync(CancellationToken.None);
        var lost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task stopping = Task.CompletedTask;
        h.Sut.LostLeadership += (_, _) =>
        {
            // Seal admission before waking the test: expiry must not race a new tenure.
            stopping = h.Sut.StopAsync(CancellationToken.None);
            lost.TrySetResult();
        };
        h.Renew = (_, _, _) => Task.FromResult(new WriteResult<SessionEntry> { Response = null! });
        await h.Clock.WaitForDelayAsync(TimeSpan.FromSeconds(5), 1);
        h.Clock.Advance(TimeSpan.FromSeconds(5)); await lost.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await stopping.WaitAsync(TimeSpan.FromSeconds(5));
        h.Sut.IsLeader.ShouldBeFalse("definitive session loss cannot wait until deadline10");
    }
}
