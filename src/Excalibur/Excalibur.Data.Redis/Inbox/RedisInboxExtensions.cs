// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Redis.Inbox;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Redis inbox store.
/// </summary>
public static class RedisInboxExtensions
{
	/// <summary>
	/// Adds Redis inbox store to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddRedisInboxStore(
		this IServiceCollection services,
		Action<RedisInboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<RedisInboxOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		services.TryAddSingleton<RedisInboxStore>();
		services.TryAddSingleton<IInboxStore>(sp => sp.GetRequiredService<RedisInboxStore>());

		return services;
	}

	/// <summary>
	/// Adds Redis inbox store to the service collection with connection string.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The Redis connection string.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddRedisInboxStore(
		this IServiceCollection services,
		string connectionString)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		return services.AddRedisInboxStore(options =>
		{
			options.ConnectionString = connectionString;
		});
	}

	/// <summary>
	/// Adds Redis inbox store to the service collection with an existing connection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionProvider">A factory function that provides the Redis connection.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddRedisInboxStore(
		this IServiceCollection services,
		Func<IServiceProvider, ConnectionMultiplexer> connectionProvider,
		Action<RedisInboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(connectionProvider);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<RedisInboxOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		services.TryAddSingleton(sp =>
		{
			var connection = connectionProvider(sp);
			var options = sp.GetRequiredService<IOptions<RedisInboxOptions>>();
			var logger = sp.GetRequiredService<ILogger<RedisInboxStore>>();
			return new RedisInboxStore(connection, options, logger);
		});
		services.TryAddSingleton<IInboxStore>(sp => sp.GetRequiredService<RedisInboxStore>());

		return services;
	}

	/// <summary>
	/// Configures the dispatch builder to use Redis inbox store.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseRedisInboxStore(
		this IDispatchBuilder builder,
		Action<RedisInboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddRedisInboxStore(configure);

		return builder;
	}

	/// <summary>
	/// Configures the dispatch builder to use Redis inbox store with connection string.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="connectionString">The Redis connection string.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseRedisInboxStore(
		this IDispatchBuilder builder,
		string connectionString)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		return builder.UseRedisInboxStore(options =>
		{
			options.ConnectionString = connectionString;
		});
	}

	/// <summary>
	/// Configures the dispatch builder to use Redis inbox store with an existing connection.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="connectionProvider">A factory function that provides the Redis connection.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseRedisInboxStore(
		this IDispatchBuilder builder,
		Func<IServiceProvider, ConnectionMultiplexer> connectionProvider,
		Action<RedisInboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(connectionProvider);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddRedisInboxStore(connectionProvider, configure);

		return builder;
	}
}
