// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.ComponentModel.DataAnnotations;

using Excalibur.Compliance;

namespace Excalibur.AuditLogging.OpenSearch;

/// <summary>
/// Configuration options for the OpenSearch audit log exporter.
/// </summary>
/// <remarks>
/// <para>
/// Uses the OpenSearch Bulk API to send audit log data for indexing.
/// Logs can be searched and analyzed using OpenSearch Dashboards or the Query DSL.
/// </para>
/// </remarks>
public sealed class OpenSearchExporterOptions
{
	/// <summary>
	/// Gets or sets the OpenSearch base URL.
	/// </summary>
	/// <remarks>
	/// Example: "https://my-cluster.os.example.com:9200"
	/// </remarks>
	[Required]
	public required string OpenSearchUrl { get; set; }

	/// <summary>
	/// Gets or sets the cluster node URLs for multi-node OpenSearch deployments.
	/// </summary>
	/// <remarks>
	/// If both <see cref="NodeUrls"/> and <see cref="OpenSearchUrl"/> are set,
	/// <see cref="NodeUrls"/> takes precedence. Use this for cluster-aware round-robin
	/// request distribution.
	/// </remarks>
	public List<string>? NodeUrls { get; set; }

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
	/// Gets or sets the refresh policy applied to the bulk write.
	/// </summary>
	/// <value>
	/// The refresh policy. Defaults to <see cref="OpenSearchAuditRefreshPolicy.None"/>, which is the right default for an
	/// audit path: the write must be durable, and it does not need to be searchable in the same breath.
	/// </value>
	public OpenSearchAuditRefreshPolicy RefreshPolicy { get; set; } = OpenSearchAuditRefreshPolicy.None;

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

	/// <summary>
	/// Gets or sets the application name to include in exported audit events.
	/// </summary>
	/// <remarks>
	/// Used as a fallback when <see cref="AuditEvent.ApplicationName"/> is not set
	/// on individual events. The event-level value takes precedence over this option.
	/// </remarks>
	public string? ApplicationName { get; set; }
}
