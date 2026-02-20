// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Result from an individual detection rule.
/// </summary>
public sealed class RuleDetectionResult
{
	/// <summary>
	/// Gets or sets the rule name.
	/// </summary>
	/// <value>
	/// The rule name.
	/// </value>
	public string RuleName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the confidence level (0-1).
	/// </summary>
	/// <value>
	/// The confidence level (0-1).
	/// </value>
	public double Confidence { get; set; }

	/// <summary>
	/// Gets or sets the reason for detection.
	/// </summary>
	/// <value>
	/// The reason for detection.
	/// </value>
	public string Reason { get; set; } = string.Empty;
}
