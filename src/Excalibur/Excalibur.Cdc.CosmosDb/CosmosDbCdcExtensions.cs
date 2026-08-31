// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Cdc;
using Excalibur.Cdc.CosmosDb;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
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
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<CosmosDbCdcOptions>, CosmosDbCdcOptionsValidator>());
		services.TryAddSingleton<ICosmosDbCdcProcessor, CosmosDbCdcProcessor>();

		// Forward to base interfaces so consumers can depend on the abstraction level they need
		services.TryAddSingleton<ICdcStreamProcessor<CosmosDbDataChangeEvent, CosmosDbCdcPosition>>(
			sp => sp.GetRequiredService<ICosmosDbCdcProcessor>());
		services.TryAddSingleton<ICdcProcessor<CosmosDbDataChangeEvent>>(
			sp => sp.GetRequiredService<ICosmosDbCdcProcessor>());

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
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<CosmosDbCdcOptions>, CosmosDbCdcOptionsValidator>());
		services.TryAddSingleton<ICosmosDbCdcProcessor, CosmosDbCdcProcessor>();

		// Forward to base interfaces so consumers can depend on the abstraction level they need
		services.TryAddSingleton<ICdcStreamProcessor<CosmosDbDataChangeEvent, CosmosDbCdcPosition>>(
			sp => sp.GetRequiredService<ICosmosDbCdcProcessor>());
		services.TryAddSingleton<ICdcProcessor<CosmosDbDataChangeEvent>>(
			sp => sp.GetRequiredService<ICosmosDbCdcProcessor>());

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
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<CosmosDbCdcOptions>, CosmosDbCdcOptionsValidator>());
		services.TryAddSingleton<ICosmosDbCdcProcessor, CosmosDbCdcProcessor>();

		// Forward to base interfaces so consumers can depend on the abstraction level they need
		services.TryAddSingleton<ICdcStreamProcessor<CosmosDbDataChangeEvent, CosmosDbCdcPosition>>(
			sp => sp.GetRequiredService<ICosmosDbCdcProcessor>());
		services.TryAddSingleton<ICdcProcessor<CosmosDbDataChangeEvent>>(
			sp => sp.GetRequiredService<ICosmosDbCdcProcessor>());

		return services;
	}

	/// <summary>
	/// Adds the CosmosDb-based CDC state store.
	/// </summary>
	/// <remarks>
	/// If a <see cref="CosmosClient"/> is registered in the service collection, the store uses it — which is
	/// what makes token-credential authentication, a custom <c>HttpClientFactory</c>, Gateway mode, and a
	/// chosen serializer reachable. Otherwise the store builds a client from
	/// <see cref="CosmosDbCdcStateStoreOptions.ConnectionString"/>, so a host that configures nothing but a
	/// connection string is unaffected.
	/// </remarks>
	public static IServiceCollection AddCosmosDbCdcStateStore(
		this IServiceCollection services,
		Action<CosmosDbCdcStateStoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		RegisterCdcStateStoreOptions(services, configure);
		RegisterCdcStateStore(services);

		return services;
	}

	/// <summary>
	/// Adds the CosmosDb-based CDC state store using configuration.
	/// </summary>
	/// <remarks>
	/// If a <see cref="CosmosClient"/> is registered in the service collection, the store uses it; otherwise
	/// it builds one from <see cref="CosmosDbCdcStateStoreOptions.ConnectionString"/>.
	/// </remarks>
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
		RegisterCdcStateStoreOptionsValidator(services);
		RegisterCdcStateStore(services);

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

		services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<CosmosDbAllVersionsChangeFeedOptions>, CosmosDbAllVersionsChangeFeedOptionsValidator>());

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

		RegisterCdcStateStoreOptionsValidator(services);
	}

	/// <summary>
	/// Registers the state store, preferring a <see cref="CosmosClient"/> the host registered over one built
	/// from the connection string.
	/// </summary>
	/// <remarks>
	/// Resolved explicitly rather than left to constructor selection: which client a store talks to is the
	/// kind of thing that should be readable at the registration site, not inferred from which constructor
	/// happened to be satisfiable.
	/// </remarks>
	private static void RegisterCdcStateStore(IServiceCollection services) =>
		services.TryAddSingleton<ICosmosDbCdcStateStore>(static sp =>
		{
			var options = sp.GetRequiredService<IOptions<CosmosDbCdcStateStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<CosmosDbCdcStateStore>>();
			var client = sp.GetService<CosmosClient>();

			return client is not null
				? new CosmosDbCdcStateStore(client, options, logger)
				: new CosmosDbCdcStateStore(options, logger);
		});

	/// <summary>
	/// Registers the options validator, telling it whether a client was supplied so it does not demand a
	/// connection string the store will never read.
	/// </summary>
	private static void RegisterCdcStateStoreOptionsValidator(IServiceCollection services) =>
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<CosmosDbCdcStateStoreOptions>, CosmosDbCdcStateStoreOptionsValidator>(
				static sp => new CosmosDbCdcStateStoreOptionsValidator(sp.GetService<CosmosClient>())));
}
