// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Recommendation for handling a poison message.
/// </summary>
public sealed class PoisonRecommendation
{
	/// <summary>
	/// Gets or sets the recommended action.
	/// </summary>
	/// <value>
	/// The recommended action.
	/// </value>
	public RecommendedAction Action { get; set; }

	/// <summary>
	/// Gets or sets the reason for the recommendation.
	/// </summary>
	/// <value>
	/// The reason for the recommendation.
	/// </value>
	public string Reason { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the suggested retry delay if applicable.
	/// </summary>
	/// <value>
	/// The suggested retry delay if applicable.
	/// </value>
	public TimeSpan? RetryDelay { get; set; }

	/// <summary>
	/// Gets or sets the suggested fix if known.
	/// </summary>
	/// <value>
	/// The suggested fix if known.
	/// </value>
	public string? SuggestedFix { get; set; }
}
