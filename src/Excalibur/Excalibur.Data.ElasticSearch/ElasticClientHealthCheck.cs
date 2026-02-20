// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.Diagnostics.HealthChecks;

using HealthStatus = Elastic.Clients.Elasticsearch.HealthStatus;

namespace Excalibur.Data.ElasticSearch;

/// <summary>
/// Provides health check functionality for Elasticsearch client.
/// </summary>
public sealed class ElasticClientHealthCheck(IElasticsearchHealthClient client) : IHealthCheck
{
	/// <summary>
	/// Performs the health check by querying the Elasticsearch cluster status.
	/// </summary>
	/// <param name="context"> The health check context. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The health check result. </returns>
	public async Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken)
	{
		try
		{
			var response = await client.GetClusterHealthAsync(cancellationToken).ConfigureAwait(false);

			return response.Status switch
			{
				HealthStatus.Green or HealthStatus.Yellow => HealthCheckResult.Healthy($"Cluster status: {response.Status}"),
				HealthStatus.Red => HealthCheckResult.Unhealthy("Cluster is in RED state."),
				_ => HealthCheckResult.Unhealthy("Unknown cluster health status."),
			};
		}
		catch (Exception ex)
		{
			return HealthCheckResult.Unhealthy("Failed to check Elasticsearch cluster health.", ex);
		}
	}
}
