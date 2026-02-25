// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.MongoDB.Saga;
using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Dispatch.Abstractions.Serialization;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MongoDB.Driver;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering MongoDB saga store services.
/// </summary>
public static class MongoDbSagaExtensions
{
	/// <summary>
	/// Adds the MongoDB saga store to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Action to configure saga store options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// This method registers <see cref="MongoDbSagaStore"/> as the implementation of <see cref="ISagaStore"/>.
	/// The store uses MongoDB document storage with JSON serialization for saga state.
	/// </para>
	/// <para>
	/// Example usage:
	/// <code>
	/// services.AddMongoDbSagaStore(options =>
	/// {
	///     options.ConnectionString = "mongodb://localhost:27017";
	///     options.DatabaseName = "myapp";
	///     options.CollectionName = "sagas";
	/// });
	/// </code>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddMongoDbSagaStore(
		this IServiceCollection services,
		Action<MongoDbSagaOptions> configureOptions)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureOptions);

		_ = services.AddOptions<MongoDbSagaOptions>()
			.Configure(configureOptions)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.TryAddSingleton<MongoDbSagaStore>();
		services.TryAddSingleton<ISagaStore>(sp => sp.GetRequiredService<MongoDbSagaStore>());

		return services;
	}

	/// <summary>
	/// Adds the MongoDB saga store to the service collection with a connection string.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The MongoDB connection string.</param>
	/// <param name="databaseName">The database name. Defaults to "excalibur".</param>
	/// <param name="collectionName">The collection name. Defaults to "sagas".</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// Simplified registration for common scenarios where only the connection string and
	/// optional database/collection names are needed.
	/// </para>
	/// <para>
	/// Example usage:
	/// <code>
	/// services.AddMongoDbSagaStore("mongodb://localhost:27017");
	/// </code>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddMongoDbSagaStore(
		this IServiceCollection services,
		string connectionString,
		string databaseName = "excalibur",
		string collectionName = "sagas")
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		return services.AddMongoDbSagaStore(options =>
		{
			options.ConnectionString = connectionString;
			options.DatabaseName = databaseName;
			options.CollectionName = collectionName;
		});
	}

	/// <summary>
	/// Adds the MongoDB saga store to the service collection with an existing client.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="clientFactory">Factory function that provides a MongoDB client.</param>
	/// <param name="configureOptions">Action to configure saga store options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// Use this overload for advanced scenarios like shared client instances,
	/// custom connection pooling, or integration with existing MongoDB infrastructure.
	/// </para>
	/// <para>
	/// Example usage:
	/// <code>
	/// services.AddMongoDbSagaStore(
	///     sp => sp.GetRequiredService&lt;IMongoClient&gt;(),
	///     options => options.DatabaseName = "sagas");
	/// </code>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddMongoDbSagaStore(
		this IServiceCollection services,
		Func<IServiceProvider, IMongoClient> clientFactory,
		Action<MongoDbSagaOptions> configureOptions)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(clientFactory);
		ArgumentNullException.ThrowIfNull(configureOptions);

		_ = services.AddOptions<MongoDbSagaOptions>()
			.Configure(configureOptions)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.TryAddSingleton(sp =>
		{
			var client = clientFactory(sp);
			var options = sp.GetRequiredService<IOptions<MongoDbSagaOptions>>();
			var logger = sp.GetRequiredService<ILogger<MongoDbSagaStore>>();
			var serializer = sp.GetRequiredService<IJsonSerializer>();
			return new MongoDbSagaStore(client, options, logger, serializer);
		});
		services.TryAddSingleton<ISagaStore>(sp => sp.GetRequiredService<MongoDbSagaStore>());

		return services;
	}
}
