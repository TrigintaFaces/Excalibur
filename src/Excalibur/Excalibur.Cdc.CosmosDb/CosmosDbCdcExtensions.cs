// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Cdc.CosmosDb;

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
	public static IServiceCollection AddCosmosDbCdc(
		this IServiceCollection services,
		Action<CosmosDbCdcOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<CosmosDbCdcOptions>()
			.Configure(configure)
			.ValidateOnStart();
		services.TryAddSingleton<ICosmosDbCdcProcessor, CosmosDbCdcProcessor>();

		return services;
	}

	/// <summary>
	/// Adds CosmosDb CDC processor services to the service collection using configuration.
	/// </summary>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddCosmosDbCdc(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<CosmosDbCdcOptions>()
			.Bind(configuration)
			.ValidateOnStart();
		services.TryAddSingleton<ICosmosDbCdcProcessor, CosmosDbCdcProcessor>();

		return services;
	}

	/// <summary>
	/// Adds CosmosDb CDC processor services to the service collection using a named configuration section.
	/// </summary>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
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
			.ValidateOnStart();
		services.TryAddSingleton<ICosmosDbCdcProcessor, CosmosDbCdcProcessor>();

		return services;
	}

	/// <summary>
	/// Adds the CosmosDb-based CDC state store.
	/// </summary>
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
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddCosmosDbCdcStateStore(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<CosmosDbCdcStateStoreOptions>()
			.Bind(configuration)
			.ValidateOnStart();
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<CosmosDbCdcStateStoreOptions>, CosmosDbCdcStateStoreOptionsValidator>());
		services.TryAddSingleton<ICosmosDbCdcStateStore, CosmosDbCdcStateStore>();

		return services;
	}

	/// <summary>
	/// Adds the in-memory CDC state store for testing scenarios.
	/// </summary>
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
	public static IServiceCollection AddCosmosDbAllVersionsChangeFeed(
		this IServiceCollection services,
		Action<CosmosDbAllVersionsChangeFeedOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<CosmosDbAllVersionsChangeFeedOptions>()
			.Configure(configure)
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
			.ValidateOnStart();

		services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<CosmosDbCdcStateStoreOptions>, CosmosDbCdcStateStoreOptionsValidator>());
	}
}
