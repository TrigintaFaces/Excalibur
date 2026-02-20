// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.LeaderElection;
using Excalibur.LeaderElection.Diagnostics;
using Excalibur.LeaderElection.Redis;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Redis leader election services.
/// </summary>
public static class RedisLeaderElectionExtensions
{
	/// <summary>
	/// Adds Redis leader election to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="lockKey">The Redis key for the leader lock (e.g., "myapp:leader").</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// Requires an <see cref="IConnectionMultiplexer"/> to be registered in the service collection.
	/// </remarks>
	public static IServiceCollection AddRedisLeaderElection(
		this IServiceCollection services,
		string lockKey)
	{
		return services.AddRedisLeaderElection(lockKey, _ => { });
	}

	/// <summary>
	/// Adds Redis leader election to the service collection with configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="lockKey">The Redis key for the leader lock (e.g., "myapp:leader").</param>
	/// <param name="configure">Action to configure leader election options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// Requires an <see cref="IConnectionMultiplexer"/> to be registered in the service collection.
	/// </remarks>
	public static IServiceCollection AddRedisLeaderElection(
		this IServiceCollection services,
		string lockKey,
		Action<LeaderElectionOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(lockKey);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.Configure(configure);

		services.TryAddSingleton(sp =>
		{
			var redis = sp.GetRequiredService<IConnectionMultiplexer>();
			var options = sp.GetRequiredService<IOptions<LeaderElectionOptions>>();
			var logger = sp.GetRequiredService<ILogger<RedisLeaderElection>>();
			return new RedisLeaderElection(redis, lockKey, options, logger);
		});
		services.TryAddSingleton<ILeaderElection>(sp =>
		{
			var inner = sp.GetRequiredService<RedisLeaderElection>();
			var meterFactory = sp.GetService<IMeterFactory>();
			var meter = meterFactory?.Create(LeaderElectionTelemetryConstants.MeterName) ?? new Meter(LeaderElectionTelemetryConstants.MeterName);
			var activitySource = new ActivitySource(LeaderElectionTelemetryConstants.ActivitySourceName);
			return new TelemetryLeaderElection(inner, meter, activitySource, "Redis");
		});

		return services;
	}

	/// <summary>
	/// Adds Redis leader election factory to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// Requires an <see cref="IConnectionMultiplexer"/> to be registered in the service collection.
	/// Use the factory when you need multiple leader elections with different lock keys.
	/// </remarks>
	public static IServiceCollection AddRedisLeaderElectionFactory(
		this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<ILeaderElectionFactory>(sp =>
		{
			var redis = sp.GetRequiredService<IConnectionMultiplexer>();
			var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
			var inner = new RedisLeaderElectionFactory(redis, loggerFactory);
			var meterFactory = sp.GetService<IMeterFactory>();
			var meter = meterFactory?.Create(LeaderElectionTelemetryConstants.MeterName) ?? new Meter(LeaderElectionTelemetryConstants.MeterName);
			var activitySource = new ActivitySource(LeaderElectionTelemetryConstants.ActivitySourceName);
			return new TelemetryLeaderElectionFactory(inner, meter, activitySource, "Redis");
		});

		return services;
	}
}
