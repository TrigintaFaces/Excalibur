// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Excalibur.Outbox.Health;

/// <summary>
/// Health check for the outbox background processing service.
/// </summary>
/// <remarks>
/// <para>
/// Reports health based on the outbox background service state:
/// </para>
/// <list type="bullet">
/// <item><b>Healthy:</b> Service is running, processing normally with acceptable failure rate.</item>
/// <item><b>Degraded:</b> Service is running but has elevated failure rate or recent inactivity.</item>
/// <item><b>Unhealthy:</b> Service is not running, has high failure rate, or no activity beyond timeout.</item>
/// </list>
/// <para>
/// Register using:
/// <code>
/// builder.Services.AddHealthChecks()
///     .AddOutboxHealthCheck();
/// </code>
/// </para>
/// </remarks>
public sealed class OutboxHealthCheck : IHealthCheck
{
	private readonly BackgroundServiceHealthState _state;
	private readonly OutboxHealthCheckOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="OutboxHealthCheck"/> class.
	/// </summary>
	/// <param name="state">The shared health state updated by the outbox background service.</param>
	/// <param name="options">The health check threshold options.</param>
	public OutboxHealthCheck(
		BackgroundServiceHealthState state,
		IOptions<OutboxHealthCheckOptions> options)
	{
		ArgumentNullException.ThrowIfNull(state);
		ArgumentNullException.ThrowIfNull(options);

		_state = state;
		_options = options.Value;
	}

	/// <inheritdoc/>
	public Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken)
	{
		var data = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["IsRunning"] = _state.IsRunning,
			["TotalProcessed"] = _state.TotalProcessed,
			["TotalFailed"] = _state.TotalFailed,
			["TotalCycles"] = _state.TotalCycles,
		};

		if (_state.LastActivityTime.HasValue)
		{
			data["LastActivityTime"] = _state.LastActivityTime.Value;
		}

		// Not running
		if (!_state.IsRunning)
		{
			return Task.FromResult(HealthCheckResult.Unhealthy(
				"Outbox background service is not running.",
				data: data));
		}

		// Check inactivity
		if (_state.LastActivityTime.HasValue)
		{
			var inactivity = DateTimeOffset.UtcNow - _state.LastActivityTime.Value;
			data["InactivitySeconds"] = inactivity.TotalSeconds;

			if (inactivity > _options.UnhealthyInactivityTimeout)
			{
				return Task.FromResult(HealthCheckResult.Unhealthy(
					$"Outbox background service has been inactive for {inactivity.TotalSeconds:F0}s (threshold: {_options.UnhealthyInactivityTimeout.TotalSeconds:F0}s).",
					data: data));
			}

			if (inactivity > _options.DegradedInactivityTimeout)
			{
				return Task.FromResult(HealthCheckResult.Degraded(
					$"Outbox background service has been inactive for {inactivity.TotalSeconds:F0}s (threshold: {_options.DegradedInactivityTimeout.TotalSeconds:F0}s).",
					data: data));
			}
		}

		// Check failure rate
		var totalProcessed = _state.TotalProcessed;
		var totalFailed = _state.TotalFailed;
		var total = totalProcessed + totalFailed;

		if (total > 0)
		{
			var failureRate = (double)totalFailed / total * 100.0;
			data["FailureRatePercent"] = failureRate;

			if (failureRate >= _options.UnhealthyFailureRatePercent)
			{
				return Task.FromResult(HealthCheckResult.Unhealthy(
					$"Outbox failure rate is {failureRate:F1}% (threshold: {_options.UnhealthyFailureRatePercent:F1}%).",
					data: data));
			}

			if (failureRate >= _options.DegradedFailureRatePercent)
			{
				return Task.FromResult(HealthCheckResult.Degraded(
					$"Outbox failure rate is {failureRate:F1}% (threshold: {_options.DegradedFailureRatePercent:F1}%).",
					data: data));
			}
		}

		return Task.FromResult(HealthCheckResult.Healthy(
			"Outbox background service is processing normally.",
			data: data));
	}
}
