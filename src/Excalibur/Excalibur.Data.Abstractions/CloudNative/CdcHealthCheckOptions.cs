// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Data.CloudNative;

/// <summary>
/// General configuration options for CDC health checks across all providers.
/// </summary>
/// <remarks>
/// <para>
/// Provider-specific health check options classes can inherit from this base class
/// to add provider-specific thresholds while sharing common configuration.
/// </para>
/// </remarks>
public class CdcHealthCheckOptions
{
	/// <summary>
	/// Gets or sets the lag threshold (number of unprocessed events) for degraded status.
	/// </summary>
	/// <value>The degraded lag threshold. Default is 1000.</value>
	public long DegradedLagThreshold { get; set; } = 1000;

	/// <summary>
	/// Gets or sets the lag threshold for unhealthy status.
	/// </summary>
	/// <value>The unhealthy lag threshold. Default is 10000.</value>
	public long UnhealthyLagThreshold { get; set; } = 10000;

	/// <summary>
	/// Gets or sets how long the processor can be inactive before it is considered unhealthy.
	/// </summary>
	/// <value>The inactivity timeout. Default is 10 minutes.</value>
	public TimeSpan UnhealthyInactivityTimeout { get; set; } = TimeSpan.FromMinutes(10);

	/// <summary>
	/// Gets or sets how long the processor can be inactive before it is considered degraded.
	/// </summary>
	/// <value>The degraded inactivity timeout. Default is 5 minutes.</value>
	public TimeSpan DegradedInactivityTimeout { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets or sets how many consecutive transient failures without progress make the CDC health check
	/// report Unhealthy.
	/// </summary>
	/// <remarks>
	/// Streaming processors report each reconnect that fails without making progress. The count returns to
	/// zero as soon as the processor makes progress again.
	/// </remarks>
	/// <value>The threshold; defaults to 3. Must be at least 1.</value>
	public int UnhealthyConsecutiveTransientFailures { get; set; } = 3;
}
