// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.Redis;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Redis outbox store.
/// </summary>
public static class RedisOutboxExtensions
{
	/// <summary>
	/// Adds Redis outbox store to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddRedisOutboxStore(
		this IServiceCollection services,
		Action<RedisOutboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<RedisOutboxOptions>()
			.Configure(configure)
			.ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<RedisOutboxOptions>, RedisOutboxOptionsValidator>());
		services.TryAddSingleton<RedisOutboxStore>();
		services.AddKeyedSingleton<IOutboxStore>("redis", (sp, _) => sp.GetRequiredService<RedisOutboxStore>());
		services.TryAddKeyedSingleton<IOutboxStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IOutboxStore>("redis"));

		return services;
	}

	/// <summary>
	/// Adds Redis outbox store to the service collection with an existing connection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionProvider">A factory function that provides the Redis connection.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddRedisOutboxStore(
		this IServiceCollection services,
		Func<IServiceProvider, ConnectionMultiplexer> connectionProvider,
		Action<RedisOutboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(connectionProvider);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<RedisOutboxOptions>()
			.Configure(configure)
			.ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<RedisOutboxOptions>, RedisOutboxOptionsValidator>());
		services.TryAddSingleton(sp =>
		{
			var connection = connectionProvider(sp);
			var options = sp.GetRequiredService<IOptions<RedisOutboxOptions>>();
			var logger = sp.GetRequiredService<ILogger<RedisOutboxStore>>();
			return new RedisOutboxStore(connection, options, logger);
		});
		services.AddKeyedSingleton<IOutboxStore>("redis", (sp, _) => sp.GetRequiredService<RedisOutboxStore>());
		services.TryAddKeyedSingleton<IOutboxStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IOutboxStore>("redis"));

		return services;
	}

	/// <summary>
	/// Configures the dispatch builder to use Redis outbox store.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseRedisOutboxStore(
		this IDispatchBuilder builder,
		Action<RedisOutboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddRedisOutboxStore(configure);

		return builder;
	}

	/// <summary>
	/// Configures the dispatch builder to use Redis outbox store with an existing connection.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="connectionProvider">A factory function that provides the Redis connection.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseRedisOutboxStore(
		this IDispatchBuilder builder,
		Func<IServiceProvider, ConnectionMultiplexer> connectionProvider,
		Action<RedisOutboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(connectionProvider);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddRedisOutboxStore(connectionProvider, configure);

		return builder;
	}
}
