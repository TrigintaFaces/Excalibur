// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
/// Property names are serialized camel-cased by the client the store builds, so <c>Id</c> lands as the
/// <c>id</c> Cosmos requires and <c>LeasedAt</c> as the <c>leasedAt</c> the claim predicate names. Anything
/// added here must keep that correspondence with the queries in <see cref="CosmosDbOutboxStore"/>.
/// </remarks>
internal sealed class CosmosDbOutboxDocument
{
	public required string Id { get; set; }

	public required string PartitionKey { get; set; }

	public required string MessageType { get; set; }

	public required string Payload { get; set; }

	public string? Headers { get; set; }

	public string? AggregateId { get; set; }

	public string? AggregateType { get; set; }

	public string? CorrelationId { get; set; }

	public string? CausationId { get; set; }

	public string? TenantId { get; set; }

	public string? Destination { get; set; }

	public required string CreatedAt { get; set; }

	public string? PublishedAt { get; set; }

	public bool IsPublished { get; set; }

	public int RetryCount { get; set; }

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
	public string? LeasedAt { get; set; }

	/// <summary>
	/// Gets or sets the claimant holding the current lease.
	/// </summary>
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
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = false
	};

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
#pragma warning disable IL2026, IL3050
			Headers = message.Headers != null
				? JsonSerializer.Serialize(message.Headers, JsonOptions)
				: null,
#pragma warning restore IL2026, IL3050
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
#pragma warning disable IL2026, IL3050
			Headers = !string.IsNullOrEmpty(doc.Headers)
				? JsonSerializer.Deserialize<Dictionary<string, string>>(doc.Headers, JsonOptions)
				: null,
#pragma warning restore IL2026, IL3050
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
