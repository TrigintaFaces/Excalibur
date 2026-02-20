// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Provides contextual information for security analysis operations.
/// </summary>
public sealed class SecurityAnalysisContext
{
	/// <summary>
	/// Gets or sets the timestamp when the analysis was initiated.
	/// </summary>
	/// <value> The analysis start timestamp. </value>
	public DateTimeOffset AnalysisStartTime { get; set; }

	/// <summary>
	/// Gets or sets the identifier of the user or system initiating the analysis.
	/// </summary>
	/// <value> The requester identifier. </value>
	public string? RequesterId { get; set; }

	/// <summary>
	/// Gets or sets the time window for the security analysis.
	/// </summary>
	/// <value> The analysis time window in minutes. </value>
	public int TimeWindowMinutes { get; set; }

	/// <summary>
	/// Gets or sets the analysis parameters and configuration.
	/// </summary>
	/// <value> A dictionary containing analysis-specific parameters. </value>
	public Dictionary<string, object> Parameters { get; set; } = [];

	/// <summary>
	/// Gets or sets the scope of the security analysis.
	/// </summary>
	/// <value> The analysis scope description. </value>
	public string? Scope { get; set; }

	/// <summary>
	/// Gets or sets additional context information for the analysis.
	/// </summary>
	/// <value> A dictionary containing contextual data. </value>
	public Dictionary<string, object> ContextData { get; set; } = [];

	/// <summary>
	/// Gets or sets a value indicating whether the target is a high value target.
	/// </summary>
	/// <value> True if the target is high value, false otherwise. </value>
	public bool IsHighValueTarget { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether there have been recent security incidents.
	/// </summary>
	/// <value> True if there have been recent incidents, false otherwise. </value>
	public bool HasRecentIncidents { get; set; }
}
