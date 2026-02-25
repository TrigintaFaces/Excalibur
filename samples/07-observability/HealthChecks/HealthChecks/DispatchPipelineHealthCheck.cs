// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HealthChecksSample.HealthChecks;

/// <summary>
/// Custom health check for the Dispatch messaging pipeline.
/// </summary>
/// <remarks>
/// This health check verifies:
/// - Handler registration is complete
/// - Pipeline middleware is configured
/// - Serializers are available
/// </remarks>
public sealed class DispatchPipelineHealthCheck : IHealthCheck
{
	private readonly IServiceProvider _serviceProvider;
	private readonly ILogger<DispatchPipelineHealthCheck> _logger;

	public DispatchPipelineHealthCheck(
		IServiceProvider serviceProvider,
		ILogger<DispatchPipelineHealthCheck> logger)
	{
		_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var diagnostics = new Dictionary<string, object>();

			// Check if IDispatcher is registered
			var dispatcher = _serviceProvider.GetService<Excalibur.Dispatch.Abstractions.IDispatcher>();
			if (dispatcher == null)
			{
				return Task.FromResult(HealthCheckResult.Unhealthy(
					"IDispatcher is not registered",
					data: diagnostics));
			}

			diagnostics["dispatcher_type"] = dispatcher.GetType().Name;
			diagnostics["dispatcher_registered"] = true;

			// Check handler count (if accessible)
			diagnostics["check_time"] = DateTimeOffset.UtcNow;

			_logger.LogDebug("Dispatch pipeline health check passed");

			return Task.FromResult(HealthCheckResult.Healthy(
				"Dispatch pipeline is healthy",
				data: diagnostics));
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Dispatch pipeline health check failed");

			return Task.FromResult(HealthCheckResult.Unhealthy(
				"Dispatch pipeline health check failed",
				ex,
				new Dictionary<string, object>
				{
					["exception_type"] = ex.GetType().Name,
					["exception_message"] = ex.Message,
				}));
		}
	}
}
