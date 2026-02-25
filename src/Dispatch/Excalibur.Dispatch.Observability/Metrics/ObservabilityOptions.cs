// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Observability.Metrics;

/// <summary>
/// Configuration options for Dispatch observability features.
/// </summary>
public sealed class ObservabilityOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether metrics collection is enabled.
	/// </summary>
	public bool EnableMetrics { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether tracing is enabled.
	/// </summary>
	public bool EnableTracing { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether logging enrichment is enabled.
	/// </summary>
	public bool EnableLogging { get; set; } = true;

	/// <summary>
	/// Gets or sets the activity source name for tracing.
	/// </summary>
	[Required]
	public string ActivitySourceName { get; set; } = "Excalibur.Dispatch";

	/// <summary>
	/// Gets or sets the meter name for metrics.
	/// </summary>
	[Required]
	public string MeterName { get; set; } = DispatchMetrics.MeterName;

	/// <summary>
	/// Gets or sets the service name for telemetry.
	/// </summary>
	[Required]
	public string ServiceName { get; set; } = "Excalibur.Dispatch";

	/// <summary>
	/// Gets or sets the service version for telemetry.
	/// </summary>
	[Required]
	public string ServiceVersion { get; set; } = "1.0.0";

	/// <summary>
	/// Gets or sets a value indicating whether detailed middleware timing is enabled.
	/// </summary>
	public bool EnableDetailedTiming { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether sensitive data should be included in telemetry.
	/// </summary>
	public bool IncludeSensitiveData { get; set; }
}
