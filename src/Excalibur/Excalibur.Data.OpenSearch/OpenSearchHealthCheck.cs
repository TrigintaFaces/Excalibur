// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Diagnostics.HealthChecks;

using OpenSearch.Client;

namespace Excalibur.Data.OpenSearch;

/// <summary>
/// Provides health check functionality for OpenSearch clusters.
/// </summary>
/// <remarks>
/// Queries the <c>/_cluster/health</c> endpoint which is compatible between
/// Elasticsearch and OpenSearch.
/// </remarks>
internal sealed class OpenSearchHealthCheck(OpenSearchClient client) : IHealthCheck
{
	/// <inheritdoc/>
	public async Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken)
	{
		try
		{
			var response = await client.Cluster.HealthAsync(new ClusterHealthRequest(), cancellationToken)
				.ConfigureAwait(false);

			if (!response.IsValid)
			{
				return HealthCheckResult.Unhealthy(
					$"OpenSearch cluster health check failed: {response.ServerError?.Error?.Reason ?? "Unknown error"}");
			}

			var statusStr = response.Status.ToString();

			return statusStr switch
			{
				"green" or "Green" or "yellow" or "Yellow" =>
					HealthCheckResult.Healthy($"Cluster status: {statusStr}"),
				"red" or "Red" =>
					HealthCheckResult.Unhealthy("Cluster is in RED state."),
				_ => HealthCheckResult.Unhealthy($"Unknown cluster health status: {statusStr}"),
			};
		}
		catch (Exception ex)
		{
			return HealthCheckResult.Unhealthy("Failed to check OpenSearch cluster health.", ex);
		}
	}
}
