// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.ElasticSearch.Monitoring;

namespace Excalibur.Data.ElasticSearch;

/// <summary>
/// Configures the settings for Elasticsearch client connections and operations.
/// </summary>
/// <remarks>
/// This class provides comprehensive configuration options for connecting to Elasticsearch clusters, including authentication,
/// connection pooling, SSL/TLS settings, resilience policies, monitoring, performance optimizations, and projection management.
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
	/// Gets the certificate fingerprint for SSL/TLS verification.
	/// </summary>
	/// <value> A <see cref="string" /> representing the certificate fingerprint for secure connections, or <c> null </c> if not required. </value>
	public string? CertificateFingerprint { get; init; }

	/// <summary>
	/// Gets the username for basic authentication.
	/// </summary>
	/// <value> A <see cref="string" /> representing the username, or <c> null </c> if basic authentication is not used. </value>
	public string? Username { get; init; }

	/// <summary>
	/// Gets the password for basic authentication.
	/// </summary>
	/// <value> A <see cref="string" /> representing the password, or <c> null </c> if basic authentication is not used. </value>
	public string? Password { get; init; }

	/// <summary>
	/// Gets the API key for Elasticsearch authentication.
	/// </summary>
	/// <value> A <see cref="string" /> representing the API key, or <c> null </c> if API key authentication is not used. </value>
	public string? ApiKey { get; init; }

	/// <summary>
	/// Gets the Base64-encoded API key for Elasticsearch authentication.
	/// </summary>
	/// <value>
	/// A <see cref="string" /> representing the Base64-encoded API key, or <c> null </c> if Base64 API key authentication is not used.
	/// </value>
	public string? Base64ApiKey { get; init; }

	/// <summary>
	/// Gets the request timeout for Elasticsearch operations.
	/// </summary>
	/// <value> A <see cref="TimeSpan" /> representing the request timeout. Defaults to 30 seconds. </value>
	public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets the ping timeout for node health checks.
	/// </summary>
	/// <value> A <see cref="TimeSpan" /> representing the ping timeout. Defaults to 5 seconds. </value>
	public TimeSpan PingTimeout { get; init; } = TimeSpan.FromSeconds(5);

	/// <summary>
	/// Gets the maximum number of connections per node.
	/// </summary>
	/// <value> An <see cref="int" /> representing the maximum connections per node. Defaults to 80. </value>
	public int MaximumConnectionsPerNode { get; init; } = 80;

	/// <summary>
	/// Gets a value indicating whether to disable SSL certificate validation.
	/// </summary>
	/// <value> A <see cref="bool" /> indicating whether to disable SSL certificate validation. Defaults to <c> false </c>. </value>
	public bool DisableCertificateValidation { get; init; }

	/// <summary>
	/// Gets a value indicating whether to enable connection sniffing for dynamic node discovery.
	/// </summary>
	/// <value>
	/// A <see cref="bool" /> indicating whether to enable sniffing. Only applicable when using
	/// <see cref="ConnectionPoolType.Sniffing" />. Defaults to <c> false </c>.
	/// </value>
	public bool EnableSniffing { get; init; }

	/// <summary>
	/// Gets the interval for sniffing node information.
	/// </summary>
	/// <value> A <see cref="TimeSpan" /> representing the sniffing interval. Defaults to 1 hour. </value>
	public TimeSpan SniffingInterval { get; init; } = TimeSpan.FromHours(1);

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
