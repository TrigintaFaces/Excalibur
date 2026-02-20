// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.SqlServer.Cdc;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.Data.SqlServer;

/// <summary>
/// Provides extension methods to configure health checks for SQL Server connections.
/// </summary>
public static class HealthChecksBuilderExtensions
{
	/// <summary>
	/// Adds a SQL Server health check to the health checks builder using a connection string from configuration.
	/// </summary>
	/// <param name="healthChecks"> The health checks builder to which the health check will be added. </param>
	/// <param name="configuration"> The application configuration containing the connection string. </param>
	/// <param name="name"> The name of the health check and the connection string key in the configuration. </param>
	/// <param name="timeout"> The timeout duration for the health check operation. </param>
	/// <returns> The updated health checks builder. </returns>
	/// <exception cref="ArgumentException"> Thrown if <paramref name="name" /> is null, empty, or whitespace. </exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown if the connection string with the specified name is not found in the configuration.
	/// </exception>
	public static IHealthChecksBuilder AddSqlHealthCheck(
		this IHealthChecksBuilder healthChecks,
		IConfiguration configuration,
		string name,
		TimeSpan timeout)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		var connectionString = configuration.GetConnectionString(name);
		return AddSqlHealthCheck(healthChecks, connectionString, name, timeout);
	}

	/// <summary>
	/// Adds a SQL Server health check to the health checks builder using the specified connection string.
	/// </summary>
	/// <param name="healthChecks"> The health checks builder to which the health check will be added. </param>
	/// <param name="connectionString"> The connection string for the SQL Server. </param>
	/// <param name="name"> The name of the health check. </param>
	/// <param name="timeout"> The timeout duration for the health check operation. </param>
	/// <returns> The updated health checks builder. </returns>
	/// <exception cref="ArgumentException"> Thrown if <paramref name="name" /> is null, empty, or whitespace. </exception>
	/// <exception cref="InvalidOperationException"> Thrown if <paramref name="connectionString" /> is null or empty. </exception>
	public static IHealthChecksBuilder AddSqlHealthCheck(
		this IHealthChecksBuilder healthChecks,
		string? connectionString,
		string name,
		TimeSpan timeout)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		if (string.IsNullOrEmpty(connectionString))
		{
			throw new InvalidOperationException($"Could not find a connection string named '{name}");
		}

		_ = healthChecks.AddSqlServer(
			connectionString,
			healthQuery: "select 1",
			name: name,
			failureStatus: HealthStatus.Unhealthy,
			tags: ["Feedback", "Database"],
			timeout: timeout);

		return healthChecks;
	}

	/// <summary>
	/// Adds a health check for the CDC (Change Data Capture) processor.
	/// </summary>
	/// <param name="builder">The health checks builder.</param>
	/// <param name="configure">Optional configuration for health check thresholds.</param>
	/// <param name="name">The health check name. Default is "cdc".</param>
	/// <param name="failureStatus">The failure status. Default is null (uses context default).</param>
	/// <param name="tags">Optional tags for filtering health checks.</param>
	/// <returns>The health checks builder for chaining.</returns>
	public static IHealthChecksBuilder AddCdcHealthCheck(
		this IHealthChecksBuilder builder,
		Action<CdcHealthCheckOptions>? configure = null,
		string name = "cdc",
		HealthStatus? failureStatus = null,
		IEnumerable<string>? tags = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		if (configure is not null)
		{
			_ = builder.Services.Configure(configure);
		}

		builder.Services.TryAddSingleton<CdcHealthState>();

		return builder.Add(new HealthCheckRegistration(
			name,
			sp => ActivatorUtilities.CreateInstance<CdcHealthCheck>(sp),
			failureStatus,
			tags));
	}
}
