// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Jobs.Coordination;

using Microsoft.Extensions.Logging;

using StackExchange.Redis;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods for configuring job coordination services in an <see cref="IServiceCollection" />.
/// </summary>
public static class CoordinationServiceCollectionExtensions
{
	/// <summary>
	/// Adds distributed job coordination services using Redis as the coordination backend.
	/// </summary>
	/// <param name="services"> The service collection to configure. </param>
	/// <param name="connectionString"> The Redis connection string. </param>
	/// <param name="keyPrefix"> Optional prefix for Redis keys to avoid Tests.CloudProviders. </param>
	/// <returns> The configured <see cref="IServiceCollection" />. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="services" /> is null. </exception>
	/// <exception cref="ArgumentException"> Thrown if <paramref name="connectionString" /> is null or whitespace. </exception>
	public static IServiceCollection AddJobCoordinationRedis(
		this IServiceCollection services,
		string connectionString,
		string keyPrefix = "excalibur:jobs:")
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		// Register Redis connection
		_ = services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(connectionString));
		_ = services.AddSingleton(provider =>
		{
			var connection = provider.GetRequiredService<IConnectionMultiplexer>();
			return connection.GetDatabase();
		});

		// Register job coordinator and sub-interfaces
		_ = services.AddSingleton(provider =>
		{
			var database = provider.GetRequiredService<IDatabase>();
			var logger = provider.GetRequiredService<ILogger<RedisJobCoordinator>>();
			return new RedisJobCoordinator(database, logger, keyPrefix);
		});
		_ = services.AddSingleton<IJobCoordinator>(provider => provider.GetRequiredService<RedisJobCoordinator>());
		_ = services.AddSingleton<IJobLockProvider>(provider => provider.GetRequiredService<RedisJobCoordinator>());
		_ = services.AddSingleton<IJobRegistry>(provider => provider.GetRequiredService<RedisJobCoordinator>());
		_ = services.AddSingleton<IJobDistributor>(provider => provider.GetRequiredService<RedisJobCoordinator>());

		return services;
	}

	/// <summary>
	/// Adds distributed job coordination services using Redis with an existing connection multiplexer.
	/// </summary>
	/// <param name="services"> The service collection to configure. </param>
	/// <param name="connectionMultiplexer"> The existing Redis connection multiplexer. </param>
	/// <param name="keyPrefix"> Optional prefix for Redis keys to avoid Tests.CloudProviders. </param>
	/// <returns> The configured <see cref="IServiceCollection" />. </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown if <paramref name="services" /> or <paramref name="connectionMultiplexer" /> is null.
	/// </exception>
	public static IServiceCollection AddJobCoordinationRedis(
		this IServiceCollection services,
		IConnectionMultiplexer connectionMultiplexer,
		string keyPrefix = "excalibur:jobs:")
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(connectionMultiplexer);

		// Register the provided Redis connection
		_ = services.AddSingleton(connectionMultiplexer);
		_ = services.AddSingleton(provider =>
		{
			var connection = provider.GetRequiredService<IConnectionMultiplexer>();
			return connection.GetDatabase();
		});

		// Register job coordinator and sub-interfaces
		_ = services.AddSingleton(provider =>
		{
			var database = provider.GetRequiredService<IDatabase>();
			var logger = provider.GetRequiredService<ILogger<RedisJobCoordinator>>();
			return new RedisJobCoordinator(database, logger, keyPrefix);
		});
		_ = services.AddSingleton<IJobCoordinator>(provider => provider.GetRequiredService<RedisJobCoordinator>());
		_ = services.AddSingleton<IJobLockProvider>(provider => provider.GetRequiredService<RedisJobCoordinator>());
		_ = services.AddSingleton<IJobRegistry>(provider => provider.GetRequiredService<RedisJobCoordinator>());
		_ = services.AddSingleton<IJobDistributor>(provider => provider.GetRequiredService<RedisJobCoordinator>());

		return services;
	}

	/// <summary>
	/// Adds distributed job coordination services with a custom coordinator implementation.
	/// </summary>
	/// <typeparam name="TJobCoordinator"> The type of the custom job coordinator implementation. </typeparam>
	/// <param name="services"> The service collection to configure. </param>
	/// <returns> The configured <see cref="IServiceCollection" />. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="services" /> is null. </exception>
	public static IServiceCollection AddJobCoordination<TJobCoordinator>(this IServiceCollection services)
		where TJobCoordinator : class, IJobCoordinator
	{
		ArgumentNullException.ThrowIfNull(services);

		// Register custom job coordinator and sub-interfaces
		_ = services.AddSingleton<TJobCoordinator>();
		_ = services.AddSingleton<IJobCoordinator>(provider => provider.GetRequiredService<TJobCoordinator>());
		_ = services.AddSingleton<IJobLockProvider>(provider => provider.GetRequiredService<TJobCoordinator>());
		_ = services.AddSingleton<IJobRegistry>(provider => provider.GetRequiredService<TJobCoordinator>());
		_ = services.AddSingleton<IJobDistributor>(provider => provider.GetRequiredService<TJobCoordinator>());

		return services;
	}
}
