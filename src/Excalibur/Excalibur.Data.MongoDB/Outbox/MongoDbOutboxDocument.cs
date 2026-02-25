// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Excalibur.Data.MongoDB.Outbox;

/// <summary>
/// MongoDB document model for outbox messages.
/// </summary>
internal sealed class MongoDbOutboxDocument
{
	/// <summary>
	/// Gets or sets the document ID (message ID).
	/// </summary>
	[BsonId]
	[BsonRepresentation(BsonType.String)]
	public string Id { get; set; } = string.Empty;

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
	/// Gets or sets the message headers.
	/// </summary>
	[BsonElement("headers")]
	public Dictionary<string, object> Headers { get; set; } = new(StringComparer.Ordinal);

	/// <summary>
	/// Gets or sets the destination.
	/// </summary>
	[BsonElement("destination")]
	public string Destination { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets when the message was created.
	/// </summary>
	[BsonElement("createdAt")]
	public DateTimeOffset CreatedAt { get; set; }

	/// <summary>
	/// Gets or sets when the message is scheduled for delivery.
	/// </summary>
	[BsonElement("scheduledAt")]
	public DateTimeOffset? ScheduledAt { get; set; }

	/// <summary>
	/// Gets or sets when the message was sent.
	/// </summary>
	[BsonElement("sentAt")]
	public DateTimeOffset? SentAt { get; set; }

	/// <summary>
	/// Gets or sets the message status.
	/// </summary>
	[BsonElement("status")]
	public int Status { get; set; }

	/// <summary>
	/// Gets or sets the retry count.
	/// </summary>
	[BsonElement("retryCount")]
	public int RetryCount { get; set; }

	/// <summary>
	/// Gets or sets the last error message.
	/// </summary>
	[BsonElement("lastError")]
	public string? LastError { get; set; }

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
	/// Gets or sets the causation ID.
	/// </summary>
	[BsonElement("causationId")]
	public string? CausationId { get; set; }

	/// <summary>
	/// Gets or sets the tenant ID.
	/// </summary>
	[BsonElement("tenantId")]
	public string? TenantId { get; set; }

	/// <summary>
	/// Gets or sets the message priority.
	/// </summary>
	[BsonElement("priority")]
	public int Priority { get; set; }

	/// <summary>
	/// Creates a document from an <see cref="OutboundMessage"/>.
	/// </summary>
	/// <param name="message">The outbound message.</param>
	/// <returns>The MongoDB document.</returns>
	public static MongoDbOutboxDocument FromOutboundMessage(OutboundMessage message)
	{
		ArgumentNullException.ThrowIfNull(message);

		return new MongoDbOutboxDocument
		{
			Id = message.Id,
			MessageType = message.MessageType,
			Payload = message.Payload,
			Headers = new Dictionary<string, object>(message.Headers, StringComparer.Ordinal),
			Destination = message.Destination,
			CreatedAt = message.CreatedAt,
			ScheduledAt = message.ScheduledAt,
			SentAt = message.SentAt,
			Status = (int)message.Status,
			RetryCount = message.RetryCount,
			LastError = message.LastError,
			LastAttemptAt = message.LastAttemptAt,
			CorrelationId = message.CorrelationId,
			CausationId = message.CausationId,
			TenantId = message.TenantId,
			Priority = message.Priority
		};
	}

	/// <summary>
	/// Converts the document to an <see cref="OutboundMessage"/>.
	/// </summary>
	/// <returns>The outbound message.</returns>
	public OutboundMessage ToOutboundMessage()
	{
		return new OutboundMessage
		{
			Id = Id,
			MessageType = MessageType,
			Payload = Payload,
			Destination = Destination,
			CreatedAt = CreatedAt,
			ScheduledAt = ScheduledAt,
			SentAt = SentAt,
			Status = (OutboxStatus)Status,
			RetryCount = RetryCount,
			LastError = LastError,
			LastAttemptAt = LastAttemptAt,
			CorrelationId = CorrelationId,
			CausationId = CausationId,
			TenantId = TenantId,
			Priority = Priority
		};
	}
}
