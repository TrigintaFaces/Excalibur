// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.CosmosDb.Projections;
using Excalibur.EventSourcing;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

#pragma warning disable IL2091 // DI registration methods create stores with DynamicallyAccessedMembers-annotated TProjection; consumer types are preserved by the store contract

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Cosmos DB projection store services.
/// </summary>
/// <remarks>
/// <para>
/// Each projection type gets its own named options instance, keyed by
/// <see cref="CosmosDbProjectionContainerConvention.GetOptionsName{TProjection}"/>. Several projection
/// stores therefore coexist with independent configurations — their own database, container, partition
/// key path, throughput and time-to-live.
/// </para>
/// </remarks>
public static class CosmosDbProjectionStoreExtensions
{
	/// <summary>
	/// Adds the Cosmos DB projection store to the service collection.
	/// </summary>
	/// <typeparam name="TProjection">The projection type to store.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Action to configure projection store options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// The action configures the named options instance belonging to <typeparamref name="TProjection"/>, so
	/// it cannot alter the settings of a projection registered before or after it.
	/// </remarks>
	public static IServiceCollection AddCosmosDbProjectionStore<TProjection>(
		this IServiceCollection services,
		Action<CosmosDbProjectionStoreOptions> configureOptions)
		where TProjection : class
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureOptions);

		// Named options, keyed by the projection type, through the one function that produces that key.
		// The unnamed instance is deliberately left alone: configuring it would make the LAST registration
		// in a container decide the database and container for EVERY projection in it.
		var optionsName = CosmosDbProjectionContainerConvention.GetOptionsName<TProjection>();
		_ = services.Configure(optionsName, configureOptions);
		WireOptionsValidation(services, optionsName);

		// Register projection store -- uses IOptionsMonitor to resolve named options
		services.TryAddScoped<IProjectionStore<TProjection>>(sp =>
		{
			var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<CosmosDbProjectionStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<CosmosDbProjectionStore<TProjection>>>();

			return new CosmosDbProjectionStore<TProjection>(optionsMonitor, logger);
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
	/// <remarks>
	/// The database and connection are described once; each projection keeps its own named options, so a
	/// per-projection container override applies to that projection alone. In the example below
	/// <c>CustomerProfile</c> is the only read model in <c>"customers"</c>; the other two stay in the
	/// default <c>"projections"</c> container.
	/// </remarks>
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

	/// <summary>
	/// Wires eager (fail-fast at startup) validation for a named projection-store options instance.
	/// Registers the validator once (idempotent) and marks the named instance for <c>ValidateOnStart</c>,
	/// so a misconfiguration surfaces at host start rather than only at the store's first use.
	/// </summary>
	/// <remarks>
	/// The name is load-bearing: validation registered against the unnamed instance does not run for a
	/// named one, so a per-projection registration that skipped this would never be validated at all.
	/// </remarks>
	private static void WireOptionsValidation(IServiceCollection services, string optionsName)
	{
		_ = services.AddOptions<CosmosDbProjectionStoreOptions>(optionsName).ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<CosmosDbProjectionStoreOptions>,
				CosmosDbProjectionStoreOptionsValidator>());
	}
}
