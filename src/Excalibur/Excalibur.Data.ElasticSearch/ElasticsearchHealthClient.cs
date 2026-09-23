// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Cluster;

namespace Excalibur.Data.ElasticSearch;

/// <summary>
/// Provides a wrapper around the Elasticsearch client for health checks.
/// </summary>
public sealed class ElasticsearchHealthClient(ElasticsearchClient client) : IElasticsearchHealthClient
{
	/// <inheritdoc />
	public Task<HealthResponse> GetClusterHealthAsync(CancellationToken cancellationToken)
		=> client.Cluster.HealthAsync(cancellationToken: cancellationToken);
}
