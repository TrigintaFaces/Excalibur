// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Configuration options for Google Cloud telemetry.
/// </summary>
public sealed class GoogleTelemetryOptions
{
	/// <summary>
	/// Gets or sets project ID for Google Cloud Monitoring.
	/// </summary>
	/// <value>
	/// Project ID for Google Cloud Monitoring
	/// </value>
	public required string ProjectId { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether enable Cloud Monitoring export.
	/// </summary>
	/// <value>
	/// A value indicating whether enable Cloud Monitoring export
	/// </value>
	public bool EnableCloudMonitoring { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether enable Cloud Trace export.
	/// </summary>
	/// <value>
	/// A value indicating whether enable Cloud Trace export
	/// </value>
	public bool EnableCloudTrace { get; set; } = true;

	/// <summary>
	/// Gets or sets export interval for metrics.
	/// </summary>
	/// <value>
	/// Export interval for metrics
	/// </value>
	public TimeSpan ExportInterval { get; set; } = TimeSpan.FromMinutes(1);

	/// <summary>
	/// Gets or sets batch size for metric exports.
	/// </summary>
	/// <value>
	/// Batch size for metric exports
	/// </value>
	public int BatchSize { get; set; } = 200;

	/// <summary>
	/// Gets custom labels to add to all metrics.
	/// </summary>
	/// <value>
	/// Custom labels to add to all metrics.
	/// </value>
	public Dictionary<string, string> Labels { get; } = [];

	/// <summary>
	/// Gets or sets resource type for metrics.
	/// </summary>
	/// <value>
	/// Resource type for metrics
	/// </value>
	public string ResourceType { get; set; } = "global";

	/// <summary>
	/// Gets or sets metric name prefix.
	/// </summary>
	/// <value>
	/// Metric name prefix
	/// </value>
	public string MetricPrefix { get; set; } = "custom.googleapis.com/dispatch";
}
