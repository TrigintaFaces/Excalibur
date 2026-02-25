// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.MongoDB.Projections;
using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MongoDB.Driver;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering MongoDB projection store services.
/// </summary>
public static class MongoDbProjectionStoreExtensions
{
	/// <summary>
	/// Adds the MongoDB projection store to the service collection.
	/// </summary>
	/// <typeparam name="TProjection">The projection type to store.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Action to configure projection store options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddMongoDbProjectionStore<TProjection>(
		this IServiceCollection services,
		Action<MongoDbProjectionStoreOptions> configureOptions)
		where TProjection : class
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureOptions);

		// Configure options
		_ = services.AddOptions<MongoDbProjectionStoreOptions>()
			.Configure(configureOptions)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// Register projection store
		services.TryAddScoped<IProjectionStore<TProjection>>(sp =>
		{
			var options = sp.GetRequiredService<IOptions<MongoDbProjectionStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<MongoDbProjectionStore<TProjection>>>();

			return new MongoDbProjectionStore<TProjection>(options, logger);
		});

		return services;
	}

	/// <summary>
	/// Adds the MongoDB projection store to the service collection with a connection string.
	/// </summary>
	/// <typeparam name="TProjection">The projection type to store.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The MongoDB connection string.</param>
	/// <param name="databaseName">The database name.</param>
	/// <param name="configureOptions">Optional action to further configure projection store options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddMongoDbProjectionStore<TProjection>(
		this IServiceCollection services,
		string connectionString,
		string databaseName,
		Action<MongoDbProjectionStoreOptions>? configureOptions = null)
		where TProjection : class
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

		return services.AddMongoDbProjectionStore<TProjection>(options =>
		{
			options.ConnectionString = connectionString;
			options.DatabaseName = databaseName;
			configureOptions?.Invoke(options);
		});
	}

	/// <summary>
	/// Adds the MongoDB projection store to the service collection with an existing client.
	/// </summary>
	/// <typeparam name="TProjection">The projection type to store.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="clientFactory">Factory function that provides a MongoDB client.</param>
	/// <param name="configureOptions">Action to configure projection store options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// Use this overload for advanced scenarios like shared client instances,
	/// custom connection pooling, or integration with existing MongoDB infrastructure.
	/// </remarks>
	public static IServiceCollection AddMongoDbProjectionStore<TProjection>(
		this IServiceCollection services,
		Func<IServiceProvider, IMongoClient> clientFactory,
		Action<MongoDbProjectionStoreOptions> configureOptions)
		where TProjection : class
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(clientFactory);
		ArgumentNullException.ThrowIfNull(configureOptions);

		// Configure options
		_ = services.AddOptions<MongoDbProjectionStoreOptions>()
			.Configure(configureOptions)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// Register projection store with client factory
		services.TryAddScoped<IProjectionStore<TProjection>>(sp =>
		{
			var client = clientFactory(sp);
			var options = sp.GetRequiredService<IOptions<MongoDbProjectionStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<MongoDbProjectionStore<TProjection>>>();

			return new MongoDbProjectionStore<TProjection>(client, options, logger);
		});

		return services;
	}
}
