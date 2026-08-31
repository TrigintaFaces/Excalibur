// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json.Serialization;

using Excalibur.Dispatch;

namespace Excalibur.Inbox.CosmosDb;

/// <summary>
/// Cosmos DB document representation of an inbox entry.
/// </summary>
internal sealed class CosmosDbInboxDocument
{
	/// <summary>
	/// Gets or sets the document ID: the composite dedup key <c>{tenantId}:{messageId}:{handlerType}</c>
	/// produced by <see cref="CreateId"/>. Opaque — any of its terms may itself contain a colon, so each
	/// term is percent-escaped before the join to keep the id injective, and the id is never parsed back
	/// into its segments; each segment is stored in its own field instead.
	/// </summary>
	[JsonPropertyName("id")]
	[Newtonsoft.Json.JsonProperty("id")]
	public string Id { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the message ID.
	/// </summary>
	[JsonPropertyName("message_id")]
	[Newtonsoft.Json.JsonProperty("message_id")]
	public string MessageId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the value of the container's partition-key field. Cosmos derives a document's partition
	/// from this field, so it must equal the partition the document is written to: the configured shared
	/// partition key when one is set, otherwise the handler type. It is therefore <strong>not</strong> a
	/// reliable source of the logical handler type — read <see cref="LogicalHandlerType"/> for that.
	/// </summary>
	[JsonPropertyName("handler_type")]
	[Newtonsoft.Json.JsonProperty("handler_type")]
	public string HandlerType { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the logical handler type this entry deduplicates — the value the store was called with,
	/// unaffected by partition placement. Carried separately from <see cref="HandlerType"/> because that
	/// field is claimed by the partition key and is overwritten with the shared partition value whenever one
	/// is configured. <see langword="null"/> only on a document written before this field existed.
	/// </summary>
	[JsonPropertyName("logical_handler_type")]
	[Newtonsoft.Json.JsonProperty("logical_handler_type")]
	public string? LogicalHandlerType { get; set; }

	/// <summary>
	/// Gets or sets the tenant discriminator. A component of the dedup <see cref="Id"/> so two tenants
	/// carrying the same message id and handler type never collide on the dedup key. Never empty: a real
	/// tenant, or the reserved untenanted sentinel when no tenant context is established.
	/// </summary>
	/// <remarks>
	/// Carries BOTH serializer attributes deliberately. The Cosmos v3 SDK's default serializer is Newtonsoft,
	/// and System.Text.Json is opt-in, so a property annotated only for STJ round-trips as PascalCase on a
	/// consumer-supplied client left on the default — the tenant discriminator would silently change key on
	/// exactly the setup a consumer who registers their own client gets. Every persisted property on this
	/// type carries the pair for that reason.
	/// </remarks>
	[JsonPropertyName("tenant_id")]
	[Newtonsoft.Json.JsonProperty("tenant_id")]
	public string TenantId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the message type.
	/// </summary>
	[JsonPropertyName("message_type")]
	[Newtonsoft.Json.JsonProperty("message_type")]
	public string MessageType { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the message payload as Base64 encoded string.
	/// </summary>
	[JsonPropertyName("payload")]
	[Newtonsoft.Json.JsonProperty("payload")]
	public string Payload { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the message metadata.
	/// </summary>
	[JsonPropertyName("metadata")]
	[Newtonsoft.Json.JsonProperty("metadata")]
	public IDictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

	/// <summary>
	/// Gets or sets the inbox status.
	/// </summary>
	[JsonPropertyName("status")]
	[Newtonsoft.Json.JsonProperty("status")]
	public int Status { get; set; }

	/// <summary>
	/// Gets or sets when the message was received.
	/// </summary>
	[JsonPropertyName("received_at")]
	[Newtonsoft.Json.JsonProperty("received_at")]
	public DateTimeOffset ReceivedAt { get; set; }

	/// <summary>
	/// Gets or sets when the message was processed.
	/// </summary>
	[JsonPropertyName("processed_at")]
	[Newtonsoft.Json.JsonProperty("processed_at")]
	public DateTimeOffset? ProcessedAt { get; set; }

	/// <summary>
	/// Gets or sets when the last attempt was made.
	/// </summary>
	[JsonPropertyName("last_attempt_at")]
	[Newtonsoft.Json.JsonProperty("last_attempt_at")]
	public DateTimeOffset? LastAttemptAt { get; set; }

	/// <summary>
	/// Gets or sets the retry count.
	/// </summary>
	[JsonPropertyName("retry_count")]
	[Newtonsoft.Json.JsonProperty("retry_count")]
	public int RetryCount { get; set; }

	/// <summary>
	/// Gets or sets the last error message if failed.
	/// </summary>
	[JsonPropertyName("last_error")]
	[Newtonsoft.Json.JsonProperty("last_error")]
	public string? LastError { get; set; }

	/// <summary>
	/// Gets or sets the per-item time-to-live, in seconds, applied once the entry reaches a terminal
	/// (processed) state so completed dedup records are reaped automatically. <see langword="null"/>
	/// (the default) leaves the entry non-expiring — Cosmos only honors this when the container has its
	/// TTL subsystem enabled.
	/// </summary>
	[JsonPropertyName("ttl")]
	[Newtonsoft.Json.JsonProperty("ttl")]
	public int? Ttl { get; set; }

	/// <summary>
	/// Creates a composite document ID from message ID, handler type, and tenant discriminator. When a
	/// tenant term is supplied it prefixes the id, so two tenants carrying the same message id and handler
	/// type never collide on the dedup key. A <see langword="null"/> term yields the tenant-less form (used
	/// only as a placeholder the store overrides with the ambient-tenant-composed id).
	/// </summary>
	/// <param name="messageId">The message ID.</param>
	/// <param name="handlerType">The handler type.</param>
	/// <param name="tenantId">The tenant discriminator term, or <see langword="null"/> for the tenant-less form.</param>
	/// <returns>The composite document ID.</returns>
	public static string CreateId(string messageId, string handlerType, string? tenantId = null)
		=> tenantId is null
			? $"{EscapeSegment(messageId)}:{EscapeSegment(handlerType)}"
			: $"{EscapeSegment(tenantId)}:{EscapeSegment(messageId)}:{EscapeSegment(handlerType)}";

	// The ':' joining the terms is not injective on its own. Neither the tenant term nor the message id is
	// validated against any charset -- both are caller data -- so tenant "a:b" with message "c" and tenant
	// "a" with message "b:c" both rendered "a:b:c:<handler>" and became ONE document. This is the dedup
	// key, so the collision does not surface as an error: the second message reads as already-processed
	// and is dropped, silently, across a tenant boundary.
	//
	// '%' is escaped FIRST and is what makes the encoding reversible. Escaping only ':' would map the
	// distinct terms "a:b" and "a%3Ab" onto one id -- a collision introduced by the escaping itself.
	//
	// This is deliberately a no-op for any term containing neither '%' nor ':'. The id is PERSISTED and is
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
	/// <returns>The Cosmos DB document.</returns>
	public static CosmosDbInboxDocument FromInboxEntry(InboxEntry entry)
	{
		return new CosmosDbInboxDocument
		{
			Id = CreateId(entry.MessageId, entry.HandlerType),
			MessageId = entry.MessageId,
			HandlerType = entry.HandlerType,
			LogicalHandlerType = entry.HandlerType,
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
		// The logical handler type is read from its own field, never recovered by parsing the id: the id's
		// tenant and message-id terms may themselves contain colons, so no length- or split-based rule can
		// recover its segments. A document written before that field existed carries null and falls back to
		// the partition-key field, which holds the true handler type exactly when no shared partition key is
		// configured; under a shared partition key that value was overwritten at write time and the logical
		// handler type was never persisted anywhere, so no correct recovery exists for such a document.
		// Every current write path populates the field, so the fallback is legacy-only.
		var realHandlerType = string.IsNullOrEmpty(LogicalHandlerType) ? HandlerType : LogicalHandlerType;

		return new InboxEntry
		{
			MessageId = MessageId,
			HandlerType = realHandlerType,
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
