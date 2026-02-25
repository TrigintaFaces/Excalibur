// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Represents the result of poison message detection.
/// </summary>
public sealed class PoisonDetectionResult
{
	/// <summary>
	/// Gets or sets a value indicating whether gets or sets whether the message is detected as poison.
	/// </summary>
	/// <value>
	/// A value indicating whether gets or sets whether the message is detected as poison.
	/// </value>
	public bool IsPoison { get; set; }

	/// <summary>
	/// Gets or sets the message ID.
	/// </summary>
	/// <value>
	/// The message ID.
	/// </value>
	public string MessageId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the failure count.
	/// </summary>
	/// <value>
	/// The failure count.
	/// </value>
	public int FailureCount { get; set; }

	/// <summary>
	/// Gets or sets the detection results from individual rules.
	/// </summary>
	/// <value>
	/// The detection results from individual rules.
	/// </value>
	public List<RuleDetectionResult> DetectionResults { get; set; } = [];

	/// <summary>
	/// Gets or sets the recommendation for handling the message.
	/// </summary>
	/// <value>
	/// The recommendation for handling the message.
	/// </value>
	public PoisonRecommendation Recommendation { get; set; } = null!;

	/// <summary>
	/// Gets or sets additional metadata.
	/// </summary>
	/// <value>
	/// Additional metadata.
	/// </value>
	public Dictionary<string, string> Metadata { get; set; } = [];
}
