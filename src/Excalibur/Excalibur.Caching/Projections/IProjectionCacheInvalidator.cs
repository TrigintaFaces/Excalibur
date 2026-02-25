// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Caching.Projections;

/// <summary>
/// Contract for projection handlers that want to perform cache invalidation.
/// </summary>
/// <remarks>
/// This interface enables CQRS projection handlers to invalidate cached query results
/// when the underlying data changes. Implementations should use registered
/// <see cref="IProjectionTagResolver{T}"/> instances to determine which cache tags
/// to invalidate based on the message content.
/// </remarks>
public interface IProjectionCacheInvalidator
{
	/// <summary>
	/// Invalidates cache entries related to the projection for the specified message.
	/// </summary>
	/// <param name="message">The message that triggered the projection.</param>
	/// <param name="cancellationToken">Token used to cancel the operation.</param>
	/// <returns>A task that completes when invalidation is finished.</returns>
	[RequiresDynamicCode("Calls ExtractTags which uses MakeGenericType for resolver lookup")]
	ValueTask InvalidateCacheAsync(object message, CancellationToken cancellationToken);
}
