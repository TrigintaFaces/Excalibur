// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using HealthChecks.UI.Client;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HealthChecksSample.HealthChecks;

/// <summary>
/// Builds the options for a Kubernetes probe endpoint.
/// </summary>
internal static class HealthProbeOptions
{
	/// <summary>
	/// Creates options that run only the checks carrying <paramref name="tag" />.
	/// </summary>
	/// <param name="tag">The health-check tag the probe selects, such as <c>live</c> or <c>ready</c>.</param>
	/// <returns>The probe's health-check options.</returns>
	/// <remarks>
	/// Degraded still answers 200, so a slow dependency does not get the pod restarted or pulled from
	/// the load balancer. Only Unhealthy answers 503.
	/// </remarks>
	public static HealthCheckOptions For(string tag) => new()
	{
		ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
		Predicate = check => check.Tags.Contains(tag),
		ResultStatusCodes =
		{
			[HealthStatus.Healthy] = StatusCodes.Status200OK,
			[HealthStatus.Degraded] = StatusCodes.Status200OK,
			[HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
		},
	};
}
