// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Consul;

using Excalibur.Dispatch.LeaderElection.Fencing;
using Excalibur.LeaderElection.Consul;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Integration.Tests.LeaderElection;

/// <summary>
/// Concurrent-atomicity coverage for pc9azv (real-Consul twin of
/// <see cref="MongoDbLeaderElectionFencingShould"/>). SA spot-verified the Consul mint IS atomic
/// (Check-And-Set on the KV key's <c>ModifyIndex</c>) -- this is a coverage gap, not a bug fix: no
/// N-concurrent-<c>IssueTokenAsync</c>-&gt;N-distinct-tokens lock had ever exercised it.
/// </summary>
/// <remarks>
/// <para>
/// <b>verify-against-real-infra-not-mock:</b> every fact runs against a real Consul server (Testcontainers,
/// via <see cref="ConsulContainerFixture"/>) and asserts observable behavior through the real atomic CAS --
/// a mocked <c>IConsulClient</c> cannot run a server-side ModifyIndex CAS and would certify a broken
/// (non-atomic) mint. Docker availability makes the lock NON-SKIPPED. Per-test isolation via a unique
/// resource id (the KV counter key is derived from it).
/// </para>
/// <para>
/// <b>Concurrency is 8, not 20</b> -- deliberately matching <c>ConsulFencingTokenProvider</c>'s own bounded
/// CAS retry budget (<c>MaxCasAttempts = 8</c>). Each concurrent caller gets its own 8-attempt budget, so
/// this stays comfortably within it while still proving genuine concurrent atomicity; the sibling
/// Mongo/Postgres locks use 20 because those mints are single-round-trip atomic server operations with no
/// retry budget to exhaust.
/// </para>
/// <para>
/// <b>RED-on-mutant:</b> replace the atomic ModifyIndex CAS with an unconditional read-then-write and
/// <see cref="ConcurrentMints_YieldDistinctContiguousTokens"/> (duplicate tokens under contention) goes RED.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "LeaderElection")]
[Trait("Database", "Consul")]
[Collection(ConsulLeaderElectionTestCollection.CollectionName)]
public sealed class ConsulLeaderElectionFencingShould
{
	private readonly ConsulContainerFixture _fixture;

	public ConsulLeaderElectionFencingShould(ConsulContainerFixture fixture) => _fixture = fixture;

	private IFencingTokenProvider CreateProvider(string keyPrefix, out ServiceProvider serviceProvider)
	{
		var services = new ServiceCollection();
		_ = services.AddSingleton<IConsulClient>(_ => new ConsulClient(config =>
		{
			config.Address = new Uri(_fixture.ConsulAddress);
		}));
		_ = services.Configure<ConsulLeaderElectionOptions>(o => o.KeyPrefix = keyPrefix);
		_ = services.AddConsulFencingTokenProvider();
		serviceProvider = services.BuildServiceProvider();
		return serviceProvider.GetRequiredService<IFencingTokenProvider>();
	}

	private static string UniqueKeyPrefix() => "fence-" + Guid.NewGuid().ToString("N");

	private static string UniqueResourceId() => "resource-" + Guid.NewGuid().ToString("N");

	[Fact]
	public async Task FirstToken_IsOne_ThenStrictlyMonotonicAcrossHandovers()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"pc9azv concurrent-atomicity coverage is a split-brain safety control -- this real-Consul lock must never be skipped");

		var keyPrefix = UniqueKeyPrefix();
		var resourceId = UniqueResourceId();

		var providerA = CreateProvider(keyPrefix, out var spA);
		await using var _spA = spA;

		var first = await providerA.IssueTokenAsync(resourceId, CancellationToken.None);
		first.ShouldBe(1L, "the first leader on a fresh per-resource counter key receives fencing token 1");

		var second = await providerA.IssueTokenAsync(resourceId, CancellationToken.None);
		second.ShouldBeGreaterThan(first, "every subsequent mint is strictly greater (monotonic)");

		// Instance B = a fresh provider over the SAME real Consul KV key (a handover to a new leader).
		var providerB = CreateProvider(keyPrefix, out var spB);
		await using var _spB = spB;

		var afterHandover = await providerB.IssueTokenAsync(resourceId, CancellationToken.None);
		afterHandover.ShouldBeGreaterThan(second,
			"a new leader's token must be strictly greater than the prior leader's (monotonic across handovers)");
	}

	[Fact]
	public async Task ConcurrentMints_YieldDistinctContiguousTokens()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"pc9azv atomic-mint coverage is a split-brain safety control -- this real-Consul lock must never be skipped");

		var keyPrefix = UniqueKeyPrefix();
		var resourceId = UniqueResourceId();
		var provider = CreateProvider(keyPrefix, out var sp);
		await using var _sp = sp;

		// N candidates mint concurrently. The atomic server-side ModifyIndex CAS guarantees each gets a
		// DISTINCT token -- no two candidates can ever share a fence (the structural no-split-brain
		// guarantee). An unconditional read-then-write mint would hand out duplicates under this
		// contention -> RED. See the class remarks for why N=8 here, not 20.
		const int concurrency = 8;
		var tokens = await Task.WhenAll(
			Enumerable.Range(0, concurrency)
				.Select(_ => provider.IssueTokenAsync(resourceId, CancellationToken.None).AsTask()));

		var distinct = tokens.Distinct().ToList();
		distinct.Count.ShouldBe(concurrency, "atomic CAS must hand every concurrent mint a DISTINCT token (no split-brain fence)");
		// The tokens are exactly the contiguous set 1..N (a strictly-monotonic atomic counter, no gaps/dupes).
		distinct.Order().ShouldBe(Enumerable.Range(1, concurrency).Select(i => (long)i));
	}

	[Fact]
	public async Task StaleToken_IsRejected_AfterFenceAdvances()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"pc9azv fail-closed validation coverage is a split-brain safety control -- this real-Consul lock must never be skipped");

		var keyPrefix = UniqueKeyPrefix();
		var resourceId = UniqueResourceId();
		var provider = CreateProvider(keyPrefix, out var sp);
		await using var _sp = sp;

		var stale = await provider.IssueTokenAsync(resourceId, CancellationToken.None);
		var current = await provider.IssueTokenAsync(resourceId, CancellationToken.None);

		// After the fence advances, the prior (stale) token falls below the high-water mark and is rejected;
		// the current token still validates. This is the fail-closed check that fences off a superseded leader.
		(await provider.ValidateTokenAsync(resourceId, stale, CancellationToken.None)).ShouldBeFalse(
			"a superseded leader's stale token must be rejected once the fence advanced (fail-closed)");
		(await provider.ValidateTokenAsync(resourceId, current, CancellationToken.None)).ShouldBeTrue(
			"the current high-water token must still validate");
	}
}
