// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Redis;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Redis event sourcing services.
/// </summary>
public static class RedisEventSourcingExtensions
{
	/// <summary>
	/// Adds Redis event store to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the event store options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddRedisEventStore(
		this IServiceCollection services,
		Action<RedisEventStoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<RedisEventStoreOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.TryAddSingleton(static sp =>
		{
			var options = sp.GetRequiredService<IOptions<RedisEventStoreOptions>>().Value;
			return ConnectionMultiplexer.Connect(options.ConnectionString);
		});

		services.TryAddSingleton<RedisEventStore>();
		services.TryAddSingleton<IEventStore>(static sp => sp.GetRequiredService<RedisEventStore>());

		return services;
	}

	/// <summary>
	/// Adds Redis event store to the service collection with a connection string.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The Redis connection string.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddRedisEventStore(
		this IServiceCollection services,
		string connectionString)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		return services.AddRedisEventStore(options => options.ConnectionString = connectionString);
	}

	/// <summary>
	/// Adds Redis event store to the service collection with an existing connection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connection">The Redis connection multiplexer.</param>
	/// <param name="configure">Action to configure the event store options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddRedisEventStore(
		this IServiceCollection services,
		ConnectionMultiplexer connection,
		Action<RedisEventStoreOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(connection);

		if (configure != null)
		{
			_ = services.AddOptions<RedisEventStoreOptions>()
				.Configure(configure)
				.ValidateDataAnnotations()
				.ValidateOnStart();
		}
		else
		{
			_ = services.AddOptions<RedisEventStoreOptions>()
				.ValidateDataAnnotations()
				.ValidateOnStart();
		}

		services.TryAddSingleton(connection);
		services.TryAddSingleton<RedisEventStore>();
		services.TryAddSingleton<IEventStore>(static sp => sp.GetRequiredService<RedisEventStore>());

		return services;
	}

	/// <summary>
	/// Adds Redis snapshot store to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the snapshot store options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddRedisSnapshotStore(
		this IServiceCollection services,
		Action<RedisSnapshotStoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<RedisSnapshotStoreOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.TryAddSingleton(static sp =>
		{
			var options = sp.GetRequiredService<IOptions<RedisSnapshotStoreOptions>>().Value;
			return ConnectionMultiplexer.Connect(options.ConnectionString);
		});

		services.TryAddSingleton<RedisSnapshotStore>();
		services.TryAddSingleton<ISnapshotStore>(static sp => sp.GetRequiredService<RedisSnapshotStore>());

		return services;
	}

	/// <summary>
	/// Adds Redis snapshot store to the service collection with a connection string.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The Redis connection string.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddRedisSnapshotStore(
		this IServiceCollection services,
		string connectionString)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		return services.AddRedisSnapshotStore(options => options.ConnectionString = connectionString);
	}

	/// <summary>
	/// Adds both Redis event store and snapshot store to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The Redis connection string.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddRedisEventSourcing(
		this IServiceCollection services,
		string connectionString)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		_ = services.AddRedisEventStore(connectionString);
		_ = services.AddRedisSnapshotStore(connectionString);

		return services;
	}

	/// <summary>
	/// Adds both Redis event store and snapshot store to the service collection with separate configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureEventStore">Action to configure the event store options.</param>
	/// <param name="configureSnapshotStore">Action to configure the snapshot store options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddRedisEventSourcing(
		this IServiceCollection services,
		Action<RedisEventStoreOptions> configureEventStore,
		Action<RedisSnapshotStoreOptions> configureSnapshotStore)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureEventStore);
		ArgumentNullException.ThrowIfNull(configureSnapshotStore);

		_ = services.AddRedisEventStore(configureEventStore);
		_ = services.AddRedisSnapshotStore(configureSnapshotStore);

		return services;
	}
}
