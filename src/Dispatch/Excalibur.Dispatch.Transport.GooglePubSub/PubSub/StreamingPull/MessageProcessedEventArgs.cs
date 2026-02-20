// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Event arguments for message processed events.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="MessageProcessedEventArgs" /> class. </remarks>
public sealed class MessageProcessedEventArgs(string messageId, bool success, TimeSpan duration, Exception? error = null) : EventArgs
{
	/// <summary>
	/// Gets the message ID.
	/// </summary>
	/// <value>
	/// The message ID.
	/// </value>
	public string MessageId { get; } = messageId;

	/// <summary>
	/// Gets a value indicating whether gets whether the message was successfully processed.
	/// </summary>
	/// <value>
	/// A value indicating whether gets whether the message was successfully processed.
	/// </value>
	public bool Success { get; } = success;

	/// <summary>
	/// Gets the processing duration.
	/// </summary>
	/// <value>
	/// The processing duration.
	/// </value>
	public TimeSpan Duration { get; } = duration;

	/// <summary>
	/// Gets the exception if processing failed.
	/// </summary>
	/// <value>
	/// The exception if processing failed.
	/// </value>
	public Exception? Error { get; } = error;
}
