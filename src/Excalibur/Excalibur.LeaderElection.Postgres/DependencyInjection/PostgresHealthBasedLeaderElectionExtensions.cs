// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.LeaderElection;
using Excalibur.Dispatch.LeaderElection.Fencing;
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
			.ValidateOnStart();

		_ = services.AddOptions<PostgresHealthBasedLeaderElectionOptions>()
			.Configure(configureHealth)
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<PostgresHealthBasedLeaderElectionOptions>, PostgresHealthBasedLeaderElectionOptionsValidator>());

		_ = services.AddOptions<LeaderElectionOptions>()
			.Configure(configureElection)
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<PostgresLeaderElectionOptions>, PostgresLeaderElectionOptionsValidator>());
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<LeaderElectionOptions>, LeaderElectionOptionsValidator>());

		services.TryAddSingleton(TimeProvider.System);

		services.TryAddSingleton(sp =>
		{
			var pgOptions = sp.GetRequiredService<IOptions<PostgresLeaderElectionOptions>>();
			var electionOptions = sp.GetRequiredService<IOptions<LeaderElectionOptions>>();
			var healthOptions = sp.GetRequiredService<IOptions<PostgresHealthBasedLeaderElectionOptions>>();
			var logger = sp.GetRequiredService<ILogger<PostgresHealthBasedLeaderElection>>();
			var innerLogger = sp.GetRequiredService<ILogger<PostgresLeaderElection>>();
			// Forwarded to the inner PostgresLeaderElection for fail-closed fencing-token issuance on
			// acquisition — the same auto-registered provider the non-health-based path uses below.
			var fencingTokenProvider = sp.GetService<IFencingTokenProvider>();
			return new PostgresHealthBasedLeaderElection(pgOptions, electionOptions, healthOptions, logger, innerLogger, fencingTokenProvider);
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

		// Also register unkeyed. Consumers of a single leader election resolve ILeaderElection directly —
		// including the outbox leader gate — and a keyed registration does not satisfy an unkeyed request.
		// Without this, a host that registers leader election here resolves nothing and drains unfenced.
		// TryAdd, so a consumer's own unkeyed registration still wins.
		services.TryAddSingleton<ILeaderElection>(sp =>
			sp.GetRequiredKeyedService<ILeaderElection>("default"));

		// Fencing is default-ON for a framework-protected outbox: registering a leader election is the
		// multi-instance signal, so the outbox drain is gated automatically rather than through an
		// easily-forgotten second opt-in. Without this, a host that wires its election here resolves no
		// gate and every instance drains concurrently — the coordination guarantee the election was added
		// to provide, silently absent. Idempotent via TryAdd, so an explicit outbox.WithLeaderElection()
		// composes with it. A single-active-writer topology opts the outbox out with AsSingleWriter().
		OutboxBuilderLeaderElectionExtensions.RegisterOutboxLeaderGate(services);

		// Fencing is on by default: a stalled ex-leader's writes landing after a new leader is
		// elected is silent data corruption, so the safe posture is auto-registering the store's arbitrated
		// provider rather than requiring a second, easily-forgotten AddPostgresFencingTokenProvider() +
		// WithFencingTokens() call. WithoutFencingTokens() opts out.
		services.TryAddDefaultFencingTokenProvider(sp =>
			new PostgresFencingTokenProvider(sp.GetRequiredService<IOptions<PostgresLeaderElectionOptions>>().Value.ConnectionString));

		return services;
	}
}
