// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.SqlServer.Cdc;

/// <summary>
/// Health check for the CDC (Change Data Capture) processor.
/// </summary>
/// <remarks>
/// <para>
/// Reports health based on the CDC processor state:
/// </para>
/// <list type="bullet">
/// <item><b>Healthy:</b> Producer and consumer are running, processing is current.</item>
/// <item><b>Degraded:</b> Recent inactivity beyond degraded threshold.</item>
/// <item><b>Unhealthy:</b> Processor has failed, or no activity beyond timeout.</item>
/// </list>
/// <para>
/// Register using:
/// <code>
/// builder.Services.AddHealthChecks()
///     .AddCdcHealthCheck();
/// </code>
/// </para>
/// </remarks>
public sealed class CdcHealthCheck : IHealthCheck
{
	private readonly CdcHealthState _state;
	private readonly CdcHealthCheckOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="CdcHealthCheck"/> class.
	/// </summary>
	/// <param name="state">The shared health state updated by the CDC processor.</param>
	/// <param name="options">The health check threshold options.</param>
	public CdcHealthCheck(
		CdcHealthState state,
		IOptions<CdcHealthCheckOptions> options)
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
			// CDC processor may legitimately not be running if it hasn't been started yet
			if (_state.TotalCycles == 0)
			{
				return Task.FromResult(HealthCheckResult.Healthy(
					"CDC processor has not been started.",
					data: data));
			}

			return Task.FromResult(HealthCheckResult.Unhealthy(
				"CDC processor is not running.",
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
					$"CDC processor has been inactive for {inactivity.TotalSeconds:F0}s (threshold: {_options.UnhealthyInactivityTimeout.TotalSeconds:F0}s).",
					data: data));
			}

			if (inactivity > _options.DegradedInactivityTimeout)
			{
				return Task.FromResult(HealthCheckResult.Degraded(
					$"CDC processor has been inactive for {inactivity.TotalSeconds:F0}s (threshold: {_options.DegradedInactivityTimeout.TotalSeconds:F0}s).",
					data: data));
			}
		}

		return Task.FromResult(HealthCheckResult.Healthy(
			"CDC processor is operating normally.",
			data: data));
	}
}
