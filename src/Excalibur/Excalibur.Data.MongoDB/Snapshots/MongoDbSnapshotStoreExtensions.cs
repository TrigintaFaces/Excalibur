// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.MongoDB.Snapshots;
using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MongoDB.Driver;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering MongoDB snapshot store services.
/// </summary>
public static class MongoDbSnapshotStoreExtensions
{
	/// <summary>
	/// Adds the MongoDB snapshot store to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Action to configure snapshot store options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddMongoDbSnapshotStore(
		this IServiceCollection services,
		Action<MongoDbSnapshotStoreOptions> configureOptions)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureOptions);

		// Configure options
		_ = services.AddOptions<MongoDbSnapshotStoreOptions>()
			.Configure(configureOptions)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// Register snapshot store
		services.TryAddScoped<ISnapshotStore>(sp =>
		{
			var options = sp.GetRequiredService<IOptions<MongoDbSnapshotStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<MongoDbSnapshotStore>>();

			return new MongoDbSnapshotStore(options, logger);
		});

		return services;
	}

	/// <summary>
	/// Adds the MongoDB snapshot store to the service collection with a connection string.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The MongoDB connection string.</param>
	/// <param name="databaseName">The database name.</param>
	/// <param name="configureOptions">Optional action to further configure snapshot store options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddMongoDbSnapshotStore(
		this IServiceCollection services,
		string connectionString,
		string databaseName,
		Action<MongoDbSnapshotStoreOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

		return services.AddMongoDbSnapshotStore(options =>
		{
			options.ConnectionString = connectionString;
			options.DatabaseName = databaseName;
			configureOptions?.Invoke(options);
		});
	}

	/// <summary>
	/// Adds the MongoDB snapshot store to the service collection with an existing client.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="clientFactory">Factory function that provides a MongoDB client.</param>
	/// <param name="configureOptions">Action to configure snapshot store options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// Use this overload for advanced scenarios like shared client instances,
	/// custom connection pooling, or integration with existing MongoDB infrastructure.
	/// </remarks>
	public static IServiceCollection AddMongoDbSnapshotStore(
		this IServiceCollection services,
		Func<IServiceProvider, IMongoClient> clientFactory,
		Action<MongoDbSnapshotStoreOptions> configureOptions)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(clientFactory);
		ArgumentNullException.ThrowIfNull(configureOptions);

		// Configure options
		_ = services.AddOptions<MongoDbSnapshotStoreOptions>()
			.Configure(configureOptions)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// Register snapshot store with client factory
		services.TryAddScoped<ISnapshotStore>(sp =>
		{
			var client = clientFactory(sp);
			var options = sp.GetRequiredService<IOptions<MongoDbSnapshotStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<MongoDbSnapshotStore>>();

			return new MongoDbSnapshotStore(client, options, logger);
		});

		return services;
	}
}
