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
}
