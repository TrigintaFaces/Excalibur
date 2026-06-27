// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Coordination;
using Excalibur.Jobs.Redis.Coordination;

using Microsoft.Extensions.Logging.Abstractions;

using StackExchange.Redis;

namespace Excalibur.Integration.Tests.Jobs;

/// <summary>
/// Author≠impl regression lock for bd-jqlqc8 (MS-A2, split-brain / mutual-exclusion):
/// a stale Redis job-lock handle (whose TTL expired and whose lock was re-acquired by another holder) must
/// NOT release or extend the new holder's lock — release/extend act only when the caller still owns the
/// lock (per-acquisition owner-token compare-and-act).
/// </summary>
/// <remarks>
/// <para>
/// Non-vacuity (RED on the pre-fix code): pre-fix <c>ReleaseAsync</c> did an unconditional
/// <c>KeyDeleteAsync(lockKey)</c> and <c>ExtendAsync</c> an unconditional <c>KeyExpireAsync</c> — neither
/// checked the stored owner token. So a stale handle A would DELETE (or re-expire) holder B's lock, letting
/// two instances run the same job. The fix guards both with an atomic Lua <c>GET == token</c> check, so a
/// stale A's release/extend is a no-op (returns false / leaves B intact). This test acquires with A, lets
/// A's short TTL expire, re-acquires with B, then asserts A.ExtendAsync returns false and A.ReleaseAsync
/// leaves B's lock claimable-by-no-one-else.
/// </para>
/// <para>Serial (-m:1, real Redis via TestContainers). Per-test isolation via a unique key prefix + job key.</para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Jobs")]
[Trait("Database", "Redis")]
public sealed class RedisJobLockOwnershipShould : IntegrationTestBase, IClassFixture<RedisContainerFixture>
{
	private readonly RedisContainerFixture _redisFixture;

	public RedisJobLockOwnershipShould(RedisContainerFixture redisFixture)
	{
		_redisFixture = redisFixture;
	}

	[Fact]
	public async Task NotReleaseOrExtendLockReacquiredByAnotherHolderAfterTtlExpiry()
	{
		// Arrange
		await using var connection = await ConnectionMultiplexer.ConnectAsync(_redisFixture.ConnectionString);
		var database = connection.GetDatabase();
		var coordinator = new RedisJobCoordinator(
			database,
			NullLogger<RedisJobCoordinator>.Instance,
			keyPrefix: $"jobs-test-{Guid.NewGuid():N}:");

		var jobKey = $"job-{Guid.NewGuid():N}";

		// Instance A acquires with a short TTL.
		var lockA = await coordinator.TryAcquireLockAsync(jobKey, TimeSpan.FromSeconds(1), TestCancellationToken);
		lockA.ShouldNotBeNull("instance A must acquire the lock");

		// Wait for A's TTL to expire, then instance B re-acquires the now-free lock.
		IDistributedJobLock? lockB = null;
		var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
		while (DateTimeOffset.UtcNow < deadline)
		{
			lockB = await coordinator.TryAcquireLockAsync(jobKey, TimeSpan.FromSeconds(60), TestCancellationToken);
			if (lockB is not null)
			{
				break;
			}

			await Task.Delay(200, TestCancellationToken);
		}

		lockB.ShouldNotBeNull("instance B must re-acquire the lock after A's TTL expires");

		await using (lockB)
		{
			// Act + Assert — A's stale handle must not touch B's lock.
			var extended = await lockA.ExtendAsync(TimeSpan.FromSeconds(60), TestCancellationToken);
			extended.ShouldBeFalse("a stale handle must NOT extend the new holder's lock (owner-token mismatch)");

			await lockA.ReleaseAsync(TestCancellationToken);

			// B's lock must still be held: nobody else can acquire it.
			var lockC = await coordinator.TryAcquireLockAsync(jobKey, TimeSpan.FromSeconds(5), TestCancellationToken);
			lockC.ShouldBeNull("B's lock must remain intact after the stale A.ReleaseAsync (no clobber)");
		}
	}
}
