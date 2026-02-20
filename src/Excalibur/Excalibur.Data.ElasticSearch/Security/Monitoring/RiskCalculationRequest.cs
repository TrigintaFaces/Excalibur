// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Represents a risk calculation request.
/// </summary>
public sealed class RiskCalculationRequest
{
	/// <summary>
	/// Gets or sets the identifier of the user for whom to calculate security risk.
	/// </summary>
	/// <value> The user ID or username to analyze for risk factors. </value>
	public string UserId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the time window to consider for the risk calculation.
	/// </summary>
	/// <value> The end timestamp of the analysis window, or null to use the default window. </value>
	public DateTimeOffset? TimeWindow { get; set; }
}
