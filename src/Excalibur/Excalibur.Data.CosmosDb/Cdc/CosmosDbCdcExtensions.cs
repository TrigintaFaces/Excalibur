// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Data.CosmosDb.Cdc;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;


namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering CosmosDb CDC services.
/// </summary>
public static class CosmosDbCdcServiceCollectionExtensions

{
	/// <summary>
	/// Adds CosmosDb CDC processor services to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The configuration action for CDC options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddCosmosDbCdc(
		this IServiceCollection services,
		Action<CosmosDbCdcOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<CosmosDbCdcOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		services.TryAddSingleton<ICosmosDbCdcProcessor, CosmosDbCdcProcessor>();

		return services;
	}

	/// <summary>
	/// Adds CosmosDb CDC processor services to the service collection using configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section.</param>
	/// <returns>The service collection for chaining.</returns>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	public static IServiceCollection AddCosmosDbCdc(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<CosmosDbCdcOptions>()
			.Bind(configuration)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		services.TryAddSingleton<ICosmosDbCdcProcessor, CosmosDbCdcProcessor>();

		return services;
	}

	/// <summary>
	/// Adds CosmosDb CDC processor services to the service collection using a named configuration section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration.</param>
	/// <param name="sectionName">The configuration section name.</param>
	/// <returns>The service collection for chaining.</returns>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	public static IServiceCollection AddCosmosDbCdc(
		this IServiceCollection services,
		IConfiguration configuration,
		string sectionName)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);

		_ = services.AddOptions<CosmosDbCdcOptions>()
			.Bind(configuration.GetSection(sectionName))
			.ValidateDataAnnotations()
			.ValidateOnStart();
		services.TryAddSingleton<ICosmosDbCdcProcessor, CosmosDbCdcProcessor>();

		return services;
	}

	/// <summary>
	/// Adds the CosmosDb-based CDC state store.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The configuration action for state store options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddCosmosDbCdcStateStore(
		this IServiceCollection services,
		Action<CosmosDbCdcStateStoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		RegisterCdcStateStoreOptions(services, configure);
		services.TryAddSingleton<ICosmosDbCdcStateStore, CosmosDbCdcStateStore>();

		return services;
	}

	/// <summary>
	/// Adds the CosmosDb-based CDC state store using configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section.</param>
	/// <returns>The service collection for chaining.</returns>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	public static IServiceCollection AddCosmosDbCdcStateStore(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<CosmosDbCdcStateStoreOptions>()
			.Bind(configuration)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<CosmosDbCdcStateStoreOptions>, CosmosDbCdcStateStoreOptionsValidator>());
		services.TryAddSingleton<ICosmosDbCdcStateStore, CosmosDbCdcStateStore>();

		return services;
	}

	/// <summary>
	/// Adds the in-memory CDC state store for testing scenarios.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// This state store is not persistent and should only be used for testing and development.
	/// </remarks>
	public static IServiceCollection AddInMemoryCosmosDbCdcStateStore(
		this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<ICosmosDbCdcStateStore, InMemoryCosmosDbCdcStateStore>();

		return services;
	}

	/// <summary>
	/// Adds CosmosDb AllVersionsAndDeletes change feed processor to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The configuration action for AllVersionsAndDeletes change feed options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// This processor captures all changes including deletes, with before/after state
	/// for update operations. The container must be configured with a full fidelity
	/// retention window.
	/// </para>
	/// <para>
	/// This also requires the base CDC options to be configured via <see cref="AddCosmosDbCdc(IServiceCollection, Action{CosmosDbCdcOptions})"/>
	/// for database and container identification.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddCosmosDbAllVersionsChangeFeed(
		this IServiceCollection services,
		Action<CosmosDbAllVersionsChangeFeedOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<CosmosDbAllVersionsChangeFeedOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.TryAddSingleton<CosmosDbAllVersionsChangeFeedProcessor>();

		return services;
	}

	private static void RegisterCdcStateStoreOptions(
		IServiceCollection services,
		Action<CosmosDbCdcStateStoreOptions>? configure)
	{
		var optionsBuilder = services.AddOptions<CosmosDbCdcStateStoreOptions>();
		if (configure is not null)
		{
			_ = optionsBuilder.Configure(configure);
		}

		_ = optionsBuilder
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<CosmosDbCdcStateStoreOptions>, CosmosDbCdcStateStoreOptionsValidator>());
	}
}
