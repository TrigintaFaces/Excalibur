// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.DataProcessing.Diagnostics;

/// <summary>
/// Configuration options for the data processing health check.
/// </summary>
/// <remarks>
/// <para>
/// Controls the inactivity thresholds that determine when the data processing
/// service transitions between Healthy, Degraded, and Unhealthy states.
/// </para>
/// </remarks>
public sealed class DataProcessingHealthCheckOptions
{
	/// <summary>
	/// Gets or sets how long the processor can be inactive before it is considered degraded.
	/// </summary>
	/// <value>The degraded inactivity timeout. Default is 5 minutes.</value>
	public TimeSpan DegradedInactivityTimeout { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets or sets how long the processor can be inactive before it is considered unhealthy.
	/// </summary>
	/// <value>The unhealthy inactivity timeout. Default is 10 minutes.</value>
	public TimeSpan UnhealthyInactivityTimeout { get; set; } = TimeSpan.FromMinutes(10);
}
