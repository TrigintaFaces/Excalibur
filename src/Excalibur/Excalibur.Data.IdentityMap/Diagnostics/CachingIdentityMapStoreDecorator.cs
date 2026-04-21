// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Caching.Distributed;

namespace Excalibur.Data.IdentityMap.Diagnostics;

/// <summary>
/// Caching decorator for <see cref="IIdentityMapStore"/> that uses
/// <see cref="IDistributedCache"/> to cache resolved identity mappings.
/// </summary>
internal sealed class CachingIdentityMapStoreDecorator : IIdentityMapStore
{
	private const string CacheKeyPrefix = "idmap:";

	private readonly IIdentityMapStore _inner;
	private readonly IDistributedCache _cache;
	private readonly DistributedCacheEntryOptions _cacheEntryOptions;

	/// <summary>
	/// Initializes a new instance of the <see cref="CachingIdentityMapStoreDecorator"/> class.
	/// </summary>
	/// <param name="inner">The inner identity map store to decorate.</param>
	/// <param name="cache">The distributed cache.</param>
	/// <param name="options">The cache options.</param>
	public CachingIdentityMapStoreDecorator(
		IIdentityMapStore inner,
		IDistributedCache cache,
		IdentityMapCacheOptions options)
	{
		_inner = inner ?? throw new ArgumentNullException(nameof(inner));
		_cache = cache ?? throw new ArgumentNullException(nameof(cache));
		ArgumentNullException.ThrowIfNull(options);

		_cacheEntryOptions = new DistributedCacheEntryOptions
		{
			AbsoluteExpirationRelativeToNow = options.AbsoluteExpiration,
			SlidingExpiration = options.SlidingExpiration,
		};
	}

	/// <inheritdoc/>
	public async ValueTask<string?> ResolveAsync(
		string externalSystem,
		string externalId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		var cacheKey = BuildCacheKey(externalSystem, externalId, aggregateType);
		var cached = await _cache.GetStringAsync(cacheKey, cancellationToken).ConfigureAwait(false);

		if (cached is not null)
		{
			return cached;
		}

		var result = await _inner.ResolveAsync(externalSystem, externalId, aggregateType, cancellationToken)
			.ConfigureAwait(false);

		if (result is not null)
		{
			await _cache.SetStringAsync(cacheKey, result, _cacheEntryOptions, cancellationToken)
				.ConfigureAwait(false);
		}

		return result;
	}

	/// <inheritdoc/>
	public async ValueTask BindAsync(
		string externalSystem,
		string externalId,
		string aggregateType,
		string aggregateId,
		CancellationToken cancellationToken)
	{
		await _inner.BindAsync(externalSystem, externalId, aggregateType, aggregateId, cancellationToken)
			.ConfigureAwait(false);

		var cacheKey = BuildCacheKey(externalSystem, externalId, aggregateType);
		await _cache.SetStringAsync(cacheKey, aggregateId, _cacheEntryOptions, cancellationToken)
			.ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async ValueTask<IdentityBindResult> TryBindAsync(
		string externalSystem,
		string externalId,
		string aggregateType,
		string aggregateId,
		CancellationToken cancellationToken)
	{
		var result = await _inner.TryBindAsync(externalSystem, externalId, aggregateType, aggregateId, cancellationToken)
			.ConfigureAwait(false);

		var cacheKey = BuildCacheKey(externalSystem, externalId, aggregateType);
		await _cache.SetStringAsync(cacheKey, result.AggregateId, _cacheEntryOptions, cancellationToken)
			.ConfigureAwait(false);

		return result;
	}

	/// <inheritdoc/>
	public async ValueTask<bool> UnbindAsync(
		string externalSystem,
		string externalId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		var removed = await _inner.UnbindAsync(externalSystem, externalId, aggregateType, cancellationToken)
			.ConfigureAwait(false);

		if (removed)
		{
			var cacheKey = BuildCacheKey(externalSystem, externalId, aggregateType);
			await _cache.RemoveAsync(cacheKey, cancellationToken).ConfigureAwait(false);
		}

		return removed;
	}

	/// <inheritdoc/>
	public async ValueTask<IReadOnlyDictionary<string, string>> ResolveBatchAsync(
		string externalSystem,
		IReadOnlyList<string> externalIds,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		// For batch, resolve from cache first, then delegate misses to inner store
		var result = new Dictionary<string, string>(externalIds.Count);
		var misses = new List<string>();

		foreach (var externalId in externalIds)
		{
			var cacheKey = BuildCacheKey(externalSystem, externalId, aggregateType);
			var cached = await _cache.GetStringAsync(cacheKey, cancellationToken).ConfigureAwait(false);

			if (cached is not null)
			{
				result[externalId] = cached;
			}
			else
			{
				misses.Add(externalId);
			}
		}

		if (misses.Count > 0)
		{
			var fetched = await _inner.ResolveBatchAsync(externalSystem, misses, aggregateType, cancellationToken)
				.ConfigureAwait(false);

			foreach (var (externalId, aggregateId) in fetched)
			{
				result[externalId] = aggregateId;
				var cacheKey = BuildCacheKey(externalSystem, externalId, aggregateType);
				await _cache.SetStringAsync(cacheKey, aggregateId, _cacheEntryOptions, cancellationToken)
					.ConfigureAwait(false);
			}
		}

		return result;
	}

	private static string BuildCacheKey(string externalSystem, string externalId, string aggregateType)
	{
		return string.Concat(CacheKeyPrefix, externalSystem, ":", aggregateType, ":", externalId);
	}
}
