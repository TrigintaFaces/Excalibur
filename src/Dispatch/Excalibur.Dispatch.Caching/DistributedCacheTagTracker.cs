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
/// <c>dispatch:tagver:{tag}</c>. Resolution reads this key and, when absent, writes a new stamp.
/// Invalidation writes a new stamp. These operations do not form an atomic read-modify-write
/// transaction; consistency also depends on the selected backend and overlapping operations.
/// </para>
/// <para>
/// Backend stamp retention uses <see cref="CacheOptions.TagStampLifetime"/>. Local memo capacity
/// does not delete backend stamps or alter their expiration. An absent backend record causes a
/// new stamp to be created; entries carrying a different stamp can then be treated as stale.
/// </para>
/// <para>
/// Both in-flight and completed resolution tasks are memoized up to
/// <see cref="CacheOptions.TagTrackerCapacity"/>. New tags at capacity bypass memoization.
/// Refresh is demand-driven after <see cref="CacheOptions.TagStampRefreshInterval"/>; backend latency,
/// failures and overlapping operations prevent a strict cross-instance visibility deadline.
/// Callers sharing a memo task share its initiating cancellation token. A backend must not recursively
/// await resolution of the same tag through this tracker, since it would await its own pending task.
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
	private readonly Lock _memoLock = new();
	private readonly int _capacity;

	/// <summary>Number of locally memoized tags, exposed for bounded-retention regression tests.</summary>
	internal int MemoizedTagCount => _memo.Count;

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
		_capacity = options.Value.TagTrackerCapacity > 0 ? options.Value.TagTrackerCapacity : 10_000;

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
		var frequency = _timeProvider.TimestampFrequency;
		if (frequency <= 0)
		{
			throw new InvalidOperationException("TimeProvider.TimestampFrequency must be positive.");
		}
		var ticksPerTimestamp = (double)TimeSpan.TicksPerSecond / frequency;

		if (_memo.TryGetValue(tag, out var existing)
			&& !existing.StampTask.IsFaulted
			&& !existing.StampTask.IsCanceled
			&& (long)((now - existing.FetchedAt) * ticksPerTimestamp) < _refreshInterval.Ticks)
		{
			return existing.StampTask;
		}

		TaskCompletionSource<string>? completion = null;
		lock (_memoLock)
		{
			if (_memo.TryGetValue(tag, out existing)
				&& !existing.StampTask.IsFaulted
				&& !existing.StampTask.IsCanceled
				&& (long)((now - existing.FetchedAt) * ticksPerTimestamp) < _refreshInterval.Ticks)
			{
				return existing.StampTask;
			}

			if (existing is not null || _memo.Count < _capacity)
			{
				completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
				// Publish before calling the backend: a reentrant bump can remove this entry.
				// Completing a displaced promise must never reinstall its stale stamp.
				_memo[tag] = new StampMemo(completion.Task, now);
			}
		}

		if (completion is null)
		{
			return ResolveStampAsync(tag, cancellationToken);
		}

		_ = CompleteResolutionAsync(tag, completion, cancellationToken);
		return completion.Task;
	}

	/// <inheritdoc />
	public async Task BumpStampAsync(string tag, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(tag);

		var stamp = CacheTagStamp.CreateNew();
		var stampKey = string.Concat(StampKeyPrefix, tag);
		var bytes = Encoding.UTF8.GetBytes(stamp);
		await _cache.SetAsync(stampKey, bytes, _entryOptions, cancellationToken).ConfigureAwait(false);

		// Concurrent writes may complete out of backend-write order. Reload the backend on the
		// next read instead of publishing a possibly superseded local stamp.
		lock (_memoLock)
		{
			_ = _memo.TryRemove(tag, out _);
		}
	}

	private async Task CompleteResolutionAsync(string tag, TaskCompletionSource<string> completion, CancellationToken cancellationToken)
	{
		try
		{
			completion.TrySetResult(await ResolveStampAsync(tag, cancellationToken).ConfigureAwait(false));
		}
		catch (OperationCanceledException exception)
		{
			completion.TrySetCanceled(exception.CancellationToken);
		}
		catch (Exception exception)
		{
			completion.TrySetException(exception);
		}
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
