// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;

using Excalibur.Dispatch.LeaderElection.Fencing;
using Excalibur.LeaderElection.MongoDB;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using MongoDB.Driver;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.LeaderElection;

/// <summary>
/// Author≠impl split-brain regression lock for 5fswhd (MongoDB leader-election fencing hardening, MS-05).
/// The MongoDB-backed <see cref="IFencingTokenProvider"/> mints a <b>strictly-monotonic</b> token from an
/// atomic server-side <c>$inc</c> counter — the first leader receives <c>1</c> and every acquisition is
/// strictly greater — so two candidates can never obtain the same fence, and a superseded leader's stale
/// token is rejected once the fence advances. Same contract as the committed Postgres / Redis / SqlServer
/// fencing siblings, different provider.
/// </summary>
/// <remarks>
/// <para>
/// <b>verify-against-real-infra-not-mock:</b> every fact runs against a real MongoDB (TestContainers) and
/// asserts observable behavior through the real atomic <c>findOneAndUpdate</c>/<c>$inc</c> CAS — a mocked
/// <c>IMongoCollection</c> cannot run <c>$inc</c> and would certify a broken (non-atomic) mint.
/// <c>DockerAvailable.ShouldBeTrue(...)</c> makes the lock NON-SKIPPED (a skipped split-brain safety test
/// is the exact gap that ships a split-brain bug). Per-test isolation via a unique database name.
/// </para>
/// <para>
/// <b>RED-on-mutant:</b> replace the atomic <c>$inc</c> mint with a non-atomic read-then-write or a
/// non-monotonic assignment and <see cref="ConcurrentMints_YieldDistinctContiguousTokens"/> (duplicate
/// tokens under concurrency) and <see cref="FirstToken_IsOne_ThenStrictlyMonotonicAcrossHandovers"/>
/// (first != 1 / not strictly increasing) go RED. A stale fence surviving the advance turns
/// <see cref="StaleToken_IsRejected_AfterFenceAdvances"/> RED.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "LeaderElection")]
[Trait("Database", "MongoDb")]
public sealed class MongoDbLeaderElectionFencingShould : IClassFixture<MongoDbContainerFixture>
{
	private readonly MongoDbContainerFixture _fixture;

	public MongoDbLeaderElectionFencingShould(MongoDbContainerFixture fixture) => _fixture = fixture;

	private IFencingTokenProvider CreateProvider(string databaseName, out ServiceProvider serviceProvider)
	{
		var services = new ServiceCollection();
		_ = services.AddSingleton<IMongoClient>(new MongoClient(_fixture.ConnectionString));
		_ = services.Configure<MongoDbLeaderElectionOptions>(o =>
		{
			o.ConnectionString = _fixture.ConnectionString;
			o.DatabaseName = databaseName;
			o.CollectionName = "leader_elections";
		});
		_ = services.AddMongoDbFencingTokenProvider();
		serviceProvider = services.BuildServiceProvider();
		return serviceProvider.GetRequiredService<IFencingTokenProvider>();
	}

	private static string UniqueDb() => "fence_" + Guid.NewGuid().ToString("N");

	[Fact]
	public async Task FirstToken_IsOne_ThenStrictlyMonotonicAcrossHandovers()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"5fswhd monotonic fencing is a split-brain safety control — this real-Mongo lock must never be skipped");

		var db = UniqueDb();
		var resourceId = "resource-" + Guid.NewGuid().ToString("N");

		// Instance A = the first leader.
		var providerA = CreateProvider(db, out var spA);
		await using var _spA = spA;

		var first = await providerA.IssueTokenAsync(resourceId, CancellationToken.None);
		first.ShouldBe(1L, "the first leader on a fresh per-resource counter receives fencing token 1");

		var second = await providerA.IssueTokenAsync(resourceId, CancellationToken.None);
		second.ShouldBeGreaterThan(first, "every subsequent mint is strictly greater (monotonic)");

		// Instance B = a fresh provider over the SAME real database (a handover to a new leader).
		var providerB = CreateProvider(db, out var spB);
		await using var _spB = spB;

		var afterHandover = await providerB.IssueTokenAsync(resourceId, CancellationToken.None);
		afterHandover.ShouldBeGreaterThan(second,
			"a new leader's token must be strictly greater than the prior leader's (monotonic across handovers)");
	}

	[Fact]
	public async Task ConcurrentMints_YieldDistinctContiguousTokens()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"5fswhd atomic-CAS mint is a split-brain safety control — this real-Mongo lock must never be skipped");

		var db = UniqueDb();
		var resourceId = "resource-" + Guid.NewGuid().ToString("N");
		var provider = CreateProvider(db, out var sp);
		await using var _sp = sp;

		// N candidates mint concurrently. The atomic server-side $inc guarantees each gets a DISTINCT token
		// — no two candidates can ever share a fence (the structural no-split-brain guarantee). A non-atomic
		// read-then-write mint would hand out duplicates under this contention -> RED.
		const int concurrency = 20;
		var tokens = await Task.WhenAll(
			Enumerable.Range(0, concurrency)
				.Select(_ => provider.IssueTokenAsync(resourceId, CancellationToken.None).AsTask()));

		var distinct = tokens.Distinct().ToList();
		distinct.Count.ShouldBe(concurrency, "atomic $inc must hand every concurrent mint a DISTINCT token (no split-brain fence)");
		// The tokens are exactly the contiguous set 1..N (a strictly-monotonic atomic counter, no gaps/dupes).
		distinct.Order().ShouldBe(Enumerable.Range(1, concurrency).Select(i => (long)i));
	}

	[Fact]
	public async Task StaleToken_IsRejected_AfterFenceAdvances()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"5fswhd fail-closed validation is a split-brain safety control — this real-Mongo lock must never be skipped");

		var db = UniqueDb();
		var resourceId = "resource-" + Guid.NewGuid().ToString("N");
		var provider = CreateProvider(db, out var sp);
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
