// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Telemetry configuration options.
/// </summary>
public sealed class TelemetryOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether gets or sets whether to enable OpenTelemetry.
	/// </summary>
	/// <value>
	/// A value indicating whether gets or sets whether to enable OpenTelemetry.
	/// </value>
	public bool EnableOpenTelemetry { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether gets or sets whether to export to Cloud Monitoring.
	/// </summary>
	/// <value>
	/// A value indicating whether gets or sets whether to export to Cloud Monitoring.
	/// </value>
	public bool ExportToCloudMonitoring { get; set; }

	/// <summary>
	/// Gets or sets the OTLP endpoint.
	/// </summary>
	/// <value>
	/// The OTLP endpoint.
	/// </value>
	public string? OtlpEndpoint { get; set; }
}
