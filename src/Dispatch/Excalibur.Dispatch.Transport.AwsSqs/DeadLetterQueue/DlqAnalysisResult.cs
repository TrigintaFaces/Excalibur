// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Result of analyzing a message for DLQ eligibility.
/// </summary>
public sealed class DlqAnalysisResult
{
	/// <summary>
	/// Gets or sets a value indicating whether the message should be moved to DLQ.
	/// </summary>
	/// <value>
	/// A value indicating whether the message should be moved to DLQ.
	/// </value>
	public bool ShouldMoveToDeadLetter { get; set; }

	/// <summary>
	/// Gets or sets the reason for the decision.
	/// </summary>
	/// <value>
	/// The reason for the decision.
	/// </value>
	public string? Reason { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the message is potentially recoverable.
	/// </summary>
	/// <value>
	/// A value indicating whether the message is potentially recoverable.
	/// </value>
	public bool IsRecoverable { get; set; }

	/// <summary>
	/// Gets or sets the recommended action.
	/// </summary>
	/// <value>
	/// The recommended action.
	/// </value>
	public DlqAction RecommendedAction { get; set; }

	/// <summary>
	/// Gets or sets the suggested retry delay if recoverable.
	/// </summary>
	/// <value>
	/// The suggested retry delay if recoverable.
	/// </value>
	public TimeSpan? SuggestedRetryDelay { get; set; }
}
