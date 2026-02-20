// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Represents a threat detection result.
/// </summary>
public sealed class ThreatDetectionResult
{
	/// <summary>
	/// Gets or sets the request ID.
	/// </summary>
	/// <value>
	/// The request ID.
	/// </value>
	public string RequestId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the analysis start time.
	/// </summary>
	/// <value>
	/// The analysis start time.
	/// </value>
	public DateTimeOffset AnalysisStartTime { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets or sets the analysis end time.
	/// </summary>
	/// <value>
	/// The analysis end time.
	/// </value>
	public DateTimeOffset AnalysisEndTime { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets the analysis duration.
	/// </summary>
	/// <value>
	/// The analysis duration.
	/// </value>
	public TimeSpan AnalysisDuration => AnalysisEndTime - AnalysisStartTime;

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
	/// Gets or sets the number of threats detected.
	/// </summary>
	/// <value>
	/// The number of threats detected.
	/// </value>
	public int ThreatsDetected { get; set; }

	/// <summary>
	/// Gets or sets the list of security alerts representing detected threats.
	/// </summary>
	/// <value>
	/// The list of security alerts representing detected threats.
	/// </value>
	public List<SecurityAlert> DetectedThreats { get; set; } = [];

	/// <summary>
	/// Gets or sets the countermeasures dictionary containing recommended security actions and their parameters.
	/// </summary>
	/// <value>
	/// The countermeasures dictionary containing recommended security actions and their parameters.
	/// </value>
	public Dictionary<string, object> Countermeasures { get; set; } = [];
}
