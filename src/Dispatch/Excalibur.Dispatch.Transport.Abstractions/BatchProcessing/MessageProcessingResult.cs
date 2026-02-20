// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Result of processing a single message within a batch.
/// </summary>
public sealed class MessageProcessingResult
{
	/// <summary>
	/// Gets or sets the message ID.
	/// </summary>
	/// <value> The current <see cref="MessageId" /> value. </value>
	public string MessageId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets a value indicating whether processing was successful.
	/// </summary>
	/// <value> The current <see cref="IsSuccess" /> value. </value>
	public bool IsSuccess { get; set; }

	/// <summary>
	/// Gets or sets the error message if failed.
	/// </summary>
	/// <value> The current <see cref="ErrorMessage" /> value. </value>
	public string? ErrorMessage { get; set; }

	/// <summary>
	/// Gets or sets the exception if one occurred.
	/// </summary>
	/// <value> The current <see cref="Exception" /> value. </value>
	public Exception? Exception { get; set; }

	/// <summary>
	/// Gets or sets the processing duration.
	/// </summary>
	/// <value> The current <see cref="ProcessingDuration" /> value. </value>
	public TimeSpan ProcessingDuration { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the message should be retried.
	/// </summary>
	/// <value> The current <see cref="ShouldRetry" /> value. </value>
	public bool ShouldRetry { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the message was moved to DLQ.
	/// </summary>
	/// <value> The current <see cref="MovedToDeadLetter" /> value. </value>
	public bool MovedToDeadLetter { get; set; }
}
