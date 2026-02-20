// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Represents a dispatch action whose result can be cached.
/// </summary>
/// <typeparam name="T">The type of the result.</typeparam>
public interface ICacheable<T> : IDispatchAction<T>
{
	/// <summary>
	/// Gets cache duration in seconds. Default: 60.
	/// </summary>
	/// <value>The cache duration in seconds.</value>
	int ExpirationSeconds => 60;

	/// <summary>
	/// A stable key representing the identity of this request's result.
	/// </summary>
	/// <returns>A stable cache key for this request.</returns>
	string GetCacheKey();

	/// <summary>
	/// Optional tags for invalidation (e.g., entity IDs).
	/// </summary>
	/// <returns>An array of cache tags, or null if no tags are specified.</returns>
	string[]? GetCacheTags() => null;

	/// <summary>
	/// Default: cache everything.
	/// </summary>
	/// <param name="result">The result to check for caching eligibility.</param>
	/// <returns>True if the result should be cached; otherwise, false. Default: true.</returns>
	bool ShouldCache(object? result) => true;
}
