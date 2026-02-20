// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.AuditLogging.Datadog;

/// <summary>
/// Configuration options for Datadog audit log exporter.
/// </summary>
/// <remarks>
/// <para>
/// Uses the Datadog Logs API v2 to send custom audit logs to Datadog.
/// Logs can be searched and analyzed in Datadog Log Management.
/// </para>
/// <para>
/// Authentication requires an API key with Logs Write permission.
/// </para>
/// </remarks>
public sealed class DatadogExporterOptions
{
	/// <summary>
	/// Gets or sets the Datadog API key.
	/// </summary>
	/// <remarks>
	/// The API key must have the Logs Write permission.
	/// Found in Datadog: Organization Settings > API Keys.
	/// Keep this secret secure - do not commit to source control.
	/// </remarks>
	[Required]
	public required string ApiKey { get; set; }

	/// <summary>
	/// Gets or sets the Datadog site/region.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Common values:
	/// - datadoghq.com (US1)
	/// - us3.datadoghq.com (US3)
	/// - us5.datadoghq.com (US5)
	/// - datadoghq.eu (EU)
	/// - ap1.datadoghq.com (AP1)
	/// </para>
	/// </remarks>
	public string Site { get; set; } = "datadoghq.com";

	/// <summary>
	/// Gets or sets the service name for the logs.
	/// </summary>
	/// <remarks>
	/// Used for filtering and grouping logs in Datadog.
	/// </remarks>
	public string Service { get; set; } = "dispatch-audit";

	/// <summary>
	/// Gets or sets the source for the logs.
	/// </summary>
	/// <remarks>
	/// Identifies the technology or integration that produced the log.
	/// </remarks>
	public string Source { get; set; } = "dispatch";

	/// <summary>
	/// Gets or sets the hostname for the logs.
	/// </summary>
	/// <remarks>
	/// If not specified, the machine name is used.
	/// </remarks>
	public string? Hostname { get; set; }

	/// <summary>
	/// Gets or sets additional tags for all logs.
	/// </summary>
	/// <remarks>
	/// Format: "key1:value1,key2:value2"
	/// Tags are used for filtering and analytics in Datadog.
	/// </remarks>
	public string? Tags { get; set; }

	/// <summary>
	/// Gets or sets the maximum number of events to send in a single batch.
	/// </summary>
	/// <remarks>
	/// The Datadog Logs API has a 5 MB per request limit and max 1000 items.
	/// Default is 500 events per batch for optimal performance.
	/// </remarks>
	public int MaxBatchSize { get; set; } = 500;

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
	/// Gets or sets whether to use gzip compression for requests.
	/// </summary>
	/// <remarks>
	/// Recommended for large batches to reduce bandwidth and improve performance.
	/// </remarks>
	public bool UseCompression { get; set; } = true;
}
