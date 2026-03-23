// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.LeaderElection;
using Excalibur.LeaderElection.Diagnostics;
using Excalibur.LeaderElection.Postgres;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Postgres health-based leader election services.
/// </summary>
public static class PostgresHealthBasedLeaderElectionExtensions
{
	/// <summary>
	/// Adds Postgres health-based leader election to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Action to configure Postgres leader election options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddPostgresHealthBasedLeaderElection(
		this IServiceCollection services,
		Action<PostgresLeaderElectionOptions> configureOptions)
	{
		return services.AddPostgresHealthBasedLeaderElection(configureOptions, _ => { }, _ => { });
	}

	/// <summary>
	/// Adds Postgres health-based leader election to the service collection with health configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Action to configure Postgres leader election options.</param>
	/// <param name="configureHealth">Action to configure health-based leader election options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddPostgresHealthBasedLeaderElection(
		this IServiceCollection services,
		Action<PostgresLeaderElectionOptions> configureOptions,
		Action<PostgresHealthBasedLeaderElectionOptions> configureHealth)
	{
		return services.AddPostgresHealthBasedLeaderElection(configureOptions, configureHealth, _ => { });
	}

	/// <summary>
	/// Adds Postgres health-based leader election to the service collection with full configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Action to configure Postgres leader election options.</param>
	/// <param name="configureHealth">Action to configure health-based leader election options.</param>
	/// <param name="configureElection">Action to configure general leader election options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddPostgresHealthBasedLeaderElection(
		this IServiceCollection services,
		Action<PostgresLeaderElectionOptions> configureOptions,
		Action<PostgresHealthBasedLeaderElectionOptions> configureHealth,
		Action<LeaderElectionOptions> configureElection)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureOptions);
		ArgumentNullException.ThrowIfNull(configureHealth);
		ArgumentNullException.ThrowIfNull(configureElection);

		_ = services.AddOptions<PostgresLeaderElectionOptions>()
			.Configure(configureOptions)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		_ = services.AddOptions<PostgresHealthBasedLeaderElectionOptions>()
			.Configure(configureHealth)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		_ = services.AddOptions<LeaderElectionOptions>()
			.Configure(configureElection)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<LeaderElectionOptions>, LeaderElectionOptionsValidator>());

		services.TryAddSingleton(sp =>
		{
			var pgOptions = sp.GetRequiredService<IOptions<PostgresLeaderElectionOptions>>();
			var electionOptions = sp.GetRequiredService<IOptions<LeaderElectionOptions>>();
			var healthOptions = sp.GetRequiredService<IOptions<PostgresHealthBasedLeaderElectionOptions>>();
			var logger = sp.GetRequiredService<ILogger<PostgresHealthBasedLeaderElection>>();
			var innerLogger = sp.GetRequiredService<ILogger<PostgresLeaderElection>>();
			return new PostgresHealthBasedLeaderElection(pgOptions, electionOptions, healthOptions, logger, innerLogger);
		});

		services.TryAddSingleton<IHealthBasedLeaderElection>(sp =>
			sp.GetRequiredService<PostgresHealthBasedLeaderElection>());

		services.AddKeyedSingleton<ILeaderElection>("postgres", (sp, _) =>
		{
			var inner = sp.GetRequiredService<PostgresHealthBasedLeaderElection>();
			var meterFactory = sp.GetService<IMeterFactory>();
			var meter = meterFactory?.Create(LeaderElectionTelemetryConstants.MeterName) ?? new Meter(LeaderElectionTelemetryConstants.MeterName);
			var activitySource = new ActivitySource(LeaderElectionTelemetryConstants.ActivitySourceName);
			return new TelemetryLeaderElection(inner, meter, activitySource, "Postgres.HealthBased");
		});
		services.TryAddKeyedSingleton<ILeaderElection>("default", (sp, _) =>
			sp.GetRequiredKeyedService<ILeaderElection>("postgres"));

		return services;
	}
}
