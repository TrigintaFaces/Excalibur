// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Data.Postgres.LeaderElection;
using Excalibur.Dispatch.LeaderElection;
using Excalibur.LeaderElection.Diagnostics;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Postgres leader election services.
/// </summary>
public static class PostgresLeaderElectionExtensions
{
	/// <summary>
	/// Adds Postgres advisory lock-based leader election to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Action to configure Postgres leader election options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// This method registers <see cref="PostgresLeaderElection"/> as the implementation of
	/// <see cref="ILeaderElection"/>, wrapped in a <see cref="TelemetryLeaderElection"/> decorator.
	/// </para>
	/// <para>
	/// Uses Postgres session-level advisory locks (<c>pg_try_advisory_lock</c>) for coordination.
	/// The lock is automatically released when the connection is closed or drops.
	/// </para>
	/// <para>
	/// Example usage:
	/// <code>
	/// services.AddPostgresLeaderElection(options =>
	/// {
	///     options.ConnectionString = "Host=localhost;Database=myapp;";
	///     options.LockKey = 12345;
	/// });
	/// </code>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddPostgresLeaderElection(
		this IServiceCollection services,
		Action<PostgresLeaderElectionOptions> configureOptions)
	{
		return services.AddPostgresLeaderElection(configureOptions, _ => { });
	}

	/// <summary>
	/// Adds Postgres advisory lock-based leader election to the service collection with
	/// additional leader election configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Action to configure Postgres leader election options.</param>
	/// <param name="configureElection">Action to configure general leader election options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddPostgresLeaderElection(
		this IServiceCollection services,
		Action<PostgresLeaderElectionOptions> configureOptions,
		Action<LeaderElectionOptions> configureElection)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureOptions);
		ArgumentNullException.ThrowIfNull(configureElection);

		_ = services.AddOptions<PostgresLeaderElectionOptions>()
			.Configure(configureOptions)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		_ = services.Configure(configureElection);

		services.TryAddSingleton(sp =>
		{
			var pgOptions = sp.GetRequiredService<IOptions<PostgresLeaderElectionOptions>>();
			var electionOptions = sp.GetRequiredService<IOptions<LeaderElectionOptions>>();
			var logger = sp.GetRequiredService<ILogger<PostgresLeaderElection>>();
			return new PostgresLeaderElection(pgOptions, electionOptions, logger);
		});

		services.TryAddSingleton<ILeaderElection>(sp =>
		{
			var inner = sp.GetRequiredService<PostgresLeaderElection>();
			var meterFactory = sp.GetService<IMeterFactory>();
			var meter = meterFactory?.Create(LeaderElectionTelemetryConstants.MeterName) ?? new Meter(LeaderElectionTelemetryConstants.MeterName);
			var activitySource = new ActivitySource(LeaderElectionTelemetryConstants.ActivitySourceName);
			return new TelemetryLeaderElection(inner, meter, activitySource, "Postgres");
		});

		return services;
	}
}
