// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Messages;

/// <summary>
/// Represents a message that has been moved to the dead letter queue.
/// </summary>
public sealed class DlqMessage
{
	/// <summary>
	/// Gets or sets the unique identifier of the message.
	/// </summary>
	/// <value> The current <see cref="MessageId" /> value. </value>
	public string MessageId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the original message body.
	/// </summary>
	/// <value> The current <see cref="Body" /> value. </value>
	public string Body { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the reason why the message was moved to the DLQ.
	/// </summary>
	/// <value> The current <see cref="Reason" /> value. </value>
	public string Reason { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the exception details if the message failed due to an error.
	/// </summary>
	/// <value> The current <see cref="ExceptionDetails" /> value. </value>
	public string? ExceptionDetails { get; set; }

	/// <summary>
	/// Gets or sets the number of processing attempts made.
	/// </summary>
	/// <value> The current <see cref="ProcessingAttempts" /> value. </value>
	public int ProcessingAttempts { get; set; }

	/// <summary>
	/// Gets or sets the timestamp when the message was first received.
	/// </summary>
	/// <value> The current <see cref="FirstReceivedAt" /> value. </value>
	public DateTime FirstReceivedAt { get; set; }

	/// <summary>
	/// Gets or sets the timestamp when the message was moved to the DLQ.
	/// </summary>
	/// <value> The current <see cref="MovedToDlqAt" /> value. </value>
	public DateTime MovedToDlqAt { get; set; }

	/// <summary>
	/// Gets or sets the source queue or topic name.
	/// </summary>
	/// <value> The current <see cref="SourceQueue" /> value. </value>
	public string SourceQueue { get; set; } = string.Empty;

	/// <summary>
	/// Gets the message headers.
	/// </summary>
	/// <value> The current <see cref="Headers" /> value. </value>
	public Dictionary<string, string> Headers { get; init; } = [];

	/// <summary>
	/// Gets additional metadata about the message.
	/// </summary>
	/// <value> The current <see cref="Metadata" /> value. </value>
	public Dictionary<string, object> Metadata { get; init; } = [];

	/// <summary>
	/// Gets or sets a value indicating whether the message can be retried.
	/// </summary>
	/// <value> The current <see cref="CanRetry" /> value. </value>
	public bool CanRetry { get; set; } = true;

	/// <summary>
	/// Gets or sets the message priority if applicable.
	/// </summary>
	/// <value> The current <see cref="Priority" /> value. </value>
	public int? Priority { get; set; }
}
