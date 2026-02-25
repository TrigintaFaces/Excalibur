// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.MongoDB.Outbox;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MongoDB.Driver;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring MongoDB outbox store.
/// </summary>
public static class MongoDbOutboxExtensions
{
	/// <summary>
	/// Adds MongoDB outbox store to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddMongoDbOutboxStore(
		this IServiceCollection services,
		Action<MongoDbOutboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<MongoDbOutboxOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		services.TryAddSingleton<MongoDbOutboxStore>();
		services.TryAddSingleton<IOutboxStore>(sp => sp.GetRequiredService<MongoDbOutboxStore>());

		return services;
	}

	/// <summary>
	/// Adds MongoDB outbox store to the service collection with connection string.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The MongoDB connection string.</param>
	/// <param name="databaseName">The database name.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddMongoDbOutboxStore(
		this IServiceCollection services,
		string connectionString,
		string databaseName = "excalibur")
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

		return services.AddMongoDbOutboxStore(options =>
		{
			options.ConnectionString = connectionString;
			options.DatabaseName = databaseName;
		});
	}

	/// <summary>
	/// Adds MongoDB outbox store to the service collection with an existing client.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="clientProvider">A factory function that provides the MongoDB client.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddMongoDbOutboxStore(
		this IServiceCollection services,
		Func<IServiceProvider, IMongoClient> clientProvider,
		Action<MongoDbOutboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(clientProvider);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<MongoDbOutboxOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		services.TryAddSingleton(sp =>
		{
			var client = clientProvider(sp);
			var options = sp.GetRequiredService<IOptions<MongoDbOutboxOptions>>();
			var logger = sp.GetRequiredService<ILogger<MongoDbOutboxStore>>();
			return new MongoDbOutboxStore(client, options, logger);
		});
		services.TryAddSingleton<IOutboxStore>(sp => sp.GetRequiredService<MongoDbOutboxStore>());

		return services;
	}

	/// <summary>
	/// Configures the dispatch builder to use MongoDB outbox store.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseMongoDbOutboxStore(
		this IDispatchBuilder builder,
		Action<MongoDbOutboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddMongoDbOutboxStore(configure);

		return builder;
	}

	/// <summary>
	/// Configures the dispatch builder to use MongoDB outbox store with connection string.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="connectionString">The MongoDB connection string.</param>
	/// <param name="databaseName">The database name.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseMongoDbOutboxStore(
		this IDispatchBuilder builder,
		string connectionString,
		string databaseName = "excalibur")
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

		return builder.UseMongoDbOutboxStore(options =>
		{
			options.ConnectionString = connectionString;
			options.DatabaseName = databaseName;
		});
	}

	/// <summary>
	/// Configures the dispatch builder to use MongoDB outbox store with an existing client.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="clientProvider">A factory function that provides the MongoDB client.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseMongoDbOutboxStore(
		this IDispatchBuilder builder,
		Func<IServiceProvider, IMongoClient> clientProvider,
		Action<MongoDbOutboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(clientProvider);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddMongoDbOutboxStore(clientProvider, configure);

		return builder;
	}
}
