// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using Excalibur.Dispatch.LeaderElection.Fencing;
using Microsoft.Extensions.Time.Testing;
namespace Excalibur.LeaderElection.Tests.InMemory;
[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
[Trait("Pattern", "Regression")]
public sealed class InMemoryExactOwnershipShould
{
    [Fact]
    public async Task KeepTheIncumbentWhenARunningSameDisplayAliasBecomesUnhealthy()
    {
        var state = new InMemoryLeaderElectionSharedState();
        await using var incumbent = Create(state);
        await using var alias = Create(state);
        await incumbent.StartAsync(CancellationToken.None);
        incumbent.IsLeader.ShouldBeTrue();
        var original = state.Leaders["resource"];
        await alias.StartAsync(CancellationToken.None);
        alias.IsLeader.ShouldBeFalse();
        await alias.UpdateHealthAsync(false, null, CancellationToken.None);
        incumbent.IsLeader.ShouldBeTrue();
        state.Leaders["resource"].ShouldBeSameAs(original);
        await incumbent.UpdateHealthAsync(true, new Dictionary<string, string> { ["owner"] = "incumbent" }, CancellationToken.None);
        await alias.DisposeAsync();
        incumbent.IsLeader.ShouldBeTrue();
        (await incumbent.GetCandidateHealthAsync(CancellationToken.None)).ShouldHaveSingleItem().Metadata["owner"].ShouldBe("incumbent");
    }

    [Fact]
    public async Task ReleaseAnUnpublishedReservationWhenTheClockThrowsAfterSuccessfulMint()
    {
        var state = new InMemoryLeaderElectionSharedState();
        var clock = new ThrowAfterMintClock();
        var fence = A.Fake<IFencingTokenIssuer>();
        A.CallTo(() => fence.IssueTokenAsync(A<string>._, A<CancellationToken>._))
            .ReturnsLazily(() => { clock.Arm(); return new ValueTask<long>(41); });
        await using var failed = Create(state, clock, fence);
        var observed = await Record.ExceptionAsync(() => failed.StartAsync(CancellationToken.None));
        observed.ShouldBeSameAs(clock.Failure);
        failed.CurrentLeadership.ShouldBeNull();
        state.Leaders.ShouldBeEmpty("a successful mint never excuses an unpublished reservation leak");
        await using var successor = Create(state);
        await successor.StartAsync(CancellationToken.None);
        successor.IsLeader.ShouldBeTrue();
    }

    [Fact]
    public async Task NeverRemoveASameDisplaySuccessorWhenAnOldUnpublishedReservationFinallySettles()
    {
        var state = new InMemoryLeaderElectionSharedState();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reply = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
        var fence = A.Fake<IFencingTokenIssuer>();
        A.CallTo(() => fence.IssueTokenAsync(A<string>._, A<CancellationToken>._))
            .ReturnsLazily(() => { entered.TrySetResult(); return new ValueTask<long>(reply.Task); });
        await using var old = Create(state, fence: fence);
        await using var successor = Create(state);
        Task? starting = null;
        Task? stopping = null;
        try
        {
            starting = old.StartAsync(CancellationToken.None);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var stale = state.Leaders["resource"];
            old.CurrentLeadership.ShouldBeNull();
            // Remove exactly the old reservation at the shared-store boundary, then let a real same-ID
            // contender acquire a distinct object. No public display-ID compare can distinguish them.
            state.Leaders.TryRemove(new KeyValuePair<string, InMemoryLeaderElectionSharedState.TenureOwnership>("resource", stale)).ShouldBeTrue();
            await successor.StartAsync(CancellationToken.None);
            successor.IsLeader.ShouldBeTrue();
            var current = state.Leaders["resource"];
            current.ShouldNotBeSameAs(stale);
            stopping = old.StopAsync(CancellationToken.None);
            stopping.IsCompleted.ShouldBeFalse();
            reply.TrySetResult(41);
            await starting.WaitAsync(TimeSpan.FromSeconds(5));
            await stopping.WaitAsync(TimeSpan.FromSeconds(5));
            await old.DisposeAsync();
            successor.IsLeader.ShouldBeTrue();
            state.Leaders["resource"].ShouldBeSameAs(current);
        }
        finally
        {
            reply.TrySetResult(41);
            if (starting is not null) { await starting.WaitAsync(TimeSpan.FromSeconds(5)); }
            if (stopping is not null) { await stopping.WaitAsync(TimeSpan.FromSeconds(5)); }
        }
    }

    private static InMemoryLeaderElection Create(InMemoryLeaderElectionSharedState state,
        TimeProvider? clock = null, IFencingTokenIssuer? fence = null) =>
        new("resource", Options.Create(new LeaderElectionOptions
        { InstanceId = "same-display", StepDownWhenUnhealthy = true, RenewInterval = TimeSpan.FromHours(1) }),
        null, state, clock, fence);

    private sealed class ThrowAfterMintClock : FakeTimeProvider
    {
        private int _armed;
        internal InvalidOperationException Failure { get; } = new("clock failed after successful mint");
        internal void Arm() => Volatile.Write(ref _armed, 1);
        public override DateTimeOffset GetUtcNow()
        {
            if (Interlocked.Exchange(ref _armed, 0) == 1) { throw Failure; }
            return base.GetUtcNow();
        }
    }
}
