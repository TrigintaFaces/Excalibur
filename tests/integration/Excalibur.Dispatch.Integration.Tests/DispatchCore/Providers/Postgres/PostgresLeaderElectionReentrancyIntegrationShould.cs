// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection;
using Excalibur.LeaderElection.Postgres;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.Postgres;

/// <summary>
/// Author≠impl TestContainers regression lock for the Postgres leader-election provider's <c>1qvg3k</c>
/// reentrancy fix (Sprint-846 Lane C, AC-C2): consumer event handlers (<c>LostLeadership</c>/
/// <c>LeaderChanged</c>) are raised OUTSIDE the internal lock, so a handler that calls back into the
/// election object (e.g. reads <see cref="ILeaderElection.CurrentLeaderId"/>, which takes the lock) from
/// another thread does not deadlock against the raising thread.
///
/// (The Postgres AC-C4 ownership guarantee is structural — see
/// <c>PostgresLeaderElectionConnectionHardeningShould</c> — because Npgsql has no transparent reconnect.)
/// </summary>
[IntegrationTest]
[Collection(ContainerCollections.Postgres)]
[Trait("Component", "Platform")]
[Trait("Infrastructure", TestInfrastructure.Postgres)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait("Feature", "LeaderElection")]
public sealed class PostgresLeaderElectionReentrancyIntegrationShould : IntegrationTestBase
{
	private readonly PostgresFixture _fixture;

	public PostgresLeaderElectionReentrancyIntegrationShould(PostgresFixture fixture)
	{
		_fixture = fixture;
	}

	private PostgresLeaderElection CreateElection(long lockKey)
	{
		var pgOptions = new PostgresLeaderElectionOptions
		{
			ConnectionString = _fixture.ConnectionString,
			LockKey = lockKey,
			CommandTimeoutSeconds = 5,
		};

		var electionOptions = new LeaderElectionOptions
		{
			// Cross-property rule (ol729k): Renew + Grace + 1s skew < Lease.
			LeaseDuration = TimeSpan.FromSeconds(10),
			RenewInterval = TimeSpan.FromSeconds(1),
			GracePeriod = TimeSpan.FromSeconds(1),
		};

		return new PostgresLeaderElection(
			Microsoft.Extensions.Options.Options.Create(pgOptions),
			Microsoft.Extensions.Options.Options.Create(electionOptions),
			EnabledTestLogger.Create<PostgresLeaderElection>());
	}

	[Fact]
	public async Task RaiseLostLeadershipOutsideLock_NoReentrantDeadlock_OnStop()
	{
		// AC-C2 — a handler probing CurrentLeaderId (which takes the internal lock) from another thread
		// must not deadlock against the raising thread.
		var lockKey = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		await using var sut = CreateElection(lockKey);
		await sut.StartAsync(TestCancellationToken);
		sut.IsLeader.ShouldBeTrue("precondition: acquired Postgres advisory-lock leadership");

		var lockHeldDuringHandler = false;
		sut.LostLeadership += (_, _) =>
		{
			using var probed = new ManualResetEventSlim(initialState: false);
			_ = Task.Run(() =>
			{
				_ = sut.CurrentLeaderId; // getter takes the internal lock
				probed.Set();
			});
			lockHeldDuringHandler = !probed.Wait(TimeSpan.FromSeconds(2));
		};

		// Act
		await sut.StopAsync(TestCancellationToken);

		// Assert — non-vacuity: pre-fix the handler is raised INSIDE the lock → the cross-thread probe
		// blocks → true (RED). Post-fix (raised outside) → false (GREEN).
		lockHeldDuringHandler.ShouldBeFalse();
	}
}
