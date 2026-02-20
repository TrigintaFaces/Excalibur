// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Options.Pooling;

/// <summary>
/// Global pool behavior options.
/// </summary>
public sealed class GlobalPoolOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to enable telemetry.
	/// </summary>
	/// <value> The current <see cref="EnableTelemetry" /> value. </value>
	public bool EnableTelemetry { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable detailed metrics.
	/// </summary>
	/// <value> The current <see cref="EnableDetailedMetrics" /> value. </value>
	public bool EnableDetailedMetrics { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable diagnostics.
	/// </summary>
	/// <value> The current <see cref="EnableDiagnostics" /> value. </value>
	public bool EnableDiagnostics { get; set; } = true;

	/// <summary>
	/// Gets or sets the diagnostics interval.
	/// </summary>
	/// <value>
	/// The diagnostics interval.
	/// </value>
	public TimeSpan DiagnosticsInterval { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets or sets memory pressure threshold (0.0 to 1.0).
	/// </summary>
	/// <value> The current <see cref="MemoryPressureThreshold" /> value. </value>
	public double MemoryPressureThreshold { get; set; } = 0.8;

	/// <summary>
	/// Gets or sets a value indicating whether to enable adaptive sizing.
	/// </summary>
	/// <value> The current <see cref="EnableAdaptiveSizing" /> value. </value>
	public bool EnableAdaptiveSizing { get; set; } = true;

	/// <summary>
	/// Gets or sets the adaptation interval.
	/// </summary>
	/// <value>
	/// The adaptation interval.
	/// </value>
	public TimeSpan AdaptationInterval { get; set; } = TimeSpan.FromMinutes(1);
}
