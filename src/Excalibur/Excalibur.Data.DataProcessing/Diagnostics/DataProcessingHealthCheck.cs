// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.DataProcessing.Diagnostics;

/// <summary>
/// Health check for the data processing background service.
/// </summary>
/// <remarks>
/// <para>
/// Reports health based on the data processing service state:
/// </para>
/// <list type="bullet">
/// <item><b>Healthy:</b> Service is running and processing within normal intervals.</item>
/// <item><b>Degraded:</b> Recent inactivity beyond degraded threshold.</item>
/// <item><b>Unhealthy:</b> Service has stopped or no activity beyond unhealthy timeout.</item>
/// </list>
/// <para>
/// Register using:
/// <code>
/// builder.Services.AddHealthChecks()
///     .AddDataProcessingHealthCheck();
/// </code>
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
	"Performance",
	"CA1812:AvoidUninstantiatedInternalClasses",
	Justification = "Instantiated by the DI container via health check registration.")]
internal sealed class DataProcessingHealthCheck : IHealthCheck
{
	private readonly DataProcessingHealthState _state;
	private readonly DataProcessingHealthCheckOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="DataProcessingHealthCheck"/> class.
	/// </summary>
	/// <param name="state">The shared health state updated by the data processing service.</param>
	/// <param name="options">The health check threshold options.</param>
	public DataProcessingHealthCheck(
		DataProcessingHealthState state,
		IOptions<DataProcessingHealthCheckOptions> options)
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
			// Service may legitimately not be running if it hasn't been started yet
			if (_state.TotalCycles == 0)
			{
				return Task.FromResult(HealthCheckResult.Healthy(
					"Data processing service has not been started.",
					data: data));
			}

			return Task.FromResult(HealthCheckResult.Unhealthy(
				"Data processing service is not running.",
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
					$"Data processing service has been inactive for {inactivity.TotalSeconds:F0}s " +
					$"(threshold: {_options.UnhealthyInactivityTimeout.TotalSeconds:F0}s).",
					data: data));
			}

			if (inactivity > _options.DegradedInactivityTimeout)
			{
				return Task.FromResult(HealthCheckResult.Degraded(
					$"Data processing service has been inactive for {inactivity.TotalSeconds:F0}s " +
					$"(threshold: {_options.DegradedInactivityTimeout.TotalSeconds:F0}s).",
					data: data));
			}
		}

		return Task.FromResult(HealthCheckResult.Healthy(
			"Data processing service is operating normally.",
			data: data));
	}
}
