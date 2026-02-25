// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0



using Excalibur.Dispatch.Transport.Kafka;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering schema registry services.
/// </summary>
public static class SchemaRegistryServiceCollectionExtensions
{
	/// <summary>
	/// Adds Confluent Schema Registry services with the specified configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The configuration action for schema registry options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// This method registers:
	/// </para>
	/// <list type="bullet">
	///   <item><description><see cref="ConfluentSchemaRegistryClient"/> as the underlying client</description></item>
	///   <item><description><see cref="CachingSchemaRegistryClient"/> as a caching decorator</description></item>
	///   <item><description><see cref="ISchemaRegistryClient"/> resolved to the caching client</description></item>
	/// </list>
	/// <para>
	/// The client is registered as a singleton since schema IDs are immutable
	/// once assigned and caching is beneficial.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddConfluentSchemaRegistry(
		this IServiceCollection services,
		Action<ConfluentSchemaRegistryOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new ConfluentSchemaRegistryOptions();
		configure(options);

		services.TryAddSingleton(options);
		services.TryAddSingleton<IMemoryCache, MemoryCache>();

		// Register the underlying Confluent client
		services.TryAddSingleton<ConfluentSchemaRegistryClient>();

		// Register the caching decorator as the primary implementation
		services.TryAddSingleton<ISchemaRegistryClient>(sp =>
		{
			var inner = sp.GetRequiredService<ConfluentSchemaRegistryClient>();
			var cache = sp.GetRequiredService<IMemoryCache>();
			var cachingOptions = sp.GetService<CachingSchemaRegistryOptions>() ?? new CachingSchemaRegistryOptions();
			var logger = sp.GetRequiredService<ILogger<CachingSchemaRegistryClient>>();

			return new CachingSchemaRegistryClient(inner, cache, cachingOptions, logger);
		});

		return services;
	}

	/// <summary>
	/// Adds Confluent Schema Registry services with caching configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The configuration action for schema registry options.</param>
	/// <param name="configureCaching">The configuration action for caching options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddConfluentSchemaRegistry(
		this IServiceCollection services,
		Action<ConfluentSchemaRegistryOptions> configure,
		Action<CachingSchemaRegistryOptions> configureCaching)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);
		ArgumentNullException.ThrowIfNull(configureCaching);

		var cachingOptions = new CachingSchemaRegistryOptions();
		configureCaching(cachingOptions);

		services.TryAddSingleton(cachingOptions);

		return services.AddConfluentSchemaRegistry(configure);
	}

	/// <summary>
	/// Adds Confluent Schema Registry services without the caching decorator.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The configuration action for schema registry options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// Use this method when you want direct access to the schema registry without
	/// the additional caching layer. Note that the Confluent SDK already provides
	/// its own internal caching.
	/// </remarks>
	public static IServiceCollection AddConfluentSchemaRegistryWithoutCaching(
		this IServiceCollection services,
		Action<ConfluentSchemaRegistryOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new ConfluentSchemaRegistryOptions();
		configure(options);

		services.TryAddSingleton(options);
		services.TryAddSingleton<ConfluentSchemaRegistryClient>();
		services.TryAddSingleton<ISchemaRegistryClient>(sp => sp.GetRequiredService<ConfluentSchemaRegistryClient>());

		return services;
	}
}
