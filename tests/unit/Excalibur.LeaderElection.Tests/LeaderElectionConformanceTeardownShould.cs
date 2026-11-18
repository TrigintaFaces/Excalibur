// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Tests.Shared.Conformance.LeaderElection;

namespace Excalibur.LeaderElection.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
[Trait("Pattern", "Regression")]
public sealed class LeaderElectionConformanceTeardownShould
{
    private static readonly string[] ExpectedOrder = ["dispose", "cleanup"];
    [Fact]
    public async Task RetireTheOwnerBeforeDestroyingItsBorrowedResources()
    {
        var order = new List<string>();
        var election = A.Fake<ILeaderElection>();
        A.CallTo(() => election.DisposeAsync()).ReturnsLazily(() =>
        {
            order.Add("dispose");
            return ValueTask.CompletedTask;
        });
        var fixture = new Fixture(election, () => { order.Add("cleanup"); return Task.CompletedTask; });
        await fixture.InitializeAsync();
        await fixture.DisposeAsync();
        order.ShouldBe(ExpectedOrder);
    }

    [Fact]
    public async Task StillCleanUpAndPreserveTheOriginalDisposalFailure()
    {
        var failure = new IOException("owned disposal");
        var election = A.Fake<ILeaderElection>();
        A.CallTo(() => election.DisposeAsync()).ReturnsLazily(() => ValueTask.FromException(failure));
        var cleaned = false;
        var fixture = new Fixture(election, () => { cleaned = true; return Task.CompletedTask; });
        await fixture.InitializeAsync();
        var actual = await Should.ThrowAsync<IOException>(async () => await fixture.DisposeAsync());
        actual.ShouldBeSameAs(failure);
        cleaned.ShouldBeTrue();
    }

    [Fact]
    public async Task PreserveBothFailuresInDisposalThenCleanupOrder()
    {
        var disposal = new IOException("owned disposal");
        var cleanup = new InvalidOperationException("borrowed resource cleanup");
        var election = A.Fake<ILeaderElection>();
        A.CallTo(() => election.DisposeAsync()).ReturnsLazily(() => ValueTask.FromException(disposal));
        var fixture = new Fixture(election, () => Task.FromException(cleanup));
        await fixture.InitializeAsync();
        var actual = await Should.ThrowAsync<AggregateException>(async () => await fixture.DisposeAsync());
        actual.InnerExceptions.Count.ShouldBe(2);
        actual.InnerExceptions[0].ShouldBeSameAs(disposal);
        actual.InnerExceptions[1].ShouldBeSameAs(cleanup);
    }

    [Fact]
    public async Task CleanUpPartialInitializationWithoutDisposingAnAbsentOwner()
    {
        var setup = new IOException("initialization failed");
        var cleaned = false;
        var fixture = new Fixture(null, () => { cleaned = true; return Task.CompletedTask; }, setup);
        (await Should.ThrowAsync<IOException>(async () => await fixture.InitializeAsync())).ShouldBeSameAs(setup);
        await fixture.DisposeAsync();
        cleaned.ShouldBeTrue();
    }

    private sealed class Fixture(ILeaderElection? election, Func<Task> cleanup, Exception? setup = null)
        : LeaderElectionConformanceTestBase
    {
        protected override Task<ILeaderElection> CreateElectionAsync() => setup is null
            ? Task.FromResult(election!) : Task.FromException<ILeaderElection>(setup);
        protected override Task<ILeaderElection> CreateCompetingElectionAsync() => throw new NotSupportedException();
        protected override Task CleanupAsync() => cleanup();
    }
}
