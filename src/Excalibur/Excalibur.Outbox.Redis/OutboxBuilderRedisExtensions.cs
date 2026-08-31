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
			opt.ConnectionSuppliedExternally = options.ConnectionSuppliedExternally;
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
			RegisterBuilderManagedMultiplexer(builder.Services, redisBuilder);
		}
		else if (redisBuilder.ConnectionStringValue is not null)
		{
			var connStr = redisBuilder.ConnectionStringValue;
			builder.Services.TryAddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(connStr));
		}

		// Register store services (use constructor with ConnectionMultiplexer when available)
		if (hasBuilderConnection || redisBuilder.ConnectionStringValue is not null)
		{
			// AddTenantAwareStore emits the ITenantPartitionedCapability<IOutboxStore> marker as part
			// of THIS registration, so the marker cannot exist without the store it attests. It is the
			// partitioned seam rather than the scoped one because this store reads no ambient tenant on any
			// path: it persists the tenant on the hash it writes and hands that value back on the drain, so
			// the owning tenant is re-established from the row. That seam takes no ITenantContext, so there
			// is no dependency here to be handed to the factory and silently discarded.
			builder.Services.AddTenantAwareStore<IOutboxStore, RedisOutboxStore>(sp =>
			{
				var connection = sp.GetRequiredService<IConnectionMultiplexer>();
				var opts = sp.GetRequiredService<IOptions<RedisOutboxOptions>>();
				var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<RedisOutboxStore>>();
				return new RedisOutboxStore(connection, opts, logger);
			});
		}
		else
		{
			// Same partitioned attestation on the DI-constructed branch. Both branches are registration call
			// sites for the same contract, so a marker emitted on only one of them would leave the other
			// shape rejected by row-discriminator multi-tenancy while looking fixed.
			builder.Services.AddTenantAwareStore<IOutboxStore, RedisOutboxStore>(
				static sp => ActivatorUtilities.CreateInstance<RedisOutboxStore>(sp));
		}

		builder.Services.AddKeyedSingleton<IOutboxStore>("redis", (sp, _) => sp.GetRequiredService<RedisOutboxStore>());
		builder.Services.TryAddKeyedSingleton<IOutboxStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IOutboxStore>("redis"));
	}

	private static void RegisterBuilderManagedMultiplexer(
		IServiceCollection services,
		RedisOutboxBuilder redisBuilder)
	{
		// The host owns the connection on this path, so the store is handed the multiplexer directly and
		// never reads RedisOutboxOptions.ConnectionString. Nothing is written to that option here: a
		// placeholder would be indistinguishable from a real endpoint and would silently override whatever
		// the host configured.
		if (redisBuilder.MultiplexerInstance is not null)
		{
			services.TryAddSingleton(redisBuilder.MultiplexerInstance);
		}
		else if (redisBuilder.MultiplexerFactoryFunc is not null)
		{
			services.TryAddSingleton(redisBuilder.MultiplexerFactoryFunc);
		}
	}
}
