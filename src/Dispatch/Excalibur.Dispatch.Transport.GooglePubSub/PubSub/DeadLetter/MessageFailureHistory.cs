// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Tracks the failure history of a message.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="MessageFailureHistory" /> class. </remarks>
public sealed class MessageFailureHistory(string messageId)
{
	/// <summary>
	/// Gets the message ID.
	/// </summary>
	/// <value>
	/// The message ID.
	/// </value>
	public string MessageId { get; } = messageId;

	/// <summary>
	/// Gets the list of failures.
	/// </summary>
	/// <value>
	/// The list of failures.
	/// </value>
	public List<FailureRecord> Failures { get; } = [];

	/// <summary>
	/// Gets or sets the last failure time.
	/// </summary>
	/// <value>
	/// The last failure time.
	/// </value>
	public DateTimeOffset LastFailureTime { get; set; }
}
