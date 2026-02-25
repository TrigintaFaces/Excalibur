// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Excalibur.Data.MongoDB.Inbox;

/// <summary>
/// MongoDB document model for inbox entries.
/// </summary>
/// <remarks>
/// Uses compound key: MessageId + HandlerType as the document _id.
/// </remarks>
internal sealed class MongoDbInboxDocument
{
	/// <summary>
	/// Gets or sets the compound document ID: {MessageId}:{HandlerType}.
	/// </summary>
	[BsonId]
	[BsonRepresentation(BsonType.String)]
	public string Id { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the message identifier.
	/// </summary>
	[BsonElement("messageId")]
	public string MessageId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the handler type.
	/// </summary>
	[BsonElement("handlerType")]
	public string HandlerType { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the message type.
	/// </summary>
	[BsonElement("messageType")]
	public string MessageType { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the serialized payload.
	/// </summary>
	[BsonElement("payload")]
	public byte[] Payload { get; set; } = [];

	/// <summary>
	/// Gets or sets the metadata dictionary.
	/// </summary>
	[BsonElement("metadata")]
	public Dictionary<string, object> Metadata { get; set; } = new(StringComparer.Ordinal);

	/// <summary>
	/// Gets or sets when the message was received.
	/// </summary>
	[BsonElement("receivedAt")]
	public DateTimeOffset ReceivedAt { get; set; }

	/// <summary>
	/// Gets or sets when the message was processed.
	/// </summary>
	[BsonElement("processedAt")]
	public DateTimeOffset? ProcessedAt { get; set; }

	/// <summary>
	/// Gets or sets the processing status.
	/// </summary>
	[BsonElement("status")]
	public int Status { get; set; }

	/// <summary>
	/// Gets or sets the last error message.
	/// </summary>
	[BsonElement("lastError")]
	public string? LastError { get; set; }

	/// <summary>
	/// Gets or sets the retry count.
	/// </summary>
	[BsonElement("retryCount")]
	public int RetryCount { get; set; }

	/// <summary>
	/// Gets or sets when the last attempt was made.
	/// </summary>
	[BsonElement("lastAttemptAt")]
	public DateTimeOffset? LastAttemptAt { get; set; }

	/// <summary>
	/// Gets or sets the correlation ID.
	/// </summary>
	[BsonElement("correlationId")]
	public string? CorrelationId { get; set; }

	/// <summary>
	/// Gets or sets the tenant ID.
	/// </summary>
	[BsonElement("tenantId")]
	public string? TenantId { get; set; }

	/// <summary>
	/// Gets or sets the message source.
	/// </summary>
	[BsonElement("source")]
	public string? Source { get; set; }

	/// <summary>
	/// Creates the compound document ID from message and handler.
	/// </summary>
	/// <param name="messageId">The message identifier.</param>
	/// <param name="handlerType">The handler type.</param>
	/// <returns>The compound ID string.</returns>
	public static string CreateId(string messageId, string handlerType) =>
		$"{messageId}:{handlerType}";

	/// <summary>
	/// Creates a document from an <see cref="InboxEntry"/>.
	/// </summary>
	/// <param name="entry">The inbox entry.</param>
	/// <returns>The MongoDB document.</returns>
	public static MongoDbInboxDocument FromInboxEntry(InboxEntry entry)
	{
		ArgumentNullException.ThrowIfNull(entry);

		return new MongoDbInboxDocument
		{
			Id = CreateId(entry.MessageId, entry.HandlerType),
			MessageId = entry.MessageId,
			HandlerType = entry.HandlerType,
			MessageType = entry.MessageType,
			Payload = entry.Payload,
			Metadata = new Dictionary<string, object>(entry.Metadata, StringComparer.Ordinal),
			ReceivedAt = entry.ReceivedAt,
			ProcessedAt = entry.ProcessedAt,
			Status = (int)entry.Status,
			LastError = entry.LastError,
			RetryCount = entry.RetryCount,
			LastAttemptAt = entry.LastAttemptAt,
			CorrelationId = entry.CorrelationId,
			TenantId = entry.TenantId,
			Source = entry.Source
		};
	}

	/// <summary>
	/// Converts the document to an <see cref="InboxEntry"/>.
	/// </summary>
	/// <returns>The inbox entry.</returns>
	public InboxEntry ToInboxEntry()
	{
		var metadata = Metadata ?? new Dictionary<string, object>(StringComparer.Ordinal);

		return new InboxEntry
		{
			MessageId = MessageId,
			HandlerType = HandlerType,
			MessageType = MessageType,
			Payload = Payload,
			Metadata = metadata,
			ReceivedAt = ReceivedAt,
			ProcessedAt = ProcessedAt,
			Status = (InboxStatus)Status,
			LastError = LastError,
			RetryCount = RetryCount,
			LastAttemptAt = LastAttemptAt,
			CorrelationId = CorrelationId,
			TenantId = TenantId,
			Source = Source
		};
	}
}
