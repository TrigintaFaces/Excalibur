// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.Options;

using StackExchange.Redis;

namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Redis-native implementation of <see cref="ICacheTagTracker"/> using atomic set primitives
/// (<c>SADD</c>/<c>SMEMBERS</c>/<c>SREM</c>) for cross-instance tag-to-key mapping.
/// </summary>
/// <remarks>
/// <para>
/// Storage scheme: each tag's key set is a Redis SET under <c>dispatch:tag:{tagName}</c>; each key's tag set is a
/// Redis SET under <c>dispatch:keytags:{cacheKey}</c>. Both carry a TTL of 2×
/// <see cref="CacheBehaviorOptions.DefaultExpiration"/> so tracker entries outlive cache entries (self-healing on
/// crash/restart); the TTL is refreshed on each registration.
/// </para>
/// <para>
/// <b>Concurrency:</b> unlike the generic <see cref="DistributedCacheTagTracker"/>, registration is atomic — a
/// single <c>SADD</c> adds the member server-side with no read-modify-write, so concurrent registrations of
/// different keys under the same tag never lose a key (satisfies the all-keys-present guarantee for the
/// multi-instance production scenario). This tracker is selected by DI when a real Redis
/// <see cref="IConnectionMultiplexer"/> is configured.
/// </para>
/// </remarks>
internal sealed class RedisCacheTagTracker : ICacheTagTracker
{
	private const string TagKeyPrefix = "dispatch:tag:";
	private const string KeyTagsPrefix = "dispatch:keytags:";

	private readonly IConnectionMultiplexer _connection;
	private readonly TimeSpan _ttl;
	private readonly int _maxKeysPerTag;

	/// <summary>
	/// Initializes a new instance of the <see cref="RedisCacheTagTracker"/> class.
	/// </summary>
	/// <param name="connection">The Redis connection multiplexer providing atomic set commands.</param>
	/// <param name="options">Cache options providing TTL and capacity configuration.</param>
	public RedisCacheTagTracker(IConnectionMultiplexer connection, IOptions<CacheOptions> options)
	{
		ArgumentNullException.ThrowIfNull(connection);
		ArgumentNullException.ThrowIfNull(options);

		_connection = connection;

		var ttl = options.Value.Behavior.DefaultExpiration * 2;
		_ttl = ttl > TimeSpan.Zero ? ttl : TimeSpan.FromMinutes(20);

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

		cancellationToken.ThrowIfCancellationRequested();

		var db = _connection.GetDatabase();

		// Record the reverse key->tags mapping (a Redis SET) so UnregisterKeyAsync can find every tag set to clean.
		var keyTagsKey = (RedisKey)string.Concat(KeyTagsPrefix, key);
		var tagValues = Array.ConvertAll(tags, static t => (RedisValue)t);
		_ = await db.SetAddAsync(keyTagsKey, tagValues).ConfigureAwait(false);
		_ = await db.KeyExpireAsync(keyTagsKey, _ttl).ConfigureAwait(false);

		foreach (var tag in tags)
		{
			cancellationToken.ThrowIfCancellationRequested();

			var tagKey = (RedisKey)string.Concat(TagKeyPrefix, tag);

			// Best-effort soft cap to prevent unbounded growth (a small overshoot under concurrency is acceptable
			// for a memory bound; it is not a correctness invariant).
			if (_maxKeysPerTag > 0)
			{
				var count = await db.SetLengthAsync(tagKey).ConfigureAwait(false);
				if (count >= _maxKeysPerTag)
				{
					continue;
				}
			}

			// Atomic server-side add — no read-modify-write, so concurrent adders never overwrite each other.
			_ = await db.SetAddAsync(tagKey, (RedisValue)key).ConfigureAwait(false);
			_ = await db.KeyExpireAsync(tagKey, _ttl).ConfigureAwait(false);
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

		var db = _connection.GetDatabase();

		foreach (var tag in tags)
		{
			cancellationToken.ThrowIfCancellationRequested();

			var members = await db.SetMembersAsync((RedisKey)string.Concat(TagKeyPrefix, tag)).ConfigureAwait(false);
			foreach (var member in members)
			{
				var value = (string?)member;
				if (value is not null)
				{
					_ = result.Add(value);
				}
			}
		}

		return result;
	}

	/// <inheritdoc />
	public async Task UnregisterKeyAsync(string key, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var db = _connection.GetDatabase();

		var keyTagsKey = (RedisKey)string.Concat(KeyTagsPrefix, key);
		var tags = await db.SetMembersAsync(keyTagsKey).ConfigureAwait(false);

		foreach (var tag in tags)
		{
			cancellationToken.ThrowIfCancellationRequested();

			var tagName = (string?)tag;
			if (tagName is null)
			{
				continue;
			}

			// Atomic server-side remove of just this key from the tag's set.
			_ = await db.SetRemoveAsync((RedisKey)string.Concat(TagKeyPrefix, tagName), (RedisValue)key).ConfigureAwait(false);
		}

		_ = await db.KeyDeleteAsync(keyTagsKey).ConfigureAwait(false);
	}
}
