// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch;
using Excalibur.Outbox;
using Excalibur.Outbox.Redis;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Redis provider on <see cref="IOutboxBuilder"/>.
/// </summary>
public static class OutboxBuilderRedisExtensions
{
	/// <summary>
	/// Configures the outbox to use Redis storage.
	/// </summary>
	/// <param name="builder">The outbox builder.</param>
	/// <param name="configure">Configuration action for the Redis outbox builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcalibur(x => x.AddOutbox(outbox =&gt;
	/// {
	///     outbox.UseRedis(redis =&gt;
	///     {
	///         redis.ConnectionString("localhost:6379")
	///              .KeyPrefix("outbox")
	///              .Database(0);
	///     });
	/// }));
	/// </code>
	/// </example>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	public static IOutboxBuilder UseRedis(
		this IOutboxBuilder builder,
		Action<IRedisOutboxBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new RedisOutboxOptions();
		var redisBuilder = new RedisOutboxBuilder(options);
		configure(redisBuilder);

		var hasBuilderConnection = redisBuilder.MultiplexerInstance is not null
			|| redisBuilder.MultiplexerFactoryFunc is not null;

		RegisterOptionsAndServices(builder, redisBuilder, options, hasBuilderConnection);

		return builder;
	}

	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	private static void RegisterOptionsAndServices(
		IOutboxBuilder builder,
		RedisOutboxBuilder redisBuilder,
		RedisOutboxOptions options,
		bool hasBuilderConnection)
	{
		// Register store-specific options from builder state
		_ = builder.Services.Configure<RedisOutboxOptions>(opt =>
		{
			opt.ConnectionString = options.ConnectionString;
			opt.KeyPrefix = options.KeyPrefix;
			if (redisBuilder.DatabaseValue.HasValue)
			{
				opt.DatabaseId = redisBuilder.DatabaseValue.Value;
			}
		});

		// Register BindConfiguration if set
		if (redisBuilder.BindConfigurationPath is not null)
		{
			builder.Services.AddOptions<RedisOutboxOptions>()
				.BindConfiguration(redisBuilder.BindConfigurationPath)
				.ValidateOnStart();
		}

		// Register ValidateOnStart
		builder.Services.AddOptions<RedisOutboxOptions>().ValidateOnStart();

		// Register validator
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<RedisOutboxOptions>, RedisOutboxOptionsValidator>());

		// Register ConnectionMultiplexer based on connection path
		if (hasBuilderConnection)
		{
			RegisterBuilderManagedMultiplexer(builder.Services, redisBuilder, options);
		}
		else if (redisBuilder.ConnectionStringValue is not null)
		{
			var connStr = redisBuilder.ConnectionStringValue;
			builder.Services.TryAddSingleton(_ => ConnectionMultiplexer.Connect(connStr));
		}

		// Register store services (use constructor with ConnectionMultiplexer when available)
		if (hasBuilderConnection || redisBuilder.ConnectionStringValue is not null)
		{
			builder.Services.TryAddSingleton(sp =>
			{
				var connection = sp.GetRequiredService<ConnectionMultiplexer>();
				var opts = sp.GetRequiredService<IOptions<RedisOutboxOptions>>();
				var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<RedisOutboxStore>>();
				return new RedisOutboxStore(connection, opts, logger);
			});
		}
		else
		{
			builder.Services.TryAddSingleton<RedisOutboxStore>();
		}

		builder.Services.AddKeyedSingleton<IOutboxStore>("redis", (sp, _) => sp.GetRequiredService<RedisOutboxStore>());
		builder.Services.TryAddKeyedSingleton<IOutboxStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IOutboxStore>("redis"));
	}

	private static void RegisterBuilderManagedMultiplexer(
		IServiceCollection services,
		RedisOutboxBuilder redisBuilder,
		RedisOutboxOptions options)
	{
		const string sentinel = "builder-managed-multiplexer:6379";

		// Set sentinel so the store's options validation passes
		options.ConnectionString = sentinel;

		_ = services.Configure<RedisOutboxOptions>(opt =>
		{
			opt.ConnectionString = sentinel;
		});

		if (redisBuilder.MultiplexerInstance is not null)
		{
			var multiplexer = redisBuilder.MultiplexerInstance;
			services.TryAddSingleton(multiplexer);

			// Store requires concrete ConnectionMultiplexer
			services.TryAddSingleton(sp =>
				(ConnectionMultiplexer)sp.GetRequiredService<IConnectionMultiplexer>());
		}
		else if (redisBuilder.MultiplexerFactoryFunc is not null)
		{
			var factory = redisBuilder.MultiplexerFactoryFunc;
			services.TryAddSingleton(factory);

			// Store requires concrete ConnectionMultiplexer
			services.TryAddSingleton(sp =>
				(ConnectionMultiplexer)sp.GetRequiredService<IConnectionMultiplexer>());
		}
	}
}
