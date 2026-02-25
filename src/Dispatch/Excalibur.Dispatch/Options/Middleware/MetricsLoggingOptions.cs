// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Options.Middleware;

/// <summary>
/// Configuration options for metrics logging middleware.
/// </summary>
public sealed class MetricsLoggingOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether metrics collection is enabled.
	/// </summary>
	/// <value> Default is true. </value>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to record OpenTelemetry metrics.
	/// </summary>
	/// <value> Default is true. </value>
	public bool RecordOpenTelemetryMetrics { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to record custom metrics via Tests.Shared.Common.IMetricsCollector.
	/// </summary>
	/// <value> Default is true. </value>
	public bool RecordCustomMetrics { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to log detailed processing information.
	/// </summary>
	/// <value> Default is true. </value>
	public bool LogProcessingDetails { get; set; } = true;

	/// <summary>
	/// Gets or sets the minimum duration threshold for logging slow operations.
	/// </summary>
	/// <value> Default is 1 second. </value>
	public TimeSpan SlowOperationThreshold { get; set; } = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Gets or sets message types that bypass metrics collection.
	/// </summary>
	/// <value>The current <see cref="BypassMetricsForTypes"/> value.</value>
	public string[]? BypassMetricsForTypes { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to include message payload sizes in metrics.
	/// </summary>
	/// <value> Default is true. </value>
	public bool IncludeMessageSizes { get; set; } = true;

	/// <summary>
	/// Gets or sets the sample rate for detailed metrics (0.0 to 1.0).
	/// </summary>
	/// <value> Default is 1.0 (sample all messages). </value>
	public double SampleRate { get; set; } = 1.0;
}
