// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.LeaderElection.Postgres;
using Excalibur.Dispatch;
using Excalibur.Dispatch.LeaderElection;

using Npgsql;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.Postgres;

[IntegrationTest]
[Collection(ContainerCollections.Postgres)]
[Trait("Component", TestComponents.Data)]
[Trait("Infrastructure", TestInfrastructure.Postgres)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait("Component", "Platform")]
public sealed class PostgresLeaderElectionIntegrationShould : IntegrationTestBase
{
	private readonly PostgresFixture _fixture;

	public PostgresLeaderElectionIntegrationShould(PostgresFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task Leader_election_start_and_stop_acquires_and_releases_lock()
	{
		await using var election = CreateLeaderElection();
		var becameLeader = false;
		var lostLeadership = false;
		election.BecameLeader += (_, _) => becameLeader = true;
		election.LostLeadership += (_, _) => lostLeadership = true;

		await election.StartAsync(TestCancellationToken);
		await election.StopAsync(TestCancellationToken);

		becameLeader.ShouldBeTrue();
		lostLeadership.ShouldBeTrue();
		election.IsLeader.ShouldBeFalse();
		election.CurrentLeaderId.ShouldBeNull();
	}

	[Fact]
	public async Task Leader_election_dispose_while_started_is_safe()
	{
		var election = CreateLeaderElection();

		await election.StartAsync(TestCancellationToken);

		await Should.NotThrowAsync(() => election.DisposeAsync().AsTask());
	}

	[Fact]
	public async Task Leader_election_second_candidate_remains_follower_while_lock_is_held()
	{
		var sharedLockKey = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		await using var leader = CreateLeaderElection(lockKey: sharedLockKey);
		await using var follower = CreateLeaderElection(lockKey: sharedLockKey);
		var leaderAcquired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		leader.BecameLeader += (_, _) => leaderAcquired.TrySetResult();

		await leader.StartAsync(TestCancellationToken);
		await follower.StartAsync(TestCancellationToken);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
				leaderAcquired.Task,
				global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(5)),
				cancellationToken: TestCancellationToken)
			;

		leader.IsLeader.ShouldBeTrue();
		follower.IsLeader.ShouldBeFalse();

		await follower.StopAsync(TestCancellationToken);
		await leader.StopAsync(TestCancellationToken);
	}

	[Fact]
	public async Task Leader_election_second_candidate_takes_leadership_after_primary_stops()
	{
		var sharedLockKey = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		await using var leader = CreateLeaderElection(lockKey: sharedLockKey);
		await using var follower = CreateLeaderElection(lockKey: sharedLockKey);
		var leaderAcquired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var followerAcquired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		leader.BecameLeader += (_, _) => leaderAcquired.TrySetResult();
		follower.BecameLeader += (_, _) => followerAcquired.TrySetResult();

		await leader.StartAsync(TestCancellationToken);
		await follower.StartAsync(TestCancellationToken);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
				leaderAcquired.Task,
				global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(5)),
				cancellationToken: TestCancellationToken)
			;

		leader.IsLeader.ShouldBeTrue();
		follower.IsLeader.ShouldBeFalse();

		await leader.StopAsync(TestCancellationToken);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
				followerAcquired.Task,
				global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(10)),
				cancellationToken: TestCancellationToken)
			;

		follower.IsLeader.ShouldBeTrue();
		await follower.StopAsync(TestCancellationToken);
	}

	[Fact]
	public async Task Leader_election_loses_leadership_when_connection_breaks_past_grace_period()
	{
		var sharedLockKey = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		await using var leader = CreateLeaderElection(
			lockKey: sharedLockKey,
			renewInterval: TimeSpan.FromMilliseconds(100),
			gracePeriod: TimeSpan.FromMilliseconds(100));
		var leaderAcquired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var leadershipLost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		leader.BecameLeader += (_, _) => leaderAcquired.TrySetResult();
		leader.LostLeadership += (_, _) => leadershipLost.TrySetResult();

		await leader.StartAsync(TestCancellationToken);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
				leaderAcquired.Task,
				global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(10)),
				cancellationToken: TestCancellationToken)
			;

		var connectionField = typeof(PostgresLeaderElection).GetField(
			"_connection",
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
		connectionField.SetValue(leader, null);

		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
				leadershipLost.Task,
				global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(10)),
				cancellationToken: TestCancellationToken)
			;

		leader.IsLeader.ShouldBeFalse();
		await leader.StopAsync(TestCancellationToken);
	}

	private PostgresLeaderElection CreateLeaderElection(
		long? lockKey = null,
		TimeSpan? leaseDuration = null,
		TimeSpan? renewInterval = null,
		TimeSpan? gracePeriod = null)
	{
		var pgOptions = new PostgresLeaderElectionOptions
		{
			ConnectionString = _fixture.ConnectionString,
			LockKey = lockKey ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
			CommandTimeoutSeconds = 5
		};

		var electionOptions = new LeaderElectionOptions
		{
			LeaseDuration = leaseDuration ?? TimeSpan.FromSeconds(5),
			RenewInterval = renewInterval ?? TimeSpan.FromMilliseconds(200),
			GracePeriod = gracePeriod ?? TimeSpan.FromSeconds(2)
		};

		return new PostgresLeaderElection(
			Microsoft.Extensions.Options.Options.Create(pgOptions),
			Microsoft.Extensions.Options.Options.Create(electionOptions),
			EnabledTestLogger.Create<PostgresLeaderElection>());
	}

}
