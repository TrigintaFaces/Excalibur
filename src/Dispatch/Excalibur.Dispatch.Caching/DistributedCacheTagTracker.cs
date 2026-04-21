// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Distributed implementation of <see cref="ICacheTagTracker"/> using <see cref="IDistributedCache"/>
/// for cross-instance tag-to-key mapping. Enables tag-based cache invalidation across multiple
/// application instances sharing the same distributed cache backend (Redis, SQL Server, etc.).
/// </summary>
/// <remarks>
/// <para>
/// Storage scheme: tag-to-key sets are stored as JSON-serialized <see cref="HashSet{T}"/> under
/// <c>dispatch:tag:{tagName}</c>; key-to-tag arrays are stored under <c>dispatch:keytags:{cacheKey}</c>.
/// All entries use a TTL of 2x <see cref="CacheBehaviorOptions.DefaultExpiration"/> to ensure tracker
/// entries outlive cache entries (self-healing on crash/restart).
/// </para>
/// <para>
/// Concurrency: read-modify-write operations on tag sets are not atomic. Concurrent registrations
/// for the same tag may lose one key (last-writer-wins). This is acceptable for cache invalidation
/// where the worst case is an extra cache miss. TTL-based self-healing cleans up stale entries.
/// </para>
/// </remarks>
internal sealed class DistributedCacheTagTracker : ICacheTagTracker
{
	private const string TagKeyPrefix = "dispatch:tag:";
	private const string KeyTagsPrefix = "dispatch:keytags:";

	private readonly IDistributedCache _cache;
	private readonly DistributedCacheEntryOptions _entryOptions;
	private readonly int _maxKeysPerTag;

	/// <summary>
	/// Initializes a new instance of the <see cref="DistributedCacheTagTracker"/> class.
	/// </summary>
	/// <param name="cache">The distributed cache backend for storing tag-to-key mappings.</param>
	/// <param name="options">Cache options providing TTL and capacity configuration.</param>
	public DistributedCacheTagTracker(
		IDistributedCache cache,
		IOptions<CacheOptions> options)
	{
		ArgumentNullException.ThrowIfNull(cache);
		ArgumentNullException.ThrowIfNull(options);

		_cache = cache;
		var ttl = options.Value.Behavior.DefaultExpiration * 2;
		if (ttl <= TimeSpan.Zero)
		{
			ttl = TimeSpan.FromMinutes(20);
		}

		_entryOptions = new DistributedCacheEntryOptions
		{
			AbsoluteExpirationRelativeToNow = ttl,
		};
		_maxKeysPerTag = options.Value.TagTrackerCapacity > 0
			? options.Value.TagTrackerCapacity
			: 10_000;
	}

	/// <inheritdoc />
	public async Task RegisterKeyAsync(string key, string[] tags, CancellationToken cancellationToken)
	{
		if (tags is null || tags.Length == 0)
		{
			return;
		}

		// Store key-to-tags mapping for UnregisterKeyAsync
		var keyTagsKey = string.Concat(KeyTagsPrefix, key);
		var tagsJson = JsonSerializer.SerializeToUtf8Bytes(tags, TagTrackerJsonContext.Default.StringArray);
		await _cache.SetAsync(keyTagsKey, tagsJson, _entryOptions, cancellationToken).ConfigureAwait(false);

		// Add key to each tag's key set
		foreach (var tag in tags)
		{
			var tagKey = string.Concat(TagKeyPrefix, tag);
			var existingBytes = await _cache.GetAsync(tagKey, cancellationToken).ConfigureAwait(false);

			HashSet<string> keySet;
			if (existingBytes is not null)
			{
				keySet = JsonSerializer.Deserialize(existingBytes, TagTrackerJsonContext.Default.HashSetString)
						 ?? new HashSet<string>(StringComparer.Ordinal);
			}
			else
			{
				keySet = new HashSet<string>(StringComparer.Ordinal);
			}

			// Bounded: skip adding if at capacity
			if (keySet.Count >= _maxKeysPerTag)
			{
				continue;
			}

			keySet.Add(key);

			var setJson = JsonSerializer.SerializeToUtf8Bytes(keySet, TagTrackerJsonContext.Default.HashSetString);
			await _cache.SetAsync(tagKey, setJson, _entryOptions, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <inheritdoc />
	public async Task<HashSet<string>> GetKeysByTagsAsync(string[] tags, CancellationToken cancellationToken)
	{
		var result = new HashSet<string>(StringComparer.Ordinal);

		if (tags is null || tags.Length == 0)
		{
			return result;
		}

		foreach (var tag in tags)
		{
			var tagKey = string.Concat(TagKeyPrefix, tag);
			var existingBytes = await _cache.GetAsync(tagKey, cancellationToken).ConfigureAwait(false);

			if (existingBytes is not null)
			{
				var keySet = JsonSerializer.Deserialize(existingBytes, TagTrackerJsonContext.Default.HashSetString);
				if (keySet is not null)
				{
					result.UnionWith(keySet);
				}
			}
		}

		return result;
	}

	/// <inheritdoc />
	public async Task UnregisterKeyAsync(string key, CancellationToken cancellationToken)
	{
		// Read tags for this key
		var keyTagsKey = string.Concat(KeyTagsPrefix, key);
		var tagsBytes = await _cache.GetAsync(keyTagsKey, cancellationToken).ConfigureAwait(false);

		if (tagsBytes is not null)
		{
			var tags = JsonSerializer.Deserialize(tagsBytes, TagTrackerJsonContext.Default.StringArray);

			if (tags is not null)
			{
				// Remove key from each tag's set
				foreach (var tag in tags)
				{
					var tagKey = string.Concat(TagKeyPrefix, tag);
					var existingBytes = await _cache.GetAsync(tagKey, cancellationToken).ConfigureAwait(false);

					if (existingBytes is not null)
					{
						var keySet = JsonSerializer.Deserialize(existingBytes, TagTrackerJsonContext.Default.HashSetString);

						if (keySet is not null)
						{
							keySet.Remove(key);

							if (keySet.Count == 0)
							{
								await _cache.RemoveAsync(tagKey, cancellationToken).ConfigureAwait(false);
							}
							else
							{
								var setJson = JsonSerializer.SerializeToUtf8Bytes(keySet, TagTrackerJsonContext.Default.HashSetString);
								await _cache.SetAsync(tagKey, setJson, _entryOptions, cancellationToken).ConfigureAwait(false);
							}
						}
					}
				}
			}

			// Remove the key-to-tags entry
			await _cache.RemoveAsync(keyTagsKey, cancellationToken).ConfigureAwait(false);
		}
	}
}

/// <summary>
/// Source-generated JSON serialization context for tag tracker data structures.
/// AOT-safe serialization of tag-to-key mappings stored in <see cref="IDistributedCache"/>.
/// </summary>
[JsonSerializable(typeof(HashSet<string>))]
[JsonSerializable(typeof(string[]))]
internal sealed partial class TagTrackerJsonContext : JsonSerializerContext;
