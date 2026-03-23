// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.LeaderElection;
using Excalibur.Dispatch.LeaderElection.DependencyInjection;
using Excalibur.LeaderElection.Diagnostics;
using Excalibur.LeaderElection.Postgres;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Postgres leader election on <see cref="ILeaderElectionBuilder"/>.
/// </summary>
public static class PostgresLeaderElectionBuilderExtensions
{
	/// <summary>
	/// Configures the leader election builder to use the Postgres advisory lock provider.
	/// </summary>
	/// <param name="builder">The leader election builder.</param>
	/// <param name="configureOptions">Action to configure Postgres leader election options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	public static ILeaderElectionBuilder UsePostgres(
		this ILeaderElectionBuilder builder,
		Action<PostgresLeaderElectionOptions> configureOptions)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configureOptions);

		_ = builder.Services.AddOptions<PostgresLeaderElectionOptions>()
			.Configure(configureOptions)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		builder.Services.TryAddSingleton(sp =>
		{
			var pgOptions = sp.GetRequiredService<IOptions<PostgresLeaderElectionOptions>>();
			var electionOptions = sp.GetRequiredService<IOptions<LeaderElectionOptions>>();
			var logger = sp.GetRequiredService<ILogger<PostgresLeaderElection>>();
			return new PostgresLeaderElection(pgOptions, electionOptions, logger);
		});
		builder.Services.AddKeyedSingleton<ILeaderElection>("postgres", (sp, _) =>
		{
			var inner = sp.GetRequiredService<PostgresLeaderElection>();
			var meterFactory = sp.GetService<IMeterFactory>();
			var meter = meterFactory?.Create(LeaderElectionTelemetryConstants.MeterName) ?? new Meter(LeaderElectionTelemetryConstants.MeterName);
			var activitySource = new ActivitySource(LeaderElectionTelemetryConstants.ActivitySourceName);
			return new TelemetryLeaderElection(inner, meter, activitySource, "Postgres");
		});
		builder.Services.TryAddKeyedSingleton<ILeaderElection>("default", (sp, _) =>
			sp.GetRequiredKeyedService<ILeaderElection>("postgres"));

		return builder;
	}

	/// <summary>
	/// Configures the leader election builder to use the Postgres factory provider.
	/// </summary>
	/// <param name="builder">The leader election builder.</param>
	/// <param name="configureOptions">Action to configure Postgres leader election options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// Use the factory when you need multiple leader elections with different lock keys.
	/// </remarks>
	public static ILeaderElectionBuilder UsePostgresFactory(
		this ILeaderElectionBuilder builder,
		Action<PostgresLeaderElectionOptions> configureOptions)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configureOptions);

		_ = builder.Services.AddOptions<PostgresLeaderElectionOptions>()
			.Configure(configureOptions)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		builder.Services.AddKeyedSingleton<ILeaderElectionFactory>("postgres", (sp, _) =>
		{
			var pgOptions = sp.GetRequiredService<IOptions<PostgresLeaderElectionOptions>>();
			var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
			var inner = new PostgresLeaderElectionFactory(pgOptions, loggerFactory);
			var meterFactory = sp.GetService<IMeterFactory>();
			var meter = meterFactory?.Create(LeaderElectionTelemetryConstants.MeterName) ?? new Meter(LeaderElectionTelemetryConstants.MeterName);
			var activitySource = new ActivitySource(LeaderElectionTelemetryConstants.ActivitySourceName);
			return new TelemetryLeaderElectionFactory(inner, meter, activitySource, "Postgres");
		});
		builder.Services.TryAddKeyedSingleton<ILeaderElectionFactory>("default", (sp, _) =>
			sp.GetRequiredKeyedService<ILeaderElectionFactory>("postgres"));

		return builder;
	}
}
