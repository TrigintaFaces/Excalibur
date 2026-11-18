// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.LeaderElection.Tests.Redis;

/// <summary>
/// Verifies authoritative leadership observations and safe concurrent lifecycle access (AD-540.3).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class RedisLeaderElectionVolatileShould : UnitTestBase
{
	[Fact]
	public Task PublishLeadershipThatIsVisibleAcrossThreads() =>
		RedisTenureProtocolShould.VerifyPublishedLeadershipVisibilityAcrossThreadsAsync();

	[Fact]
	public void ReturnFalseForIsLeaderByDefault()
	{
		// Arrange
		var fakeMultiplexer = A.Fake<IConnectionMultiplexer>();
		var leaderElection = new RedisLeaderElection(
			fakeMultiplexer,
			"test-key",
			Microsoft.Extensions.Options.Options.Create(new LeaderElectionOptions()),
			NullLogger<RedisLeaderElection>.Instance);

		// Act & Assert
		leaderElection.IsLeader.ShouldBeFalse();
	}

	[Fact]
	public Task SerializeConcurrentStopsWhileLeadershipIsObserved() =>
		RedisTenureProtocolShould.VerifyConcurrentStopAndLeadershipReadsAsync();

	[Fact]
	public void ReadIsLeaderWithoutContention()
	{
		// Arrange
		var fakeMultiplexer = A.Fake<IConnectionMultiplexer>();
		var leaderElection = new RedisLeaderElection(
			fakeMultiplexer,
			"test-key",
			Microsoft.Extensions.Options.Options.Create(new LeaderElectionOptions()),
			NullLogger<RedisLeaderElection>.Instance);

		// Act — concurrent reads should not deadlock or throw
		var results = new bool[1000];
		Parallel.For(0, 1000, i => { results[i] = leaderElection.IsLeader; });

		// Assert — all reads should return consistent value (false since not started)
		results.ShouldAllBe(v => v == false);
	}
}
