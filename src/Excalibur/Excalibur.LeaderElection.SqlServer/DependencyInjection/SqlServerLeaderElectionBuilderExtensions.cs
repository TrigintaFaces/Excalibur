// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.LeaderElection;
using Excalibur.Dispatch.LeaderElection.DependencyInjection;
using Excalibur.LeaderElection.Diagnostics;
using Excalibur.LeaderElection.SqlServer;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring SQL Server leader election on <see cref="ILeaderElectionBuilder"/>.
/// </summary>
public static class SqlServerLeaderElectionBuilderExtensions
{
	/// <summary>
	/// Configures the leader election builder to use the SQL Server provider.
	/// </summary>
	/// <param name="builder">The leader election builder.</param>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="lockResource">The name of the lock resource (e.g., "MyApp.Leader").</param>
	/// <returns>The builder for fluent chaining.</returns>
	public static ILeaderElectionBuilder UseSqlServer(
		this ILeaderElectionBuilder builder,
		string connectionString,
		string lockResource)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		ArgumentException.ThrowIfNullOrWhiteSpace(lockResource);

		builder.Services.TryAddSingleton(sp =>
		{
			var options = sp.GetRequiredService<IOptions<LeaderElectionOptions>>();
			var logger = sp.GetRequiredService<ILogger<SqlServerLeaderElection>>();
			return new SqlServerLeaderElection(connectionString, lockResource, options, logger);
		});
		builder.Services.TryAddSingleton<ILeaderElection>(sp =>
		{
			var inner = sp.GetRequiredService<SqlServerLeaderElection>();
			var meterFactory = sp.GetService<IMeterFactory>();
			var meter = meterFactory?.Create(LeaderElectionTelemetryConstants.MeterName) ?? new Meter(LeaderElectionTelemetryConstants.MeterName);
			var activitySource = new ActivitySource(LeaderElectionTelemetryConstants.ActivitySourceName);
			return new TelemetryLeaderElection(inner, meter, activitySource, "SqlServer");
		});

		return builder;
	}

	/// <summary>
	/// Configures the leader election builder to use the SQL Server factory provider.
	/// </summary>
	/// <param name="builder">The leader election builder.</param>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// Use the factory when you need multiple leader elections with different lock resources.
	/// </remarks>
	public static ILeaderElectionBuilder UseSqlServerFactory(
		this ILeaderElectionBuilder builder,
		string connectionString)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		builder.Services.TryAddSingleton<ILeaderElectionFactory>(sp =>
		{
			var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
			var inner = new SqlServerLeaderElectionFactory(connectionString, loggerFactory);
			var meterFactory = sp.GetService<IMeterFactory>();
			var meter = meterFactory?.Create(LeaderElectionTelemetryConstants.MeterName) ?? new Meter(LeaderElectionTelemetryConstants.MeterName);
			var activitySource = new ActivitySource(LeaderElectionTelemetryConstants.ActivitySourceName);
			return new TelemetryLeaderElectionFactory(inner, meter, activitySource, "SqlServer");
		});

		return builder;
	}
}
