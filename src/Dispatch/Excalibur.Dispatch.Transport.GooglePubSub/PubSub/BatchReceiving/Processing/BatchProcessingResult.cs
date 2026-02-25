// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Result of batch processing operation.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="BatchProcessingResult" /> class. </remarks>
public sealed class BatchProcessingResult(
	MessageBatch batch,
	IReadOnlyList<ProcessedMessage> successfulMessages,
	IReadOnlyList<FailedMessage> failedMessages,
	TimeSpan processingDuration)
{
	/// <summary>
	/// Gets the original batch.
	/// </summary>
	/// <value>
	/// The original batch.
	/// </value>
	public MessageBatch Batch { get; } = batch ?? throw new ArgumentNullException(nameof(batch));

	/// <summary>
	/// Gets the successfully processed messages.
	/// </summary>
	/// <value>
	/// The successfully processed messages.
	/// </value>
	public IReadOnlyList<ProcessedMessage> SuccessfulMessages { get; } =
		successfulMessages ?? throw new ArgumentNullException(nameof(successfulMessages));

	/// <summary>
	/// Gets the failed messages with error details.
	/// </summary>
	/// <value>
	/// The failed messages with error details.
	/// </value>
	public IReadOnlyList<FailedMessage> FailedMessages { get; } = failedMessages ?? throw new ArgumentNullException(nameof(failedMessages));

	/// <summary>
	/// Gets the total processing duration.
	/// </summary>
	/// <value>
	/// The total processing duration.
	/// </value>
	public TimeSpan ProcessingDuration { get; } = processingDuration;

	/// <summary>
	/// Gets a value indicating whether gets whether all messages were processed successfully.
	/// </summary>
	/// <value>
	/// A value indicating whether gets whether all messages were processed successfully.
	/// </value>
	public bool IsFullySuccessful => FailedMessages.Count == 0;

	/// <summary>
	/// Gets the success rate (0-1).
	/// </summary>
	/// <value>
	/// The success rate (0-1).
	/// </value>
	public double SuccessRate => Batch.Count > 0
		? (double)SuccessfulMessages.Count / Batch.Count
		: 1.0;
}
