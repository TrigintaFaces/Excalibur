// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Provides caching for encryption key metadata with configurable TTL.
/// </summary>
/// <remarks>
/// <para>
/// Caching key metadata (not key material) reduces latency and KMS API calls.
/// Implementations should:
/// </para>
/// <list type="bullet">
///   <item><description>Never cache actual key material</description></item>
///   <item><description>Use sliding or absolute expiration based on configuration</description></item>
///   <item><description>Support cache invalidation on key rotation</description></item>
///   <item><description>Report cache hits/misses via telemetry</description></item>
/// </list>
/// </remarks>
public interface IKeyCache
{
	/// <summary>
	/// Gets the current number of cached entries.
	/// </summary>
	int Count { get; }

	/// <summary>
	/// Retrieves key metadata from the cache, or fetches and caches it if not present.
	/// </summary>
	/// <param name="keyId">The key identifier.</param>
	/// <param name="factory">A factory function to retrieve the key if not in cache.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The key metadata, or null if the key does not exist.</returns>
	Task<KeyMetadata?> GetOrAddAsync(
		string keyId,
		Func<string, CancellationToken, Task<KeyMetadata?>> factory,
		CancellationToken cancellationToken);

	/// <summary>
	/// Retrieves key metadata from the cache, or fetches and caches it with a custom TTL if not present.
	/// </summary>
	/// <param name="keyId">The key identifier.</param>
	/// <param name="ttl">The time-to-live for the cached entry.</param>
	/// <param name="factory">A factory function to retrieve the key if not in cache.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The key metadata, or null if the key does not exist.</returns>
	Task<KeyMetadata?> GetOrAddAsync(
		string keyId,
		TimeSpan ttl,
		Func<string, CancellationToken, Task<KeyMetadata?>> factory,
		CancellationToken cancellationToken);

	/// <summary>
	/// Attempts to retrieve key metadata from the cache.
	/// </summary>
	/// <param name="keyId">The key identifier.</param>
	/// <returns>The cached key metadata, or null if not in cache.</returns>
	KeyMetadata? TryGet(string keyId);

	/// <summary>
	/// Adds or updates key metadata in the cache.
	/// </summary>
	/// <param name="keyMetadata">The key metadata to cache.</param>
	void Set(KeyMetadata keyMetadata);

	/// <summary>
	/// Adds or updates key metadata in the cache with a custom TTL.
	/// </summary>
	/// <param name="keyMetadata">The key metadata to cache.</param>
	/// <param name="ttl">The time-to-live for the cached entry.</param>
	void Set(KeyMetadata keyMetadata, TimeSpan ttl);

	/// <summary>
	/// Removes key metadata from the cache.
	/// </summary>
	/// <param name="keyId">The key identifier to remove.</param>
	void Remove(string keyId);

	/// <summary>
	/// Invalidates all cached entries for a specific key, including all versions.
	/// </summary>
	/// <param name="keyId">The key identifier.</param>
	void Invalidate(string keyId);

	/// <summary>
	/// Clears all cached key metadata.
	/// </summary>
	void Clear();
}

/// <summary>
/// Options for configuring key caching behavior.
/// </summary>
public sealed record KeyCacheOptions
{
	/// <summary>
	/// Gets the default time-to-live for cached key metadata.
	/// </summary>
	/// <value>Default is 5 minutes.</value>
	public TimeSpan DefaultTtl { get; init; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets a value indicating whether to use sliding expiration.
	/// When true, the TTL resets on each access.
	/// </summary>
	/// <value>Default is false (absolute expiration).</value>
	public bool UseSlidingExpiration { get; init; }

	/// <summary>
	/// Gets the maximum number of entries to cache.
	/// </summary>
	/// <value>Default is 1000.</value>
	public int MaxEntries { get; init; } = 1000;

	/// <summary>
	/// Gets a value indicating whether to automatically refresh entries before expiration.
	/// </summary>
	public bool EnableAutoRefresh { get; init; }

	/// <summary>
	/// Gets the threshold before expiration at which to trigger auto-refresh.
	/// </summary>
	/// <value>Default is 30 seconds.</value>
	public TimeSpan AutoRefreshThreshold { get; init; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets the default options.
	/// </summary>
	public static KeyCacheOptions Default => new();
}
