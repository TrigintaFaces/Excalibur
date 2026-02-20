// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Health;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for adding saga health checks.
/// </summary>
public static class SagaHealthCheckExtensions
{
	/// <summary>
	/// Adds a saga health check to the health checks builder.
	/// </summary>
	/// <param name="builder">The health checks builder.</param>
	/// <param name="name">The name of the health check. Default is "sagas".</param>
	/// <param name="failureStatus">
	/// The health status to report when the check fails. If null, the default failure status is used.
	/// </param>
	/// <param name="tags">Optional tags to associate with the health check.</param>
	/// <param name="configure">Optional action to configure health check options.</param>
	/// <returns>The health checks builder for chaining.</returns>
	/// <remarks>
	/// <para>
	/// This health check requires an <see cref="ISagaMonitoringService"/> to be registered.
	/// Use <c>AddSqlServerSagaMonitoringService</c> or register a custom implementation.
	/// </para>
	/// <para>
	/// Example usage:
	/// <code>
	/// services.AddSqlServerSagaMonitoringService(connectionString);
	/// services.AddHealthChecks()
	///     .AddSagaHealthCheck(configure: options =>
	///     {
	///         options.StuckThreshold = TimeSpan.FromMinutes(30);
	///         options.UnhealthyStuckThreshold = 5;
	///     });
	/// </code>
	/// </para>
	/// </remarks>
	public static IHealthChecksBuilder AddSagaHealthCheck(
		this IHealthChecksBuilder builder,
		string name = "sagas",
		HealthStatus? failureStatus = null,
		IEnumerable<string>? tags = null,
		Action<SagaHealthCheckOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		var options = new SagaHealthCheckOptions();
		configure?.Invoke(options);

		return builder.Add(new HealthCheckRegistration(
			name,
			sp => new SagaHealthCheck(
				sp.GetRequiredService<ISagaMonitoringService>(),
				options),
			failureStatus,
			tags));
	}

	/// <summary>
	/// Adds a saga health check to the health checks builder with options from configuration.
	/// </summary>
	/// <param name="builder">The health checks builder.</param>
	/// <param name="options">The preconfigured health check options.</param>
	/// <param name="name">The name of the health check. Default is "sagas".</param>
	/// <param name="failureStatus">
	/// The health status to report when the check fails. If null, the default failure status is used.
	/// </param>
	/// <param name="tags">Optional tags to associate with the health check.</param>
	/// <returns>The health checks builder for chaining.</returns>
	public static IHealthChecksBuilder AddSagaHealthCheck(
		this IHealthChecksBuilder builder,
		SagaHealthCheckOptions options,
		string name = "sagas",
		HealthStatus? failureStatus = null,
		IEnumerable<string>? tags = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(options);

		return builder.Add(new HealthCheckRegistration(
			name,
			sp => new SagaHealthCheck(
				sp.GetRequiredService<ISagaMonitoringService>(),
				options),
			failureStatus,
			tags));
	}
}
