// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.LeaderElection.Tests.Redis;

/// <summary>
/// Author≠impl regression lock for <c>1qvg3k</c> (Sprint-846 Lane C) on the Redis provider: the
/// <c>LostLeadership</c>/<c>LeaderChanged</c> consumer event handlers must be raised <em>outside</em>
/// the internal <c>_lock</c> (state snapshotted under the lock, handlers invoked after release), so a
/// handler that calls back into the election object (e.g. reads <see cref="ILeaderElection.CurrentLeaderId"/>,
/// which takes <c>_lock</c>) on another thread does not deadlock against the raising thread.
///
/// Redis is the unit-constructable provider (ctor takes <c>IConnectionMultiplexer</c>); SqlServer/Postgres
/// reach leader state only with a real DB, so their AC-C2 locks are TestContainers-gated.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class RedisLeaderElectionReentrancyShould : UnitTestBase
{
	private static RedisLeaderElection CreateAcquiringElection()
	{
		var multiplexer = A.Fake<IConnectionMultiplexer>();
		var database = A.Fake<IDatabase>();
		A.CallTo(() => multiplexer.GetDatabase(A<int>._, A<object>._)).Returns(database);

		// SET key value NX PX <lease> succeeds → the election acquires leadership synchronously in
		// StartAsync. Match any StringSetAsync overload returning Task<bool> to avoid binding to a
		// specific (and version-dependent) parameter list.
		A.CallTo(database)
			.Where(call => call.Method.Name == nameof(IDatabase.StringSetAsync))
			.WithReturnType<Task<bool>>()
			.Returns(true);

		return new RedisLeaderElection(
			multiplexer,
			"reentrancy-test-key",
			Microsoft.Extensions.Options.Options.Create(new LeaderElectionOptions()),
			NullLogger<RedisLeaderElection>.Instance);
	}

	[Fact]
	public async Task RaiseLostLeadershipOutsideLock_NoReentrantDeadlock_OnStop()
	{
		// Arrange — become leader, then attach a handler that probes CurrentLeaderId from ANOTHER thread.
		// C# locks are reentrant on the SAME thread, so the probe must run on a different thread to observe
		// "lock still held while the handler runs".
		await using var sut = CreateAcquiringElection();
		await sut.StartAsync(CancellationToken.None);
		sut.IsLeader.ShouldBeTrue("precondition: the fake Redis SET must drive the election to leadership");

		var lockHeldDuringHandler = false;
		sut.LostLeadership += (_, _) =>
		{
			using var probed = new ManualResetEventSlim(initialState: false);
			// CurrentLeaderId's getter takes _lock; if the handler is raised while _lock is held, this
			// background read blocks until the handler returns → the wait times out.
			_ = Task.Run(() =>
			{
				_ = sut.CurrentLeaderId;
				probed.Set();
			});
			lockHeldDuringHandler = !probed.Wait(TimeSpan.FromSeconds(2));
		};

		// Act
		await sut.StopAsync(CancellationToken.None);

		// Assert — non-vacuity: pre-fix the handler is raised INSIDE _lock → the cross-thread probe blocks
		// → lockHeldDuringHandler == true (RED). Post-fix (raised outside) → false (GREEN).
		lockHeldDuringHandler.ShouldBeFalse();
	}

	[Fact]
	public async Task RaiseLostLeadershipOutsideLock_NoReentrantDeadlock_OnDispose()
	{
		// Arrange — the same reentrancy guarantee must hold on the DisposeAsync path (also routes through
		// StopCoreAsync), not just explicit StopAsync.
		var sut = CreateAcquiringElection();
		await sut.StartAsync(CancellationToken.None);
		sut.IsLeader.ShouldBeTrue("precondition: leadership acquired");

		var lockHeldDuringHandler = false;
		sut.LostLeadership += (_, _) =>
		{
			using var probed = new ManualResetEventSlim(initialState: false);
			_ = Task.Run(() =>
			{
				_ = sut.CurrentLeaderId;
				probed.Set();
			});
			lockHeldDuringHandler = !probed.Wait(TimeSpan.FromSeconds(2));
		};

		// Act
		await sut.DisposeAsync();

		// Assert
		lockHeldDuringHandler.ShouldBeFalse();
	}
}
