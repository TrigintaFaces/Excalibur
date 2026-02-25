// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.CosmosDb.Inbox;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Cosmos DB inbox store.
/// </summary>
public static class CosmosDbInboxExtensions
{
	/// <summary>
	/// Adds Cosmos DB inbox store to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddCosmosDbInboxStore(
		this IServiceCollection services,
		Action<CosmosDbInboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<CosmosDbInboxOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		services.TryAddSingleton<CosmosDbInboxStore>();
		services.TryAddSingleton<IInboxStore>(sp => sp.GetRequiredService<CosmosDbInboxStore>());

		return services;
	}

	/// <summary>
	/// Adds Cosmos DB inbox store to the service collection with connection string.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The Cosmos DB connection string.</param>
	/// <param name="databaseName">The database name.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddCosmosDbInboxStore(
		this IServiceCollection services,
		string connectionString,
		string databaseName)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

		return services.AddCosmosDbInboxStore(options =>
		{
			options.ConnectionString = connectionString;
			options.DatabaseName = databaseName;
		});
	}

	/// <summary>
	/// Configures the dispatch builder to use Cosmos DB inbox store.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseCosmosDbInboxStore(
		this IDispatchBuilder builder,
		Action<CosmosDbInboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddCosmosDbInboxStore(configure);

		return builder;
	}

	/// <summary>
	/// Configures the dispatch builder to use Cosmos DB inbox store with connection string.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="connectionString">The Cosmos DB connection string.</param>
	/// <param name="databaseName">The database name.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseCosmosDbInboxStore(
		this IDispatchBuilder builder,
		string connectionString,
		string databaseName)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

		return builder.UseCosmosDbInboxStore(options =>
		{
			options.ConnectionString = connectionString;
			options.DatabaseName = databaseName;
		});
	}
}
