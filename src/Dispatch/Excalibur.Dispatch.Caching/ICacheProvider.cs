// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Defines the contract for cache providers in the Dispatch caching system. Implementations can use memory, distributed, or hybrid caching strategies.
/// </summary>
public interface ICacheProvider
{
	/// <summary>
	/// Gets a cached value by key.
	/// </summary>
	/// <typeparam name="T"> The type of the cached value. </typeparam>
	/// <param name="key"> The cache key. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The cached value if found; otherwise, default(T). </returns>
	Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken);

	/// <summary>
	/// Sets a value in the cache with the specified key.
	/// </summary>
	/// <typeparam name="T"> The type of the value to cache. </typeparam>
	/// <param name="key"> The cache key. </param>
	/// <param name="value"> The value to cache. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <param name="expiration"> Optional expiration time. If not specified, uses default from configuration. </param>
	/// <param name="tags"> Optional tags for cache invalidation grouping. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	Task SetAsync<T>(
		string key,
		T value,
		CancellationToken cancellationToken,
		TimeSpan? expiration = null,
		string[]? tags = null);

	/// <summary>
	/// Removes a cached value by key.
	/// </summary>
	/// <param name="key"> The cache key to remove. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	Task RemoveAsync(string key, CancellationToken cancellationToken);

	/// <summary>
	/// Removes all cached values associated with the specified tag.
	/// </summary>
	/// <param name="tag"> The tag to invalidate. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	Task RemoveByTagAsync(string tag, CancellationToken cancellationToken);

	/// <summary>
	/// Checks if a key exists in the cache.
	/// </summary>
	/// <param name="key"> The cache key to check. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> True if the key exists; otherwise, false. </returns>
	Task<bool> ExistsAsync(string key, CancellationToken cancellationToken);
}

/// <summary>
/// Extended cache provider interface for management operations.
/// Accessible via <c>GetService(typeof(ICacheProviderManagement))</c> on providers that support it.
/// </summary>
public interface ICacheProviderManagement
{
	/// <summary>
	/// Clears all cached values.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	Task ClearAsync(CancellationToken cancellationToken);
}
