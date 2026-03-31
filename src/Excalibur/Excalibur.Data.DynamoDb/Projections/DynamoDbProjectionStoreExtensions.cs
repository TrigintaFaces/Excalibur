// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DynamoDb.Projections;
using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering DynamoDB projection stores.
/// </summary>
public static class DynamoDbProjectionStoreExtensions
{
	/// <summary>
	/// Adds a DynamoDB projection store for the specified projection type.
	/// </summary>
	/// <typeparam name="TProjection">The projection type.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Optional configuration action.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddDynamoDbProjectionStore<TProjection>(
		this IServiceCollection services,
		Action<DynamoDbProjectionStoreOptions>? configure = null)
		where TProjection : class
	{
		ArgumentNullException.ThrowIfNull(services);

		if (configure != null)
		{
			services.Configure(configure);
		}

		services.TryAddSingleton<IProjectionStore<TProjection>, DynamoDbProjectionStore<TProjection>>();

		return services;
	}
}
