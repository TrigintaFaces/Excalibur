// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Tracks cache key-to-tag mappings for cache modes that don't natively support tag-based invalidation.
/// Required for IMemoryCache and IDistributedCache modes. HybridCache has native tag support.
/// </summary>
public interface ICacheTagTracker
{
	/// <summary>
	/// Registers cache key with associated tags.
	/// </summary>
	/// <param name="key">The cache key.</param>
	/// <param name="tags">The tags associated with this key.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task RegisterKeyAsync(string key, string[] tags, CancellationToken cancellationToken);

	/// <summary>
	/// Gets all cache keys associated with the specified tags.
	/// </summary>
	/// <param name="tags">The tags to lookup.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Set of cache keys associated with any of the specified tags.</returns>
	Task<HashSet<string>> GetKeysByTagsAsync(string[] tags, CancellationToken cancellationToken);

	/// <summary>
	/// Removes tag-to-key mappings for the specified key.
	/// Called when a cache entry is removed or expires.
	/// </summary>
	/// <param name="key">The cache key to unregister.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task UnregisterKeyAsync(string key, CancellationToken cancellationToken);
}
