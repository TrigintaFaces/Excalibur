// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Caching.Diagnostics;

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Middleware that invalidates cached entries after a message has been processed.
/// Uses a unified invalidation strategy across all cache modes (Memory, Distributed, Hybrid).
/// </summary>
/// <param name="meterFactory"> The meter factory for DI-managed meter lifecycle. </param>
/// <param name="options"> Configuration options that control middleware behavior. </param>
/// <param name="tagTracker"> Tag tracker for resolving tag-to-key mappings across all modes. </param>
/// <param name="memoryCache"> Memory cache instance (fallback when HybridCache is unavailable). </param>
/// <param name="hybridCache"> Hybrid cache instance (preferred for all invalidation operations). </param>
internal sealed class CacheInvalidationMiddleware(
	IMeterFactory meterFactory,
	IOptions<CacheOptions> options,
	ICacheTagTracker? tagTracker = null,
	IMemoryCache? memoryCache = null,
	HybridCache? hybridCache = null) : IDispatchMiddleware
{
	/// <summary>
	/// Maximum number of entries allowed in the attribute cache.
	/// When the cap is reached, new lookups compute attributes without caching to prevent unbounded memory growth.
	/// </summary>
	private const int MaxCacheEntries = 1024;

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

	private readonly string[] _defaultTags = options.Value.DefaultTags ?? [];
	private readonly CacheOptions _options = options.Value;
	private readonly ICacheTagTracker? _tagTracker = tagTracker;
	private readonly IMemoryCache? _memoryCache = memoryCache;
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

		var attr = GetInvalidateCacheAttribute(message.GetType());

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

		// Unified invalidation: L1 native tags + tracker-based L2 + direct keys
		await InvalidateAsync(tags, keys, cancellationToken).ConfigureAwait(false);

		return result;
	}

	/// <summary>
	/// Gets the InvalidateCacheAttribute for a type using a bounded cache.
	/// </summary>
	private static InvalidateCacheAttribute? GetInvalidateCacheAttribute(Type type)
	{
		if (_attributeCache.TryGetValue(type, out var cached))
		{
			return cached;
		}

		if (_attributeCache.Count >= MaxCacheEntries)
		{
			// Cache full -- compute without caching to prevent unbounded growth
			return type.GetCustomAttributes(typeof(InvalidateCacheAttribute), inherit: true)
				.FirstOrDefault() as InvalidateCacheAttribute;
		}

		return _attributeCache.GetOrAdd(type, static t =>
			t.GetCustomAttributes(typeof(InvalidateCacheAttribute), inherit: true)
				.FirstOrDefault() as InvalidateCacheAttribute);
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
	/// Unified cache invalidation across all modes (Memory, Distributed, Hybrid).
	/// Three-step flow: L1 native tag removal, tracker-based L2 key resolution + removal, direct key removal.
	/// </summary>
	private async Task InvalidateAsync(List<string> tags, List<string> keys, CancellationToken cancellationToken)
	{
		// Step 1: L1 native tag invalidation via HybridCache (no-op in Distributed mode since L1 disabled)
		if (_hybridCache is not null && tags.Count > 0)
		{
			var distinctTags = tags.Distinct(StringComparer.Ordinal).ToArray();
			await _hybridCache.RemoveByTagAsync(distinctTags, cancellationToken).ConfigureAwait(false);
		}

		// Step 2: Tracker-based L2 invalidation -- resolves tags to keys, removes each key
		if (_tagTracker is not null && tags.Count > 0)
		{
			var distinctTags = tags.Distinct(StringComparer.Ordinal).ToArray();
			var trackedKeys = await _tagTracker.GetKeysByTagsAsync(distinctTags, cancellationToken).ConfigureAwait(false);

			foreach (var trackedKey in trackedKeys)
			{
				if (_hybridCache is not null)
				{
					await _hybridCache.RemoveAsync(trackedKey, cancellationToken).ConfigureAwait(false);
				}
				else if (_memoryCache is not null)
				{
					_memoryCache.Remove(trackedKey);
				}

				await _tagTracker.UnregisterKeyAsync(trackedKey, cancellationToken).ConfigureAwait(false);
			}
		}

		// Step 3: Direct key invalidation (non-tag-based)
		if (keys.Count > 0)
		{
			var distinctKeys = keys.Distinct(StringComparer.Ordinal).ToArray();
			if (_hybridCache is not null)
			{
				await _hybridCache.RemoveAsync(distinctKeys, cancellationToken).ConfigureAwait(false);
			}
			else if (_memoryCache is not null)
			{
				foreach (var key in distinctKeys)
				{
					_memoryCache.Remove(key);
				}
			}
		}
	}
}
