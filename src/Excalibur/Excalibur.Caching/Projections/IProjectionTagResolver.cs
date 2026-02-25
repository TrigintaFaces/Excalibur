// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Caching.Projections;

/// <summary>
/// Resolves cache tags for a specific message type.
/// </summary>
/// <typeparam name="T">The message type.</typeparam>
/// <remarks>
/// Implement this interface to provide custom cache tag resolution for specific message types.
/// The resolved tags are used by <see cref="IProjectionCacheInvalidator"/> to invalidate
/// the appropriate cache entries when projections are updated.
/// </remarks>
public interface IProjectionTagResolver<in T>
{
	/// <summary>
	/// Gets cache tags for the supplied message.
	/// </summary>
	/// <param name="message">The message.</param>
	/// <returns>A collection of cache tags to invalidate.</returns>
	IEnumerable<string> GetTags(T message);
}
