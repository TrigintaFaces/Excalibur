// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json.Serialization;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Data.CosmosDb.Inbox;

/// <summary>
/// Cosmos DB document representation of an inbox entry.
/// </summary>
internal sealed class CosmosDbInboxDocument
{
	/// <summary>
	/// Gets or sets the document ID (composite key: messageId:handlerType).
	/// </summary>
	[JsonPropertyName("id")]
	public string Id { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the message ID.
	/// </summary>
	[JsonPropertyName("message_id")]
	public string MessageId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the handler type.
	/// </summary>
	[JsonPropertyName("handler_type")]
	public string HandlerType { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the message type.
	/// </summary>
	[JsonPropertyName("message_type")]
	public string MessageType { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the message payload as Base64 encoded string.
	/// </summary>
	[JsonPropertyName("payload")]
	public string Payload { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the message metadata.
	/// </summary>
	[JsonPropertyName("metadata")]
	public IDictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

	/// <summary>
	/// Gets or sets the inbox status.
	/// </summary>
	[JsonPropertyName("status")]
	public int Status { get; set; }

	/// <summary>
	/// Gets or sets when the message was received.
	/// </summary>
	[JsonPropertyName("received_at")]
	public DateTimeOffset ReceivedAt { get; set; }

	/// <summary>
	/// Gets or sets when the message was processed.
	/// </summary>
	[JsonPropertyName("processed_at")]
	public DateTimeOffset? ProcessedAt { get; set; }

	/// <summary>
	/// Gets or sets when the last attempt was made.
	/// </summary>
	[JsonPropertyName("last_attempt_at")]
	public DateTimeOffset? LastAttemptAt { get; set; }

	/// <summary>
	/// Gets or sets the retry count.
	/// </summary>
	[JsonPropertyName("retry_count")]
	public int RetryCount { get; set; }

	/// <summary>
	/// Gets or sets the last error message if failed.
	/// </summary>
	[JsonPropertyName("last_error")]
	public string? LastError { get; set; }

	/// <summary>
	/// Creates a composite document ID from message ID and handler type.
	/// </summary>
	/// <param name="messageId">The message ID.</param>
	/// <param name="handlerType">The handler type.</param>
	/// <returns>The composite document ID.</returns>
	public static string CreateId(string messageId, string handlerType)
		=> $"{messageId}:{handlerType}";

	/// <summary>
	/// Creates a document from an <see cref="InboxEntry"/>.
	/// </summary>
	/// <param name="entry">The inbox entry.</param>
	/// <returns>The Cosmos DB document.</returns>
	public static CosmosDbInboxDocument FromInboxEntry(InboxEntry entry)
	{
		return new CosmosDbInboxDocument
		{
			Id = CreateId(entry.MessageId, entry.HandlerType),
			MessageId = entry.MessageId,
			HandlerType = entry.HandlerType,
			MessageType = entry.MessageType,
			Payload = entry.Payload.Length > 0 ? Convert.ToBase64String(entry.Payload) : string.Empty,
			Metadata = entry.Metadata,
			Status = (int)entry.Status,
			ReceivedAt = entry.ReceivedAt,
			ProcessedAt = entry.ProcessedAt,
			LastAttemptAt = entry.LastAttemptAt,
			RetryCount = entry.RetryCount,
			LastError = entry.LastError
		};
	}

	/// <summary>
	/// Converts this document to an <see cref="InboxEntry"/>.
	/// </summary>
	/// <returns>The inbox entry.</returns>
	public InboxEntry ToInboxEntry()
	{
		return new InboxEntry
		{
			MessageId = MessageId,
			HandlerType = HandlerType,
			MessageType = MessageType,
			Payload = string.IsNullOrEmpty(Payload) ? [] : Convert.FromBase64String(Payload),
			Metadata = Metadata,
			Status = (InboxStatus)Status,
			ReceivedAt = ReceivedAt,
			ProcessedAt = ProcessedAt,
			LastAttemptAt = LastAttemptAt,
			RetryCount = RetryCount,
			LastError = LastError
		};
	}
}
