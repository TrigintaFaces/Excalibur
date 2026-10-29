// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.DynamoDb.Projections;
using Excalibur.EventSourcing;

using Microsoft.Extensions.DependencyInjection.Extensions;

#pragma warning disable IL2091 // DI registration methods create stores with DynamicallyAccessedMembers-annotated TProjection; consumer types are preserved by the store contract

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
	/// <remarks>
	/// Settings are bound to the options instance named
	/// <see cref="DynamoDbProjectionTableConvention.GetOptionsName{TProjection}"/>, so several projection
	/// types registered in one container each keep their own table name instead of sharing the last
	/// delegate's. Validation is registered against the same name — validation attached to the unnamed
	/// instance does not validate a named one.
	/// </remarks>
	public static IServiceCollection AddDynamoDbProjectionStore<TProjection>(
		this IServiceCollection services,
		Action<DynamoDbProjectionStoreOptions>? configure = null)
		where TProjection : class
	{
		ArgumentNullException.ThrowIfNull(services);

		var optionsName = DynamoDbProjectionTableConvention.GetOptionsName<TProjection>();
		var builder = services.AddOptions<DynamoDbProjectionStoreOptions>(optionsName)
			.Validate(
				static options => !string.IsNullOrWhiteSpace(options.TableName),
				"DynamoDbProjectionStoreOptions.TableName must be a non-empty DynamoDB table name.")
			.ValidateOnStart();

		if (configure != null)
		{
			_ = builder.Configure(configure);
		}

		services.TryAddSingleton<IProjectionStore<TProjection>, DynamoDbProjectionStore<TProjection>>();

		return services;
	}
}
