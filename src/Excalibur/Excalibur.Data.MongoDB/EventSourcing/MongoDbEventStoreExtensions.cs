// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.MongoDB.EventSourcing;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MongoDB.Driver;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering MongoDB event store services.
/// </summary>
public static class MongoDbEventStoreExtensions
{
	/// <summary>
	/// Adds the MongoDB event store to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Action to configure event store options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddMongoDbEventStore(
		this IServiceCollection services,
		Action<MongoDbEventStoreOptions> configureOptions)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureOptions);

		// Configure options
		_ = services.AddOptions<MongoDbEventStoreOptions>()
			.Configure(configureOptions)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// Register event store
		services.TryAddScoped<IEventStore>(sp =>
		{
			var options = sp.GetRequiredService<IOptions<MongoDbEventStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<MongoDbEventStore>>();
			var internalSerializer = sp.GetService<IInternalSerializer>();
			var payloadSerializer = sp.GetService<IPayloadSerializer>();

			return new MongoDbEventStore(
				options,
				logger,
				internalSerializer,
				payloadSerializer);
		});

		return services;
	}

	/// <summary>
	/// Adds the MongoDB event store to the service collection with a connection string.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The MongoDB connection string.</param>
	/// <param name="databaseName">The database name.</param>
	/// <param name="configureOptions">Optional action to further configure event store options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddMongoDbEventStore(
		this IServiceCollection services,
		string connectionString,
		string databaseName,
		Action<MongoDbEventStoreOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

		return services.AddMongoDbEventStore(options =>
		{
			options.ConnectionString = connectionString;
			options.DatabaseName = databaseName;
			configureOptions?.Invoke(options);
		});
	}

	/// <summary>
	/// Adds the MongoDB event store to the service collection with an existing client.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="clientFactory">Factory function that provides a MongoDB client.</param>
	/// <param name="configureOptions">Action to configure event store options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// Use this overload for advanced scenarios like shared client instances,
	/// custom connection pooling, or integration with existing MongoDB infrastructure.
	/// </remarks>
	public static IServiceCollection AddMongoDbEventStore(
		this IServiceCollection services,
		Func<IServiceProvider, IMongoClient> clientFactory,
		Action<MongoDbEventStoreOptions> configureOptions)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(clientFactory);
		ArgumentNullException.ThrowIfNull(configureOptions);

		// Configure options
		_ = services.AddOptions<MongoDbEventStoreOptions>()
			.Configure(configureOptions)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// Register event store with client factory
		services.TryAddScoped<IEventStore>(sp =>
		{
			var client = clientFactory(sp);
			var options = sp.GetRequiredService<IOptions<MongoDbEventStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<MongoDbEventStore>>();
			var internalSerializer = sp.GetService<IInternalSerializer>();
			var payloadSerializer = sp.GetService<IPayloadSerializer>();

			return new MongoDbEventStore(
				client,
				options,
				logger,
				internalSerializer,
				payloadSerializer);
		});

		return services;
	}
}
