// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.Dispatch.Abstractions.Transport;

using Microsoft.Extensions.Diagnostics.HealthChecks;

using MsHealthCheckContext = Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext;
using MsHealthCheckResult = Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Health check that monitors all registered transports in the TransportRegistry.
/// </summary>
/// <remarks>
/// <para>
/// This health check aggregates the status of all registered transports:
/// </para>
/// <list type="bullet">
/// <item><description>Healthy: All transports are running</description></item>
/// <item><description>Degraded: At least one transport is not running, but default transport is healthy</description></item>
/// <item><description>Unhealthy: No transports registered, default transport not running, or critical failures</description></item>
/// </list>
/// </remarks>
public sealed class MultiTransportHealthCheck : IHealthCheck
{
	private readonly TransportRegistry _registry;
	private readonly MultiTransportHealthCheckOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="MultiTransportHealthCheck"/> class.
	/// </summary>
	/// <param name="registry">The transport registry to monitor.</param>
	/// <param name="options">Optional health check options.</param>
	public MultiTransportHealthCheck(
		TransportRegistry registry,
		MultiTransportHealthCheckOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(registry);
		_registry = registry;
		_options = options ?? new MultiTransportHealthCheckOptions();
	}

	/// <inheritdoc/>
	public async Task<MsHealthCheckResult> CheckHealthAsync(
		MsHealthCheckContext context,
		CancellationToken cancellationToken)
	{
		var stopwatch = ValueStopwatch.StartNew();
		var data = new Dictionary<string, object>(StringComparer.Ordinal);
		var transports = _registry.GetAllTransports();

		data["TransportCount"] = transports.Count;
		data["HasDefaultTransport"] = _registry.HasDefaultTransport;
		data["DefaultTransportName"] = _registry.DefaultTransportName ?? "None";

		// No transports registered
		if (transports.Count == 0)
		{
			data["Duration"] = stopwatch.Elapsed.TotalMilliseconds;

			if (_options.RequireAtLeastOneTransport)
			{
				return MsHealthCheckResult.Unhealthy(
					"No transports registered in TransportRegistry.",
					data: data);
			}

			return MsHealthCheckResult.Healthy(
				"No transports registered (this may be intentional).",
				data: data);
		}

		var healthyCount = 0;
		var degradedCount = 0;
		var unhealthyCount = 0;
		var transportStatuses = new Dictionary<string, object>(StringComparer.Ordinal);

		foreach (var (name, registration) in transports)
		{
			cancellationToken.ThrowIfCancellationRequested();

			var transportData = new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["TransportType"] = registration.TransportType,
				["IsRunning"] = registration.Adapter.IsRunning,
				["IsDefault"] = name == _registry.DefaultTransportName
			};

			// Check if adapter implements detailed health checking
			if (registration.Adapter is ITransportHealthChecker healthChecker)
			{
				try
				{
					var healthResult = await healthChecker.CheckQuickHealthAsync(cancellationToken)
						.ConfigureAwait(false);

					transportData["Status"] = healthResult.Status.ToString();
					transportData["Description"] = healthResult.Description;

					// Merge health check data
					foreach (var (key, value) in healthResult.Data)
					{
						transportData[$"Health_{key}"] = value;
					}

					switch (healthResult.Status)
					{
						case TransportHealthStatus.Healthy:
							healthyCount++;
							break;
						case TransportHealthStatus.Degraded:
							degradedCount++;
							break;
						case TransportHealthStatus.Unhealthy:
							unhealthyCount++;
							break;
						default:
							unhealthyCount++;
							break;
					}
				}
				catch (Exception ex)
				{
					transportData["Status"] = "Error";
					transportData["Error"] = ex.Message;
					unhealthyCount++;
				}
			}
			else
			{
				// Fall back to IsRunning check
				if (registration.Adapter.IsRunning)
				{
					transportData["Status"] = "Running";
					healthyCount++;
				}
				else
				{
					transportData["Status"] = "NotRunning";
					unhealthyCount++;
				}
			}

			transportStatuses[name] = transportData;
		}

		data["Transports"] = transportStatuses;
		data["HealthyCount"] = healthyCount;
		data["DegradedCount"] = degradedCount;
		data["UnhealthyCount"] = unhealthyCount;
		data["Duration"] = stopwatch.Elapsed.TotalMilliseconds;

		// Determine overall status
		var defaultTransportHealthy = await IsDefaultTransportHealthyAsync(transports, cancellationToken).ConfigureAwait(false);

		// All unhealthy
		if (unhealthyCount == transports.Count)
		{
			return MsHealthCheckResult.Unhealthy(
				$"All {transports.Count} transports are unhealthy.",
				data: data);
		}

		// Default transport is unhealthy (critical)
		if (_options.RequireDefaultTransportHealthy && _registry.HasDefaultTransport && !defaultTransportHealthy)
		{
			return MsHealthCheckResult.Unhealthy(
				$"Default transport '{_registry.DefaultTransportName}' is not healthy.",
				data: data);
		}

		// Some transports are degraded or unhealthy
		if (degradedCount > 0 || unhealthyCount > 0)
		{
			return MsHealthCheckResult.Degraded(
				$"{healthyCount}/{transports.Count} transports healthy, {degradedCount} degraded, {unhealthyCount} unhealthy.",
				data: data);
		}

		// All healthy
		return MsHealthCheckResult.Healthy(
			$"All {transports.Count} transports are healthy.",
			data: data);
	}

	private async Task<bool> IsDefaultTransportHealthyAsync(
		IReadOnlyDictionary<string, TransportRegistration> transports,
		CancellationToken cancellationToken)
	{
		if (!_registry.HasDefaultTransport || _registry.DefaultTransportName is null)
		{
			return true; // No default means nothing to check
		}

		if (!transports.TryGetValue(_registry.DefaultTransportName, out var defaultRegistration))
		{
			return false; // Default transport not found (should not happen)
		}

		// Check if running at minimum
		if (!defaultRegistration.Adapter.IsRunning)
		{
			return false;
		}

		// If it has a health checker, use that
		if (defaultRegistration.Adapter is ITransportHealthChecker healthChecker)
		{
			try
			{
				var result = await healthChecker.CheckQuickHealthAsync(cancellationToken).ConfigureAwait(false);

				return result.Status != TransportHealthStatus.Unhealthy;
			}
			catch
			{
				return false;
			}
		}

		return true;
	}
}
