// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Event arguments for message enqueued events.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="MessageEnqueuedEventArgs" /> class. </remarks>
public sealed class MessageEnqueuedEventArgs(string streamId, string messageId) : EventArgs
{
	/// <summary>
	/// Gets the stream ID.
	/// </summary>
	/// <value>
	/// The stream ID.
	/// </value>
	public string StreamId { get; } = streamId;

	/// <summary>
	/// Gets the message ID.
	/// </summary>
	/// <value>
	/// The message ID.
	/// </value>
	public string MessageId { get; } = messageId;
}
