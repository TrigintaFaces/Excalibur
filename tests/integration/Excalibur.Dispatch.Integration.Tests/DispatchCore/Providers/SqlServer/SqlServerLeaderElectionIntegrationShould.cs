// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using System.Reflection;

using Excalibur.Dispatch.LeaderElection;
using Excalibur.LeaderElection.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Time.Testing;

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
///    PlatformDeveloper 14965. Public StartAsync and a controlled renewal clock bind the current protocol;
///    reflection only obtains the owned physical connection for fault injection, never a private probe.
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

	private SqlServerLeaderElection CreateElection(string lockResource, TimeProvider? timeProvider = null)
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
			EnabledTestLogger.Create<SqlServerLeaderElection>(), timeProvider: timeProvider);
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
			#pragma warning disable RS0030 // bd-c36hwe: sync-over-async debt (migrate to await/poll)
			lockHeldDuringHandler = !probed.Wait(TimeSpan.FromSeconds(2));
			#pragma warning restore RS0030
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
		var resource = "le-sql-ownership-" + Guid.NewGuid().ToString("N");
		var clock = new RenewalClock();
		await using var sut = CreateElection(resource, clock);
		var stoppedFromLoss = new TaskCompletionSource<Task>(TaskCreationOptions.RunContinuationsAsynchronously);
		sut.LostLeadership += (_, _) => stoppedFromLoss.TrySetResult(sut.StopAsync(CancellationToken.None));
		await sut.StartAsync(TestCancellationToken);
		sut.IsLeader.ShouldBeTrue("precondition: public StartAsync acquired the applock");
		sut.CurrentLeadership.ShouldNotBeNull();
		await clock.RenewalArmed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestCancellationToken);

		var connection = ReadOwnedConnection(sut);
		var before = await ReadSessionProofAsync(connection, resource, TestCancellationToken);
		before.Mode.ShouldBe("Exclusive");
		before.Alive.ShouldBe(1);
		await ReleaseApplockOnLiveSessionAsync(connection, resource, TestCancellationToken);
		connection.State.ShouldBe(ConnectionState.Open);
		var after = await ReadSessionProofAsync(connection, resource, TestCancellationToken);
		after.Session.ShouldBe(before.Session, "fault injection must not replace or kill the owning SQL session");
		after.Mode.ShouldBe("NoLock");
		after.Alive.ShouldBe(1, "SELECT 1 remains true and cannot establish ownership");
		sut.IsLeader.ShouldBeTrue("the controlled clock has not yet admitted the next ownership probe");

		// Advance only to renewal, NOT the two-second local deadline. A SELECT-1 ownership
		// mutant cannot satisfy LostLeadership by ordinary deadline expiry in this arm.
		clock.Advance(TimeSpan.FromSeconds(1));
		await (await stoppedFromLoss.Task.WaitAsync(TimeSpan.FromSeconds(5), TestCancellationToken))
			.WaitAsync(TimeSpan.FromSeconds(5), TestCancellationToken);
		sut.IsLeader.ShouldBeFalse("the public protocol must observe absent ownership on a still-live session");
		sut.CurrentLeadership.ShouldBeNull();
	}

	private static SqlConnection ReadOwnedConnection(SqlServerLeaderElection election)
	{
		// Read-only fault injection, not an oracle for leadership. No provider private method is invoked.
		var field = typeof(SqlServerLeaderElection).GetField("_slot", BindingFlags.NonPublic | BindingFlags.Instance);
		field.ShouldNotBeNull();
		var slot = field.GetValue(election).ShouldNotBeNull();
		var run = ReadProperty(slot, "Run");
		var kernel = ReadProperty(run, "Kernel");
		var tenure = ReadProperty(kernel, "Current");
		var session = ReadProperty(tenure, "OwnershipProof");
		var connection = session.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
			.Single(candidate => candidate.FieldType == typeof(SqlConnection));
		return connection.GetValue(session).ShouldBeOfType<SqlConnection>();
	}

	private static object ReadProperty(object value, string name)
	{
		var property = value.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
		property.ShouldNotBeNull();
		return property.GetValue(value).ShouldNotBeNull();
	}

	private static async Task<(int Session, string Mode, int Alive)> ReadSessionProofAsync(
		SqlConnection connection, string resource, CancellationToken cancellationToken)
	{
		await using var command = connection.CreateCommand();
		command.CommandText = "SELECT CONVERT(int, @@SPID), APPLOCK_MODE(N'public', @Resource, N'Session'), 1";
		command.CommandTimeout = 3;
		command.Parameters.Add("@Resource", SqlDbType.NVarChar, 255).Value = resource;
		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		(await reader.ReadAsync(cancellationToken)).ShouldBeTrue();
		return (reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2));
	}

	private static async Task ReleaseApplockOnLiveSessionAsync(SqlConnection connection, string resource,
		CancellationToken cancellationToken)
	{
		await using var command = new SqlCommand("sp_releaseapplock", connection) { CommandType = CommandType.StoredProcedure };
		command.Parameters.Add("@Resource", SqlDbType.NVarChar, 255).Value = resource;
		command.Parameters.Add("@LockOwner", SqlDbType.VarChar, 32).Value = "Session";
		var returned = command.Parameters.Add("@ReturnValue", SqlDbType.Int);
		returned.Direction = ParameterDirection.ReturnValue;
		_ = await command.ExecuteNonQueryAsync(cancellationToken);
		Convert.ToInt32(returned.Value, System.Globalization.CultureInfo.InvariantCulture).ShouldBeGreaterThanOrEqualTo(0,
			"sp_releaseapplock must actually succeed before the ownership probe is tested");
	}

	private sealed class RenewalClock : FakeTimeProvider
	{
		internal TaskCompletionSource RenewalArmed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
		public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
		{
			var timer = base.CreateTimer(callback, state, dueTime, period);
			if (dueTime == TimeSpan.FromSeconds(1)) { RenewalArmed.TrySetResult(); }
			return timer;
		}
	}
}
