// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.OpenSearch.Monitoring;

namespace Excalibur.Data.OpenSearch;

/// <summary>
/// Configures the settings for OpenSearch client connections and operations.
/// </summary>
/// <remarks>
/// This class provides comprehensive configuration options for connecting to OpenSearch clusters, including endpoint selection,
/// connection pooling, resilience policies, and monitoring.
/// </remarks>
public sealed class OpenSearchConfigurationOptions
{
	/// <summary>
	/// Gets the URL of the OpenSearch cluster.
	/// </summary>
	/// <value>
	/// A <see cref="Uri" /> representing the base URL of the OpenSearch cluster. This property is required when using single-node configuration.
	/// </value>
	public Uri? Url { get; init; }

	/// <summary>
	/// Gets the collection of URLs for multi-node cluster configuration.
	/// </summary>
	/// <value>
	/// A collection of <see cref="Uri" /> representing the URLs of the OpenSearch cluster nodes. Used for cluster configuration with
	/// connection pooling.
	/// </value>
	public IEnumerable<Uri>? Urls { get; init; }

	/// <summary>
	/// Gets the configuration for resilience policies.
	/// </summary>
	/// <value> The resilience settings for retry and circuit breaker handling. </value>
	public OpenSearchResilienceOptions Resilience { get; init; } = new();

	/// <summary>
	/// Gets the configuration for monitoring and diagnostics.
	/// </summary>
	/// <value> The monitoring settings for metrics, logging, performance diagnostics, and tracing. </value>
	internal OpenSearchMonitoringOptions Monitoring { get; init; } = new();
}
