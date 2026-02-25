// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.Caching.Hybrid;

namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Implementation of cache invalidation service using Microsoft.Extensions.Caching.Hybrid. Provides tag-based and key-based cache
/// invalidation functionality.
/// </summary>
/// <param name="cache"> The hybrid cache instance for performing invalidation operations. </param>
public sealed class HybridCacheInvalidationService(HybridCache cache) : ICacheInvalidationService
{
	/// <inheritdoc />
	public async Task InvalidateTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken)
	{
		if (tags.Any())
		{
			await cache.RemoveByTagAsync(tags, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <inheritdoc />
	public async Task InvalidateKeysAsync(IEnumerable<string> keys, CancellationToken cancellationToken)
	{
		if (keys.Any())
		{
			await cache.RemoveAsync(keys, cancellationToken).ConfigureAwait(false);
		}
	}
}
