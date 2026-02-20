// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Represents a security risk score.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="SecurityRiskScore" /> class.
/// </remarks>
/// <param name="level"> The security risk level. </param>
/// <param name="score"> The numerical security risk score. </param>
public sealed class SecurityRiskScore(SecurityRiskLevel level, double score)
{
	/// <summary>
	/// Gets the security risk level.
	/// </summary>
	/// <value> The security risk level categorization. </value>
	public SecurityRiskLevel Level { get; } = level;

	/// <summary>
	/// Gets the numerical security risk score.
	/// </summary>
	/// <value> The numerical score representing the security risk level. </value>
	public double Score { get; } = score;
}
