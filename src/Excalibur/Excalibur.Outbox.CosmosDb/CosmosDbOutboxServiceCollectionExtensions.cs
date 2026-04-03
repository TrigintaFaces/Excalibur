// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.Abstractions.CloudNative;
using Excalibur.Outbox.CosmosDb;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Cosmos DB outbox store services.
/// </summary>
public static class CosmosDbOutboxServiceCollectionExtensions
{
	/// <summary>
	/// Adds the Cosmos DB outbox store to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The configuration action.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddCosmosDbOutboxStore(
		this IServiceCollection services,
		Action<CosmosDbOutboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<CosmosDbOutboxOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		RegisterServices(services);

		return services;
	}

	/// <summary>
	/// Adds the Cosmos DB outbox store to the service collection using configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddCosmosDbOutboxStore(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<CosmosDbOutboxOptions>()
			.Bind(configuration)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		RegisterServices(services);

		return services;
	}

	/// <summary>
	/// Adds the Cosmos DB outbox store to the service collection using a named configuration section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration.</param>
	/// <param name="sectionName">The configuration section name.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddCosmosDbOutboxStore(
		this IServiceCollection services,
		IConfiguration configuration,
		string sectionName)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);

		_ = services.AddOptions<CosmosDbOutboxOptions>()
			.Bind(configuration.GetSection(sectionName))
			.ValidateDataAnnotations()
			.ValidateOnStart();
		RegisterServices(services);

		return services;
	}

	private static void RegisterServices(IServiceCollection services)
	{
		services.TryAddSingleton<CosmosDbOutboxStore>();
		services.TryAddSingleton<ICloudNativeOutboxStore>(sp => sp.GetRequiredService<CosmosDbOutboxStore>());
	}
}
