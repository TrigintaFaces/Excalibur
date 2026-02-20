// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.MongoDB.Cdc;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using MongoDB.Driver;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering MongoDB CDC services.
/// </summary>
public static class MongoDbCdcExtensions
{
	/// <summary>
	/// Adds MongoDB CDC processor services with the specified options.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The action to configure CDC options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddMongoDbCdc(
		this IServiceCollection services,
		Action<MongoDbCdcOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<MongoDbCdcOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		services.TryAddSingleton<IMongoDbCdcProcessor, MongoDbCdcProcessor>();

		return services;
	}

	/// <summary>
	/// Adds MongoDB CDC processor services with options from configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The MongoDB connection string.</param>
	/// <param name="processorId">The unique processor identifier.</param>
	/// <param name="configure">Optional additional configuration.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddMongoDbCdc(
		this IServiceCollection services,
		string connectionString,
		string processorId,
		Action<MongoDbCdcOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		ArgumentException.ThrowIfNullOrWhiteSpace(processorId);

		return services.AddMongoDbCdc(options =>
		{
			options.ConnectionString = connectionString;
			options.ProcessorId = processorId;
			configure?.Invoke(options);
		});
	}

	/// <summary>
	/// Adds the MongoDB-based CDC state store with the specified connection string and options.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The MongoDB connection string.</param>
	/// <param name="configureOptions">Optional action to configure CDC state store options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddMongoDbCdcStateStore(
		this IServiceCollection services,
		string connectionString,
		Action<MongoDbCdcStateStoreOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		// Register the IMongoClient singleton for the CDC state store
		services.TryAddSingleton<IMongoClient>(_ => new MongoClient(connectionString));

		RegisterCdcStateStoreOptions(services, configureOptions);

		services.TryAddSingleton<IMongoDbCdcStateStore>(sp => new MongoDbCdcStateStore(
			sp.GetRequiredService<IMongoClient>(),
			sp.GetRequiredService<IOptions<MongoDbCdcStateStoreOptions>>()));

		return services;
	}

	/// <summary>
	/// Adds the MongoDB-based CDC state store using an <see cref="IMongoClient"/> from DI.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Optional action to configure CDC state store options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddMongoDbCdcStateStore(
		this IServiceCollection services,
		Action<MongoDbCdcStateStoreOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		RegisterCdcStateStoreOptions(services, configureOptions);

		services.TryAddSingleton<IMongoDbCdcStateStore>(sp => new MongoDbCdcStateStore(
			sp.GetRequiredService<IMongoClient>(),
			sp.GetRequiredService<IOptions<MongoDbCdcStateStoreOptions>>()));

		return services;
	}

	/// <summary>
	/// Adds the in-memory CDC state store (for testing).
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddInMemoryMongoDbCdcStateStore(
		this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<IMongoDbCdcStateStore, InMemoryMongoDbCdcStateStore>();

		return services;
	}

	private static void RegisterCdcStateStoreOptions(
		IServiceCollection services,
		Action<MongoDbCdcStateStoreOptions>? configureOptions)
	{
		_ = services.AddOptions<MongoDbCdcStateStoreOptions>()
			.Configure(configureOptions ?? (_ => { }))
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<MongoDbCdcStateStoreOptions>, MongoDbCdcStateStoreOptionsValidator>());
	}
}
