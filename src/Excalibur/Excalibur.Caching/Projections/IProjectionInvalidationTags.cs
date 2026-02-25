// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Caching.Projections;

/// <summary>
/// Provides cache tags for projection invalidation.
/// </summary>
/// <remarks>
/// Implement this interface on message types to explicitly define which cache tags
/// should be invalidated when the message is processed. This takes precedence over
/// <see cref="IProjectionTagResolver{T}"/> and convention-based tag resolution.
/// </remarks>
public interface IProjectionInvalidationTags
{
	/// <summary>
	/// Gets cache tags associated with the projection.
	/// </summary>
	/// <returns>A collection of cache tags to invalidate.</returns>
	IEnumerable<string> GetProjectionCacheTags();
}
