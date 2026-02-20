// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.A3.Abstractions.Authorization;
using Excalibur.Data.MongoDB.Authorization;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering MongoDB authorization services.
/// </summary>
public static class MongoDbAuthorizationExtensions
{
	/// <summary>
	/// Adds MongoDB-based authorization services to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddMongoDbAuthorization(
		this IServiceCollection services,
		Action<MongoDbAuthorizationOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<MongoDbAuthorizationOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		services.TryAddSingleton<IGrantRequestProvider, MongoDbGrantService>();
		services.TryAddSingleton<IActivityGroupGrantService, MongoDbActivityGroupGrantService>();

		return services;
	}

	/// <summary>
	/// Adds MongoDB-based authorization services to the service collection with a connection string.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The MongoDB connection string.</param>
	/// <param name="databaseName">The database name. Defaults to "authorization".</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddMongoDbAuthorization(
		this IServiceCollection services,
		string connectionString,
		string databaseName = "authorization")
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		return services.AddMongoDbAuthorization(options =>
		{
			options.ConnectionString = connectionString;
			options.DatabaseName = databaseName;
		});
	}

	/// <summary>
	/// Adds only the MongoDB grant service to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddMongoDbGrantService(
		this IServiceCollection services,
		Action<MongoDbAuthorizationOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<MongoDbAuthorizationOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		services.TryAddSingleton<IGrantRequestProvider, MongoDbGrantService>();

		return services;
	}

	/// <summary>
	/// Adds only the MongoDB activity group grant service to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddMongoDbActivityGroupGrantService(
		this IServiceCollection services,
		Action<MongoDbAuthorizationOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<MongoDbAuthorizationOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		services.TryAddSingleton<IActivityGroupGrantService, MongoDbActivityGroupGrantService>();

		return services;
	}
}
