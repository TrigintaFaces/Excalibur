// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.LeaderElection;
using Excalibur.Dispatch.LeaderElection.Fencing;
using Excalibur.LeaderElection.Redis;

using Microsoft.Extensions.Logging.Abstractions;

using StackExchange.Redis;

namespace Excalibur.Integration.Tests.LeaderElection;

/// <summary>
/// Author≠impl regression lock for the coupled bd-762uzn + bd-msmwr9 fix (S854, SA seam #7, ADR-339):
/// the Redis leader-election fencing path is <b>fail-CLOSED</b> and <b>fence-before-event</b>. Both are a
/// single edit to <c>RedisLeaderElection.TryAcquireLockAsync</c> (mint the fence BEFORE declaring leadership;
/// relinquish on bounded-retry mint exhaustion), so they share one coupled lock.
/// </summary>
/// <remarks>
/// <para>
/// <b>verify-against-real-infra-not-mock:</b> both facts run against a real Redis (TestContainers) and assert
/// observable BEHAVIOR through the real seam — a candidate that cannot advance its fence must release the
/// real lock key, and the real <c>INCR</c> high-water mark must already be advanced when the leadership event
/// fires. <c>DockerAvailable.ShouldBeTrue(...)</c> makes the lock NON-SKIPPED (a skipped fencing safety test
/// is the exact gap that ships a split-brain bug). Serial (<c>-m:1</c>); per-test isolation via a unique lock
/// key so each <c>fencing:{key}</c> counter starts fresh.
/// </para>
/// <para>
/// <b>762uzn non-vacuity (RED on pre-fix):</b> the pre-fix impl was fail-OPEN — it declared leadership first
/// and minted the fence afterward, swallowing a mint failure. Against that mutant <see cref="IsLeader"/> would
/// be <c>true</c> and <c>BecameLeader</c> would have fired → both assertions RED. The fix mints BEFORE
/// <c>BecomeLeader</c> and, on bounded-retry exhaustion, calls <c>ReleaseLockAsync()</c> + returns without
/// ever declaring leadership.
/// </para>
/// <para>
/// <b>msmwr9 non-vacuity (RED on pre-fix):</b> when leadership events fired before the mint, the
/// <c>fencing:{key}</c> high-water mark read inside the <c>BecameLeader</c> handler would be <c>null</c>
/// (never issued yet) → <c>ShouldNotBeNull</c> RED. The fix advances the fence first, so the mark is already
/// ≥ 1 at event time.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "LeaderElection")]
[Trait("Database", "Redis")]
public sealed class RedisLeaderElectionFencingShould : IntegrationTestBase, IClassFixture<RedisContainerFixture>
{
	private readonly RedisContainerFixture _redisFixture;

	public RedisLeaderElectionFencingShould(RedisContainerFixture redisFixture)
	{
		_redisFixture = redisFixture;
	}

	[Fact]
	public async Task RelinquishWithoutBecomingLeaderWhenFencingMintFails()
	{
		// 762uzn — fail-CLOSED: a fencing-enabled candidate that cannot mint its fence must NOT lead.
		_redisFixture.DockerAvailable.ShouldBeTrue(
			"762uzn fail-closed fencing is a split-brain safety control — this real-Redis lock must never be skipped");

		await using var connection = await ConnectionMultiplexer.ConnectAsync(_redisFixture.ConnectionString);
		var lockKey = $"leader-{Guid.NewGuid():N}";

		var options = Microsoft.Extensions.Options.Options.Create(new LeaderElectionOptions
		{
			InstanceId = "candidate-A",
			LeaseDuration = TimeSpan.FromMinutes(10),
			// Neutralize the background renewal loop for the deterministic test window: its first iteration
			// waits one RenewInterval, so a long interval guarantees the loop never re-attempts acquire+mint
			// before we assert and StopAsync. The lease remains longer than the renewal interval.
			RenewInterval = TimeSpan.FromMinutes(5),
			GracePeriod = TimeSpan.FromSeconds(5),
		});

		var becameLeaderFired = 0;
		await using var election = new RedisLeaderElection(
			connection,
			lockKey,
			options,
			NullLogger<RedisLeaderElection>.Instance,
			new RedisLeaderElectionContext { FencingTokenProvider = new AlwaysThrowingFencingTokenProvider() });
		election.BecameLeader += (_, _) => Interlocked.Increment(ref becameLeaderFired);

		// Act — StartAsync runs the initial acquire: SET NX acquires the key, then every bounded mint attempt
		// throws -> the impl releases the key and returns WITHOUT declaring leadership. StopAsync then halts
		// the (neutralized) renewal loop deterministically.
		await election.StartAsync(TestCancellationToken);
		await election.StopAsync(TestCancellationToken);

		// Assert — leadership was NEVER declared (fail-CLOSED, not the pre-fix fail-OPEN).
		election.IsLeader.ShouldBeFalse(
			"a candidate that cannot advance its fence must NOT become leader (fail-closed, no un-fenced leadership)");
		Volatile.Read(ref becameLeaderFired).ShouldBe(0,
			"BecameLeader must never fire when the fence mint failed — leadership is structurally coupled to a minted fence");

		// Assert — relinquish proof through real infra: the lock key was RELEASED, so a fresh candidate can
		// SET NX acquire it. A fail-OPEN impl would still hold the key (leadership declared) -> this is false.
		var db = connection.GetDatabase();
		var reacquiredByAnother = await db.StringSetAsync(
			lockKey, "candidate-B", TimeSpan.FromMinutes(1), When.NotExists);
		reacquiredByAnother.ShouldBeTrue(
			"the relinquished lock key must be free for another candidate to acquire (the real-infra relinquish proof)");
	}

	[Fact]
	public async Task AdvanceFenceHighWaterMarkBeforeBecameLeaderFires()
	{
		// msmwr9 — event-ordering: the fence high-water mark must already be advanced when BecameLeader fires.
		_redisFixture.DockerAvailable.ShouldBeTrue(
			"msmwr9 fence-before-event ordering is a split-brain safety control — this real-Redis lock must never be skipped");

		await using var connection = await ConnectionMultiplexer.ConnectAsync(_redisFixture.ConnectionString);
		var lockKey = $"leader-{Guid.NewGuid():N}";

		// Resolve the real Redis fencing provider through the public registration (it is internal) — the same
		// INCR-backed provider the production wiring uses.
		var services = new ServiceCollection();
		services.AddSingleton<IConnectionMultiplexer>(connection);
		services.AddRedisFencingTokenProvider();
		await using var serviceProvider = services.BuildServiceProvider();
		var fencing = serviceProvider.GetRequiredService<IFencingTokenIssuer>();

		var options = Microsoft.Extensions.Options.Options.Create(new LeaderElectionOptions
		{
			InstanceId = "candidate-A",
			LeaseDuration = TimeSpan.FromMinutes(10),
			RenewInterval = TimeSpan.FromMinutes(5),
			GracePeriod = TimeSpan.FromSeconds(5),
		});

		var delivered = new TaskCompletionSource<long?>(TaskCreationOptions.RunContinuationsAsynchronously);
		await using var election = new RedisLeaderElection(
			connection,
			lockKey,
			options,
			NullLogger<RedisLeaderElection>.Instance,
			new RedisLeaderElectionContext { FencingTokenProvider = fencing });

		// Keep the real GET inside the delivered callback. Notification delivery is queued independently
		// of StartAsync, so await the captured observation rather than assume Start joined the callback.
		election.BecameLeader += (_, _) =>
		{
			try
			{
			#pragma warning disable RS0030 // bd-c36hwe: sync-over-async debt (migrate to await/poll)
				var observed = fencing.GetTokenAsync(lockKey, CancellationToken.None).AsTask().GetAwaiter().GetResult();
			#pragma warning restore RS0030
				delivered.TrySetResult(observed);
			}
			catch (Exception error)
			{
				delivered.TrySetException(error);
			}
		};

		// Act — StartAsync mints the fence (INCR -> 1) BEFORE declaring leadership, firing BecameLeader.
		await election.StartAsync(TestCancellationToken);
		var highWaterMarkAtEventTime = await delivered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestCancellationToken);

		// Assert (before StopAsync relinquishes) — leadership achieved AND the fence was already advanced when
		// the callback observed it. This does not establish when a delayed notification was originally queued.
		election.IsLeader.ShouldBeTrue("with a working fence provider the candidate must become leader");
		highWaterMarkAtEventTime.ShouldNotBeNull(
			"the fence high-water mark must already be advanced when BecameLeader fires (event strictly AFTER the mint)");
		highWaterMarkAtEventTime!.Value.ShouldBeGreaterThanOrEqualTo(1L,
			"INCR on a fresh per-resource counter advances the high-water mark to >= 1 before leadership is declared");

		await election.StopAsync(TestCancellationToken);
	}

	/// <summary>
	/// A fencing-token provider whose mint always fails — drives the 762uzn fail-CLOSED relinquish path.
	/// </summary>
	private sealed class AlwaysThrowingFencingTokenProvider : IFencingTokenIssuer
	{
		public ValueTask<long> IssueTokenAsync(string resourceId, CancellationToken cancellationToken)
			=> throw new InvalidOperationException("Simulated fencing-store outage: token mint unavailable.");

		public ValueTask<long?> GetTokenAsync(string resourceId, CancellationToken cancellationToken)
			=> throw new NotSupportedException("Not exercised by the fail-closed acquisition path.");

		public ValueTask<bool> ValidateTokenAsync(string resourceId, long token, CancellationToken cancellationToken)
			=> throw new NotSupportedException("Not exercised by the fail-closed acquisition path.");
	}
}
