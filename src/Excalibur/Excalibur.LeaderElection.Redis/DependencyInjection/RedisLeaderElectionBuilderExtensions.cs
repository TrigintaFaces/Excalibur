// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.LeaderElection;
using Excalibur.Dispatch.LeaderElection.DependencyInjection;
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
	/// <param name="lockKey">The Redis key for the leader lock (e.g., "myapp:leader").</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// Requires an <see cref="IConnectionMultiplexer"/> to be registered in the service collection.
	/// </remarks>
	public static ILeaderElectionBuilder UseRedis(
		this ILeaderElectionBuilder builder,
		string lockKey)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrWhiteSpace(lockKey);

		builder.Services.TryAddSingleton(sp =>
		{
			var redis = sp.GetRequiredService<IConnectionMultiplexer>();
			var options = sp.GetRequiredService<IOptions<LeaderElectionOptions>>();
			var logger = sp.GetRequiredService<ILogger<RedisLeaderElection>>();
			return new RedisLeaderElection(redis, lockKey, options, logger);
		});
		builder.Services.TryAddSingleton<ILeaderElection>(sp =>
		{
			var inner = sp.GetRequiredService<RedisLeaderElection>();
			var meterFactory = sp.GetService<IMeterFactory>();
			var meter = meterFactory?.Create(LeaderElectionTelemetryConstants.MeterName) ?? new Meter(LeaderElectionTelemetryConstants.MeterName);
			var activitySource = new ActivitySource(LeaderElectionTelemetryConstants.ActivitySourceName);
			return new TelemetryLeaderElection(inner, meter, activitySource, "Redis");
		});

		return builder;
	}

	/// <summary>
	/// Configures the leader election builder to use the Redis factory provider.
	/// </summary>
	/// <param name="builder">The leader election builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// Requires an <see cref="IConnectionMultiplexer"/> to be registered in the service collection.
	/// Use the factory when you need multiple leader elections with different lock keys.
	/// </remarks>
	public static ILeaderElectionBuilder UseRedisFactory(
		this ILeaderElectionBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.Services.TryAddSingleton<ILeaderElectionFactory>(sp =>
		{
			var redis = sp.GetRequiredService<IConnectionMultiplexer>();
			var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
			var inner = new RedisLeaderElectionFactory(redis, loggerFactory);
			var meterFactory = sp.GetService<IMeterFactory>();
			var meter = meterFactory?.Create(LeaderElectionTelemetryConstants.MeterName) ?? new Meter(LeaderElectionTelemetryConstants.MeterName);
			var activitySource = new ActivitySource(LeaderElectionTelemetryConstants.ActivitySourceName);
			return new TelemetryLeaderElectionFactory(inner, meter, activitySource, "Redis");
		});

		return builder;
	}
}
