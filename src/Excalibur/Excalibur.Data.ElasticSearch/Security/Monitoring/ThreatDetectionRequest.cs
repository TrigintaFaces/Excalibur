// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Represents a threat detection request.
/// </summary>
public sealed class ThreatDetectionRequest
{
	/// <summary>
	/// Gets or sets the request ID.
	/// </summary>
	/// <value>
	/// The request ID.
	/// </value>
	public string RequestId { get; set; } = Guid.NewGuid().ToString();

	/// <summary>
	/// Gets or sets the start time for the threat detection analysis.
	/// </summary>
	/// <value>
	/// The start time for the threat detection analysis.
	/// </value>
	public DateTimeOffset StartTime { get; set; }

	/// <summary>
	/// Gets or sets the end time for the threat detection analysis.
	/// </summary>
	/// <value>
	/// The end time for the threat detection analysis.
	/// </value>
	public DateTimeOffset EndTime { get; set; }

	/// <summary>
	/// Gets or sets the target indices to analyze for threat detection.
	/// </summary>
	/// <value>
	/// The target indices to analyze for threat detection.
	/// </value>
	public List<string> TargetIndices { get; set; } = [];

	/// <summary>
	/// Gets or sets a value indicating whether to include historical data in the threat detection analysis.
	/// </summary>
	/// <value>
	/// A value indicating whether to include historical data in the threat detection analysis.
	/// </value>
	public bool IncludeHistoricalData { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to detect brute force attacks.
	/// </summary>
	/// <value>
	/// A value indicating whether to detect brute force attacks.
	/// </value>
	public bool DetectBruteForce { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to detect anomalies.
	/// </summary>
	/// <value>
	/// A value indicating whether to detect anomalies.
	/// </value>
	public bool DetectAnomalies { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to detect data threats.
	/// </summary>
	/// <value>
	/// A value indicating whether to detect data threats.
	/// </value>
	public bool DetectDataThreats { get; set; }
}
