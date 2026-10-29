// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using Excalibur.Data.MongoDB.Projections;
using Excalibur.EventSourcing;

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
	// Private to this registration, so the projection client cannot be satisfied by -- or satisfy --
	// any other IMongoClient in the container.
	private const string ProjectionClientKey = "Excalibur.Projections.MongoDb";

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

		// NAMED options, keyed by the projection's full type name, through the one function that
		// produces that key. Two projection types registered in one container each get their own
		// instance, so one's CollectionName override cannot overwrite the other's and leave both read
		// models addressing a single collection.
		var optionsName = MongoDbProjectionCollectionConvention.GetOptionsName<TProjection>();
		_ = services.Configure(optionsName, configureOptions);
		WireOptionsValidation(services, optionsName);

		// Register projection store
		// ONE CLIENT FOR THE PROCESS -- never one per store instance.
		//
		// MongoClient owns a connection pool and is documented as thread-safe and intended to be shared.
		// The store is SCOPED and the projection host creates a DI scope per projection per batch, so a
		// store that builds its own client built a NEW CONNECTION POOL ON EVERY BATCH, exhausting
		// connections and ephemeral ports under sustained load. The symptom is a timeout against a
		// healthy cluster, which reads as an infrastructure problem rather than a lifetime bug here.
		//
		// KEYED, like the Elasticsearch and OpenSearch stores, and the earlier reasoning for leaving it
		// unkeyed was wrong in a way that could silently write to the wrong cluster.
		//
		// TryAddSingleton KEEPS an existing registration. An unkeyed IMongoClient is exactly what other
		// parts of this framework register -- the Mongo event store among them -- so a host that
		// configured its event store against cluster A and then called this method with a connection
		// string for cluster B got a projection store addressing A. The options really do bind one
		// cluster, as that comment said; the CLIENT was the thing being shared, and nothing in the
		// resolution could tell the two apart. If A happens to hold the database and permissions the
		// writes land in the wrong place silently; otherwise the consumer gets a timeout against a
		// cluster they never configured.
		//
		// A key private to this registration cannot collide, so the configured connection string is the
		// one that is used. A consumer who deliberately wants to share another client has the explicit
		// client overload below.
		//
		// The client binds the connection settings of the FIRST projection registered through this
		// overload, because TryAddKeyedSingleton keeps the first registration. One client for the
		// process means one cluster for the process: per-projection overrides of the DATABASE and
		// COLLECTION are isolated by the named options, the connection string is not.
		services.TryAddKeyedSingleton<IMongoClient>(ProjectionClientKey, (sp, _) =>
		{
			var clientOptions = sp.GetRequiredService<IOptionsMonitor<MongoDbProjectionStoreOptions>>().Get(optionsName);
			var settings = MongoClientSettings.FromConnectionString(clientOptions.ConnectionString);
			settings.ServerSelectionTimeout = TimeSpan.FromSeconds(clientOptions.ServerSelectionTimeoutSeconds);
			settings.ConnectTimeout = TimeSpan.FromSeconds(clientOptions.ConnectTimeoutSeconds);
			settings.MaxConnectionPoolSize = clientOptions.MaxPoolSize;

			if (clientOptions.UseSsl)
			{
				settings.UseTls = true;
			}

			return new MongoClient(settings);
		});

		services.TryAddScoped<IProjectionStore<TProjection>>(sp =>
		{
			var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<MongoDbProjectionStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<MongoDbProjectionStore<TProjection>>>();
			var client = sp.GetRequiredKeyedService<IMongoClient>(ProjectionClientKey);

			return new MongoDbProjectionStore<TProjection>(client, optionsMonitor, logger);
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

		// NAMED options -- see the configure-action overload above for why.
		var optionsName = MongoDbProjectionCollectionConvention.GetOptionsName<TProjection>();
		_ = services.Configure(optionsName, configureOptions);
		WireOptionsValidation(services, optionsName);

		// Register projection store with client factory
		services.TryAddScoped<IProjectionStore<TProjection>>(sp =>
		{
			var client = clientFactory(sp);
			var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<MongoDbProjectionStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<MongoDbProjectionStore<TProjection>>>();

			return new MongoDbProjectionStore<TProjection>(client, optionsMonitor, logger);
		});

		return services;
	}

	/// <summary>
	/// Wires start-up validation for ONE named options instance.
	/// </summary>
	/// <remarks>
	/// Both halves must follow the name or validation silently stops covering it. The
	/// <c>ValidateOnStart</c> registration is per-name, so it is made once per projection type; the
	/// <see cref="IValidateOptions{TOptions}"/> descriptor is consulted for every name by the options
	/// factory, so it is added once through <c>TryAddEnumerable</c>.
	/// </remarks>
	private static void WireOptionsValidation(IServiceCollection services, string optionsName)
	{
		_ = services.AddOptions<MongoDbProjectionStoreOptions>(optionsName).ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<MongoDbProjectionStoreOptions>,
				MongoDbProjectionStoreOptionsValidator>());
	}

	/// <summary>
	/// Registers multiple MongoDB projection stores that share a common connection string
	/// and database name, reducing boilerplate when multiple projections target the same database.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The shared MongoDB connection string.</param>
	/// <param name="databaseName">The shared MongoDB database name.</param>
	/// <param name="configure">Action to register individual projection stores.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <example>
	/// <code>
	/// services.AddMongoDbProjections("mongodb://localhost:27017", "mydb", projections =>
	/// {
	///     projections.Add&lt;OrderSummary&gt;();
	///     projections.Add&lt;CustomerProfile&gt;(options => options.CollectionName = "customers");
	///     projections.Add&lt;ProductCatalog&gt;();
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddMongoDbProjections(
		this IServiceCollection services,
		string connectionString,
		string databaseName,
		Action<MongoDbProjectionRegistrar> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);
		ArgumentNullException.ThrowIfNull(configure);

		var registrar = new MongoDbProjectionRegistrar(services, connectionString, databaseName);
		configure(registrar);

		return services;
	}

	/// <summary>
	/// Registers multiple MongoDB projection stores with shared options configuration,
	/// allowing advanced settings like custom timeouts, SSL, and pool sizes.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureShared">Shared options applied to all projections before per-projection overrides.</param>
	/// <param name="configure">Action to register individual projection stores.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <example>
	/// <code>
	/// services.AddMongoDbProjections(
	///     shared => { shared.ConnectionString = connStr; shared.DatabaseName = "mydb"; shared.UseSsl = true; },
	///     projections =>
	///     {
	///         projections.Add&lt;OrderSummary&gt;();
	///         projections.Add&lt;CustomerProfile&gt;(options => options.CollectionName = "customers");
	///     });
	/// </code>
	/// </example>
	public static IServiceCollection AddMongoDbProjections(
		this IServiceCollection services,
		Action<MongoDbProjectionStoreOptions> configureShared,
		Action<MongoDbProjectionRegistrar> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureShared);
		ArgumentNullException.ThrowIfNull(configure);

		var registrar = new MongoDbProjectionRegistrar(services, configureShared);
		configure(registrar);

		return services;
	}
}
