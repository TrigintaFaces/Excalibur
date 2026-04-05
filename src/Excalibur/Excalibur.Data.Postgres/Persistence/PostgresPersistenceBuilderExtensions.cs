// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Postgres.Persistence;

/// <summary>
/// Extension methods for <see cref="IPostgresPersistenceBuilder"/>.
/// </summary>
public static class PostgresPersistenceBuilderExtensions
{
	/// <summary>
	/// Enables or disables metrics collection.
	/// </summary>
	/// <param name="builder"> The builder. </param>
	/// <param name="enabled"> Whether to enable metrics. </param>
	/// <returns> The builder for chaining. </returns>
	public static IPostgresPersistenceBuilder WithMetrics(this IPostgresPersistenceBuilder builder, bool enabled = true)
	{
		ArgumentNullException.ThrowIfNull(builder);
		_ = builder.Services.Configure<PostgresPersistenceOptions>(options => options.EnableMetrics = enabled);
		return builder;
	}

	/// <summary>
	/// Enables or disables detailed logging.
	/// </summary>
	/// <param name="builder"> The builder. </param>
	/// <param name="enabled"> Whether to enable detailed logging. </param>
	/// <returns> The builder for chaining. </returns>
	public static IPostgresPersistenceBuilder WithDetailedLogging(this IPostgresPersistenceBuilder builder, bool enabled = false)
	{
		ArgumentNullException.ThrowIfNull(builder);
		_ = builder.Services.Configure<PostgresPersistenceOptions>(options => options.EnableDetailedLogging = enabled);
		return builder;
	}

	/// <summary>
	/// Configures prepared statement caching.
	/// </summary>
	/// <param name="builder"> The builder. </param>
	/// <param name="enabled"> Whether to enable prepared statement caching. </param>
	/// <param name="maxStatements"> Maximum number of prepared statements to cache. </param>
	/// <returns> The builder for chaining. </returns>
	public static IPostgresPersistenceBuilder WithPreparedStatements(this IPostgresPersistenceBuilder builder, bool enabled = true, int maxStatements = 200)
	{
		ArgumentNullException.ThrowIfNull(builder);
		_ = builder.Services.Configure<PostgresPersistenceOptions>(options =>
		{
			options.Statements.EnablePreparedStatementCaching = enabled;
			options.Statements.MaxPreparedStatements = maxStatements;
		});
		return builder;
	}

	/// <summary>
	/// Sets the application name for connection identification.
	/// </summary>
	/// <param name="builder"> The builder. </param>
	/// <param name="applicationName"> The application name. </param>
	/// <returns> The builder for chaining. </returns>
	public static IPostgresPersistenceBuilder WithApplicationName(this IPostgresPersistenceBuilder builder, string applicationName)
	{
		ArgumentNullException.ThrowIfNull(builder);
		_ = builder.Services.Configure<PostgresPersistenceOptions>(options => options.Connection.ApplicationName = applicationName);
		return builder;
	}

	/// <summary>
	/// Adds a custom health check.
	/// </summary>
	/// <param name="builder"> The builder. </param>
	/// <param name="name"> The health check name. </param>
	/// <param name="tags"> Health check tags. </param>
	/// <returns> The builder for chaining. </returns>
	public static IPostgresPersistenceBuilder AddHealthCheck(this IPostgresPersistenceBuilder builder, string name = "Postgres", params string[] tags)
	{
		ArgumentNullException.ThrowIfNull(builder);
		_ = builder.Services.Configure<HealthCheckServiceOptions>(options => options.Registrations.Add(new HealthCheckRegistration(
			name,
			provider => new PostgresPersistenceHealthCheck(
				provider.GetRequiredService<IOptions<PostgresPersistenceOptions>>(),
				provider.GetRequiredService<ILogger<PostgresPersistenceHealthCheck>>(),
				provider.GetService<PostgresPersistenceMetrics>()),
			HealthStatus.Unhealthy,
			tags)));
		return builder;
	}
}
