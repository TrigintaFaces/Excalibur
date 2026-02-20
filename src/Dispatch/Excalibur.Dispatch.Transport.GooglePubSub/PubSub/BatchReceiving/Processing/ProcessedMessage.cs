// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Represents a successfully processed message.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="ProcessedMessage" /> class. </remarks>
public sealed class ProcessedMessage(string messageId, string ackId, object result, TimeSpan duration)
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
	/// Gets the processing result data.
	/// </summary>
	/// <value>
	/// The processing result data.
	/// </value>
	public object Result { get; } = result;

	/// <summary>
	/// Gets the processing duration.
	/// </summary>
	/// <value>
	/// The processing duration.
	/// </value>
	public TimeSpan Duration { get; } = duration;
}
