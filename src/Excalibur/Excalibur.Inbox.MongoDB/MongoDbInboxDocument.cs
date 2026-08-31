// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Excalibur.Inbox.MongoDB;

/// <summary>
/// MongoDB document model for inbox entries.
/// </summary>
/// <remarks>
/// <para>
/// Uses compound key: MessageId + HandlerType as the document _id.
/// </para>
/// <para>
/// Every instant on this document is stored as a BSON date, which is what
/// <see cref="BsonRepresentationAttribute"/> is doing on each of them. The driver's default for
/// <see cref="DateTimeOffset"/> is a three-field sub-document (<c>{ DateTime, Ticks, Offset }</c>)
/// instead, and that shape is not a date to anything outside this driver: an aggregation that dates it,
/// an index intended as a date index, and a consumer reading this collection from another language all
/// see a structure where a timestamp belongs. A TTL index is the sharpest case — one declared over a
/// sub-document field expires nothing at all, silently, so the retention this store advertises does not
/// happen.
/// </para>
/// <para>
/// <b>This is a durable format change, and a collection can hold both shapes.</b> An entry written by an
/// earlier version of this package is on disk in the sub-document shape and stays there; nothing rewrites
/// it. Query operators are type-bracketed, so a date comparison does not match such a field rather than
/// matching it wrongly — an entry in the old shape would simply become invisible to the two queries that
/// bound this collection's growth. Both of those sites therefore read either shape rather than assuming
/// the one this class writes.
/// </para>
/// <para>
/// Reading is unaffected: the driver's serializer accepts either shape for a
/// <see cref="DateTimeOffset"/> whatever representation is declared here, so an entry in either shape
/// materialises into this class with the instant it was written from. What the attribute governs is how
/// an instant is WRITTEN.
/// </para>
/// </remarks>
internal sealed class MongoDbInboxDocument
{
	/// <summary>
	/// Gets or sets the compound document ID produced by <see cref="CreateId"/>:
	/// <c>{TenantId}:{MessageId}:{HandlerType}</c>, each term percent-escaped so the join stays
	/// injective. Opaque -- any term may itself contain a colon, so the id is never parsed back into
	/// its segments; each segment is stored in its own field instead.
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
	[BsonRepresentation(BsonType.DateTime)]
	public DateTimeOffset ReceivedAt { get; set; }

	/// <summary>
	/// Gets or sets when the message was processed.
	/// </summary>
	[BsonElement("processedAt")]
	[BsonRepresentation(BsonType.DateTime)]
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
	[BsonRepresentation(BsonType.DateTime)]
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
	/// Gets or sets the server-clock expiry of the current processing lease, written by the atomic
	/// lease-claim pipeline. Mapped as a first-class field so typed reads do not treat it as an
	/// unmapped extra element (the store owns this field; it is not part of <see cref="InboxEntry"/>).
	/// </summary>
	[BsonElement("leaseExpiresAt")]
	[BsonRepresentation(BsonType.DateTime)]
	public DateTime? LeaseExpiresAt { get; set; }

	/// <summary>
	/// Creates the compound document ID from message and handler, optionally tenant-scoped.
	/// </summary>
	/// <param name="messageId">The message identifier.</param>
	/// <param name="handlerType">The handler type.</param>
	/// <param name="tenantId">
	/// When non-null (active multi-tenancy), the tenant is composed INTO the unique <c>_id</c> so two tenants
	/// carrying the same <c>(messageId, handlerType)</c> can never collide on the unique key — closing the
	/// cross-tenant false-dedup (silent message loss) leak structurally. When null (non-multi-tenant), the id
	/// is byte-identical to the un-scoped form.
	/// </param>
	/// <returns>The compound ID string.</returns>
	public static string CreateId(string messageId, string handlerType, string? tenantId = null) =>
		tenantId is null
			? $"{EscapeSegment(messageId)}:{EscapeSegment(handlerType)}"
			: $"{EscapeSegment(tenantId)}:{EscapeSegment(messageId)}:{EscapeSegment(handlerType)}";

	// The ':' joining the terms is not injective on its own. Neither the tenant term nor the message id is
	// validated against any charset -- both are caller data -- so tenant "a:b" with message "c" and tenant
	// "a" with message "b:c" both rendered "a:b:c:<handler>" and became ONE document sharing one unique
	// _id. This is the dedup key, so the collision does not surface as an error: the second message reads
	// as already-processed and is dropped, silently, across a tenant boundary.
	//
	// '%' is escaped FIRST and is what makes the encoding reversible. Escaping only ':' would map the
	// distinct terms "a:b" and "a%3Ab" onto one id -- a collision introduced by the escaping itself.
	//
	// This is deliberately a no-op for any term containing neither '%' nor ':'. The _id is PERSISTED and is
	// the dedup key, so an encoding that moved every existing document would orphan every in-flight dedup
	// record on upgrade and re-deliver already-processed messages. Under this encoding the only ids whose
	// bytes change are the ones that were ambiguous before, which had no single correct owner anyway.
	private static string EscapeSegment(string value) =>
		value.Replace("%", "%25", StringComparison.Ordinal)
			.Replace(":", "%3A", StringComparison.Ordinal);

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
