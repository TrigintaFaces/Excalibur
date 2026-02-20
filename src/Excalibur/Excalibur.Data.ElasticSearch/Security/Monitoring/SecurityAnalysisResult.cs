// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Represents a security analysis result.
/// </summary>
public sealed class SecurityAnalysisResult
{
	/// <summary>
	/// Gets or sets the event ID.
	/// </summary>
	/// <value>
	/// The event ID.
	/// </value>
	public string EventId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the analysis timestamp.
	/// </summary>
	/// <value>
	/// The analysis timestamp.
	/// </value>
	public DateTimeOffset AnalysisTimestamp { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets or sets the event type.
	/// </summary>
	/// <value>
	/// The event type.
	/// </value>
	public string EventType { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets a value indicating whether a threat was detected.
	/// </summary>
	/// <value>
	/// A value indicating whether a threat was detected.
	/// </value>
	public bool HasThreat { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether a threat was detected (alternative property).
	/// </summary>
	/// <value>
	/// A value indicating whether a threat was detected (alternative property).
	/// </value>
	public bool ThreatDetected { get; set; }

	/// <summary>
	/// Gets or sets the threat type.
	/// </summary>
	/// <value>
	/// The threat type.
	/// </value>
	public string ThreatType { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets a value indicating whether an anomaly was detected.
	/// </summary>
	/// <value>
	/// A value indicating whether an anomaly was detected.
	/// </value>
	public bool AnomalyDetected { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether an error occurred.
	/// </summary>
	/// <value>
	/// A value indicating whether an error occurred.
	/// </value>
	public bool HasError { get; set; }

	/// <summary>
	/// Gets or sets the error message.
	/// </summary>
	/// <value>
	/// The error message.
	/// </value>
	public string ErrorMessage { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the risk level of the security analysis.
	/// </summary>
	/// <value>
	/// The risk level of the security analysis.
	/// </value>
	public SecurityRiskLevel RiskLevel { get; set; }

	/// <summary>
	/// Gets or sets the list of identified threats.
	/// </summary>
	/// <value>
	/// The list of identified threats.
	/// </value>
	public List<string> Threats { get; set; } = [];

	/// <summary>
	/// Gets or sets the list of recommended actions to address the threats.
	/// </summary>
	/// <value>
	/// The list of recommended actions to address the threats.
	/// </value>
	public List<string> RecommendedActions { get; set; } = [];
}
