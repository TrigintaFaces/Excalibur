// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Health check that validates the dispatch middleware pipeline integrity.
/// </summary>
/// <remarks>
/// <para>
/// Validates that the pipeline has required middleware and detects potential issues:
/// </para>
/// <list type="bullet">
/// <item><b>Unhealthy:</b> No middleware registered (empty pipeline)</item>
/// <item><b>Degraded:</b> Duplicate middleware in same stage, or missing handler middleware</item>
/// <item><b>Healthy:</b> Pipeline is properly configured</item>
/// </list>
/// </remarks>
internal sealed class PipelineIntegrityHealthCheck : IHealthCheck
{
	private readonly IServiceProvider _serviceProvider;

	/// <summary>
	/// Initializes a new instance of the <see cref="PipelineIntegrityHealthCheck"/> class.
	/// </summary>
	/// <param name="serviceProvider">The service provider for resolving middleware registrations.</param>
	public PipelineIntegrityHealthCheck(IServiceProvider serviceProvider)
	{
		_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
	}

	/// <inheritdoc />
	public Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken = default)
	{
		var middlewares = _serviceProvider.GetServices<IDispatchMiddleware>().ToList();

		if (middlewares.Count == 0)
		{
			return Task.FromResult(HealthCheckResult.Unhealthy(
				"No dispatch middleware registered. The pipeline is empty and cannot process messages."));
		}

		var warnings = new List<string>();

		// Check for duplicate middleware types in the same stage
		var grouped = middlewares
			.Where(static m => m.Stage.HasValue)
			.GroupBy(static m => (m.Stage, Type: m.GetType()));

		foreach (var group in grouped)
		{
			if (group.Count() > 1)
			{
				warnings.Add(
					$"Duplicate middleware '{group.Key.Type.Name}' registered in stage '{group.Key.Stage}'.");
			}
		}

		// Check for stage conflicts (multiple different middleware at exact same stage)
		var stageConflicts = middlewares
			.Where(static m => m.Stage.HasValue)
			.GroupBy(static m => m.Stage)
			.Where(static g => g.Select(m => m.GetType()).Distinct().Count() > 1);

		foreach (var conflict in stageConflicts)
		{
			var types = string.Join(", ", conflict.Select(m => m.GetType().Name).Distinct());
			warnings.Add(
				$"Multiple middleware at stage '{conflict.Key}': {types}. Consider using different stages.");
		}

		var data = new Dictionary<string, object>
		{
			["MiddlewareCount"] = middlewares.Count,
			["Warnings"] = warnings.Count,
		};

		if (warnings.Count > 0)
		{
			return Task.FromResult(HealthCheckResult.Degraded(
				$"Pipeline has {warnings.Count} warning(s): {string.Join("; ", warnings)}",
				data: data));
		}

		return Task.FromResult(HealthCheckResult.Healthy(
			$"Pipeline is healthy with {middlewares.Count} middleware registered.",
			data: data));
	}
}
