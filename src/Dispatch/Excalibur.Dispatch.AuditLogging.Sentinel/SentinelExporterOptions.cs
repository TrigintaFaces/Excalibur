// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.AuditLogging.Sentinel;

/// <summary>
/// Configuration options for Azure Sentinel audit log exporter.
/// </summary>
/// <remarks>
/// <para>
/// Uses the Azure Monitor Data Collector API (HTTP Data Collector) to send
/// custom logs to Log Analytics workspace, which can then be queried via
/// Azure Sentinel.
/// </para>
/// <para>
/// The Data Collector API requires a workspace ID and shared key for authentication.
/// Logs are sent to a custom table in the format: {LogType}_CL
/// </para>
/// </remarks>
public sealed class SentinelExporterOptions
{
	/// <summary>
	/// Gets or sets the Log Analytics workspace ID (GUID).
	/// </summary>
	/// <remarks>
	/// Found in Azure Portal: Log Analytics workspace > Agents management > Workspace ID.
	/// </remarks>
	[Required]
	public required string WorkspaceId { get; set; }

	/// <summary>
	/// Gets or sets the primary or secondary shared key for the workspace.
	/// </summary>
	/// <remarks>
	/// Found in Azure Portal: Log Analytics workspace > Agents management > Primary key.
	/// Keep this secret secure - do not commit to source control.
	/// </remarks>
	[Required]
	public required string SharedKey { get; set; }

	/// <summary>
	/// Gets or sets the custom log type name.
	/// </summary>
	/// <remarks>
	/// The log type becomes the table name with '_CL' suffix in Log Analytics.
	/// For example, 'DispatchAudit' creates table 'DispatchAudit_CL'.
	/// Must contain only letters, digits, and underscores.
	/// </remarks>
	public string LogType { get; set; } = "DispatchAudit";

	/// <summary>
	/// Gets or sets the Azure resource ID for the resource being monitored.
	/// </summary>
	/// <remarks>
	/// Optional. When specified, associates the data with a specific Azure resource
	/// for better organization and resource-centric queries.
	/// Format: /subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/{namespace}/{type}/{name}
	/// </remarks>
	public string? AzureResourceId { get; set; }

	/// <summary>
	/// Gets or sets the field name in the data that contains the timestamp.
	/// </summary>
	/// <remarks>
	/// If not specified, the API uses the ingestion time as the timestamp.
	/// Should be in ISO 8601 format.
	/// </remarks>
	public string? TimeGeneratedField { get; set; } = "timestamp";

	/// <summary>
	/// Gets or sets the maximum number of events to send in a single batch.
	/// </summary>
	/// <remarks>
	/// The Data Collector API has a 30 MB per request limit.
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
}
