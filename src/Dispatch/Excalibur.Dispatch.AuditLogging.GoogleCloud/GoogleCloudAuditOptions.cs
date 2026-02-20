// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.AuditLogging.GoogleCloud;

/// <summary>
/// Configuration options for the Google Cloud Logging audit log exporter.
/// </summary>
/// <remarks>
/// <para>
/// Uses the Cloud Logging API v2 to write structured log entries.
/// Authentication uses Application Default Credentials (ADC) or a service account.
/// </para>
/// </remarks>
public sealed class GoogleCloudAuditOptions
{
	/// <summary>
	/// Gets or sets the Google Cloud project ID.
	/// </summary>
	[Required]
	public required string ProjectId { get; set; }

	/// <summary>
	/// Gets or sets the log name for audit entries.
	/// </summary>
	/// <remarks>
	/// The full log name is: "projects/{ProjectId}/logs/{LogName}".
	/// </remarks>
	public string LogName { get; set; } = "dispatch-audit";

	/// <summary>
	/// Gets or sets the monitored resource type.
	/// </summary>
	/// <remarks>
	/// Common values: "global", "gce_instance", "k8s_container", "cloud_run_revision".
	/// </remarks>
	public string ResourceType { get; set; } = "global";

	/// <summary>
	/// Gets or sets additional labels for all log entries.
	/// </summary>
	public Dictionary<string, string>? Labels { get; set; }

	/// <summary>
	/// Gets or sets the maximum number of events to send in a single batch.
	/// </summary>
	public int MaxBatchSize { get; set; } = 500;

	/// <summary>
	/// Gets or sets the maximum number of retry attempts for transient failures.
	/// </summary>
	public int MaxRetryAttempts { get; set; } = 3;

	/// <summary>
	/// Gets or sets the base delay between retries.
	/// </summary>
	public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Gets or sets the HTTP request timeout.
	/// </summary>
	public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}
