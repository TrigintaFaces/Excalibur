// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.AuditLogging.Elasticsearch;

/// <summary>
/// Configuration options for the Elasticsearch audit log exporter.
/// </summary>
/// <remarks>
/// <para>
/// Uses the Elasticsearch Bulk API to send audit log data for indexing.
/// Logs can be searched and analyzed using Kibana or the Elasticsearch Query DSL.
/// </para>
/// </remarks>
public sealed class ElasticsearchExporterOptions
{
	/// <summary>
	/// Gets or sets the Elasticsearch base URL.
	/// </summary>
	/// <remarks>
	/// Example: "https://my-cluster.es.example.com:9200"
	/// </remarks>
	[Required]
	public required string ElasticsearchUrl { get; set; }

	/// <summary>
	/// Gets or sets the index name prefix for audit documents.
	/// </summary>
	/// <remarks>
	/// Documents are indexed to "{IndexPrefix}-{yyyy.MM.dd}" for time-based partitioning.
	/// </remarks>
	public string IndexPrefix { get; set; } = "dispatch-audit";

	/// <summary>
	/// Gets or sets the maximum number of events to send in a single bulk request.
	/// </summary>
	public int BulkBatchSize { get; set; } = 500;

	/// <summary>
	/// Gets or sets the refresh policy for index operations.
	/// </summary>
	/// <remarks>
	/// Valid values: "true" (immediate), "wait_for" (wait until refreshed), "false" (no refresh).
	/// Default is "false" for optimal performance.
	/// </remarks>
	public string RefreshPolicy { get; set; } = "false";

	/// <summary>
	/// Gets or sets the optional API key for authentication.
	/// </summary>
	public string? ApiKey { get; set; }

	/// <summary>
	/// Gets or sets the maximum number of retry attempts for transient failures.
	/// </summary>
	public int MaxRetryAttempts { get; set; } = 3;

	/// <summary>
	/// Gets or sets the base delay between retries.
	/// </summary>
	/// <remarks>
	/// Actual delay uses exponential backoff: baseDelay * 2^(attempt-1).
	/// </remarks>
	public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Gets or sets the HTTP request timeout.
	/// </summary>
	public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}
