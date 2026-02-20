// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.CosmosDb.Snapshots;
using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Cosmos DB snapshot store services.
/// </summary>
public static class CosmosDbSnapshotStoreExtensions
{
	/// <summary>
	/// Adds the Cosmos DB snapshot store to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Action to configure snapshot store options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddCosmosDbSnapshotStore(
		this IServiceCollection services,
		Action<CosmosDbSnapshotStoreOptions> configureOptions)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureOptions);

		// Configure options
		_ = services.AddOptions<CosmosDbSnapshotStoreOptions>()
			.Configure(configureOptions)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// Register snapshot store
		services.TryAddScoped<ISnapshotStore>(sp =>
		{
			var options = sp.GetRequiredService<IOptions<CosmosDbSnapshotStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<CosmosDbSnapshotStore>>();

			return new CosmosDbSnapshotStore(options, logger);
		});

		return services;
	}

	/// <summary>
	/// Adds the Cosmos DB snapshot store to the service collection with a connection string.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The Cosmos DB connection string.</param>
	/// <param name="databaseName">The database name.</param>
	/// <param name="configureOptions">Optional action to further configure snapshot store options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddCosmosDbSnapshotStore(
		this IServiceCollection services,
		string connectionString,
		string databaseName,
		Action<CosmosDbSnapshotStoreOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

		return services.AddCosmosDbSnapshotStore(options =>
		{
			options.ConnectionString = connectionString;
			options.DatabaseName = databaseName;
			configureOptions?.Invoke(options);
		});
	}

	/// <summary>
	/// Adds the Cosmos DB snapshot store to the service collection with endpoint and key.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="accountEndpoint">The Cosmos DB account endpoint.</param>
	/// <param name="accountKey">The Cosmos DB account key.</param>
	/// <param name="databaseName">The database name.</param>
	/// <param name="configureOptions">Optional action to further configure snapshot store options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddCosmosDbSnapshotStore(
		this IServiceCollection services,
		string accountEndpoint,
		string accountKey,
		string databaseName,
		Action<CosmosDbSnapshotStoreOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(accountEndpoint);
		ArgumentException.ThrowIfNullOrWhiteSpace(accountKey);
		ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

		return services.AddCosmosDbSnapshotStore(options =>
		{
			options.AccountEndpoint = accountEndpoint;
			options.AccountKey = accountKey;
			options.DatabaseName = databaseName;
			configureOptions?.Invoke(options);
		});
	}
}
