// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Excalibur.Outbox.Health;

/// <summary>
/// Health check for the inbox background processing service.
/// </summary>
/// <remarks>
/// <para>
/// Reports health based on the inbox background service state:
/// </para>
/// <list type="bullet">
/// <item><b>Healthy:</b> Service is running and processing messages.</item>
/// <item><b>Degraded:</b> Service is running but inactive beyond the degraded threshold.</item>
/// <item><b>Unhealthy:</b> Service is not running or inactive beyond the unhealthy threshold.</item>
/// </list>
/// <para>
/// Register using:
/// <code>
/// builder.Services.AddHealthChecks()
///     .AddInboxHealthCheck();
/// </code>
/// </para>
/// </remarks>
public sealed class InboxHealthCheck : IHealthCheck
{
	private readonly BackgroundServiceHealthState _state;
	private readonly InboxHealthCheckOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="InboxHealthCheck"/> class.
	/// </summary>
	/// <param name="state">The shared health state updated by the inbox background service.</param>
	/// <param name="options">The health check threshold options.</param>
	public InboxHealthCheck(
		BackgroundServiceHealthState state,
		IOptions<InboxHealthCheckOptions> options)
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
				"Inbox background service is not running.",
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
					$"Inbox background service has been inactive for {inactivity.TotalSeconds:F0}s (threshold: {_options.UnhealthyInactivityTimeout.TotalSeconds:F0}s).",
					data: data));
			}

			if (inactivity > _options.DegradedInactivityTimeout)
			{
				return Task.FromResult(HealthCheckResult.Degraded(
					$"Inbox background service has been inactive for {inactivity.TotalSeconds:F0}s (threshold: {_options.DegradedInactivityTimeout.TotalSeconds:F0}s).",
					data: data));
			}
		}

		return Task.FromResult(HealthCheckResult.Healthy(
			"Inbox background service is processing normally.",
			data: data));
	}
}
