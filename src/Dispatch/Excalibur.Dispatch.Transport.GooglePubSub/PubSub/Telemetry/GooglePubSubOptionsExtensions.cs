// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Configuration options for Pub/Sub telemetry.
/// </summary>
public static class GooglePubSubOptionsExtensions
{
	/// <summary>
	/// Configures telemetry options.
	/// </summary>
	/// <param name="options"> The options to configure. </param>
	/// <param name="configure"> Configuration action. </param>
	/// <returns> The configured options. </returns>
	public static GooglePubSubOptions WithTelemetry(
		this GooglePubSubOptions options,
		Action<TelemetryOptions> configure)
	{
		var telemetryOptions = new TelemetryOptions();
		configure(telemetryOptions);

		options.EnableOpenTelemetry = telemetryOptions.EnableOpenTelemetry;
		options.ExportToCloudMonitoring = telemetryOptions.ExportToCloudMonitoring;
		options.OtlpEndpoint = telemetryOptions.OtlpEndpoint;

		return options;
	}
}
