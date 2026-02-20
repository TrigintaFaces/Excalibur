// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Elastic.Clients.Elasticsearch.Cluster;

namespace Excalibur.Data.ElasticSearch;

/// <summary>
/// Represents a client for performing Elasticsearch health checks.
/// </summary>
public interface IElasticsearchHealthClient
{
	/// <summary>
	/// Gets the cluster health status from Elasticsearch.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The cluster health response. </returns>
	Task<HealthResponse> GetClusterHealthAsync(CancellationToken cancellationToken);
}
