// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Represents the result of a security risk calculation operation.
/// </summary>
public sealed class RiskCalculationResult
{
	/// <summary>
	/// Gets or sets the unique identifier for this risk calculation.
	/// </summary>
	/// <value> The unique identifier for this calculation. </value>
	public Guid CalculationId { get; set; }

	/// <summary>
	/// Gets or sets the timestamp when the calculation was performed.
	/// </summary>
	/// <value> The calculation timestamp. </value>
	public DateTimeOffset CalculationTimestamp { get; set; }

	/// <summary>
	/// Gets or sets the time window used for the risk calculation.
	/// </summary>
	/// <value> The time window for the calculation. </value>
	public TimeSpan TimeWindow { get; set; }

	/// <summary>
	/// Gets or sets the calculated security risk score.
	/// </summary>
	/// <value> The security risk score result. </value>
	public SecurityRiskScore? RiskScore { get; set; }

	/// <summary>
	/// Gets or sets the user ID for whom the risk was calculated.
	/// </summary>
	/// <value> The user identifier. </value>
	public string? UserId { get; set; }

	/// <summary>
	/// Gets or sets additional calculation metadata.
	/// </summary>
	/// <value> A dictionary containing calculation-specific data. </value>
	public Dictionary<string, object> Metadata { get; set; } = [];
}
