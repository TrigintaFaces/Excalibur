// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using System.Text;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Time.Testing;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// Rebuilds the unit-level coverage for <see cref="DistributedCacheTagTracker"/> that was lost when the
/// tracker moved from a key-set contract to per-tag version stamps.
/// </summary>
/// <remarks>
/// <para>
/// <c>DistributedCacheTagTrackerConformanceTests</c> proves the cross-instance contract against a REAL
/// Redis backend, which is the only place that guarantee can honestly be proven. It cannot, however,
/// prove the things that are invisible from outside: how many times the backend was actually consulted,
/// and what happens at an exact refresh boundary. Those need a counting backend and a controllable
/// clock, so they live here — deliberately complementary to the real-infra arms, not a substitute for
/// them.
/// </para>
/// <para>
/// The memoization arms are the ones that matter most. The memo is what keeps a tag resolution off the
/// network in steady state, and it is also the reason another instance's invalidation is observed only
/// within a bounded window. A memo that never refreshed would serve stale entries forever; one that
/// never hit would put a network round trip on every cached read. Both failures are silent, and both
/// are invisible to a test that only inspects returned values — so these arms count backend reads
/// rather than compare stamps.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DistributedCacheTagTrackerShould
{
	private const string StampKeyPrefix = "dispatch:tagver:";

	private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

	private static DistributedCacheTagTracker CreateTracker(
		IDistributedCache cache,
		TimeProvider timeProvider,
		TimeSpan? refreshInterval = null,
		TimeSpan? lifetime = null) =>
		new(
			cache,
			MsOptions.Create(new CacheOptions
			{
				TagStampRefreshInterval = refreshInterval ?? TimeSpan.FromSeconds(5),
				TagStampLifetime = lifetime ?? TimeSpan.FromDays(1),
			}),
			timeProvider);

	// ---- Constructor guards. ----

	[Fact]
	public void RejectANullCache()
	{
		_ = Should.Throw<ArgumentNullException>(
			() => new DistributedCacheTagTracker(null!, MsOptions.Create(new CacheOptions())));
	}

	[Fact]
	public void RejectNullOptions()
	{
		using var cache = new CountingDistributedCache();

		_ = Should.Throw<ArgumentNullException>(() => new DistributedCacheTagTracker(cache, null!));
	}

	// ---- Storage scheme. ----

	[Fact]
	public async Task StoreATagsStampUnderItsOwnSingleKey()
	{
		using var cache = new CountingDistributedCache();
		var tracker = CreateTracker(cache, new FakeTimeProvider());

		var stamp = await tracker.GetOrCreateStampAsync("orders", Ct);

		cache.Entries.Keys.ShouldContain(StampKeyPrefix + "orders");
		Encoding.UTF8.GetString(cache.Entries[StampKeyPrefix + "orders"]).ShouldBe(stamp,
			"a tag's whole state is one opaque stamp under one key — that is what makes resolve and "
			+ "invalidate single-key atomic operations on every IDistributedCache backend");
	}

	[Fact]
	public async Task PersistANewlyMintedStamp_SoAnotherInstanceResolvesTheSameValue()
	{
		using var cache = new CountingDistributedCache();
		var first = CreateTracker(cache, new FakeTimeProvider());
		var second = CreateTracker(cache, new FakeTimeProvider());

		var fromFirst = await first.GetOrCreateStampAsync("orders", Ct);
		var fromSecond = await second.GetOrCreateStampAsync("orders", Ct);

		fromSecond.ShouldBe(fromFirst,
			"minting a stamp without writing it back would let every instance invent its own, so no "
			+ "entry written on one instance could ever be validated on another");
	}

	// ---- Memoization. ----

	[Fact]
	public async Task ResolveFromItsMemoWithinTheRefreshInterval_WithoutTouchingTheBackend()
	{
		using var cache = new CountingDistributedCache();
		var time = new FakeTimeProvider();
		var tracker = CreateTracker(cache, time, refreshInterval: TimeSpan.FromSeconds(5));

		_ = await tracker.GetOrCreateStampAsync("orders", Ct);
		var readsAfterFirst = cache.Reads;

		time.Advance(TimeSpan.FromSeconds(4));
		_ = await tracker.GetOrCreateStampAsync("orders", Ct);
		_ = await tracker.GetOrCreateStampAsync("orders", Ct);

		cache.Reads.ShouldBe(readsAfterFirst,
			"inside the refresh window a tag resolution must cost one dictionary lookup and no I/O — "
			+ "otherwise every cached read carries a network round trip");
	}

	[Fact]
	public async Task RefreshFromTheBackendOnceTheRefreshIntervalHasElapsed()
	{
		using var cache = new CountingDistributedCache();
		var time = new FakeTimeProvider();
		var tracker = CreateTracker(cache, time, refreshInterval: TimeSpan.FromSeconds(5));
		_ = await tracker.GetOrCreateStampAsync("orders", Ct);
		var readsAfterFirst = cache.Reads;

		time.Advance(TimeSpan.FromSeconds(6));
		_ = await tracker.GetOrCreateStampAsync("orders", Ct);

		cache.Reads.ShouldBeGreaterThan(readsAfterFirst,
			"a memo that never expires would never observe another instance's invalidation");
	}

	/// <summary>
	/// The convergence property, expressed against a controllable clock: a change written to the shared
	/// backend by someone else becomes visible on the next resolution after the refresh window, and NOT
	/// before it.
	/// </summary>
	[Fact]
	public async Task ObserveAnExternalInvalidation_OnlyAfterTheRefreshWindowElapses()
	{
		using var cache = new CountingDistributedCache();
		var time = new FakeTimeProvider();
		var tracker = CreateTracker(cache, time, refreshInterval: TimeSpan.FromSeconds(5));
		var original = await tracker.GetOrCreateStampAsync("orders", Ct);

		// Another instance bumps the tag by replacing the shared record out from under this one.
		cache.Entries[StampKeyPrefix + "orders"] = Encoding.UTF8.GetBytes("stamp-from-another-instance");

		time.Advance(TimeSpan.FromSeconds(4));
		(await tracker.GetOrCreateStampAsync("orders", Ct)).ShouldBe(original,
			"within the window the memo is authoritative — this is the bounded staleness the design "
			+ "deliberately trades for keeping resolution off the network");

		time.Advance(TimeSpan.FromSeconds(2));
		(await tracker.GetOrCreateStampAsync("orders", Ct)).ShouldBe("stamp-from-another-instance",
			"past the window the tracker must converge on what the shared backend actually holds");
	}

	// ---- Bump. ----

	[Fact]
	public async Task WriteABumpedStampToTheBackend()
	{
		using var cache = new CountingDistributedCache();
		var tracker = CreateTracker(cache, new FakeTimeProvider());
		var before = await tracker.GetOrCreateStampAsync("orders", Ct);

		await tracker.BumpStampAsync("orders", Ct);

		var persisted = Encoding.UTF8.GetString(cache.Entries[StampKeyPrefix + "orders"]);
		persisted.ShouldNotBe(before);
	}

	[Fact]
	public async Task TrustItsOwnBumpImmediately_WithoutWaitingForTheRefreshWindow()
	{
		using var cache = new CountingDistributedCache();
		var time = new FakeTimeProvider();
		var tracker = CreateTracker(cache, time, refreshInterval: TimeSpan.FromMinutes(10));
		var before = await tracker.GetOrCreateStampAsync("orders", Ct);

		await tracker.BumpStampAsync("orders", Ct);
		var after = await tracker.GetOrCreateStampAsync("orders", Ct);

		after.ShouldNotBe(before,
			"an instance that could not see its own invalidation would keep serving the entries it "
			+ "just invalidated for a whole refresh interval");
	}

	[Fact]
	public async Task BumpATagItHasNeverResolved()
	{
		using var cache = new CountingDistributedCache();
		var tracker = CreateTracker(cache, new FakeTimeProvider());

		await tracker.BumpStampAsync("never-seen", Ct);

		cache.Entries.ShouldContainKey(StampKeyPrefix + "never-seen");
		(await tracker.GetOrCreateStampAsync("never-seen", Ct)).ShouldNotBeNullOrEmpty();
	}

	// ---- Fail-open on an absent record. ----

	[Fact]
	public async Task MintAndPersistAStamp_WhenTheBackendHasNoRecordForTheTag()
	{
		using var cache = new CountingDistributedCache();
		var tracker = CreateTracker(cache, new FakeTimeProvider());

		var stamp = await tracker.GetOrCreateStampAsync("cold-tag", Ct);

		stamp.ShouldNotBeNullOrEmpty(
			"an absent record is a healthy state — a genuinely new tag, or a cold backend. Treating it "
			+ "as an invalidation would flush every tagged entry on every newly provisioned cache");
		cache.Entries.ShouldContainKey(StampKeyPrefix + "cold-tag");
	}

	// ---- Options handling. ----

	[Fact]
	public async Task ApplyTheConfiguredStampLifetimeAsTheRecordsTtl()
	{
		using var cache = new CountingDistributedCache();
		var tracker = CreateTracker(cache, new FakeTimeProvider(), lifetime: TimeSpan.FromDays(30));

		_ = await tracker.GetOrCreateStampAsync("orders", Ct);

		cache.LastWriteOptions.ShouldNotBeNull();
		cache.LastWriteOptions!.AbsoluteExpirationRelativeToNow.ShouldBe(TimeSpan.FromDays(30));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public async Task FallBackToTheDefaultLifetime_WhenTheConfiguredOneIsNotAPositiveDuration(int seconds)
	{
		using var cache = new CountingDistributedCache();
		var tracker = CreateTracker(cache, new FakeTimeProvider(), lifetime: TimeSpan.FromSeconds(seconds));

		_ = await tracker.GetOrCreateStampAsync("orders", Ct);

		cache.LastWriteOptions!.AbsoluteExpirationRelativeToNow.ShouldBe(CacheOptions.DefaultTagStampLifetime,
			"a non-positive TTL would expire the record instantly, and a stamp record that outlives no "
			+ "cache entry cannot support the fail-open read it exists to justify");
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public async Task FallBackToTheDefaultRefreshInterval_WhenTheConfiguredOneIsNotAPositiveDuration(int seconds)
	{
		using var cache = new CountingDistributedCache();
		var time = new FakeTimeProvider();
		var tracker = CreateTracker(cache, time, refreshInterval: TimeSpan.FromSeconds(seconds));
		_ = await tracker.GetOrCreateStampAsync("orders", Ct);
		var readsAfterFirst = cache.Reads;

		// Inside the DEFAULT window: a literal non-positive interval would make every resolve re-read.
		time.Advance(CacheOptions.DefaultTagStampRefreshInterval - TimeSpan.FromSeconds(1));
		_ = await tracker.GetOrCreateStampAsync("orders", Ct);

		cache.Reads.ShouldBe(readsAfterFirst);
	}

	// ---- Argument validation. ----

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public async Task RejectAnAbsentTag(string? tag)
	{
		using var cache = new CountingDistributedCache();
		var tracker = CreateTracker(cache, new FakeTimeProvider());

		_ = await Should.ThrowAsync<ArgumentException>(
			async () => await tracker.GetOrCreateStampAsync(tag!, Ct));
		_ = await Should.ThrowAsync<ArgumentException>(
			async () => await tracker.BumpStampAsync(tag!, Ct));
	}

	/// <summary>
	/// An in-process <see cref="IDistributedCache"/> that records how often it was actually consulted,
	/// which is the property the memoization arms assert and the only one a returned stamp cannot show.
	/// </summary>
	private sealed class CountingDistributedCache : IDistributedCache, IDisposable
	{
		private int _reads;

		public ConcurrentDictionary<string, byte[]> Entries { get; } = new(StringComparer.Ordinal);

		public int Reads => Volatile.Read(ref _reads);

		public DistributedCacheEntryOptions? LastWriteOptions { get; private set; }

		public byte[]? Get(string key)
		{
			_ = Interlocked.Increment(ref _reads);
			return Entries.TryGetValue(key, out var value) ? value : null;
		}

		public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult(Get(key));

		public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
		{
			LastWriteOptions = options;
			Entries[key] = value;
		}

		public Task SetAsync(
			string key,
			byte[] value,
			DistributedCacheEntryOptions options,
			CancellationToken token = default)
		{
			Set(key, value, options);
			return Task.CompletedTask;
		}

		public void Refresh(string key)
		{
			// No sliding expiration in this tracker's scheme; nothing to refresh.
		}

		public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;

		public void Remove(string key) => Entries.TryRemove(key, out _);

		public Task RemoveAsync(string key, CancellationToken token = default)
		{
			Remove(key);
			return Task.CompletedTask;
		}

		public void Dispose() => Entries.Clear();
	}
}
