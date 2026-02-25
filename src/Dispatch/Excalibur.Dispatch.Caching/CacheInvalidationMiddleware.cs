// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Text;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Caching.Diagnostics;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Middleware that invalidates cached entries after a message has been processed.
/// Supports all three cache modes: Memory, Distributed, and Hybrid.
/// </summary>
/// <param name="meterFactory"> The meter factory for DI-managed meter lifecycle. </param>
/// <param name="options"> Configuration options that control middleware behavior. </param>
/// <param name="tagTracker"> Tag tracker for Memory and Distributed modes (null for Hybrid). </param>
/// <param name="memoryCache"> Memory cache instance (null if not using Memory mode). </param>
/// <param name="distributedCache"> Distributed cache instance (null if not using Distributed mode). </param>
/// <param name="hybridCache"> Hybrid cache instance (null if not using Hybrid mode). </param>
public sealed class CacheInvalidationMiddleware(
	IMeterFactory meterFactory,
	IOptions<CacheOptions> options,
	ICacheTagTracker? tagTracker = null,
	IMemoryCache? memoryCache = null,
	IDistributedCache? distributedCache = null,
	HybridCache? hybridCache = null) : IDispatchMiddleware
{
	private readonly Counter<long> _invalidationCounter =
		meterFactory.Create(DispatchCachingTelemetryConstants.MeterName)
			.CreateCounter<long>("dispatch.cache.invalidations", "invalidations", "Number of cache invalidation operations");

	private readonly Counter<long> _tagsInvalidatedCounter =
		meterFactory.Create(DispatchCachingTelemetryConstants.MeterName)
			.CreateCounter<long>("dispatch.cache.tags_invalidated", "tags", "Number of cache tags invalidated");

	private readonly Counter<long> _keysInvalidatedCounter =
		meterFactory.Create(DispatchCachingTelemetryConstants.MeterName)
			.CreateCounter<long>("dispatch.cache.keys_invalidated", "keys", "Number of cache keys invalidated");

	private static readonly ConcurrentDictionary<Type, InvalidateCacheAttribute?> _attributeCache = new();
	private static readonly CompositeFormat UnsupportedCacheModeFormat =
		CompositeFormat.Parse(Resources.CacheInvalidationMiddleware_UnsupportedCacheModeFormat);

	private readonly string[] _defaultTags = options.Value.DefaultTags ?? [];
	private readonly CacheOptions _options = options.Value;
	private readonly ICacheTagTracker? _tagTracker = tagTracker;
	private readonly IMemoryCache? _memoryCache = memoryCache;
	private readonly IDistributedCache? _distributedCache = distributedCache;
	private readonly HybridCache? _hybridCache = hybridCache;

	/// <summary>
	/// Gets the stage at which this middleware executes.
	/// </summary>
	/// <value>The stage at which this middleware executes.</value>
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Cache;

	/// <summary>
	/// Invokes the next middleware and invalidates cache entries for the processed message.
	/// </summary>
	/// <param name="message"> The message being dispatched. </param>
	/// <param name="context"> The context associated with the message. </param>
	/// <param name="nextDelegate"> Next middleware in the pipeline. </param>
	/// <param name="cancellationToken"> Token used to cancel the operation. </param>
	/// <returns> The <see cref="IMessageResult" /> returned by the next middleware. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when any parameter is <c> null </c>. </exception>
	public async ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(nextDelegate);

		if (!_options.Enabled)
		{
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		var result = await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);

		// Performance optimization: AD-250-4 - collect tags/keys without List when possible
		// Use array storage pattern to avoid repeated List allocations
		var hasTags = false;
		var hasKeys = false;
		IEnumerable<string>? invalidatorTags = null;
		IEnumerable<string>? invalidatorKeys = null;

		if (message is ICacheInvalidator invalidator)
		{
			invalidatorTags = invalidator.GetCacheTagsToInvalidate();
			invalidatorKeys = invalidator.GetCacheKeysToInvalidate();
			hasTags = invalidatorTags?.Any() == true;
			hasKeys = invalidatorKeys?.Any() == true;
		}

		var attr = _attributeCache.GetOrAdd(message.GetType(), static type =>
			type.GetCustomAttributes(typeof(InvalidateCacheAttribute), inherit: true)
				.FirstOrDefault() as InvalidateCacheAttribute);

		hasTags = hasTags || attr?.Tags?.Length > 0 || _defaultTags.Length > 0;

		// Only allocate if we have something to invalidate
		if (!hasTags && !hasKeys)
		{
			return result;
		}

		// Build combined tag/key lists only when needed
		var tags = BuildTagList(invalidatorTags, attr?.Tags);
		var keys = hasKeys ? (invalidatorKeys?.ToList() ?? []) : [];

		// Record OTel metrics
		_invalidationCounter.Add(1);
		if (tags.Count > 0)
		{
			_tagsInvalidatedCounter.Add(tags.Count);
		}

		if (keys.Count > 0)
		{
			_keysInvalidatedCounter.Add(keys.Count);
		}

		// Dispatch to cache mode-specific invalidation
		switch (_options.CacheMode)
		{
			case CacheMode.Memory:
				await InvalidateMemoryCacheAsync(tags, keys, cancellationToken).ConfigureAwait(false);
				break;

			case CacheMode.Distributed:
				await InvalidateDistributedCacheAsync(tags, keys, cancellationToken).ConfigureAwait(false);
				break;

			case CacheMode.Hybrid:
				await InvalidateHybridCacheAsync(tags, keys, cancellationToken).ConfigureAwait(false);
				break;

			default:
				throw new InvalidOperationException(
					string.Format(
						CultureInfo.CurrentCulture,
						UnsupportedCacheModeFormat,
						_options.CacheMode));
		}

		return result;
	}

	/// <summary>
	/// Builds combined tag list from multiple sources.
	/// </summary>
	private List<string> BuildTagList(IEnumerable<string>? invalidatorTags, string[]? attrTags)
	{
		// Estimate capacity to avoid resizing
		var capacity = (invalidatorTags is ICollection<string> c1 ? c1.Count : 0)
					   + (attrTags?.Length ?? 0)
					   + _defaultTags.Length;

		var tags = new List<string>(capacity);

		if (invalidatorTags is not null)
		{
			tags.AddRange(invalidatorTags);
		}

		if (attrTags?.Length > 0)
		{
			tags.AddRange(attrTags);
		}

		if (_defaultTags.Length > 0)
		{
			tags.AddRange(_defaultTags);
		}

		return tags;
	}

	/// <summary>
	/// Invalidates memory cache entries by tags and keys.
	/// CachingMiddleware stores entries through HybridCache even in Memory mode (as L1-only),
	/// so invalidation must go through HybridCache to match the internal key/tag tracking.
	/// Falls back to IMemoryCache + ICacheTagTracker when HybridCache is not available.
	/// </summary>
	private async Task InvalidateMemoryCacheAsync(List<string> tags, List<string> keys, CancellationToken cancellationToken)
	{
		// Prefer HybridCache invalidation since entries are stored through it
		if (_hybridCache is not null)
		{
			if (tags.Count > 0)
			{
				var distinctTags = tags.Distinct(StringComparer.Ordinal).ToArray();
				await _hybridCache.RemoveByTagAsync(distinctTags, cancellationToken).ConfigureAwait(false);
			}

			if (keys.Count > 0)
			{
				var distinctKeys = keys.Distinct(StringComparer.Ordinal).ToArray();
				await _hybridCache.RemoveAsync(distinctKeys, cancellationToken).ConfigureAwait(false);
			}

			return;
		}

		// Fallback to IMemoryCache + tag tracker (when HybridCache is not registered)
		if (_memoryCache is null)
		{
			return;
		}

		if (tags.Count > 0 && _tagTracker is not null)
		{
			var distinctTags = tags.Distinct(StringComparer.Ordinal).ToArray();
			var keysToInvalidate = await _tagTracker.GetKeysByTagsAsync(distinctTags, cancellationToken).ConfigureAwait(false);

			foreach (var key in keysToInvalidate)
			{
				_memoryCache.Remove(key);
			}
		}

		if (keys.Count > 0)
		{
			var distinctKeys = keys.Distinct(StringComparer.Ordinal);
			foreach (var key in distinctKeys)
			{
				_memoryCache.Remove(key);
			}
		}
	}

	/// <summary>
	/// Invalidates distributed cache entries by tags and keys.
	/// Uses HybridCache for removal since entries are stored through HybridCache
	/// (with <see cref="HybridCacheEntryFlags.DisableLocalCache"/>), which uses
	/// its own internal key format in L2 that differs from our cache keys.
	/// </summary>
	private async Task InvalidateDistributedCacheAsync(List<string> tags, List<string> keys, CancellationToken cancellationToken)
	{
		// Distributed mode uses HybridCache with DisableLocalCache flag,
		// so invalidation must go through HybridCache to match the internal key format.
		if (_hybridCache is not null)
		{
			if (tags.Count > 0)
			{
				var distinctTags = tags.Distinct(StringComparer.Ordinal).ToArray();
				await _hybridCache.RemoveByTagAsync(distinctTags, cancellationToken).ConfigureAwait(false);
			}

			if (keys.Count > 0)
			{
				var distinctKeys = keys.Distinct(StringComparer.Ordinal).ToArray();
				await _hybridCache.RemoveAsync(distinctKeys, cancellationToken).ConfigureAwait(false);
			}

			return;
		}

		// Fallback to IDistributedCache directly (should not normally be reached
		// since all modes register HybridCache, but kept for safety)
		if (_distributedCache is null)
		{
			return;
		}

		if (tags.Count > 0 && _tagTracker is not null)
		{
			var distinctTags = tags.Distinct(StringComparer.Ordinal).ToArray();
			var keysToInvalidate = await _tagTracker.GetKeysByTagsAsync(distinctTags, cancellationToken).ConfigureAwait(false);

			foreach (var key in keysToInvalidate)
			{
				await _distributedCache.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
			}
		}

		if (keys.Count > 0)
		{
			var distinctKeys = keys.Distinct(StringComparer.Ordinal);
			foreach (var key in distinctKeys)
			{
				await _distributedCache.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
			}
		}
	}

	/// <summary>
	/// Invalidates hybrid cache entries by tags and keys using native HybridCache support.
	/// </summary>
	private async Task InvalidateHybridCacheAsync(List<string> tags, List<string> keys, CancellationToken cancellationToken)
	{
		if (_hybridCache is null)
		{
			return;
		}

		// Use HybridCache's native tag support
		if (tags.Count > 0)
		{
			var distinctTags = tags.Distinct(StringComparer.Ordinal).ToArray();
			await _hybridCache.RemoveByTagAsync(distinctTags, cancellationToken).ConfigureAwait(false);
		}

		// Direct key-based invalidation
		if (keys.Count > 0)
		{
			var distinctKeys = keys.Distinct(StringComparer.Ordinal).ToArray();
			await _hybridCache.RemoveAsync(distinctKeys, cancellationToken).ConfigureAwait(false);
		}
	}
}
