// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Data.Abstractions.CloudNative;
using Excalibur.Data.CosmosDb;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;


namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Cosmos DB services.
/// </summary>
public static class CosmosDbServiceCollectionExtensions

{
	/// <summary>
	/// Adds Azure Cosmos DB data provider to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The configuration action.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddCosmosDb(
		this IServiceCollection services,
		Action<CosmosDbOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<CosmosDbOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		RegisterCoreServices(services);

		return services;
	}

	/// <summary>
	/// Adds Azure Cosmos DB data provider to the service collection using configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section.</param>
	/// <returns>The service collection for chaining.</returns>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	public static IServiceCollection AddCosmosDb(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<CosmosDbOptions>()
			.Bind(configuration)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		RegisterCoreServices(services);

		return services;
	}

	/// <summary>
	/// Adds Azure Cosmos DB data provider to the service collection using a named configuration section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration.</param>
	/// <param name="sectionName">The configuration section name.</param>
	/// <returns>The service collection for chaining.</returns>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	public static IServiceCollection AddCosmosDb(
		this IServiceCollection services,
		IConfiguration configuration,
		string sectionName)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);

		_ = services.AddOptions<CosmosDbOptions>()
			.Bind(configuration.GetSection(sectionName))
			.ValidateDataAnnotations()
			.ValidateOnStart();
		RegisterCoreServices(services);

		return services;
	}

	private static void RegisterCoreServices(IServiceCollection services)
	{
		services.TryAddSingleton<CosmosDbPersistenceProvider>();
		services.TryAddSingleton<ICloudNativePersistenceProvider>(sp =>
			sp.GetRequiredService<CosmosDbPersistenceProvider>());

		// Register health check
		services.TryAddSingleton<CosmosDbHealthCheck>();
	}
}
