// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.Caching.Hybrid;

namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Implementation of cache invalidation service using Microsoft.Extensions.Caching.Hybrid. Provides tag-based and key-based cache
/// invalidation functionality.
/// </summary>
/// <param name="cache"> The hybrid cache instance for performing invalidation operations. </param>
internal sealed class HybridCacheInvalidationService(HybridCache cache) : ICacheInvalidationService
{
	/// <inheritdoc />
	public async Task InvalidateTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(tags);

		// Materialize once: a deferred/lazy sequence would otherwise be enumerated twice (the emptiness
		// check and the RemoveByTagAsync call), which can disagree or run its side effects twice.
		var materialized = tags as IReadOnlyCollection<string> ?? tags.ToArray();
		if (materialized.Count > 0)
		{
			await cache.RemoveByTagAsync(materialized, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <inheritdoc />
	public async Task InvalidateKeysAsync(IEnumerable<string> keys, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(keys);

		// Materialize once (see InvalidateTagsAsync) to avoid double-enumerating a deferred sequence.
		var materialized = keys as IReadOnlyCollection<string> ?? keys.ToArray();
		if (materialized.Count > 0)
		{
			await cache.RemoveAsync(materialized, cancellationToken).ConfigureAwait(false);
		}
	}
}
