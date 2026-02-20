// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Contains statistical information about security monitoring activities.
/// </summary>
public sealed class SecurityMonitoringStatistics
{
	/// <summary>
	/// Gets or sets the total number of security events processed.
	/// </summary>
	/// <value> The total count of processed security events. </value>
	public long TotalEventsProcessed { get; set; }

	/// <summary>
	/// Gets or sets the number of threats detected.
	/// </summary>
	/// <value> The total number of detected threats. </value>
	public long ThreatsDetected { get; set; }

	/// <summary>
	/// Gets or sets the number of security alerts generated.
	/// </summary>
	/// <value> The total number of security alerts. </value>
	public long AlertsGenerated { get; set; }

	/// <summary>
	/// Gets or sets the number of false positives detected.
	/// </summary>
	/// <value> The total number of false positive detections. </value>
	public long FalsePositives { get; set; }

	/// <summary>
	/// Gets or sets the timestamp when statistics were last updated.
	/// </summary>
	/// <value> The last update timestamp. </value>
	public DateTimeOffset LastUpdated { get; set; }

	/// <summary>
	/// Gets or sets the time period these statistics cover.
	/// </summary>
	/// <value> The statistics collection period in hours. </value>
	public int StatisticsPeriodHours { get; set; }

	/// <summary>
	/// Gets or sets the average threat detection time.
	/// </summary>
	/// <value> The average detection time in milliseconds. </value>
	public double AverageDetectionTimeMs { get; set; }

	/// <summary>
	/// Gets or sets additional statistical data.
	/// </summary>
	/// <value> A dictionary containing additional statistical information. </value>
	public Dictionary<string, object> AdditionalStats { get; set; } = [];
}
