// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Attribute to mark dispatch actions that should invalidate cache entries when executed. Applied at the class level to trigger cache
/// invalidation after successful message processing.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class InvalidateCacheAttribute : Attribute
{
	/// <summary>
	/// Gets or initializes the cache tags to invalidate when this action is executed. All cache entries with matching tags will be removed
	/// from the cache.
	/// </summary>
	/// <value>The cache tags to invalidate when this action is executed.</value>
	public string[] Tags { get; init; } = [];

	/// <summary>
	/// Gets or initializes a value indicating whether invalidation runs even when the handler throws.
	/// </summary>
	/// <remarks>
	/// By default (<see langword="false"/>), cache invalidation runs only after the handler returns successfully —
	/// a handler that throws propagates its exception and leaves the cache untouched. When set to
	/// <see langword="true"/>, invalidation also runs when the handler throws (for example, a command that committed
	/// a partial write before failing), so stale entries are not served on the error path. An invalidation failure
	/// never masks the handler's original exception.
	/// </remarks>
	/// <value><see langword="true"/> to invalidate on both success and failure; otherwise <see langword="false"/> (success only).</value>
	public bool InvalidateOnFailure { get; init; }
}
