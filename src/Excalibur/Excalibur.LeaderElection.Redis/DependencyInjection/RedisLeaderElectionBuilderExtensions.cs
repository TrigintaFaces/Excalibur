// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch;
using Excalibur.Dispatch.LeaderElection;
using Excalibur.Dispatch.LeaderElection.DependencyInjection;
using Excalibur.Dispatch.LeaderElection.Fencing;
using Excalibur.LeaderElection.Diagnostics;
using Excalibur.LeaderElection.Redis;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Redis leader election on <see cref="ILeaderElectionBuilder"/>.
/// </summary>
public static class RedisLeaderElectionBuilderExtensions
{
	/// <summary>
	/// Configures the leader election builder to use the Redis provider.
	/// </summary>
	/// <param name="builder">The leader election builder.</param>
	/// <param name="configure">Configuration action for the Redis leader election builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcalibur(x => x.AddLeaderElection(le =&gt;
	/// {
	///     le.UseRedis(redis =&gt;
	///     {
	///         redis.ConnectionString("localhost:6379")
	///              .LockKey("myapp:leader")
	///              .Database(0);
	///     });
	/// }));
	/// </code>
	/// </example>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	public static ILeaderElectionBuilder UseRedis(
		this ILeaderElectionBuilder builder,
		Action<IRedisLeaderElectionBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		var redisBuilder = new RedisLeaderElectionBuilder();
		configure(redisBuilder);

		var hasBuilderConnection = redisBuilder.MultiplexerInstance is not null
			|| redisBuilder.MultiplexerFactoryFunc is not null;

		RegisterOptionsAndServices(builder, redisBuilder, hasBuilderConnection);

		return builder;
	}

	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	private static void RegisterOptionsAndServices(
		ILeaderElectionBuilder builder,
		RedisLeaderElectionBuilder redisBuilder,
		bool hasBuilderConnection)
	{
		// Register LeaderElectionOptions with ValidateOnStart
		if (redisBuilder.BindConfigurationPath is not null)
		{
			builder.Services.AddOptions<LeaderElectionOptions>()
				.BindConfiguration(redisBuilder.BindConfigurationPath)
				.ValidateOnStart();
		}

		builder.Services.AddOptions<LeaderElectionOptions>().ValidateOnStart();

		// Register ConnectionMultiplexer based on connection path
		if (hasBuilderConnection)
		{
			RegisterBuilderManagedMultiplexer(builder.Services, redisBuilder);
		}
		else if (redisBuilder.ConnectionStringValue is not null)
		{
			var connStr = redisBuilder.ConnectionStringValue;
			builder.Services.TryAddSingleton<IConnectionMultiplexer>(
				_ => ConnectionMultiplexer.Connect(connStr));
		}
		else
		{
			// No connection was configured on the builder and none is registered here. Register a
			// fail-fast IConnectionMultiplexer so the missing configuration surfaces as an actionable
			// message naming the exact builder methods, instead of a raw "no service for type
			// IConnectionMultiplexer" at resolve time.
			RegisterMissingConnectionFailFast(builder.Services);
		}

		// Determine effective lock key (from builder or default)
		var lockKey = redisBuilder.LockKeyValue ?? "excalibur:leader";

		// Register RedisLeaderElection
		RegisterRedisLeaderElectionSingleton(builder.Services, lockKey);

		// Register keyed with telemetry decorator
		builder.Services.AddKeyedSingleton<ILeaderElection>("redis", (sp, _) =>
		{
			var inner = sp.GetRequiredService<RedisLeaderElection>();
			var meterFactory = sp.GetService<IMeterFactory>();
			var meter = meterFactory?.Create(LeaderElectionTelemetryConstants.MeterName)
				?? new Meter(LeaderElectionTelemetryConstants.MeterName);
			var activitySource = new ActivitySource(LeaderElectionTelemetryConstants.ActivitySourceName);
			return new TelemetryLeaderElection(inner, meter, activitySource, "Redis");
		});
		builder.Services.TryAddKeyedSingleton<ILeaderElection>("default", (sp, _) =>
			sp.GetRequiredKeyedService<ILeaderElection>("redis"));

		// Register factory
		builder.Services.AddKeyedSingleton<ILeaderElectionFactory>("redis", (sp, _) =>
		{
			var redis = sp.GetRequiredService<IConnectionMultiplexer>();
			var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
			var electionOptions = sp.GetRequiredService<IOptions<LeaderElectionOptions>>();
			var failureClassifier = sp.GetService<IMessageFailureClassifier>();
			var fencingTokenProvider = sp.GetService<IFencingTokenProvider>();
			var inner = new RedisLeaderElectionFactory(redis, loggerFactory, electionOptions, failureClassifier, fencingTokenProvider);
			var meterFactory = sp.GetService<IMeterFactory>();
			var meter = meterFactory?.Create(LeaderElectionTelemetryConstants.MeterName)
				?? new Meter(LeaderElectionTelemetryConstants.MeterName);
			var activitySource = new ActivitySource(LeaderElectionTelemetryConstants.ActivitySourceName);
			return new TelemetryLeaderElectionFactory(inner, meter, activitySource, "Redis");
		});
		builder.Services.TryAddKeyedSingleton<ILeaderElectionFactory>("default", (sp, _) =>
			sp.GetRequiredKeyedService<ILeaderElectionFactory>("redis"));
	}

	/// <summary>
	/// Registers the <see cref="RedisLeaderElection"/> singleton, resolving its optional collaborators
	/// (failure classifier, fencing-token provider) from the service provider at construction time.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="lockKey"> The effective Redis lock key. </param>
	private static void RegisterRedisLeaderElectionSingleton(IServiceCollection services, string lockKey) =>
		services.TryAddSingleton(sp =>
		{
			var redis = sp.GetRequiredService<IConnectionMultiplexer>();
			var options = sp.GetRequiredService<IOptions<LeaderElectionOptions>>();
			var logger = sp.GetRequiredService<ILogger<RedisLeaderElection>>();
			var context = new RedisLeaderElectionContext
			{
				// optional classifier-accelerated self-demotion (null when none registered → grace-only).
				FailureClassifier = sp.GetService<IMessageFailureClassifier>(),
				// optional fencing-token issuance at acquisition (null when fencing not enabled).
				FencingTokenProvider = sp.GetService<IFencingTokenProvider>(),
			};
			return new RedisLeaderElection(redis, lockKey, options, logger, context);
		});

	/// <summary>
	/// Registers a fail-fast <see cref="IConnectionMultiplexer"/> that throws an actionable
	/// <see cref="InvalidOperationException"/> when resolved, naming the exact builder methods the
	/// consumer should call. <c>TryAddSingleton</c> means a consumer who registered their OWN
	/// <see cref="IConnectionMultiplexer"/> is unaffected — the throwing factory only runs when
	/// nothing else provides one.
	/// </summary>
	private static void RegisterMissingConnectionFailFast(IServiceCollection services)
	{
		services.TryAddSingleton<IConnectionMultiplexer>(
			_ => throw new InvalidOperationException(
				"Redis leader election requires a Redis connection. Call ConnectionString(...), "
				+ "ConnectionMultiplexer(...) or ConnectionMultiplexerFactory(...) on the UseRedis "
				+ "builder, or register an IConnectionMultiplexer in the service collection before "
				+ "calling UseRedis()."));
	}

	private static void RegisterBuilderManagedMultiplexer(
		IServiceCollection services,
		RedisLeaderElectionBuilder redisBuilder)
	{
		if (redisBuilder.MultiplexerInstance is not null)
		{
			var multiplexer = redisBuilder.MultiplexerInstance;
			services.TryAddSingleton(multiplexer);
		}
		else if (redisBuilder.MultiplexerFactoryFunc is not null)
		{
			var factory = redisBuilder.MultiplexerFactoryFunc;
			services.TryAddSingleton(factory);
		}
	}
}
