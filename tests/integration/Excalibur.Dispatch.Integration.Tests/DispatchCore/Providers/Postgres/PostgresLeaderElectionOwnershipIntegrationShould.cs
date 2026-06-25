// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.LeaderElection;
using Excalibur.LeaderElection.Postgres;

using Npgsql;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.Postgres;

/// <summary>
/// Author≠impl TestContainers regression lock for the <strong>Postgres</strong> half of <c>zg4zga</c>
/// (Sprint-846 Lane C, AC-C4 — ownership-not-liveness), symmetric to the SqlServer ownership lock.
///
/// <para>
/// The non-vacuous discriminator is <em>lock-loss on a live session</em> (not connection-loss): releasing
/// the session-level advisory lock on the provider's still-open connection (<c>pg_advisory_unlock</c>)
/// leaves the backend alive but holding no advisory lock, so the post-fix <c>VerifyLockAsync</c>
/// <c>pg_locks</c> ownership probe returns false while the pre-fix <c>SELECT 1</c> returns true (RED).
/// The lock is acquired via a direct reflect-invoke of <c>TryAcquireLockAsync</c> (not <c>StartAsync</c>)
/// to avoid the renewal-loop self-demotion race (PlatformDeveloper 14965). Required on both providers per
/// the converged ruling (ProductManager 14961 / ProjectManager 14962). (Postgres connection-hardening —
/// <c>Pooling=false</c> — is locked structurally in <c>PostgresLeaderElectionConnectionHardeningShould</c>.)
/// </para>
/// </summary>
[IntegrationTest]
[Collection(ContainerCollections.Postgres)]
[Trait("Component", "Platform")]
[Trait("Infrastructure", TestInfrastructure.Postgres)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait("Feature", "LeaderElection")]
public sealed class PostgresLeaderElectionOwnershipIntegrationShould : IntegrationTestBase
{
	private readonly PostgresFixture _fixture;

	public PostgresLeaderElectionOwnershipIntegrationShould(PostgresFixture fixture)
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
	public async Task VerifyLockReturnsFalse_WhenAdvisoryLockReleasedOnLiveSession()
	{
		// AC-C4 (ownership half) — the "alive-but-not-owning" state: the backend session stays OPEN but no
		// longer holds the advisory lock. VerifyLockAsync MUST report not-owning (pg_locks), not liveness.
		var lockKey = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		await using var sut = CreateElection(lockKey);

		// Acquire the advisory lock on the provider's _connection WITHOUT starting the renewal loop, so the
		// loop can't self-demote + null _connection underneath the assertion (PlatformDeveloper 14965).
		await InvokeTryAcquireLockAsync(sut);
		sut.IsLeader.ShouldBeTrue("precondition: TryAcquireLockAsync acquired the advisory lock");

		// Manufacture alive-but-not-owning on the provider's OWN live session (connection stays open).
		ReleaseAdvisoryLockOnLiveSession(sut, lockKey);

		// Assert — directly bind the pg_locks ownership probe. Non-vacuity: post-fix → false (GREEN); the
		// probe→SELECT-1 mutant / pre-fix returns true on the still-open backend (RED).
		var stillOwnsLock = await InvokeVerifyLockAsync(sut);
		stillOwnsLock.ShouldBeFalse(
			"VerifyLockAsync must verify OWNERSHIP (pg_locks), not mere liveness (SELECT 1)");
	}

	private static async Task InvokeTryAcquireLockAsync(PostgresLeaderElection election)
	{
		var method = typeof(PostgresLeaderElection).GetMethod(
			"TryAcquireLockAsync", BindingFlags.NonPublic | BindingFlags.Instance);
		method.ShouldNotBeNull();
		await (Task)method!.Invoke(election, [CancellationToken.None])!;
	}

	private static async Task<bool> InvokeVerifyLockAsync(PostgresLeaderElection election)
	{
		var method = typeof(PostgresLeaderElection).GetMethod(
			"VerifyLockAsync", BindingFlags.NonPublic | BindingFlags.Instance);
		method.ShouldNotBeNull();
		return await (Task<bool>)method!.Invoke(election, [CancellationToken.None])!;
	}

	/// <summary>
	/// Releases the session-level advisory lock on the provider's held-open backend connection (an advisory
	/// lock is released on its owning session), reaching alive-but-not-owning without closing the connection.
	/// </summary>
	private static void ReleaseAdvisoryLockOnLiveSession(PostgresLeaderElection election, long lockKey)
	{
		var field = typeof(PostgresLeaderElection).GetField(
			"_connection", BindingFlags.NonPublic | BindingFlags.Instance);
		field.ShouldNotBeNull("_connection (the held-open lock connection) must exist");
		var connection = (NpgsqlConnection)field!.GetValue(election)!;
		connection.ShouldNotBeNull("the lock connection must be open after the advisory lock is acquired");

		using var command = new NpgsqlCommand("SELECT pg_advisory_unlock(@lockKey)", connection);
		_ = command.Parameters.AddWithValue("lockKey", lockKey);
		var released = command.ExecuteScalar();

		// pg_advisory_unlock returns true iff a lock was actually released; false ⟹ the release did not
		// happen and this lock would be vacuous, so fail loud.
		(released as bool?).ShouldBe(true, "pg_advisory_unlock must release the held lock for this lock to be meaningful");
	}
}
