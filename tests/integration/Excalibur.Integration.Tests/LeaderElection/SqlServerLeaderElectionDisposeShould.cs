// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Linq;

using Excalibur.Dispatch.LeaderElection;
using Excalibur.Integration.Tests.Data;
using Excalibur.LeaderElection.Diagnostics;
using Excalibur.LeaderElection.SqlServer;

using Tests.Shared.Helpers;

namespace Excalibur.Integration.Tests.LeaderElection;

/// <summary>
/// Author≠impl regression lock for bd-8aef8d (+ 7npc0q-S2): disposing a leader without first calling
/// <see cref="SqlServerLeaderElection.StopAsync"/> must NOT leak a hot renewal error-spin.
/// </summary>
/// <remarks>
/// <para>
/// <b>Bug (8aef8d):</b> <see cref="SqlServerLeaderElection.DisposeAsync"/> set <c>_disposed = 1</c> and then
/// routed cleanup through <c>StopAsync</c>. <c>StopAsync</c> guards on <c>_disposed</c> and so threw
/// <see cref="ObjectDisposedException"/> — which disposal swallowed — BEFORE the renewal
/// <see cref="System.Threading.CancellationTokenSource"/> was cancelled. The CTS was then
/// <c>Dispose()</c>d but never <c>Cancel()</c>d, so the still-running (leaked) renewal loop hit
/// <see cref="System.Threading.Tasks.Task.Delay(System.TimeSpan, System.Threading.CancellationToken)"/> on a
/// disposed CTS, which throws <see cref="ObjectDisposedException"/> (NOT
/// <see cref="OperationCanceledException"/>, so the loop's break-guard never fired) on every iteration — a
/// continuous post-disposal <c>SqlServerRenewalError</c> (event id 184006) hot error-spin.
/// </para>
/// <para>
/// <b>Fix:</b> shared <c>StopCoreAsync()</c> (no <c>_disposed</c> check) is invoked by BOTH <c>StopAsync</c>
/// and <c>DisposeAsync</c>, so disposal deterministically Cancel()s + awaits the renewal loop. 7npc0q-S2: the
/// renewal-await OCE filter now keys off <c>ex.CancellationToken.IsCancellationRequested</c> (the loop's own
/// token), so the expected cancellation is observed rather than re-thrown.
/// </para>
/// <para>
/// <b>Non-vacuity (RED on the pre-fix mutant):</b> with DisposeAsync→StopAsync→swallowed
/// <see cref="ObjectDisposedException"/> ⇒ the leaked renewal loop keeps spinning AFTER DisposeAsync returns
/// ⇒ continuous post-disposal <c>SqlServerRenewalError</c> events ⇒ this lock fails. GREEN on the
/// <c>StopCoreAsync</c> extraction (loop cancelled+awaited, zero post-disposal renewal errors). Production
/// RED-proof against the reverted impl is deferred to post-commit batch verification (the impl file is
/// reserved by the implementer lane; the shared tree is currently non-compiling due to an unrelated in-flight
/// keystone, so this file is authored-and-saved, not built here).
/// </para>
/// <para>
/// NON-SKIPPED real-SqlServer test (<c>verify-against-real-infra-not-mock</c>): the bug is a real
/// <see cref="System.Threading.CancellationTokenSource"/>/renewal-loop lifecycle behavior that only manifests
/// once a live lock connection + a real renewal loop are running, so Docker availability is a hard
/// requirement, never a skip.
/// </para>
/// </remarks>
[Collection(SqlServerTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "LeaderElection")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerLeaderElectionDisposeShould : IntegrationTestBase
{
	private readonly SqlServerContainerFixture _fixture;

	public SqlServerLeaderElectionDisposeShould(SqlServerContainerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task NotLeakRenewalErrorSpinWhenDisposedWithoutStop()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"8aef8d Dispose-without-Stop hot-spin is a real-infra behavior — never skipped");

		// Arrange — a real leader over the real SQL Server with a SHORT renewal interval so the leaked
		// pre-fix loop would spin several times inside the post-disposal observation window. A unique lock
		// resource per test keeps it isolated/parallel-safe.
		var lockResource = $"8aef8d-dispose-{Guid.NewGuid():N}";
		var logger = new CapturingLogger<SqlServerLeaderElection>();
		var options = Microsoft.Extensions.Options.Options.Create(new LeaderElectionOptions
		{
			RenewInterval = TimeSpan.FromMilliseconds(150),
			GracePeriod = TimeSpan.FromSeconds(1),
			LeaseDuration = TimeSpan.FromSeconds(5),
			RetryInterval = TimeSpan.FromMilliseconds(500),
		});

		var leaderElection = new SqlServerLeaderElection(
			_fixture.ConnectionString,
			lockResource,
			options,
			logger);

		// Act 1 — acquire leadership and let the renewal loop run a few intervals.
		await leaderElection.StartAsync(TestCancellationToken);

		var becameLeader = await WaitForConditionAsync(
			() => leaderElection.IsLeader,
			timeout: TimeSpan.FromSeconds(5),
			pollInterval: TimeSpan.FromMilliseconds(50));
		becameLeader.ShouldBeTrue("StartAsync should have acquired leadership on the real SQL Server");

		// Let the renewal loop spin a few healthy iterations before disposal.
		await Task.Delay(TimeSpan.FromMilliseconds(500), TestCancellationToken);

		// Act 2 — dispose WITHOUT calling StopAsync first (the exact scenario that triggered 8aef8d), and
		// assert DisposeAsync returns PROMPTLY (it must cancel+await the loop, not hang).
		var disposeTask = leaderElection.DisposeAsync().AsTask();
		var finished = await Task.WhenAny(disposeTask, Task.Delay(TimeSpan.FromSeconds(10), TestCancellationToken));
		finished.ShouldBe(disposeTask, "8aef8d: DisposeAsync (without a prior StopAsync) must return promptly, not hang");
		await disposeTask; // observe completion / surface any fault

		// Assert — capture the renewal-error baseline at the instant DisposeAsync returned, then watch for
		// ANY new SqlServerRenewalError (184006) over ~5 renew intervals. The pre-fix leaked loop spins and
		// logs continuously here (RED); the fixed impl has already cancelled+awaited the loop (zero new
		// events ⇒ GREEN).
		var baseline = RenewalErrorCount(logger);

		var spinDetected = await WaitForConditionAsync(
			() => RenewalErrorCount(logger) > baseline,
			timeout: TimeSpan.FromSeconds(2),
			pollInterval: TimeSpan.FromMilliseconds(100));

		spinDetected.ShouldBeFalse(
			$"8aef8d: after DisposeAsync returned, the renewal loop must be cancelled+awaited — ZERO " +
			$"post-disposal '{nameof(LeaderElectionEventId.SqlServerRenewalError)}' (id " +
			$"{LeaderElectionEventId.SqlServerRenewalError}) events expected, but a hot error-spin was observed.");

		leaderElection.IsLeader.ShouldBeFalse("leadership must be relinquished after disposal");
	}

	private static int RenewalErrorCount(CapturingLogger<SqlServerLeaderElection> logger) =>
		logger.Entries.Count(e => e.EventId.Id == LeaderElectionEventId.SqlServerRenewalError);
}
