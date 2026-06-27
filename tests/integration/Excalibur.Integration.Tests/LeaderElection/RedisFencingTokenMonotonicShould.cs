// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection.Fencing;

using StackExchange.Redis;

namespace Excalibur.Integration.Tests.LeaderElection;

/// <summary>
/// Author≠impl regression lock for bd-umemwa AC-D2 clause (a) (ADR-339): the Redis fencing-token provider
/// must mint STRICTLY-MONOTONIC tokens via an atomic Redis <c>INCR</c> — no two acquisitions ever receive
/// the same token, even under concurrency.
/// </summary>
/// <remarks>
/// <para>
/// Non-vacuity (RED): a fencing token's whole purpose is split-brain protection — a stale leader must hold
/// a strictly-lower token than the new leader. A non-atomic read-modify-write mint (or a constant/duplicate
/// token) would let two concurrent acquisitions receive the SAME value, so the distinct-count would be less
/// than the issuance count -> RED. Redis <c>INCR</c> is atomic and monotonic (a missing key initializes to
/// 1), so N concurrent issuances against a fresh per-resource counter yield exactly the contiguous set
/// {1..N} — all distinct. Exercised through the public <c>AddRedisFencingTokenProvider()</c> registration
/// against a real Redis instance.
/// </para>
/// <para>Serial (-m:1, real Redis via TestContainers). Per-test isolation via a unique resource id.</para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "LeaderElection")]
[Trait("Database", "Redis")]
public sealed class RedisFencingTokenMonotonicShould : IntegrationTestBase, IClassFixture<RedisContainerFixture>
{
	private readonly RedisContainerFixture _redisFixture;

	public RedisFencingTokenMonotonicShould(RedisContainerFixture redisFixture)
	{
		_redisFixture = redisFixture;
	}

	[Fact]
	public async Task IssueStrictlyMonotonicDistinctTokensUnderConcurrency()
	{
		// Arrange — public registration path: IConnectionMultiplexer + AddRedisFencingTokenProvider().
		await using var connection = await ConnectionMultiplexer.ConnectAsync(_redisFixture.ConnectionString);

		var services = new ServiceCollection();
		services.AddSingleton<IConnectionMultiplexer>(connection);
		services.AddRedisFencingTokenProvider();

		await using var provider = services.BuildServiceProvider();
		var fencing = provider.GetRequiredService<IFencingTokenProvider>();

		var resourceId = $"resource-{Guid.NewGuid():N}";
		const int issuances = 50;

		// Act — issue concurrently against a fresh counter.
		var tasks = Enumerable.Range(0, issuances)
			.Select(_ => Task.Run(async () => await fencing.IssueTokenAsync(resourceId, TestCancellationToken), TestCancellationToken));
		var tokens = await Task.WhenAll(tasks);

		// Assert — all distinct (no two acquisitions share a token) forming the contiguous set {1..N}.
		tokens.Distinct().Count().ShouldBe(issuances, "every issued fencing token must be unique");
		tokens.Min().ShouldBe(1L, "INCR on a fresh counter starts at 1");
		tokens.Max().ShouldBe((long)issuances, "INCR is strictly monotonic: N issuances -> max token N");
	}
}
