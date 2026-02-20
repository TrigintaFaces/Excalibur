// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Result of a reprocessing operation.
/// </summary>
public sealed class ReprocessResult
{
	/// <summary>
	/// Gets or sets the number of successfully reprocessed messages.
	/// </summary>
	/// <value>The current <see cref="SuccessCount"/> value.</value>
	public int SuccessCount { get; set; }

	/// <summary>
	/// Gets or sets the number of failed messages.
	/// </summary>
	/// <value>The current <see cref="FailureCount"/> value.</value>
	public int FailureCount { get; set; }

	/// <summary>
	/// Gets or sets the number of skipped messages.
	/// </summary>
	/// <value>The current <see cref="SkippedCount"/> value.</value>
	public int SkippedCount { get; set; }

	/// <summary>
	/// Gets the total count of processed messages.
	/// </summary>
	/// <value>The current <see cref="TotalCount"/> value.</value>
	public int TotalCount => SuccessCount + FailureCount + SkippedCount;

	/// <summary>
	/// Gets the list of failures.
	/// </summary>
	/// <value>The current <see cref="Failures"/> value.</value>
	public ICollection<ReprocessFailure> Failures { get; init; } = [];

	/// <summary>
	/// Gets or sets the processing time.
	/// </summary>
	/// <value>The current <see cref="ProcessingTime"/> value.</value>
	public TimeSpan ProcessingTime { get; set; }

	/// <summary>
	/// Gets a value indicating whether the operation was fully successful.
	/// </summary>
	/// <value>The current <see cref="IsSuccess"/> value.</value>
	public bool IsSuccess => FailureCount == 0;
}
