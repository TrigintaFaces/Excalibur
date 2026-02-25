// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Result of batch processing.
/// </summary>
public sealed class BatchProcessingResult
{
	/// <summary>
	/// Gets or sets the batch ID.
	/// </summary>
	/// <value> The current <see cref="BatchId" /> value. </value>
	public string BatchId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the number of successfully processed messages.
	/// </summary>
	/// <value> The current <see cref="SuccessCount" /> value. </value>
	public int SuccessCount { get; set; }

	/// <summary>
	/// Gets or sets the number of failed messages.
	/// </summary>
	/// <value> The current <see cref="FailureCount" /> value. </value>
	public int FailureCount { get; set; }

	/// <summary>
	/// Gets or sets the number of skipped messages.
	/// </summary>
	/// <value> The current <see cref="SkippedCount" /> value. </value>
	public int SkippedCount { get; set; }

	/// <summary>
	/// Gets the total message count.
	/// </summary>
	/// <value> The current <see cref="TotalCount" /> value. </value>
	public int TotalCount => SuccessCount + FailureCount + SkippedCount;

	/// <summary>
	/// Gets or sets the processing duration.
	/// </summary>
	/// <value> The current <see cref="ProcessingDuration" /> value. </value>
	public TimeSpan ProcessingDuration { get; set; }

	/// <summary>
	/// Gets or sets when processing started.
	/// </summary>
	/// <value> The current <see cref="StartedAt" /> value. </value>
	public DateTimeOffset StartedAt { get; set; }

	/// <summary>
	/// Gets or sets when processing completed.
	/// </summary>
	/// <value> The current <see cref="CompletedAt" /> value. </value>
	public DateTimeOffset CompletedAt { get; set; }

	/// <summary>
	/// Gets individual message results.
	/// </summary>
	/// <value> The current <see cref="MessageResults" /> value. </value>
	public ICollection<MessageProcessingResult> MessageResults { get; } = [];

	/// <summary>
	/// Gets processing errors.
	/// </summary>
	/// <value> The current <see cref="Errors" /> value. </value>
	public ICollection<ProcessingError> Errors { get; } = [];

	/// <summary>
	/// Gets a value indicating whether the batch was fully successful.
	/// </summary>
	/// <value> The current <see cref="IsSuccess" /> value. </value>
	public bool IsSuccess => FailureCount == 0;

	/// <summary>
	/// Gets a value indicating whether the batch was partially successful.
	/// </summary>
	/// <value> The current <see cref="IsPartialSuccess" /> value. </value>
	public bool IsPartialSuccess => SuccessCount > 0 && FailureCount > 0;

	/// <summary>
	/// Gets the success rate.
	/// </summary>
	/// <value>
	/// The success rate.
	/// </value>
	public double SuccessRate => TotalCount > 0 ? (double)SuccessCount / TotalCount : 0;

	/// <summary>
	/// Gets batch metadata.
	/// </summary>
	/// <value> The current <see cref="Metadata" /> value. </value>
	public Dictionary<string, object> Metadata { get; } = [];
}
