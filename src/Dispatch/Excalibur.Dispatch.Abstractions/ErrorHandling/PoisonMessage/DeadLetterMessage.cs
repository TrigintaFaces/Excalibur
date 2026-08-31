// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.ErrorHandling;

/// <summary>
/// Represents a message that has been moved to the dead letter queue.
/// </summary>
public sealed class DeadLetterMessage
{
	/// <summary>
	/// Gets or sets the unique identifier of the dead letter message.
	/// </summary>
	/// <value>
	/// The unique identifier of the dead letter message.
	/// </value>
	public string Id { get; set; } = Guid.NewGuid().ToString("N");

	/// <summary>
	/// Gets or sets the original message ID.
	/// </summary>
	/// <value> The current <see cref="MessageId" /> value. </value>
	public required string MessageId { get; set; }

	/// <summary>
	/// Gets or sets the message type.
	/// </summary>
	/// <value> The current <see cref="MessageType" /> value. </value>
	public required string MessageType { get; set; }

	/// <summary>
	/// Gets or sets the serialized message body.
	/// </summary>
	/// <value> The current <see cref="MessageBody" /> value. </value>
	public required string MessageBody { get; set; }

	/// <summary>
	/// Gets or sets the serialized message metadata.
	/// </summary>
	/// <value> The current <see cref="MessageMetadata" /> value. </value>
	public required string MessageMetadata { get; set; }

	/// <summary>
	/// Gets or sets the reason why the message was moved to dead letter.
	/// </summary>
	/// <value> The current <see cref="Reason" /> value. </value>
	public required string Reason { get; set; }

	/// <summary>
	/// Gets or sets the exception details if the message failed due to an exception.
	/// </summary>
	/// <value> The current <see cref="ExceptionDetails" /> value. </value>
	public string? ExceptionDetails { get; set; }

	/// <summary>
	/// Gets or sets the number of processing attempts before moving to dead letter.
	/// </summary>
	/// <value> The current <see cref="ProcessingAttempts" /> value. </value>
	public int ProcessingAttempts { get; set; }

	/// <summary>
	/// Gets or sets the timestamp when the message was moved to dead letter.
	/// </summary>
	/// <value> The current <see cref="MovedToDeadLetterAt" /> value. </value>
	public DateTimeOffset MovedToDeadLetterAt { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets or sets the timestamp of the first processing attempt.
	/// </summary>
	/// <value> The current <see cref="FirstAttemptAt" /> value. </value>
	public DateTimeOffset? FirstAttemptAt { get; set; }

	/// <summary>
	/// Gets or sets the timestamp of the last processing attempt.
	/// </summary>
	/// <value> The current <see cref="LastAttemptAt" /> value. </value>
	public DateTimeOffset? LastAttemptAt { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether this message has been replayed.
	/// </summary>
	/// <value> The current <see cref="IsReplayed" /> value. </value>
	public bool IsReplayed { get; set; }

	/// <summary>
	/// Gets or sets the timestamp when the message was replayed.
	/// </summary>
	/// <value> The current <see cref="ReplayedAt" /> value. </value>
	public DateTimeOffset? ReplayedAt { get; set; }

	/// <summary>
	/// Gets or sets the source system that originated the message.
	/// </summary>
	/// <value> The current <see cref="SourceSystem" /> value. </value>
	public string? SourceSystem { get; set; }

	/// <summary>
	/// Gets or sets the correlation ID for tracking related messages.
	/// </summary>
	/// <value> The current <see cref="CorrelationId" /> value. </value>
	public string? CorrelationId { get; set; }

	/// <summary>
	/// Gets or sets additional custom properties.
	/// </summary>
	/// <value> The current <see cref="Properties" /> value. </value>
	public Dictionary<string, string> Properties { get; set; } = [];
}
