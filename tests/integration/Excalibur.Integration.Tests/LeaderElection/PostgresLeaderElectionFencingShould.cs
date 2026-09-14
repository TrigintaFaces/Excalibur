// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection.Fencing;

using Microsoft.Extensions.DependencyInjection;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.LeaderElection;

/// <summary>
/// Concurrent-atomicity coverage for pc9azv (real-Postgres twin of
/// <see cref="MongoDbLeaderElectionFencingShould"/>). SA spot-verified the Postgres mint IS atomic
/// (<c>nextval</c> on a dedicated per-resource <c>SEQUENCE</c>) -- this is a coverage gap, not a bug fix: no
/// N-concurrent-<c>IssueTokenAsync</c>-&gt;N-distinct-tokens lock had ever exercised it.
/// </summary>
/// <remarks>
/// <para>
/// <b>verify-against-real-infra-not-mock:</b> every fact runs against a real Postgres (TestContainers) and
/// asserts observable behavior through the real atomic <c>nextval</c> sequence advance -- a mocked
/// connection cannot run a server-side sequence and would certify a broken (non-atomic) mint. Docker
/// availability makes the lock NON-SKIPPED. Per-test isolation via a unique resource id (the sequence name
/// is hash-derived from it, so a fresh resource id is a fresh sequence).
/// </para>
/// <para>
/// <b>RED-on-mutant:</b> replace the atomic <c>nextval</c> mint with a non-atomic read-then-increment and
/// <see cref="ConcurrentMints_YieldDistinctContiguousTokens"/> (duplicate tokens under contention) goes RED.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "LeaderElection")]
[Trait("Database", "Postgres")]
public sealed class PostgresLeaderElectionFencingShould : IClassFixture<PostgresContainerFixture>
{
	private readonly PostgresContainerFixture _fixture;

	public PostgresLeaderElectionFencingShould(PostgresContainerFixture fixture) => _fixture = fixture;

	private IFencingTokenProvider CreateProvider(out ServiceProvider serviceProvider)
	{
		var services = new ServiceCollection();
		_ = services.AddPostgresFencingTokenProvider(_fixture.ConnectionString);
		serviceProvider = services.BuildServiceProvider();
		return serviceProvider.GetRequiredService<IFencingTokenProvider>();
	}

	private static string UniqueResourceId() => "resource-" + Guid.NewGuid().ToString("N");

	[Fact]
	public async Task FirstToken_IsOne_ThenStrictlyMonotonicAcrossHandovers()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"pc9azv concurrent-atomicity coverage is a split-brain safety control -- this real-Postgres lock must never be skipped");

		var resourceId = UniqueResourceId();

		var providerA = CreateProvider(out var spA);
		await using var _spA = spA;

		var first = await providerA.IssueTokenAsync(resourceId, CancellationToken.None);
		first.ShouldBe(1L, "the first leader on a fresh per-resource sequence receives fencing token 1");

		var second = await providerA.IssueTokenAsync(resourceId, CancellationToken.None);
		second.ShouldBeGreaterThan(first, "every subsequent mint is strictly greater (monotonic)");

		// Instance B = a fresh provider over the SAME real database (a handover to a new leader).
		var providerB = CreateProvider(out var spB);
		await using var _spB = spB;

		var afterHandover = await providerB.IssueTokenAsync(resourceId, CancellationToken.None);
		afterHandover.ShouldBeGreaterThan(second,
			"a new leader's token must be strictly greater than the prior leader's (monotonic across handovers)");
	}

	[Fact]
	public async Task ConcurrentMints_YieldDistinctContiguousTokens()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"pc9azv atomic-mint coverage is a split-brain safety control -- this real-Postgres lock must never be skipped");

		var resourceId = UniqueResourceId();
		var provider = CreateProvider(out var sp);
		await using var _sp = sp;

		// N candidates mint concurrently. The atomic server-side nextval guarantees each gets a DISTINCT
		// token -- no two candidates can ever share a fence (the structural no-split-brain guarantee). A
		// non-atomic read-then-write mint would hand out duplicates under this contention -> RED.
		const int concurrency = 20;
		var tokens = await Task.WhenAll(
			Enumerable.Range(0, concurrency)
				.Select(_ => provider.IssueTokenAsync(resourceId, CancellationToken.None).AsTask()));

		var distinct = tokens.Distinct().ToList();
		distinct.Count.ShouldBe(concurrency, "atomic nextval must hand every concurrent mint a DISTINCT token (no split-brain fence)");
		// The tokens are exactly the contiguous set 1..N (a strictly-monotonic atomic sequence, no gaps/dupes).
		distinct.Order().ShouldBe(Enumerable.Range(1, concurrency).Select(i => (long)i));
	}

	[Fact]
	public async Task StaleToken_IsRejected_AfterFenceAdvances()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"pc9azv fail-closed validation coverage is a split-brain safety control -- this real-Postgres lock must never be skipped");

		var resourceId = UniqueResourceId();
		var provider = CreateProvider(out var sp);
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
