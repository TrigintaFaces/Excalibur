// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.ElasticSearch.Monitoring;

namespace Excalibur.Data.ElasticSearch;

/// <summary>
/// Configures the settings for Elasticsearch client connections and operations.
/// </summary>
/// <remarks>
/// This class provides comprehensive configuration options for connecting to Elasticsearch clusters, including endpoint selection,
/// connection pooling, resilience policies, monitoring, performance optimizations, and projection management.
/// Connection-level settings (authentication, timeouts, SSL/TLS) are configured via the <see cref="Connection" /> sub-options.
/// </remarks>
public sealed class ElasticsearchConfigurationOptions
{
	/// <summary>
	/// Gets the URL of the Elasticsearch cluster.
	/// </summary>
	/// <value>
	/// A <see cref="Uri" /> representing the base URL of the Elasticsearch cluster. This property is required when using single-node configuration.
	/// </value>
	public Uri? Url { get; init; }

	/// <summary>
	/// Gets the collection of URLs for multi-node cluster configuration.
	/// </summary>
	/// <value>
	/// A collection of <see cref="Uri" /> representing the URLs of the Elasticsearch cluster nodes. Used for cluster configuration with
	/// connection pooling.
	/// </value>
	public IEnumerable<Uri>? Urls { get; init; }

	/// <summary>
	/// Gets the Elastic Cloud ID for Elastic Cloud deployments.
	/// </summary>
	/// <value>
	/// A <see cref="string" /> representing the Cloud ID for Elastic Cloud connections, or <c> null </c> if not using Elastic Cloud.
	/// </value>
	public string? CloudId { get; init; }

	/// <summary>
	/// Gets the connection pool type for multi-node clusters.
	/// </summary>
	/// <value> The type of connection pool to use. Defaults to <see cref="ConnectionPoolType.Static" />. </value>
	public ConnectionPoolType ConnectionPoolType { get; init; } = ConnectionPoolType.Static;

	/// <summary>
	/// Gets a value indicating whether to enable connection sniffing for dynamic node discovery.
	/// </summary>
	/// <value>
	/// A <see cref="bool" /> indicating whether to enable sniffing. Only applicable when using
	/// <see cref="ConnectionPoolType.Sniffing" />. Defaults to <c> false </c>.
	/// </value>
	public bool EnableSniffing { get; init; }

	/// <summary>
	/// Gets the configuration for connection-level settings including authentication, timeouts, SSL/TLS, and pool tuning.
	/// </summary>
	/// <value> The connection settings for the Elasticsearch client. </value>
	public ElasticsearchConnectionOptions Connection { get; init; } = new();

	/// <summary>
	/// Gets the configuration for resilience policies.
	/// </summary>
	/// <value> The resilience settings for retry and circuit breaker handling. </value>
	public ElasticsearchResilienceOptions Resilience { get; init; } = new();

	/// <summary>
	/// Gets the configuration for monitoring and diagnostics.
	/// </summary>
	/// <value> The monitoring settings for metrics, logging, performance diagnostics, and tracing. </value>
	public ElasticsearchMonitoringOptions Monitoring { get; init; } = new();

	/// <summary>
	/// Gets the configuration for projection management.
	/// </summary>
	/// <value> The projection settings for error handling, rebuilding, and consistency tracking. </value>
	public ProjectionOptions Projections { get; init; } = new();
}
