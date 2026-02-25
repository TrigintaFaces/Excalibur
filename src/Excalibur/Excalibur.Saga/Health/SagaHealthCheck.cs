// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Saga.Abstractions;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.Saga.Health;

/// <summary>
/// ASP.NET Core health check for saga infrastructure monitoring.
/// </summary>
/// <remarks>
/// <para>
/// This health check evaluates saga system health based on:
/// <list type="bullet">
/// <item><description><b>Stuck sagas</b>: Non-completed sagas not updated within the threshold</description></item>
/// <item><description><b>Failed sagas</b>: Sagas with failure reasons recorded</description></item>
/// <item><description><b>Running count</b>: Total active sagas (for informational purposes)</description></item>
/// </list>
/// </para>
/// <para>
/// Health status determination:
/// <list type="bullet">
/// <item><description><b>Unhealthy</b>: Stuck count >= <see cref="SagaHealthCheckOptions.UnhealthyStuckThreshold"/></description></item>
/// <item><description><b>Degraded</b>: Failed count >= <see cref="SagaHealthCheckOptions.DegradedFailedThreshold"/></description></item>
/// <item><description><b>Healthy</b>: Neither threshold exceeded</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class SagaHealthCheck : IHealthCheck
{
	private readonly ISagaMonitoringService _monitoring;
	private readonly SagaHealthCheckOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="SagaHealthCheck"/> class.
	/// </summary>
	/// <param name="monitoring">The saga monitoring service.</param>
	/// <param name="options">The health check options.</param>
	public SagaHealthCheck(
		ISagaMonitoringService monitoring,
		SagaHealthCheckOptions options)
	{
		_monitoring = monitoring ?? throw new ArgumentNullException(nameof(monitoring));
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <inheritdoc />
	public async Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken)
	{
		using var activity = SagaActivitySource.StartActivity("SagaHealthCheck");

		try
		{
			// Get stuck saga count
			var stuckSagas = await _monitoring
				.GetStuckSagasAsync(_options.StuckThreshold, _options.StuckLimit, cancellationToken)
				.ConfigureAwait(false);
			var stuckCount = stuckSagas.Count;

			// Get failed saga count
			var failedSagas = await _monitoring
				.GetFailedSagasAsync(_options.FailedLimit, cancellationToken)
				.ConfigureAwait(false);
			var failedCount = failedSagas.Count;

			// Get running saga count for informational purposes
			var runningCount = await _monitoring
				.GetRunningCountAsync(null, cancellationToken)
				.ConfigureAwait(false);

			// Build data dictionary
			var data = new Dictionary<string, object>
			{
				["running"] = runningCount,
				["stuck"] = stuckCount,
				["failed"] = failedCount,
				["stuckThresholdMinutes"] = _options.StuckThreshold.TotalMinutes
			};

			_ = (activity?.SetTag("saga.running", runningCount));
			_ = (activity?.SetTag("saga.stuck", stuckCount));
			_ = (activity?.SetTag("saga.failed", failedCount));

			// Evaluate health status
			if (stuckCount >= _options.UnhealthyStuckThreshold)
			{
				_ = (activity?.SetTag("saga.health", "unhealthy"));
				return HealthCheckResult.Unhealthy(
					$"{stuckCount} stuck sagas exceed threshold of {_options.UnhealthyStuckThreshold}",
					data: data);
			}

			if (failedCount >= _options.DegradedFailedThreshold)
			{
				_ = (activity?.SetTag("saga.health", "degraded"));
				return HealthCheckResult.Degraded(
					$"{failedCount} failed sagas exceed threshold of {_options.DegradedFailedThreshold}",
					data: data);
			}

			_ = (activity?.SetTag("saga.health", "healthy"));
			return HealthCheckResult.Healthy(
				$"{runningCount} sagas running, {stuckCount} stuck, {failedCount} failed",
				data: data);
		}
		catch (Exception ex)
		{
			_ = (activity?.SetTag("saga.health", "error"));
			_ = (activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message));

			return HealthCheckResult.Unhealthy(
				"Saga health check failed",
				exception: ex);
		}
	}
}
