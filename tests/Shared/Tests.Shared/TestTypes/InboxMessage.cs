// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Tests.Shared.TestTypes;

/// <summary>
/// Represents a message stored in the inbox for deduplication and processing tracking.
/// Used in integration tests for inbox pattern validation.
/// </summary>
public class InboxMessage
{
	/// <summary>
	/// Initializes a new instance of the <see cref="InboxMessage"/> class.
	/// </summary>
	/// <param name="messageId">The unique message identifier.</param>
	/// <param name="messageType">The type of the message.</param>
	/// <param name="handlerType">The handler type for the message.</param>
	/// <param name="processedAt">The timestamp when the message was processed.</param>
	public InboxMessage(string messageId, string messageType, string handlerType, DateTimeOffset? processedAt = null)
	{
		MessageId = messageId;
		MessageType = messageType;
		HandlerType = handlerType;
		ProcessedAt = processedAt ?? DateTimeOffset.UtcNow;
	}

	/// <summary>
	/// Gets or sets the message ID.
	/// </summary>
	public string MessageId { get; set; }

	/// <summary>
	/// Gets or sets the message type.
	/// </summary>
	public string MessageType { get; set; }

	/// <summary>
	/// Gets or sets the handler type.
	/// </summary>
	public string HandlerType { get; set; }

	/// <summary>
	/// Gets or sets the timestamp when the message was processed.
	/// </summary>
	public DateTimeOffset ProcessedAt { get; set; }

	/// <summary>
	/// Gets or sets the payload.
	/// </summary>
	public byte[]? Payload { get; set; }
}
