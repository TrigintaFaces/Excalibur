// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Health check that verifies the Dispatch caching subsystem is reachable and operational.
/// </summary>
/// <remarks>
/// Delegates to the registered <see cref="ICacheHealthMonitor"/> to perform a lightweight
/// connectivity probe. Register via <c>AddHealthChecks().AddCheck&lt;CacheHealthCheck&gt;("dispatch-cache")</c>.
/// </remarks>
public sealed class CacheHealthCheck : IHealthCheck
{
	private readonly ICacheHealthMonitor _monitor;

	/// <summary>
	/// Initializes a new instance of the <see cref="CacheHealthCheck"/> class.
	/// </summary>
	/// <param name="monitor">The cache health monitor to probe.</param>
	public CacheHealthCheck(ICacheHealthMonitor monitor)
	{
		_monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
	}

	/// <inheritdoc />
	public async Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken)
	{
		try
		{
			var status = await _monitor.GetHealthStatusAsync(cancellationToken).ConfigureAwait(false);

			if (status.IsHealthy)
			{
				return HealthCheckResult.Healthy(
					$"Cache is reachable ({status.ConnectionStatus}, {status.ResponseTimeMs:F1}ms).");
			}

			return new HealthCheckResult(
				context?.Registration?.FailureStatus ?? HealthStatus.Degraded,
				$"Cache reports unhealthy: {status.ConnectionStatus}.");
		}
		catch (Exception ex)
		{
			return HealthCheckResult.Unhealthy("Cache health check failed.", ex);
		}
	}
}
