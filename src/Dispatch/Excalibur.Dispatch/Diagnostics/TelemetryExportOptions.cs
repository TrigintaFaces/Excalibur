// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Diagnostics;

/// <summary>
/// Export and sampling configuration for Dispatch telemetry.
/// </summary>
/// <remarks>
/// Follows the <c>OtlpExporterOptions</c> pattern of separating export configuration from feature toggles.
/// </remarks>
public sealed class TelemetryExportOptions
{
	/// <summary>
	/// Gets or sets the sampling ratio for high-frequency traces.
	/// </summary>
	/// <value> The ratio of traces to sample (0.0 to 1.0). Default is 0.1 (10%). </value>
	[Range(0.0, 1.0)]
	public double SamplingRatio { get; set; } = 0.1;

	/// <summary>
	/// Gets or sets the maximum number of active spans per trace.
	/// </summary>
	/// <value> Limits the depth of distributed traces to prevent memory issues. Default is 100. </value>
	[Range(1, 1000)]
	public int MaxSpansPerTrace { get; set; } = 100;

	/// <summary>
	/// Gets or sets the batch size for metric exports.
	/// </summary>
	/// <value> Number of metrics to batch before export. Default is 512. </value>
	[Range(1, 10000)]
	public int MetricBatchSize { get; set; } = 512;

	/// <summary>
	/// Gets or sets the export timeout for telemetry data.
	/// </summary>
	/// <value> Maximum time to wait for telemetry exports. Default is 30 seconds. </value>
	public TimeSpan ExportTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
