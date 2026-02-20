// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Represents a detected security threat.
/// </summary>
public sealed class DetectedThreat
{
	/// <summary>
	/// Gets or sets the unique identifier of the detected threat.
	/// </summary>
	/// <value> The unique identifier for this threat. </value>
	public Guid ThreatId { get; set; }

	/// <summary>
	/// Gets or sets the type of threat that was detected.
	/// </summary>
	/// <value> The threat type classification. </value>
	public string ThreatType { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the severity level of the detected threat.
	/// </summary>
	/// <value> The severity level of this threat. </value>
	public string Severity { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the description of the detected threat.
	/// </summary>
	/// <value> A detailed description of the threat. </value>
	public string Description { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the source from which the threat was detected.
	/// </summary>
	/// <value> The source system or component that detected this threat. </value>
	public string? Source { get; set; }

	/// <summary>
	/// Gets or sets the timestamp when the threat was detected.
	/// </summary>
	/// <value> The detection timestamp. </value>
	public DateTimeOffset DetectedAt { get; set; }

	/// <summary>
	/// Gets or sets the confidence level of the threat detection.
	/// </summary>
	/// <value> The confidence level as a value between 0.0 and 1.0. </value>
	public double Confidence { get; set; }

	/// <summary>
	/// Gets or sets additional metadata associated with the threat.
	/// </summary>
	/// <value> A dictionary containing additional threat-related data. </value>
	public Dictionary<string, object>? Metadata { get; set; }
}
