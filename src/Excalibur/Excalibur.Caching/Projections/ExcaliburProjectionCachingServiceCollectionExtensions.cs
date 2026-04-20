// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Caching.Projections;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Internal extension methods for registering Excalibur projection caching services.
/// Consumers opt-in via <c>IEventSourcingBuilder.AddProjectionCaching()</c>.
/// </summary>
internal static class ExcaliburProjectionCachingServiceCollectionExtensions
{
	/// <summary>
	/// Adds projection caching invalidation services to the service collection.
	/// </summary>
	/// <param name="services">The service collection to add services to.</param>
	/// <returns>The service collection for chaining.</returns>
	internal static IServiceCollection AddExcaliburProjectionCaching(this IServiceCollection services)
	{
		services.TryAddSingleton<IProjectionCacheInvalidator, ProjectionCacheInvalidator>();
		return services;
	}
}
