// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Collections.Concurrent;
using System.Text;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Distributed implementation of <see cref="ICacheTagTracker"/> using <see cref="IDistributedCache"/>
/// to share per-tag version stamps across application instances (Redis, SQL Server, or any other
/// <see cref="IDistributedCache"/> backend).
/// </summary>
/// <remarks>
/// <para>
/// Storage scheme: a tag's current stamp is a single opaque string stored under
/// <c>dispatch:tagver:{tag}</c>. Resolving or invalidating a tag is therefore a single-key read or
/// write, both of which are atomic on every <see cref="IDistributedCache"/> backend -- unlike the
/// key-set model this type previously implemented, there is no multi-key read-modify-write and no
/// possibility of a lost update between concurrent writers.
/// </para>
/// <para>
/// The stamp record's TTL (<see cref="CacheOptions.TagStampLifetime"/>) is enforced by
/// <see cref="CacheOptionsValidator"/> to strictly exceed the largest configurable cache entry
/// expiration. This is what makes it safe to treat a tag whose stamp record is absent from the backend
/// as still valid (fail open): under that invariant, a cache entry cannot legitimately outlive the tag
/// record it was written against, so an absent record means either a genuinely new tag (nothing to
/// invalidate yet) or a backend that lost the record out of band -- in neither case does treating the
/// referencing entries as pre-emptively invalidated buy any correctness, and doing so would effectively
/// flush every tagged entry on every cold or newly provisioned backend.
/// </para>
/// <para>
/// Resolution is memoized per tag, per instance, as an in-flight <see cref="Task{TResult}"/> (never as
/// a completed value) so that concurrent callers for the same tag collapse onto one backend round trip,
/// and the memo is refreshed on a bounded interval (<see cref="CacheOptions.TagStampRefreshInterval"/>)
/// so that an invalidation from another instance is observed within a bounded, configurable window
/// rather than never. In steady state -- no refresh due -- resolving a tag costs one dictionary lookup
/// and no backend I/O at all.
/// </para>
/// </remarks>
internal sealed class DistributedCacheTagTracker : ICacheTagTracker
{
	private const string StampKeyPrefix = "dispatch:tagver:";

	private readonly IDistributedCache _cache;
	private readonly DistributedCacheEntryOptions _entryOptions;
	private readonly TimeSpan _refreshInterval;
	private readonly TimeProvider _timeProvider;
	private readonly ConcurrentDictionary<string, StampMemo> _memo = new(StringComparer.Ordinal);

	/// <summary>
	/// Initializes a new instance of the <see cref="DistributedCacheTagTracker"/> class.
	/// </summary>
	/// <param name="cache">The distributed cache backend storing per-tag version stamps.</param>
	/// <param name="options">Cache options providing the stamp TTL and refresh interval.</param>
	/// <param name="timeProvider">
	/// The time source used to bound the per-tag resolution memo. Defaults to <see cref="TimeProvider.System"/>.
	/// </param>
	public DistributedCacheTagTracker(
		IDistributedCache cache,
		IOptions<CacheOptions> options,
		TimeProvider? timeProvider = null)
	{
		ArgumentNullException.ThrowIfNull(cache);
		ArgumentNullException.ThrowIfNull(options);

		_cache = cache;
		_timeProvider = timeProvider ?? TimeProvider.System;

		var ttl = options.Value.TagStampLifetime;
		_entryOptions = new DistributedCacheEntryOptions
		{
			AbsoluteExpirationRelativeToNow = ttl > TimeSpan.Zero ? ttl : CacheOptions.DefaultTagStampLifetime,
		};

		var refreshInterval = options.Value.TagStampRefreshInterval;
		_refreshInterval = refreshInterval > TimeSpan.Zero ? refreshInterval : CacheOptions.DefaultTagStampRefreshInterval;
	}

	/// <inheritdoc />
	public Task<string> GetOrCreateStampAsync(string tag, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(tag);

		var now = _timeProvider.GetTimestamp();

		if (_memo.TryGetValue(tag, out var existing)
			&& !existing.StampTask.IsFaulted
			&& !existing.StampTask.IsCanceled
			&& _timeProvider.GetElapsedTime(existing.FetchedAt, now) < _refreshInterval)
		{
			return existing.StampTask;
		}

		// Started outside any lock: a cold or expired memo may race with another caller resolving the
		// same tag concurrently, and both are allowed to hit the backend rather than one blocking the
		// other -- a lock held across the backend round trip would let a slow tag resolution stall
		// every other reader/writer waiting on that same tag.
		var factory = ResolveStampAsync(tag, cancellationToken);
		_memo[tag] = new StampMemo(factory, now);
		return factory;
	}

	/// <inheritdoc />
	public async Task BumpStampAsync(string tag, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(tag);

		var stamp = CacheTagStamp.CreateNew();
		var stampKey = string.Concat(StampKeyPrefix, tag);
		var bytes = Encoding.UTF8.GetBytes(stamp);
		await _cache.SetAsync(stampKey, bytes, _entryOptions, cancellationToken).ConfigureAwait(false);

		// This instance can trust its own bump immediately; other instances converge within
		// _refreshInterval the next time they resolve this tag.
		_memo[tag] = new StampMemo(Task.FromResult(stamp), _timeProvider.GetTimestamp());
	}

	private async Task<string> ResolveStampAsync(string tag, CancellationToken cancellationToken)
	{
		var stampKey = string.Concat(StampKeyPrefix, tag);
		var existingBytes = await _cache.GetAsync(stampKey, cancellationToken).ConfigureAwait(false);

		if (existingBytes is { Length: > 0 })
		{
			return Encoding.UTF8.GetString(existingBytes);
		}

		// Absent from the backend: either a genuinely new tag, or a stamp record that expired/was
		// evicted out of band. Either way there is no prior stamp to preserve, so create one now.
		var stamp = CacheTagStamp.CreateNew();
		var bytes = Encoding.UTF8.GetBytes(stamp);
		await _cache.SetAsync(stampKey, bytes, _entryOptions, cancellationToken).ConfigureAwait(false);
		return stamp;
	}

	private sealed record StampMemo(Task<string> StampTask, long FetchedAt);
}
