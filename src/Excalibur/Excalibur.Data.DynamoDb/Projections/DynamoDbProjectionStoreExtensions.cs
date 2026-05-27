// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DynamoDb.Projections;
using Excalibur.EventSourcing;

using Microsoft.Extensions.DependencyInjection.Extensions;

#pragma warning disable IL2091 // DI registration methods create stores with DynamicallyAccessedMembers-annotated TProjection; consumer types are preserved by the store contract
#pragma warning disable IL2026 // Projection stores use reflection-based JSON serialization as fallback
#pragma warning disable IL3050 // Generic JSON serialization may require dynamic code generation

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
