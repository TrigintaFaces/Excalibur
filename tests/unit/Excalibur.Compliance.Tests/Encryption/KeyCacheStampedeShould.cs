// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance;
using Excalibur.Compliance.Encryption;

namespace Excalibur.Compliance.Tests.Encryption;

/// <summary>
/// Binds the stampede guarantee: concurrent misses for one key make one factory call, and that holds
/// however many distinct keys the cache has served over its lifetime.
/// </summary>
/// <remarks>
/// <para>
/// The lock table has to be bounded, or a high-cardinality workload grows it without limit. Bounding it
/// by refusing new entries once it is full makes the bound a one-way latch: the table fills with the
/// keys seen so far and never releases them, so from that point on every key is served with no lock at
/// all and the deduplication is gone cache-wide -- not while those keys are hot, but permanently. A key
/// fetch is typically a call to an external key-management service, so what is lost is the protection
/// against a burst of identical calls to it.
/// </para>
/// <para>
/// Both arms are needed. The safety arm -- one factory call under concurrency -- is satisfied by a cache
/// that serialises everything behind a single global lock, which would destroy throughput. The liveness
/// arm -- distinct keys are not serialised against each other -- is satisfied by no locking at all.
/// Together they say the exclusion is per-key and still present after the table has been churned.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class KeyCacheStampedeShould
{
	/// <summary>
	/// Comfortably more distinct keys than any internal lock-table bound, so that a table which retains
	/// every key it has ever seen is certain to be full by the time the measured key is requested.
	/// </summary>
	private const int WarmUpKeyCount = 2048;

	private const int ConcurrentCallers = 32;

	private static readonly TimeSpan FactoryDuration = TimeSpan.FromMilliseconds(300);

	[Fact]
	public async Task MakeOneFactoryCallForConcurrentMisses_AfterManyDistinctKeysHaveBeenServed()
	{
		using var sut = new KeyCache(KeyCacheOptions.Default with { MaxEntries = WarmUpKeyCount * 2 });

		await ServeDistinctKeysAsync(sut, WarmUpKeyCount);

		var factoryCalls = 0;

		async Task<KeyMetadata?> CallAsync()
		{
			return await sut.GetOrAddAsync(
				"contended-key",
				async (id, ct) =>
				{
					_ = Interlocked.Increment(ref factoryCalls);

					// Hold the factory open long enough that any caller not excluded by the per-key lock
					// is certain to have entered it as well, rather than arriving after the value landed.
					await Task.Delay(FactoryDuration, ct).ConfigureAwait(false);
					return (KeyMetadata?)CreateKeyMetadata(id);
				},
				TestContext.Current.CancellationToken).ConfigureAwait(false);
		}

		var results = await Task.WhenAll(Enumerable.Range(0, ConcurrentCallers).Select(_ => CallAsync()));

		Volatile.Read(ref factoryCalls).ShouldBe(
			1,
			$"{ConcurrentCallers} concurrent callers missed on the same key, so exactly one of them should "
			+ $"have reached the factory -- after {WarmUpKeyCount} distinct keys had already been served, "
			+ "which is where a lock table that never releases an entry stops deduplicating");

		results.ShouldAllBe(metadata => metadata != null && metadata.KeyId == "contended-key");
	}

	[Fact]
	public async Task NotSerialiseCallersForDistinctKeys()
	{
		using var sut = new KeyCache(KeyCacheOptions.Default with { MaxEntries = ConcurrentCallers * 2 });

		var inFactory = 0;
		var peakInFactory = 0;

		async Task CallAsync(int index)
		{
			_ = await sut.GetOrAddAsync(
				$"distinct-key-{index}",
				async (id, ct) =>
				{
					var current = Interlocked.Increment(ref inFactory);
					_ = InterlockedMax(ref peakInFactory, current);

					await Task.Delay(FactoryDuration, ct).ConfigureAwait(false);

					_ = Interlocked.Decrement(ref inFactory);
					return (KeyMetadata?)CreateKeyMetadata(id);
				},
				TestContext.Current.CancellationToken).ConfigureAwait(false);
		}

		await Task.WhenAll(Enumerable.Range(0, ConcurrentCallers).Select(CallAsync));

		Volatile.Read(ref peakInFactory).ShouldBe(
			ConcurrentCallers,
			$"all {ConcurrentCallers} callers ask for different keys, so all {ConcurrentCallers} factories "
			+ "should run at once -- a lower peak means the exclusion is not per-key");
	}

	private static int InterlockedMax(ref int target, int value)
	{
		var observed = Volatile.Read(ref target);
		while (observed < value)
		{
			var previous = Interlocked.CompareExchange(ref target, value, observed);
			if (previous == observed)
			{
				return value;
			}

			observed = previous;
		}

		return observed;
	}

	private static async Task ServeDistinctKeysAsync(KeyCache cache, int count)
	{
		for (var index = 0; index < count; index++)
		{
			_ = await cache.GetOrAddAsync(
				$"warm-up-key-{index}",
				(id, _) => Task.FromResult<KeyMetadata?>(CreateKeyMetadata(id)),
				TestContext.Current.CancellationToken).ConfigureAwait(false);
		}
	}

	private static KeyMetadata CreateKeyMetadata(string keyId) => new()
	{
		KeyId = keyId,
		Version = 1,
		Status = KeyStatus.Active,
		Algorithm = EncryptionAlgorithm.Aes256Gcm,
		CreatedAt = DateTimeOffset.UtcNow,
	};
}
