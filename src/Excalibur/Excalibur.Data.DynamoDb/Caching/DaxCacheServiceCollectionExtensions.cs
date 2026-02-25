// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DynamoDb.Caching;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering DynamoDB DAX caching services.
/// </summary>
public static class DaxCacheServiceCollectionExtensions
{
	/// <summary>
	/// Adds DynamoDB DAX caching services to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The configuration action for DAX cache options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddDynamoDbDaxCaching(
		this IServiceCollection services,
		Action<DaxCacheOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<DaxCacheOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.TryAddSingleton<IDaxCacheProvider, InMemoryDaxCacheProvider>();

		return services;
	}
}
