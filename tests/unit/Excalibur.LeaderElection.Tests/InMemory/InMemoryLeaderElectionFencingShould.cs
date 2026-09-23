// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.LeaderElection.Fencing;

namespace Excalibur.LeaderElection.Tests.InMemory;

/// <summary>
/// LIVENESS arm for b8ht6u's InMemory fencing build-out: proves the DI-auto-registered
/// <see cref="InMemoryFencingTokenProvider"/> is genuinely wired into <see cref="InMemoryLeaderElection"/>'s
/// acquisition path (not merely resolvable), issues real tokens, and fails closed on exhaustion — the
/// counterpart to <see cref="InMemoryLeaderElectionFencingTokenShould"/>'s SAFETY-only "unconfigured => null"
/// lock.
/// </summary>
/// <remarks>
/// Per the b8ht6u PM ruling: InMemory leader election is single-process by construction, so an in-process
/// monotonic counter (<see cref="InMemoryLeaderElectionSharedState.FencingTokens"/>, minted via
/// <c>ConcurrentDictionary.AddOrUpdate</c>) is a genuinely correct arbitrated fencing source here — not a
/// weaker stand-in for the distributed providers' external counters. No Docker/TestContainers needed: the
/// election, the provider, and the counter are all in-process.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryLeaderElectionFencingShould : UnitTestBase
{
	[Fact]
	public async Task IssueTokenOne_OnFirstAcquisition_WhenTheDefaultProviderIsUsed()
	{
		// Arrange — the DI auto-registration path (b8ht6u), not a hand-wired provider.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddInMemoryLeaderElection();
		await using var provider = services.BuildServiceProvider(validateScopes: false);

		var factory = provider.GetRequiredKeyedService<ILeaderElectionFactory>("default");
		var resourceName = $"fencing-liveness-{Guid.NewGuid():N}";
		var election = factory.CreateElection(resourceName, candidateId: null);

		// Act
		await election.StartAsync(TestContext.Current.CancellationToken);
		await WaitUntilAsync(() => election.CurrentLeadership is not null, TimeSpan.FromSeconds(5));

		// Assert — LIVENESS: real acquisition, real token, not a stub that refuses every call.
		election.CurrentLeadership.ShouldNotBeNull("the sole candidate must acquire leadership");
		election.CurrentLeadership!.Value.FencingToken.ShouldBe(1L,
			"the DI-registered default InMemoryFencingTokenProvider must actually be consulted on acquisition " +
			"and mint the first token for a fresh resource, not leave the tenure's token null.");

		await ((IAsyncDisposable)election).DisposeAsync();
	}

	[Fact]
	public async Task IssueStrictlyIncreasingTokens_AcrossHandovers()
	{
		// Arrange — two independent elections over the SAME resource + shared state, modelling a handover
		// from one leader to the next (the provider mints from process-shared state, not per-instance state).
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddInMemoryLeaderElection();
		await using var provider = services.BuildServiceProvider(validateScopes: false);

		var factory = provider.GetRequiredKeyedService<ILeaderElectionFactory>("default");
		var resourceName = $"fencing-handover-{Guid.NewGuid():N}";

		var electionA = factory.CreateElection(resourceName, candidateId: "candidate-a");
		await electionA.StartAsync(TestContext.Current.CancellationToken);
		await WaitUntilAsync(() => electionA.CurrentLeadership is not null, TimeSpan.FromSeconds(5));
		var firstToken = electionA.CurrentLeadership!.Value.FencingToken;
		await electionA.StopAsync(TestContext.Current.CancellationToken);
		await ((IAsyncDisposable)electionA).DisposeAsync();

		// Act — a fresh election instance (the "successor") acquires the same now-vacant resource.
		var electionB = factory.CreateElection(resourceName, candidateId: "candidate-b");
		await electionB.StartAsync(TestContext.Current.CancellationToken);
		await WaitUntilAsync(() => electionB.CurrentLeadership is not null, TimeSpan.FromSeconds(5));
		var secondToken = electionB.CurrentLeadership!.Value.FencingToken;
		await ((IAsyncDisposable)electionB).DisposeAsync();

		// Assert
		firstToken.ShouldBe(1L);
		secondToken.ShouldNotBeNull();
		secondToken!.Value.ShouldBeGreaterThan(firstToken!.Value,
			"a successor's fencing token must be strictly greater than its predecessor's — the whole point of " +
			"the fence is that a stale leader's (lower) token is distinguishable from the current leader's.");
	}

	[Fact]
	public async Task RelinquishRatherThanLead_WhenTheTokenDomainIsExhausted()
	{
		// Arrange — seed the shared counter one below the int64 ceiling so the FIRST mint attempted by
		// StartAsync is the one that overflows, real-infra-free (an in-process dictionary write).
		var sharedState = new InMemoryLeaderElectionSharedState();
		var resourceName = $"fencing-exhaustion-{Guid.NewGuid():N}";
		sharedState.FencingTokens[resourceName] = long.MaxValue;

		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton(sharedState);
		_ = services.AddInMemoryLeaderElection();
		await using var provider = services.BuildServiceProvider(validateScopes: false);

		var factory = provider.GetRequiredKeyedService<ILeaderElectionFactory>("default");
		var election = factory.CreateElection(resourceName, candidateId: null);

		AcquisitionFailedReason? failedReason = null;
		election.AcquisitionFailed += (_, args) => failedReason = new AcquisitionFailedReason(args.Reason);

		// Act
		await election.StartAsync(TestContext.Current.CancellationToken);
		await WaitUntilAsync(() => failedReason is not null, TimeSpan.FromSeconds(5));

		// Assert — SAFETY: exhaustion must relinquish (fail closed), never wrap to a reused low token and
		// declare leadership anyway.
		election.CurrentLeadership.ShouldBeNull(
			"a leadership attempt whose fencing mint overflows the int64 domain must NOT declare leadership — " +
			"wrapping to a reused low token would let a later, genuinely-stale leader validate as current.");
		failedReason.ShouldNotBeNull("AcquisitionFailed must fire so the caller can observe the fail-closed relinquish.");
		failedReason!.Value.Reason.ShouldBe("fencing token domain exhausted");

		await ((IAsyncDisposable)election).DisposeAsync();
	}

	[Fact]
	public async Task IssueTokenViaTheProviderDirectly_AndThrowOnExhaustion_MatchingAllOtherProviders()
	{
		// A tighter, non-election-mediated proof of the provider's own contract (IssueTokenAsync /
		// GetTokenAsync / ValidateTokenAsync), mirroring the shape of the other six providers' unit tests.
		var sharedState = new InMemoryLeaderElectionSharedState();
		var fencingProvider = new InMemoryFencingTokenProvider(sharedState);
		var resourceId = $"fencing-direct-{Guid.NewGuid():N}";

		var first = await fencingProvider.IssueTokenAsync(resourceId, TestContext.Current.CancellationToken);
		first.ShouldBe(1L);

		var second = await fencingProvider.IssueTokenAsync(resourceId, TestContext.Current.CancellationToken);
		second.ShouldBeGreaterThan(first);

		(await fencingProvider.ValidateTokenAsync(resourceId, second, TestContext.Current.CancellationToken))
			.ShouldBeTrue("the current token must validate");
		(await fencingProvider.ValidateTokenAsync(resourceId, first, TestContext.Current.CancellationToken))
			.ShouldBeFalse("a stale (superseded) token must be rejected fail-closed");

		sharedState.FencingTokens[resourceId] = long.MaxValue;
		_ = await Should.ThrowAsync<FencingTokenExhaustedException>(
			() => fencingProvider.IssueTokenAsync(resourceId, TestContext.Current.CancellationToken).AsTask());
	}

	private readonly record struct AcquisitionFailedReason(string Reason);
}
