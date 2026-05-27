// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Data.Persistence;
using Excalibur.Data.Redis;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Redis data services.
/// </summary>
public static class RedisProviderServiceCollectionExtensions
{
	/// <summary>
	/// Adds Redis data provider to the service collection using the fluent builder.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Configuration action for the Redis data builder.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcaliburRedis(redis =&gt;
	/// {
	///     redis.ConnectionString("localhost:6379")
	///          .KeyPrefix("myapp")
	///          .Database(0);
	/// });
	/// </code>
	/// </example>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	public static IServiceCollection AddExcaliburRedis(
		this IServiceCollection services,
		Action<IRedisDataBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new RedisProviderOptions();
		var redisBuilder = new RedisDataBuilder(options);
		configure(redisBuilder);

		var hasBuilderConnection = redisBuilder.MultiplexerInstance is not null
			|| redisBuilder.MultiplexerFactoryFunc is not null;

		RegisterOptionsAndServices(services, redisBuilder, options, hasBuilderConnection);

		return services;
	}

	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	private static void RegisterOptionsAndServices(
		IServiceCollection services,
		RedisDataBuilder redisBuilder,
		RedisProviderOptions options,
		bool hasBuilderConnection)
	{
		// Register store-specific options from builder state
		_ = services.Configure<RedisProviderOptions>(opt =>
		{
			opt.ConnectionString = options.ConnectionString;
			if (redisBuilder.KeyPrefixValue is not null)
			{
				opt.Name = redisBuilder.KeyPrefixValue;
			}
			if (redisBuilder.DatabaseValue.HasValue)
			{
				opt.DatabaseId = redisBuilder.DatabaseValue.Value;
			}
		});

		// Register BindConfiguration if set
		if (redisBuilder.BindConfigurationPath is not null)
		{
			services.AddOptions<RedisProviderOptions>()
				.BindConfiguration(redisBuilder.BindConfigurationPath)
				.ValidateOnStart();
		}

		// Register ValidateOnStart
		services.AddOptions<RedisProviderOptions>().ValidateOnStart();

		// Register validator
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<RedisProviderOptions>, RedisProviderOptionsValidator>());

		// Register ConnectionMultiplexer based on connection path
		if (hasBuilderConnection)
		{
			RegisterBuilderManagedMultiplexer(services, redisBuilder, options);
		}
		else if (redisBuilder.ConnectionStringValue is not null)
		{
			var connStr = redisBuilder.ConnectionStringValue;
			_ = services.Configure<RedisProviderOptions>(opt =>
			{
				opt.ConnectionString = connStr;
			});
		}

		// Register core services
		RegisterCoreServices(services);
	}

	private static void RegisterBuilderManagedMultiplexer(
		IServiceCollection services,
		RedisDataBuilder redisBuilder,
		RedisProviderOptions options)
	{
		const string sentinel = "builder-managed-multiplexer:6379";

		// Set sentinel so the options validation passes
		options.ConnectionString = sentinel;

		_ = services.Configure<RedisProviderOptions>(opt =>
		{
			opt.ConnectionString = sentinel;
		});

		if (redisBuilder.MultiplexerInstance is not null)
		{
			var multiplexer = redisBuilder.MultiplexerInstance;
			services.TryAddSingleton(multiplexer);

			// Provider requires concrete ConnectionMultiplexer
			services.TryAddSingleton(sp =>
				(ConnectionMultiplexer)sp.GetRequiredService<IConnectionMultiplexer>());
		}
		else if (redisBuilder.MultiplexerFactoryFunc is not null)
		{
			var factory = redisBuilder.MultiplexerFactoryFunc;
			services.TryAddSingleton(factory);

			// Provider requires concrete ConnectionMultiplexer
			services.TryAddSingleton(sp =>
				(ConnectionMultiplexer)sp.GetRequiredService<IConnectionMultiplexer>());
		}
	}

	private static void RegisterCoreServices(IServiceCollection services)
	{
		services.TryAddSingleton<RedisPersistenceProvider>();
		services.AddKeyedSingleton<IPersistenceProvider>("redis",
			(sp, _) => sp.GetRequiredService<RedisPersistenceProvider>());
		services.TryAddKeyedSingleton<IPersistenceProvider>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IPersistenceProvider>("redis"));

		// Register health check
		services.TryAddSingleton<RedisHealthCheck>();
	}
}
