// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.MongoDB.Inbox;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MongoDB.Driver;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring MongoDB inbox store.
/// </summary>
public static class MongoDbInboxExtensions
{
	/// <summary>
	/// Adds MongoDB inbox store to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddMongoDbInboxStore(
		this IServiceCollection services,
		Action<MongoDbInboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<MongoDbInboxOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		services.TryAddSingleton<MongoDbInboxStore>();
		services.TryAddSingleton<IInboxStore>(sp => sp.GetRequiredService<MongoDbInboxStore>());

		return services;
	}

	/// <summary>
	/// Adds MongoDB inbox store to the service collection with connection string and database.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The MongoDB connection string.</param>
	/// <param name="databaseName">The database name.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddMongoDbInboxStore(
		this IServiceCollection services,
		string connectionString,
		string databaseName)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

		return services.AddMongoDbInboxStore(options =>
		{
			options.ConnectionString = connectionString;
			options.DatabaseName = databaseName;
		});
	}

	/// <summary>
	/// Adds MongoDB inbox store to the service collection with an existing client.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="clientProvider">A factory function that provides the MongoDB client.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddMongoDbInboxStore(
		this IServiceCollection services,
		Func<IServiceProvider, IMongoClient> clientProvider,
		Action<MongoDbInboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(clientProvider);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<MongoDbInboxOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		services.TryAddSingleton(sp =>
		{
			var client = clientProvider(sp);
			var options = sp.GetRequiredService<IOptions<MongoDbInboxOptions>>();
			var logger = sp.GetRequiredService<ILogger<MongoDbInboxStore>>();
			return new MongoDbInboxStore(client, options, logger);
		});
		services.TryAddSingleton<IInboxStore>(sp => sp.GetRequiredService<MongoDbInboxStore>());

		return services;
	}

	/// <summary>
	/// Configures the dispatch builder to use MongoDB inbox store.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseMongoDbInboxStore(
		this IDispatchBuilder builder,
		Action<MongoDbInboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddMongoDbInboxStore(configure);

		return builder;
	}

	/// <summary>
	/// Configures the dispatch builder to use MongoDB inbox store with connection string and database.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="connectionString">The MongoDB connection string.</param>
	/// <param name="databaseName">The database name.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseMongoDbInboxStore(
		this IDispatchBuilder builder,
		string connectionString,
		string databaseName)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

		return builder.UseMongoDbInboxStore(options =>
		{
			options.ConnectionString = connectionString;
			options.DatabaseName = databaseName;
		});
	}

	/// <summary>
	/// Configures the dispatch builder to use MongoDB inbox store with an existing client.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="clientProvider">A factory function that provides the MongoDB client.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseMongoDbInboxStore(
		this IDispatchBuilder builder,
		Func<IServiceProvider, IMongoClient> clientProvider,
		Action<MongoDbInboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(clientProvider);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddMongoDbInboxStore(clientProvider, configure);

		return builder;
	}
}
