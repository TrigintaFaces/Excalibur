// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.LeaderElection.Fencing;

using StackExchange.Redis;

namespace Excalibur.Integration.Tests.LeaderElection;

/// <summary>Distinguishes a real remote mint from delivery of its result to the caller.</summary>
[Trait("Category", "Integration")]
[Trait("Component", "LeaderElection")]
[Trait("Database", "Redis")]
public sealed class RedisFencingUnknownOutcomeShould(RedisContainerFixture fixture)
	: IntegrationTestBase, IClassFixture<RedisContainerFixture>
{
	[Fact]
	public async Task RetainTheRealMintWhenItsResultCannotReachTheIssuerCaller()
	{
		fixture.DockerAvailable.ShouldBeTrue("unknown-outcome fencing requires a real Redis counter, not a simulated one");
		await using var connection = await ConnectionMultiplexer.ConnectAsync(fixture.ConnectionString);
		var database = connection.GetDatabase();
		var resource = $"unknown-outcome-{Guid.NewGuid():N}";
		RedisKey expectedKey = "fencing:" + resource;
		var lostReply = new IOException("Injected transport failure after a successful real INCR");
		var increments = 0;
		long committedWithoutReturn = 0;

		// Only delivery is fault-injected. Every increment and read uses the same actual Redis database.
		// This is not a claim that a real TCP response was dropped.
		var forwardingDatabase = A.Fake<IDatabase>();
		using var forwardingConnection = A.Fake<IConnectionMultiplexer>();
		A.CallTo(() => forwardingConnection.GetDatabase(A<int>._, A<object>._)).Returns(forwardingDatabase);
		A.CallTo(() => forwardingDatabase.StringIncrementAsync(A<RedisKey>._, A<long>._, A<CommandFlags>._))
			.ReturnsLazily((RedisKey key, long increment, CommandFlags flags) => IncrementAndDeliverAsync(key, increment, flags));
		A.CallTo(() => forwardingDatabase.StringGetAsync(A<RedisKey>._, A<CommandFlags>._))
			.ReturnsLazily((RedisKey key, CommandFlags flags) =>
			{
				key.ShouldBe(expectedKey);
				flags.ShouldBe(CommandFlags.DemandMaster);
				return database.StringGetAsync(key, flags);
			});

		var services = new ServiceCollection();
		services.AddSingleton(forwardingConnection);
		services.AddRedisFencingTokenProvider();
		await using var providerScope = services.BuildServiceProvider();
		var provider = providerScope.GetRequiredService<IFencingTokenIssuer>();
		var first = await provider.IssueTokenAsync(resource, TestCancellationToken);
		first.ShouldBe(1L);
		(await database.StringGetAsync(expectedKey)).ShouldBe((RedisValue)1L);

		var observed = await Should.ThrowAsync<IOException>(() => provider.IssueTokenAsync(resource, TestCancellationToken).AsTask());
		observed.ShouldBeSameAs(lostReply);
		increments.ShouldBe(2);
		committedWithoutReturn.ShouldBe(2L);
		(await database.StringGetAsync(expectedKey)).ShouldBe((RedisValue)2L);
		(await provider.GetTokenAsync(resource, TestCancellationToken)).ShouldBe(2L);
		(await provider.ValidateTokenAsync(resource, first, TestCancellationToken)).ShouldBeFalse();

		var next = await provider.IssueTokenAsync(resource, TestCancellationToken);
		next.ShouldBe(3L);
		increments.ShouldBe(3);
		(await provider.ValidateTokenAsync(resource, next, TestCancellationToken)).ShouldBeTrue();
		(await database.StringGetAsync(expectedKey)).ShouldBe((RedisValue)3L);

		async Task<long> IncrementAndDeliverAsync(RedisKey key, long increment, CommandFlags flags)
		{
			key.ShouldBe(expectedKey);
			increment.ShouldBe(1L);
			flags.ShouldBe(CommandFlags.None);
			var minted = await database.StringIncrementAsync(key, increment, flags).ConfigureAwait(false);
			if (++increments == 2)
			{
				committedWithoutReturn = minted;
				throw lostReply;
			}

			return minted;
		}
	}
}
