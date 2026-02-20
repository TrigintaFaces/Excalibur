// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.CosmosDb.Saga;
using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Dispatch.Abstractions.Serialization;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Cosmos DB saga store services.
/// </summary>
public static class CosmosDbSagaExtensions
{
	/// <summary>
	/// Adds the Cosmos DB saga store to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Action to configure saga store options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// This method registers <see cref="CosmosDbSagaStore"/> as the implementation of <see cref="ISagaStore"/>.
	/// The store uses Cosmos DB document storage with JSON serialization for saga state.
	/// </para>
	/// <para>
	/// Example usage:
	/// <code>
	/// services.AddCosmosDbSagaStore(options =>
	/// {
	///     options.ConnectionString = "AccountEndpoint=...;AccountKey=...";
	///     options.DatabaseName = "myapp";
	///     options.ContainerName = "sagas";
	/// });
	/// </code>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddCosmosDbSagaStore(
		this IServiceCollection services,
		Action<CosmosDbSagaOptions> configureOptions)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureOptions);

		_ = services.AddOptions<CosmosDbSagaOptions>()
			.Configure(configureOptions)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.TryAddSingleton<CosmosDbSagaStore>();
		services.TryAddSingleton<ISagaStore>(sp => sp.GetRequiredService<CosmosDbSagaStore>());

		return services;
	}

	/// <summary>
	/// Adds the Cosmos DB saga store to the service collection with a connection string.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The Cosmos DB connection string.</param>
	/// <param name="databaseName">The database name. Defaults to "excalibur".</param>
	/// <param name="containerName">The container name. Defaults to "sagas".</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// Simplified registration for common scenarios where only the connection string and
	/// optional database/container names are needed.
	/// </para>
	/// <para>
	/// Example usage:
	/// <code>
	/// services.AddCosmosDbSagaStore("AccountEndpoint=...;AccountKey=...");
	/// </code>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddCosmosDbSagaStore(
		this IServiceCollection services,
		string connectionString,
		string databaseName = "excalibur",
		string containerName = "sagas")
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		return services.AddCosmosDbSagaStore(options =>
		{
			options.ConnectionString = connectionString;
			options.DatabaseName = databaseName;
			options.ContainerName = containerName;
		});
	}

	/// <summary>
	/// Adds the Cosmos DB saga store to the service collection with an existing client.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="clientFactory">Factory function that provides a Cosmos DB client.</param>
	/// <param name="configureOptions">Action to configure saga store options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// Use this overload for advanced scenarios like shared client instances,
	/// custom connection management, or integration with existing Cosmos DB infrastructure.
	/// </para>
	/// <para>
	/// Example usage:
	/// <code>
	/// services.AddCosmosDbSagaStore(
	///     sp => sp.GetRequiredService&lt;CosmosClient&gt;(),
	///     options => options.DatabaseName = "sagas");
	/// </code>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddCosmosDbSagaStore(
		this IServiceCollection services,
		Func<IServiceProvider, CosmosClient> clientFactory,
		Action<CosmosDbSagaOptions> configureOptions)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(clientFactory);
		ArgumentNullException.ThrowIfNull(configureOptions);

		_ = services.AddOptions<CosmosDbSagaOptions>()
			.Configure(configureOptions)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.TryAddSingleton(sp =>
		{
			var client = clientFactory(sp);
			var options = sp.GetRequiredService<IOptions<CosmosDbSagaOptions>>();
			var logger = sp.GetRequiredService<ILogger<CosmosDbSagaStore>>();
			var serializer = sp.GetRequiredService<IJsonSerializer>();
			return new CosmosDbSagaStore(client, options, logger, serializer);
		});
		services.TryAddSingleton<ISagaStore>(sp => sp.GetRequiredService<CosmosDbSagaStore>());

		return services;
	}
}
