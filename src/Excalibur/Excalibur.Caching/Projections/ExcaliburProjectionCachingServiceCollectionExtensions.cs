// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Caching.Projections;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for adding Excalibur projection caching services to the DI container.
/// </summary>
public static class ExcaliburProjectionCachingServiceCollectionExtensions
{
	/// <summary>
	/// Adds projection caching invalidation services to the service collection.
	/// </summary>
	/// <param name="services">The service collection to add services to.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// This method registers the <see cref="IProjectionCacheInvalidator"/> service which
	/// enables CQRS projection handlers to invalidate cached query results when data changes.
	/// </para>
	/// <para>
	/// <strong>Prerequisites:</strong> This method requires Excalibur.Dispatch.Caching to be configured first
	/// (via <c>AddDispatchCaching()</c>) as it depends on <c>ICacheInvalidationService</c>.
	/// </para>
	/// <example>
	/// <code>
	/// services.AddDispatchCaching();  // Register ICacheInvalidationService first
	/// services.AddExcaliburProjectionCaching();  // Then register projection caching
	/// </code>
	/// </example>
	/// </remarks>
	public static IServiceCollection AddExcaliburProjectionCaching(this IServiceCollection services)
	{
		services.TryAddSingleton<IProjectionCacheInvalidator, ProjectionCacheInvalidator>();
		return services;
	}
}
