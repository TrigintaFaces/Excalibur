// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Represents a threat intelligence result.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ThreatIntelligenceResult" /> class.
/// </remarks>
/// <param name="isThreat"> Indicates whether a threat was detected. </param>
/// <param name="threatType"> The type of threat detected. </param>
/// <param name="details"> Additional details about the threat. </param>
public sealed class ThreatIntelligenceResult(bool isThreat, ThreatType threatType, string? details)
{
	/// <summary>
	/// Gets a value indicating whether a threat was detected.
	/// </summary>
	/// <value>
	/// A value indicating whether a threat was detected.
	/// </value>
	public bool IsThreat { get; } = isThreat;

	/// <summary>
	/// Gets the type of threat detected.
	/// </summary>
	/// <value>
	/// The type of threat detected.
	/// </value>
	public ThreatType ThreatType { get; } = threatType;

	/// <summary>
	/// Gets additional details about the threat.
	/// </summary>
	/// <value>
	/// Additional details about the threat.
	/// </value>
	public string? Details { get; } = details;
}
