// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Represents a failed message with error details.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="FailedMessage" /> class. </remarks>
public sealed class FailedMessage(
	string messageId,
	string ackId,
	Exception error,
	bool shouldRetry = true,
	TimeSpan? retryDelay = null)
{
	/// <summary>
	/// Gets the message ID.
	/// </summary>
	/// <value>
	/// The message ID.
	/// </value>
	public string MessageId { get; } = messageId ?? throw new ArgumentNullException(nameof(messageId));

	/// <summary>
	/// Gets the acknowledgment ID.
	/// </summary>
	/// <value>
	/// The acknowledgment ID.
	/// </value>
	public string AckId { get; } = ackId ?? throw new ArgumentNullException(nameof(ackId));

	/// <summary>
	/// Gets the error that occurred.
	/// </summary>
	/// <value>
	/// The error that occurred.
	/// </value>
	public Exception Error { get; } = error ?? throw new ArgumentNullException(nameof(error));

	/// <summary>
	/// Gets a value indicating whether gets whether the message should be retried.
	/// </summary>
	/// <value>
	/// A value indicating whether gets whether the message should be retried.
	/// </value>
	public bool ShouldRetry { get; } = shouldRetry;

	/// <summary>
	/// Gets the suggested retry delay.
	/// </summary>
	/// <value>
	/// The suggested retry delay.
	/// </value>
	public TimeSpan? RetryDelay { get; } = retryDelay;
}
