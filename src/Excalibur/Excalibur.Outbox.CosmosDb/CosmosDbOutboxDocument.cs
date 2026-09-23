// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

using Excalibur.Data.CloudNative;
using Excalibur.Dispatch;

namespace Excalibur.Outbox.CosmosDb;

/// <summary>
/// The stored shape of an outbox message in Cosmos DB.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every persisted property is mapped explicitly, for BOTH serializers, to the exact wire name the
/// queries in <see cref="CosmosDbOutboxStore"/> name.</b> Anything added here must do the same.
/// </para>
/// <para>
/// This does not depend on a naming policy, and that is the point. The client this store builds configures
/// System.Text.Json with a camelCase policy, but <c>ICosmosDbOutboxBuilder</c> also lets a consumer supply
/// their own <c>CosmosClient</c>, which bypasses that configuration entirely and uses the SDK default —
/// Newtonsoft, PascalCase. Under that shape an unmapped <c>LeasedAt</c> goes to the wire as
/// <c>LeasedAt</c>, the claim predicate's <c>c.leasedAt</c> is undefined on every document, and
/// <c>NOT IS_DEFINED(c.leasedAt)</c> is therefore TRUE for every row: every message reads as unclaimed no
/// matter who holds the lease, so the atomic claim is inert and two instances can publish the same message.
/// A claim predicate that fails OPEN is a duplicate-delivery defect, not a configuration mistake, so the
/// correspondence is pinned on the type rather than left to how the client happened to be built.
/// </para>
/// </remarks>
internal sealed class CosmosDbOutboxDocument
{
	[System.Text.Json.Serialization.JsonPropertyName("id")]
	[Newtonsoft.Json.JsonProperty("id")]
	public required string Id { get; set; }

	[System.Text.Json.Serialization.JsonPropertyName("partitionKey")]
	[Newtonsoft.Json.JsonProperty("partitionKey")]
	public required string PartitionKey { get; set; }

	[System.Text.Json.Serialization.JsonPropertyName("messageType")]
	[Newtonsoft.Json.JsonProperty("messageType")]
	public required string MessageType { get; set; }

	[System.Text.Json.Serialization.JsonPropertyName("payload")]
	[Newtonsoft.Json.JsonProperty("payload")]
	public required string Payload { get; set; }

	[System.Text.Json.Serialization.JsonPropertyName("headers")]
	[Newtonsoft.Json.JsonProperty("headers")]
	public string? Headers { get; set; }

	[System.Text.Json.Serialization.JsonPropertyName("aggregateId")]
	[Newtonsoft.Json.JsonProperty("aggregateId")]
	public string? AggregateId { get; set; }

	[System.Text.Json.Serialization.JsonPropertyName("aggregateType")]
	[Newtonsoft.Json.JsonProperty("aggregateType")]
	public string? AggregateType { get; set; }

	[System.Text.Json.Serialization.JsonPropertyName("correlationId")]
	[Newtonsoft.Json.JsonProperty("correlationId")]
	public string? CorrelationId { get; set; }

	[System.Text.Json.Serialization.JsonPropertyName("causationId")]
	[Newtonsoft.Json.JsonProperty("causationId")]
	public string? CausationId { get; set; }

	[System.Text.Json.Serialization.JsonPropertyName("tenantId")]
	[Newtonsoft.Json.JsonProperty("tenantId")]
	public string? TenantId { get; set; }

	[System.Text.Json.Serialization.JsonPropertyName("destination")]
	[Newtonsoft.Json.JsonProperty("destination")]
	public string? Destination { get; set; }

	[System.Text.Json.Serialization.JsonPropertyName("createdAt")]
	[Newtonsoft.Json.JsonProperty("createdAt")]
	public required string CreatedAt { get; set; }

	[System.Text.Json.Serialization.JsonPropertyName("publishedAt")]
	[Newtonsoft.Json.JsonProperty("publishedAt")]
	public string? PublishedAt { get; set; }

	[System.Text.Json.Serialization.JsonPropertyName("isPublished")]
	[Newtonsoft.Json.JsonProperty("isPublished")]
	public bool IsPublished { get; set; }

	[System.Text.Json.Serialization.JsonPropertyName("retryCount")]
	[Newtonsoft.Json.JsonProperty("retryCount")]
	public int RetryCount { get; set; }

	[System.Text.Json.Serialization.JsonPropertyName("lastError")]
	[Newtonsoft.Json.JsonProperty("lastError")]
	public string? LastError { get; set; }

	/// <summary>
	/// Gets or sets the server-assigned concurrency token.
	/// </summary>
	/// <remarks>
	/// Bound explicitly to Cosmos's system property name. Without this the property binds to <c>eTag</c>
	/// under the client's camelCase policy, which no document has, so the token read back was always
	/// <see langword="null"/> — leaving a caller that reads pending messages nothing to write conditionally
	/// against. Mapped for both serializers because the SDK's default is Newtonsoft while the client this
	/// store builds uses System.Text.Json, matching how the sibling Cosmos event store maps the same field.
	/// </remarks>
	[System.Text.Json.Serialization.JsonPropertyName("_etag")]
	[Newtonsoft.Json.JsonProperty("_etag")]
	public string? ETag { get; set; }

	/// <summary>
	/// Gets or sets the instant the current claim lease was stamped, round-trip formatted in UTC.
	/// </summary>
	/// <remarks>
	/// Round-trip ("o") format is fixed width, so the claim predicate can compare it against a cutoff with
	/// the ordinal string comparison Cosmos applies to a string range — no parsing server-side.
	/// </remarks>
	[System.Text.Json.Serialization.JsonPropertyName("leasedAt")]
	[Newtonsoft.Json.JsonProperty("leasedAt")]
	public string? LeasedAt { get; set; }

	/// <summary>
	/// Gets or sets the claimant holding the current lease.
	/// </summary>
	[System.Text.Json.Serialization.JsonPropertyName("leasedBy")]
	[Newtonsoft.Json.JsonProperty("leasedBy")]
	public string? LeasedBy { get; set; }

	/// <summary>
	/// Gets or sets the per-document time-to-live, in seconds.
	/// </summary>
	/// <remarks>
	/// Omitted from the wire when it has no value. Cosmos validates this property whenever it is present and
	/// rejects the whole write with <c>400 BadRequest</c> for anything that is not -1 or a positive integer —
	/// an explicit null included. Only the mark-published path sets it, so on the staging path it is always
	/// absent, and emitting it there made every staged message fail at the server.
	/// </remarks>
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? Ttl { get; set; }
}

/// <summary>
/// The projection used by queries that need only document identifiers.
/// </summary>
internal sealed class CosmosDbOutboxIdProjection
{
	[JsonPropertyName("id")]
	[Newtonsoft.Json.JsonProperty("id")]
	public string Id { get; set; } = string.Empty;
}

/// <summary>
/// Translates between the transport-facing <see cref="CloudOutboxMessage"/> and the stored document.
/// </summary>
/// <remarks>
/// Kept apart from the store so that the store owns the Cosmos protocol — queries, conditional writes,
/// telemetry — and this owns the representation. They change for different reasons.
/// </remarks>
internal static class CosmosDbOutboxDocumentMap
{
	/// <summary>Projects a message onto its stored document.</summary>
	/// <param name="message">The message to store.</param>
	/// <param name="partitionKey">The partition the document belongs to.</param>
	/// <returns>The document to write.</returns>
	public static CosmosDbOutboxDocument ToDocument(CloudOutboxMessage message, IPartitionKey partitionKey) =>
		new()
		{
			Id = message.MessageId,
			PartitionKey = partitionKey.Value,
			MessageType = message.MessageType,
			Payload = Convert.ToBase64String(message.Payload),
			Headers = message.Headers != null
				? JsonSerializer.Serialize(
					message.Headers,
					CosmosDbOutboxSerializerContext.Default.DictionaryStringString)
				: null,
			AggregateId = message.AggregateId,
			AggregateType = message.AggregateType,
			CorrelationId = message.CorrelationId,
			CausationId = message.CausationId,
			TenantId = KeyedTenantPartition.FromStoredValue(message.TenantId).TenantId,
			Destination = message.Destination,
			CreatedAt = message.CreatedAt.ToString("o", CultureInfo.InvariantCulture),
			PublishedAt = message.PublishedAt?.ToString("o", CultureInfo.InvariantCulture),
			IsPublished = message.IsPublished,
			RetryCount = message.RetryCount,
			LastError = message.LastError
		};

	/// <summary>Reconstitutes a message from its stored document.</summary>
	/// <param name="doc">The stored document.</param>
	/// <returns>The message, carrying whatever lease the document holds.</returns>
	public static CloudOutboxMessage FromDocument(CosmosDbOutboxDocument doc) =>
		new()
		{
			MessageId = doc.Id,
			MessageType = doc.MessageType,
			Payload = Convert.FromBase64String(doc.Payload),
			Headers = !string.IsNullOrEmpty(doc.Headers)
				? JsonSerializer.Deserialize(
					doc.Headers,
					CosmosDbOutboxSerializerContext.Default.DictionaryStringString)
				: null,
			AggregateId = doc.AggregateId,
			AggregateType = doc.AggregateType,
			CorrelationId = doc.CorrelationId,
			CausationId = doc.CausationId,
			TenantId = KeyedTenantPartition.FromStoredValue(doc.TenantId).TenantId,
			Destination = doc.Destination,
			CreatedAt = DateTimeOffset.Parse(doc.CreatedAt, CultureInfo.InvariantCulture),
			PublishedAt = !string.IsNullOrEmpty(doc.PublishedAt) ? DateTimeOffset.Parse(doc.PublishedAt, CultureInfo.InvariantCulture) : null,
			RetryCount = doc.RetryCount,
			LastError = doc.LastError,
			PartitionKeyValue = doc.PartitionKey,
			ETag = doc.ETag,
			LeasedAt = !string.IsNullOrEmpty(doc.LeasedAt) ? DateTimeOffset.Parse(doc.LeasedAt, CultureInfo.InvariantCulture) : null,
			LeasedBy = doc.LeasedBy
		};
}
