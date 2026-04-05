// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Extension methods for <see cref="IKeyCache"/>.
/// </summary>
public static class KeyCacheExtensions
{
	/// <summary>Gets or adds key metadata with a custom TTL.</summary>
	public static Task<KeyMetadata?> GetOrAddAsync(this IKeyCache cache, string keyId, TimeSpan ttl, Func<string, CancellationToken, Task<KeyMetadata?>> factory, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(cache);
		if (cache is IKeyCacheAdmin admin)
		{
			return admin.GetOrAddAsync(keyId, ttl, factory, cancellationToken);
		}

		return cache.GetOrAddAsync(keyId, factory, cancellationToken);
	}

	/// <summary>Adds or updates key metadata with a custom TTL.</summary>
	public static void Set(this IKeyCache cache, KeyMetadata keyMetadata, TimeSpan ttl)
	{
		ArgumentNullException.ThrowIfNull(cache);
		if (cache is IKeyCacheAdmin admin)
		{
			admin.Set(keyMetadata, ttl);
			return;
		}

		cache.Set(keyMetadata);
	}

	/// <summary>Invalidates all cached entries for a specific key.</summary>
	public static void Invalidate(this IKeyCache cache, string keyId)
	{
		ArgumentNullException.ThrowIfNull(cache);
		if (cache is IKeyCacheAdmin admin)
		{
			admin.Invalidate(keyId);
			return;
		}

		cache.Remove(keyId);
	}

	/// <summary>Clears all cached key metadata.</summary>
	public static void Clear(this IKeyCache cache)
	{
		ArgumentNullException.ThrowIfNull(cache);
		if (cache is IKeyCacheAdmin admin)
		{
			admin.Clear();
		}
	}
}
