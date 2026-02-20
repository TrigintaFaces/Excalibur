// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.LeaderElection;
using Excalibur.LeaderElection.Diagnostics;
using Excalibur.LeaderElection.SqlServer;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring SQL Server leader election services.
/// </summary>
public static class SqlServerLeaderElectionExtensions
{
	/// <summary>
	/// Adds SQL Server leader election to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="lockResource">The name of the lock resource (e.g., "MyApp.Leader").</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSqlServerLeaderElection(
		this IServiceCollection services,
		string connectionString,
		string lockResource)
	{
		return services.AddSqlServerLeaderElection(connectionString, lockResource, _ => { });
	}

	/// <summary>
	/// Adds SQL Server leader election to the service collection with configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="lockResource">The name of the lock resource (e.g., "MyApp.Leader").</param>
	/// <param name="configure">Action to configure leader election options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSqlServerLeaderElection(
		this IServiceCollection services,
		string connectionString,
		string lockResource,
		Action<LeaderElectionOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		ArgumentException.ThrowIfNullOrWhiteSpace(lockResource);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.Configure(configure);

		services.TryAddSingleton(sp =>
		{
			var options = sp.GetRequiredService<IOptions<LeaderElectionOptions>>();
			var logger = sp.GetRequiredService<ILogger<SqlServerLeaderElection>>();
			return new SqlServerLeaderElection(connectionString, lockResource, options, logger);
		});
		services.TryAddSingleton<ILeaderElection>(sp =>
		{
			var inner = sp.GetRequiredService<SqlServerLeaderElection>();
			var meterFactory = sp.GetService<IMeterFactory>();
			var meter = meterFactory?.Create(LeaderElectionTelemetryConstants.MeterName) ?? new Meter(LeaderElectionTelemetryConstants.MeterName);
			var activitySource = new ActivitySource(LeaderElectionTelemetryConstants.ActivitySourceName);
			return new TelemetryLeaderElection(inner, meter, activitySource, "SqlServer");
		});

		return services;
	}

	/// <summary>
	/// Adds SQL Server leader election factory to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// Use the factory when you need multiple leader elections with different lock resources.
	/// </remarks>
	public static IServiceCollection AddSqlServerLeaderElectionFactory(
		this IServiceCollection services,
		string connectionString)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		services.TryAddSingleton<ILeaderElectionFactory>(sp =>
		{
			var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
			var inner = new SqlServerLeaderElectionFactory(connectionString, loggerFactory);
			var meterFactory = sp.GetService<IMeterFactory>();
			var meter = meterFactory?.Create(LeaderElectionTelemetryConstants.MeterName) ?? new Meter(LeaderElectionTelemetryConstants.MeterName);
			var activitySource = new ActivitySource(LeaderElectionTelemetryConstants.ActivitySourceName);
			return new TelemetryLeaderElectionFactory(inner, meter, activitySource, "SqlServer");
		});

		return services;
	}
}
