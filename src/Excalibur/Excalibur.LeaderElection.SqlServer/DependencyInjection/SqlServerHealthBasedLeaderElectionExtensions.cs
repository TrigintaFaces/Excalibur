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
/// Extension methods for configuring SQL Server health-based leader election services.
/// </summary>
public static class SqlServerHealthBasedLeaderElectionExtensions
{
	/// <summary>
	/// Adds SQL Server health-based leader election to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="lockResource">The name of the lock resource (e.g., "MyApp.Leader").</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// Registers both <see cref="ILeaderElection"/> and <see cref="IHealthBasedLeaderElection"/>
	/// backed by the same SQL Server instance. The health-based implementation adds a health
	/// tracking table alongside the standard sp_getapplock leader election.
	/// </para>
	/// <para>
	/// Example usage:
	/// <code>
	/// services.AddSqlServerHealthBasedLeaderElection(
	///     connectionString,
	///     "MyApp.Leader",
	///     health => health.StepDownWhenUnhealthy = true);
	/// </code>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddSqlServerHealthBasedLeaderElection(
		this IServiceCollection services,
		string connectionString,
		string lockResource)
	{
		return services.AddSqlServerHealthBasedLeaderElection(connectionString, lockResource, _ => { }, _ => { });
	}

	/// <summary>
	/// Adds SQL Server health-based leader election to the service collection with configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="lockResource">The name of the lock resource (e.g., "MyApp.Leader").</param>
	/// <param name="configureHealth">Action to configure health-based leader election options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSqlServerHealthBasedLeaderElection(
		this IServiceCollection services,
		string connectionString,
		string lockResource,
		Action<SqlServerHealthBasedLeaderElectionOptions> configureHealth)
	{
		return services.AddSqlServerHealthBasedLeaderElection(connectionString, lockResource, configureHealth, _ => { });
	}

	/// <summary>
	/// Adds SQL Server health-based leader election to the service collection with full configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="lockResource">The name of the lock resource (e.g., "MyApp.Leader").</param>
	/// <param name="configureHealth">Action to configure health-based leader election options.</param>
	/// <param name="configureElection">Action to configure general leader election options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSqlServerHealthBasedLeaderElection(
		this IServiceCollection services,
		string connectionString,
		string lockResource,
		Action<SqlServerHealthBasedLeaderElectionOptions> configureHealth,
		Action<LeaderElectionOptions> configureElection)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		ArgumentException.ThrowIfNullOrWhiteSpace(lockResource);
		ArgumentNullException.ThrowIfNull(configureHealth);
		ArgumentNullException.ThrowIfNull(configureElection);

		_ = services.AddOptions<SqlServerHealthBasedLeaderElectionOptions>()
			.Configure(configureHealth)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		_ = services.Configure(configureElection);

		services.TryAddSingleton(sp =>
		{
			var electionOptions = sp.GetRequiredService<IOptions<LeaderElectionOptions>>();
			var healthOptions = sp.GetRequiredService<IOptions<SqlServerHealthBasedLeaderElectionOptions>>();
			var logger = sp.GetRequiredService<ILogger<SqlServerHealthBasedLeaderElection>>();
			var innerLogger = sp.GetRequiredService<ILogger<SqlServerLeaderElection>>();
			return new SqlServerHealthBasedLeaderElection(connectionString, lockResource, electionOptions, healthOptions, logger, innerLogger);
		});

		services.TryAddSingleton<IHealthBasedLeaderElection>(sp =>
			sp.GetRequiredService<SqlServerHealthBasedLeaderElection>());

		services.TryAddSingleton<ILeaderElection>(sp =>
		{
			var inner = sp.GetRequiredService<SqlServerHealthBasedLeaderElection>();
			var meterFactory = sp.GetService<IMeterFactory>();
			var meter = meterFactory?.Create(LeaderElectionTelemetryConstants.MeterName) ?? new Meter(LeaderElectionTelemetryConstants.MeterName);
			var activitySource = new ActivitySource(LeaderElectionTelemetryConstants.ActivitySourceName);
			return new TelemetryLeaderElection(inner, meter, activitySource, "SqlServer.HealthBased");
		});

		return services;
	}
}
