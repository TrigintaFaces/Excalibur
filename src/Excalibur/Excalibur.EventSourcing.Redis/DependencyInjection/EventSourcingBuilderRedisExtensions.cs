// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using StackExchange.Redis;

namespace Excalibur.EventSourcing.Redis;

/// <summary>
/// Extension methods for configuring Redis event sourcing on <see cref="IEventSourcingBuilder"/>.
/// </summary>
/// <remarks>
/// <para>
/// These extensions provide fluent provider selection following the established
/// CDC builder pattern (see <c>EventSourcingBuilderSqlServerExtensions</c>).
/// </para>
/// </remarks>
public static class EventSourcingBuilderRedisExtensions
{
	/// <summary>
	/// Configures the event sourcing builder to use Redis for event store and snapshot store.
	/// </summary>
	/// <param name="builder">The event sourcing builder.</param>
	/// <param name="configure">Configuration action for the Redis event sourcing builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcaliburEventSourcing(es =&gt;
	/// {
	///     es.UseRedis(redis =&gt;
	///     {
	///         redis.ConnectionString("localhost:6379")
	///              .KeyPrefix("myapp")
	///              .Database(0);
	///     })
	///       .AddRepository&lt;OrderAggregate, Guid&gt;();
	/// });
	/// </code>
	/// </example>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IEventSourcingBuilder UseRedis(
		this IEventSourcingBuilder builder,
		Action<IRedisEventSourcingBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		var eventStoreOptions = new RedisEventStoreOptions();
		var redisBuilder = new RedisEventSourcingBuilder(eventStoreOptions);
		configure(redisBuilder);

		var hasBuilderConnection = redisBuilder.MultiplexerInstance is not null
			|| redisBuilder.MultiplexerFactoryFunc is not null;

		RegisterOptionsAndServices(builder, redisBuilder, eventStoreOptions, hasBuilderConnection);

		return builder;
	}

	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	private static void RegisterOptionsAndServices(
		IEventSourcingBuilder builder,
		RedisEventSourcingBuilder redisBuilder,
		RedisEventStoreOptions eventStoreOptions,
		bool hasBuilderConnection)
	{
		// Register event store options from builder state
		_ = builder.Services.Configure<RedisEventStoreOptions>(opt =>
		{
			opt.ConnectionString = eventStoreOptions.ConnectionString;
			opt.StreamKeyPrefix = eventStoreOptions.StreamKeyPrefix;
			if (redisBuilder.DatabaseValue.HasValue)
			{
				opt.DatabaseIndex = redisBuilder.DatabaseValue.Value;
			}
		});

		// Register snapshot store options from shared builder state
		_ = builder.Services.Configure<RedisSnapshotStoreOptions>(opt =>
		{
			opt.ConnectionString = eventStoreOptions.ConnectionString;
			if (redisBuilder.DatabaseValue.HasValue)
			{
				opt.DatabaseIndex = redisBuilder.DatabaseValue.Value;
			}
		});

		// Register BindConfiguration if set (applies to both options types)
		if (redisBuilder.BindConfigurationPath is not null)
		{
			builder.Services.AddOptions<RedisEventStoreOptions>()
				.BindConfiguration(redisBuilder.BindConfigurationPath)
				.ValidateOnStart();

			builder.Services.AddOptions<RedisSnapshotStoreOptions>()
				.BindConfiguration(redisBuilder.BindConfigurationPath)
				.ValidateOnStart();
		}

		// Register ValidateOnStart for both options types
		builder.Services.AddOptions<RedisEventStoreOptions>().ValidateOnStart();
		builder.Services.AddOptions<RedisSnapshotStoreOptions>().ValidateOnStart();

		// Register ConnectionMultiplexer based on connection path
		if (hasBuilderConnection)
		{
			RegisterBuilderManagedMultiplexer(builder, redisBuilder, eventStoreOptions);
		}
		else if (redisBuilder.ConnectionStringValue is not null)
		{
			var connStr = redisBuilder.ConnectionStringValue;
			builder.Services.TryAddSingleton(_ => ConnectionMultiplexer.Connect(connStr));
		}

		// Register event store
		builder.Services.TryAddSingleton<RedisEventStore>();
		builder.Services.AddKeyedSingleton<IEventStore>("redis", (sp, _) => sp.GetRequiredService<RedisEventStore>());
		builder.Services.TryAddKeyedSingleton<IEventStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IEventStore>("redis"));

		// Register snapshot store
		builder.Services.TryAddSingleton<RedisSnapshotStore>();
		builder.Services.AddKeyedSingleton<ISnapshotStore>("redis", (sp, _) => sp.GetRequiredService<RedisSnapshotStore>());
		builder.Services.TryAddKeyedSingleton<ISnapshotStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<ISnapshotStore>("redis"));
	}

	private static void RegisterBuilderManagedMultiplexer(
		IEventSourcingBuilder builder,
		RedisEventSourcingBuilder redisBuilder,
		RedisEventStoreOptions eventStoreOptions)
	{
		const string sentinel = "builder-managed-multiplexer:6379";

		// Set sentinel on event store options so validation passes
		eventStoreOptions.ConnectionString = sentinel;

		// Update both options with sentinel
		_ = builder.Services.Configure<RedisEventStoreOptions>(opt =>
		{
			opt.ConnectionString = sentinel;
		});

		_ = builder.Services.Configure<RedisSnapshotStoreOptions>(opt =>
		{
			opt.ConnectionString = sentinel;
		});

		if (redisBuilder.MultiplexerInstance is not null)
		{
			var multiplexer = redisBuilder.MultiplexerInstance;
			builder.Services.TryAddSingleton(multiplexer);

			// Stores require concrete ConnectionMultiplexer
			builder.Services.TryAddSingleton(sp =>
				(ConnectionMultiplexer)sp.GetRequiredService<IConnectionMultiplexer>());
		}
		else if (redisBuilder.MultiplexerFactoryFunc is not null)
		{
			var factory = redisBuilder.MultiplexerFactoryFunc;
			builder.Services.TryAddSingleton(factory);

			// Stores require concrete ConnectionMultiplexer
			builder.Services.TryAddSingleton(sp =>
				(ConnectionMultiplexer)sp.GetRequiredService<IConnectionMultiplexer>());
		}
	}
}
