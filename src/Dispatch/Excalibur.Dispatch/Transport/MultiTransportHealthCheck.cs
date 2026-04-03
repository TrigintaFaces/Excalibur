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
internal sealed class MultiTransportHealthCheck : IHealthCheck
{
	private readonly ITransportRegistry _registry;
	private readonly MultiTransportHealthCheckOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="MultiTransportHealthCheck"/> class.
	/// </summary>
	/// <param name="registry">The transport registry to monitor.</param>
	/// <param name="options">Optional health check options.</param>
	public MultiTransportHealthCheck(
		ITransportRegistry registry,
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

		// Admin properties available from concrete registry.
		var concreteRegistry = _registry as TransportRegistry;
		var hasDefault = concreteRegistry?.HasDefaultTransport ?? false;
		var defaultName = concreteRegistry?.DefaultTransportName;

		var transportNames = _registry.GetTransportNames().ToList();
		var transportCount = transportNames.Count;

		data["TransportCount"] = transportCount;
		data["HasDefaultTransport"] = hasDefault;
		data["DefaultTransportName"] = defaultName ?? "None";

		// No transports registered
		if (transportCount == 0)
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

		foreach (var name in transportNames)
		{
			cancellationToken.ThrowIfCancellationRequested();

			var adapter = _registry.GetTransportAdapter(name);
			if (adapter is null)
			{
				continue;
			}

			var transportData = new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["TransportType"] = adapter.TransportType,
				["IsRunning"] = adapter.IsRunning,
				["IsDefault"] = name == defaultName
			};

			// Check if adapter implements detailed health checking
			if (adapter is ITransportHealthChecker healthChecker)
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
				if (adapter.IsRunning)
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
		var defaultTransportHealthy = await IsDefaultTransportHealthyAsync(defaultName, cancellationToken).ConfigureAwait(false);

		// All unhealthy
		if (unhealthyCount == transportCount)
		{
			return MsHealthCheckResult.Unhealthy(
				$"All {transportCount} transports are unhealthy.",
				data: data);
		}

		// Default transport is unhealthy (critical)
		if (_options.RequireDefaultTransportHealthy && hasDefault && !defaultTransportHealthy)
		{
			return MsHealthCheckResult.Unhealthy(
				$"Default transport '{defaultName}' is not healthy.",
				data: data);
		}

		// Some transports are degraded or unhealthy
		if (degradedCount > 0 || unhealthyCount > 0)
		{
			return MsHealthCheckResult.Degraded(
				$"{healthyCount}/{transportCount} transports healthy, {degradedCount} degraded, {unhealthyCount} unhealthy.",
				data: data);
		}

		// All healthy
		return MsHealthCheckResult.Healthy(
			$"All {transportCount} transports are healthy.",
			data: data);
	}

	private async Task<bool> IsDefaultTransportHealthyAsync(
		string? defaultTransportName,
		CancellationToken cancellationToken)
	{
		if (defaultTransportName is null)
		{
			return true; // No default means nothing to check
		}

		var defaultAdapter = _registry.GetTransportAdapter(defaultTransportName);
		if (defaultAdapter is null)
		{
			return false; // Default transport not found
		}

		// Check if running at minimum
		if (!defaultAdapter.IsRunning)
		{
			return false;
		}

		// If it has a health checker, use that
		if (defaultAdapter is ITransportHealthChecker healthChecker)
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
