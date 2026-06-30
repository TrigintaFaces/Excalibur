// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection;
using Excalibur.LeaderElection.Redis;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using StackExchange.Redis;

namespace Excalibur.Integration.Tests.LeaderElection;

/// <summary>
/// Real-infrastructure liveness lock for <see cref="RedisLeaderElection"/> (vdvkkd-a): once a candidate becomes
/// leader, its renewal loop keeps the Redis lease alive past the lease duration — leadership is retained without any
/// external renewal driver, because the renewer is started up front (renewer-before-acquire).
/// </summary>
/// <remarks>
/// Short <c>LeaseDuration</c> + short <c>RenewInterval</c>: acquire, then wait well past the lease with no external
/// action. The lock key (the Redis SET-NX key) must still exist (the renewer kept extending its TTL) and
/// <see cref="RedisLeaderElection.IsLeader"/> stays true. <b>RED behavior:</b> a renewer that never runs (or is
/// started after acquire and torn down in the gap, the pre-fix window) lets the lease lapse at the lease duration →
/// the key expires → leadership silently lost. Never skipped.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Database", "Redis")]
[Trait("Component", "LeaderElection")]
public sealed class RedisLeaderElectionRenewerLivenessShould : IClassFixture<RedisContainerFixture>
{
	private readonly RedisContainerFixture _fixture;

	public RedisLeaderElectionRenewerLivenessShould(RedisContainerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task Keep_the_lease_alive_past_the_lease_duration_via_the_renewer()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Redis must be available - real-infra leader-election liveness is never skipped.");

		await using var connection = await ConnectionMultiplexer.ConnectAsync(_fixture.ConnectionString).ConfigureAwait(false);
		var lockKey = $"leader-liveness-{Guid.NewGuid():N}";

		var options = Options.Create(new LeaderElectionOptions
		{
			InstanceId = "candidate-A",
			LeaseDuration = TimeSpan.FromSeconds(2),  // short: a non-renewing leader's key would expire here
			RenewInterval = TimeSpan.FromMilliseconds(500),
			GracePeriod = TimeSpan.FromSeconds(1),
		});

		await using var election = new RedisLeaderElection(
			connection, lockKey, options, NullLogger<RedisLeaderElection>.Instance);

		await election.StartAsync(CancellationToken.None);
		election.IsLeader.ShouldBeTrue("the candidate must acquire leadership on start");

		var db = connection.GetDatabase();

		// Wait well past the lease duration (3× = 6s) with NO external renewal — only the internal renewer keeps it.
		await Task.Delay(TimeSpan.FromSeconds(6), CancellationToken.None).ConfigureAwait(false);

		// The renewer kept extending the lease: the key still exists and leadership is retained.
		(await db.KeyExistsAsync(lockKey)).ShouldBeTrue(
			"the renewer must keep the Redis lease key alive past the lease duration (a non-renewing leader's key "
			+ "would have expired)");
		election.IsLeader.ShouldBeTrue("leadership must be retained while the renewer keeps the lease alive");

		await election.StopAsync(CancellationToken.None);
	}
}
