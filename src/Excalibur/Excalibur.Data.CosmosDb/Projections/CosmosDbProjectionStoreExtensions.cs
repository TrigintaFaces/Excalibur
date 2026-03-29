// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.CosmosDb.Projections;
using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Cosmos DB projection store services.
/// </summary>
public static class CosmosDbProjectionStoreExtensions
{
	/// <summary>
	/// Adds the Cosmos DB projection store to the service collection.
	/// </summary>
	/// <typeparam name="TProjection">The projection type to store.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Action to configure projection store options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddCosmosDbProjectionStore<TProjection>(
		this IServiceCollection services,
		Action<CosmosDbProjectionStoreOptions> configureOptions)
		where TProjection : class
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureOptions);

		// Configure options
		_ = services.Configure(configureOptions);

		// Register projection store
		services.TryAddScoped<IProjectionStore<TProjection>>(sp =>
		{
			var options = sp.GetRequiredService<IOptions<CosmosDbProjectionStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<CosmosDbProjectionStore<TProjection>>>();

			return new CosmosDbProjectionStore<TProjection>(options, logger);
		});

		return services;
	}

	/// <summary>
	/// Adds the Cosmos DB projection store to the service collection with a connection string.
	/// </summary>
	/// <typeparam name="TProjection">The projection type to store.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The Cosmos DB connection string.</param>
	/// <param name="databaseName">The database name.</param>
	/// <param name="configureOptions">Optional action to further configure projection store options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddCosmosDbProjectionStore<TProjection>(
		this IServiceCollection services,
		string connectionString,
		string databaseName,
		Action<CosmosDbProjectionStoreOptions>? configureOptions = null)
		where TProjection : class
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

		return services.AddCosmosDbProjectionStore<TProjection>(options =>
		{
			options.Client.ConnectionString = connectionString;
			options.DatabaseName = databaseName;
			configureOptions?.Invoke(options);
		});
	}

	/// <summary>
	/// Adds the Cosmos DB projection store to the service collection with endpoint and key.
	/// </summary>
	/// <typeparam name="TProjection">The projection type to store.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="accountEndpoint">The Cosmos DB account endpoint.</param>
	/// <param name="accountKey">The Cosmos DB account key.</param>
	/// <param name="databaseName">The database name.</param>
	/// <param name="configureOptions">Optional action to further configure projection store options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddCosmosDbProjectionStore<TProjection>(
		this IServiceCollection services,
		string accountEndpoint,
		string accountKey,
		string databaseName,
		Action<CosmosDbProjectionStoreOptions>? configureOptions = null)
		where TProjection : class
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(accountEndpoint);
		ArgumentException.ThrowIfNullOrWhiteSpace(accountKey);
		ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

		return services.AddCosmosDbProjectionStore<TProjection>(options =>
		{
			options.Client.AccountEndpoint = accountEndpoint;
			options.Client.AccountKey = accountKey;
			options.DatabaseName = databaseName;
			configureOptions?.Invoke(options);
		});
	}

	/// <summary>
	/// Registers multiple Cosmos DB projection stores that share a common connection string
	/// and database name, reducing boilerplate when multiple projections target the same database.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The shared Cosmos DB connection string.</param>
	/// <param name="databaseName">The shared Cosmos DB database name.</param>
	/// <param name="configure">Action to register individual projection stores.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <example>
	/// <code>
	/// services.AddCosmosDbProjections("AccountEndpoint=...;AccountKey=...", "mydb", projections =>
	/// {
	///     projections.Add&lt;OrderSummary&gt;();
	///     projections.Add&lt;CustomerProfile&gt;(options => options.ContainerName = "customers");
	///     projections.Add&lt;ProductCatalog&gt;();
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddCosmosDbProjections(
		this IServiceCollection services,
		string connectionString,
		string databaseName,
		Action<CosmosDbProjectionRegistrar> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);
		ArgumentNullException.ThrowIfNull(configure);

		var registrar = new CosmosDbProjectionRegistrar(services, connectionString, databaseName);
		configure(registrar);

		return services;
	}

	/// <summary>
	/// Registers multiple Cosmos DB projection stores with shared options configuration,
	/// allowing advanced settings like custom timeouts, consistency levels, and endpoint overrides.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureShared">Shared options applied to all projections before per-projection overrides.</param>
	/// <param name="configure">Action to register individual projection stores.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <example>
	/// <code>
	/// services.AddCosmosDbProjections(
	///     shared => { shared.Client.ConnectionString = connStr; shared.DatabaseName = "mydb"; },
	///     projections =>
	///     {
	///         projections.Add&lt;OrderSummary&gt;();
	///         projections.Add&lt;CustomerProfile&gt;(options => options.ContainerName = "customers");
	///     });
	/// </code>
	/// </example>
	public static IServiceCollection AddCosmosDbProjections(
		this IServiceCollection services,
		Action<CosmosDbProjectionStoreOptions> configureShared,
		Action<CosmosDbProjectionRegistrar> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureShared);
		ArgumentNullException.ThrowIfNull(configure);

		var registrar = new CosmosDbProjectionRegistrar(services, configureShared);
		configure(registrar);

		return services;
	}
}
