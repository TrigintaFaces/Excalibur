// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;
using System.Reflection;

using Excalibur.Dispatch.LeaderElection;
using Excalibur.LeaderElection.SqlServer;

using Microsoft.Data.SqlClient;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.SqlServer;

/// <summary>
/// Author≠impl TestContainers regression locks for the SqlServer leader-election provider (Sprint-846
/// Lane C): <c>1qvg3k</c> (AC-C2 reentrancy) and <c>zg4zga</c> (AC-C4 ownership-not-liveness).
///
/// <para>
/// <b>AC-C4 (the split-brain invariant)</b> is bound on both halves:
/// 1. <b>Ownership probe</b> — <c>VerifyLockAsync</c> → <c>APPLOCK_MODE != 'NoLock'</c> (replacing
///    <c>SELECT 1</c>) — guarded behaviorally HERE. The non-vacuous discriminator is <em>lock-loss on a
///    live session</em>, NOT connection-loss: <c>sp_releaseapplock</c> on the provider's still-open
///    connection leaves it alive but no longer owning the applock, so the post-fix probe returns false
///    while the pre-fix <c>SELECT 1</c> returns true (RED). A hard <c>KILL</c> is vacuous (it closes the
///    connection → pre-fix <c>SELECT 1</c> also fails). Credit: ProductManager 14952; mechanism per
///    PlatformDeveloper 14965. The lock is acquired via a direct reflect-invoke of
///    <c>TryAcquireLockAsync</c> (not <c>StartAsync</c>) to avoid the renewal-loop self-demotion race.
/// 2. <b>Connection hardening</b> (<c>ConnectRetryCount=0</c> + <c>Pooling=false</c>) — guarded
///    structurally in <c>SqlServerLeaderElectionConnectionHardeningShould</c>.
/// </para>
/// </summary>
[IntegrationTest]
[Collection(ContainerCollections.SqlServer)]
[Trait("Component", "Platform")]
[Trait("Infrastructure", TestInfrastructure.SqlServer)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait("Feature", "LeaderElection")]
public sealed class SqlServerLeaderElectionIntegrationShould : IntegrationTestBase
{
	private readonly SqlServerFixture _fixture;

	public SqlServerLeaderElectionIntegrationShould(SqlServerFixture fixture)
	{
		_fixture = fixture;
	}

	private SqlServerLeaderElection CreateElection(string lockResource)
	{
		var electionOptions = new LeaderElectionOptions
		{
			// Cross-property rule (ol729k): Renew + Grace + 1s skew < Lease (1+1+1 < 10).
			LeaseDuration = TimeSpan.FromSeconds(10),
			RenewInterval = TimeSpan.FromSeconds(1),
			GracePeriod = TimeSpan.FromSeconds(1),
		};

		return new SqlServerLeaderElection(
			_fixture.ConnectionString,
			lockResource,
			Microsoft.Extensions.Options.Options.Create(electionOptions),
			EnabledTestLogger.Create<SqlServerLeaderElection>());
	}

	[Fact]
	public async Task RaiseLostLeadershipOutsideLock_NoReentrantDeadlock_OnStop()
	{
		// AC-C2 — a handler that calls back into the election (CurrentLeaderId getter takes the internal
		// lock) from another thread must not deadlock against the raising thread.
		var resource = "le-sql-reentrancy-" + Guid.NewGuid().ToString("N");
		await using var sut = CreateElection(resource);
		await sut.StartAsync(TestCancellationToken);
		sut.IsLeader.ShouldBeTrue("precondition: acquired SqlServer applock leadership");

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
		// blocks → true (RED, verified in the pre-fix worktree). Post-fix (raised outside) → false (GREEN).
		lockHeldDuringHandler.ShouldBeFalse();
	}

	[Fact]
	public async Task VerifyLockReturnsFalse_WhenApplockReleasedOnLiveSession()
	{
		// AC-C4 (ownership half, :409) — the "alive-but-not-owning" state: the lock-holding session stays
		// OPEN but no longer owns the applock. VerifyLockAsync MUST report not-owning (APPLOCK_MODE), not
		// merely alive (SELECT 1).
		var resource = "le-sql-ownership-" + Guid.NewGuid().ToString("N");
		await using var sut = CreateElection(resource);

		// Acquire the applock on the provider's _connection WITHOUT starting the renewal loop, so the loop
		// can't self-demote + null _connection underneath the assertion (PlatformDeveloper 14965).
		await InvokeTryAcquireLockAsync(sut);
		sut.IsLeader.ShouldBeTrue("precondition: TryAcquireLockAsync acquired the applock");

		// Manufacture alive-but-not-owning on the provider's OWN live session (connection stays Open).
		ReleaseApplockOnLiveSession(sut, resource);

		// Assert — directly bind the :409 probe. Non-vacuity: post-fix APPLOCK_MODE='NoLock' → false
		// (GREEN); the :409→SELECT-1 mutant / pre-fix returns true on the still-open connection (RED).
		var stillOwnsLock = await InvokeVerifyLockAsync(sut);
		stillOwnsLock.ShouldBeFalse(
			"VerifyLockAsync must verify OWNERSHIP (APPLOCK_MODE), not mere liveness (SELECT 1)");
	}

	private static async Task InvokeTryAcquireLockAsync(SqlServerLeaderElection election)
	{
		var method = typeof(SqlServerLeaderElection).GetMethod(
			"TryAcquireLockAsync", BindingFlags.NonPublic | BindingFlags.Instance);
		method.ShouldNotBeNull();
		await (Task)method!.Invoke(election, [CancellationToken.None])!;
	}

	private static async Task<bool> InvokeVerifyLockAsync(SqlServerLeaderElection election)
	{
		var method = typeof(SqlServerLeaderElection).GetMethod(
			"VerifyLockAsync", BindingFlags.NonPublic | BindingFlags.Instance);
		method.ShouldNotBeNull();
		return await (Task<bool>)method!.Invoke(election, [CancellationToken.None])!;
	}

	/// <summary>
	/// Releases the session-scoped <c>sp_getapplock</c> on the provider's held-open lock connection (an
	/// applock can only be released on its owning session), reaching the alive-but-not-owning state without
	/// closing the connection. Reflection reaches the live session only; the connection is not substituted.
	/// </summary>
	private static void ReleaseApplockOnLiveSession(SqlServerLeaderElection election, string resource)
	{
		var field = typeof(SqlServerLeaderElection).GetField(
			"_connection", BindingFlags.NonPublic | BindingFlags.Instance);
		field.ShouldNotBeNull("_connection (the held-open lock connection) must exist");
		var connection = (SqlConnection)field!.GetValue(election)!;
		connection.ShouldNotBeNull("the lock connection must be open after the applock is acquired");

		using var command = new SqlCommand("sp_releaseapplock", connection) { CommandType = CommandType.StoredProcedure };
		_ = command.Parameters.AddWithValue("@Resource", resource);
		_ = command.Parameters.AddWithValue("@LockOwner", "Session");
		var returnValue = command.Parameters.Add("@ReturnValue", SqlDbType.Int);
		returnValue.Direction = ParameterDirection.ReturnValue;

		_ = command.ExecuteNonQuery();

		// sp_releaseapplock returns >= 0 on success; a negative code means the release did not happen (which
		// would make this lock vacuous), so fail loud rather than silently testing nothing.
		Convert.ToInt32(returnValue.Value, System.Globalization.CultureInfo.InvariantCulture)
			.ShouldBeGreaterThanOrEqualTo(0, "sp_releaseapplock must succeed for this lock to be meaningful");
	}
}
