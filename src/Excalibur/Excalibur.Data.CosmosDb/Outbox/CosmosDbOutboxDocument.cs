// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Data.CosmosDb.Outbox;

/// <summary>
/// Cosmos DB document representation of an outbox message.
/// </summary>
/// <remarks>
/// <para>
/// Uses partition key based on status for efficient status-based queries.
/// This allows GetUnsentMessagesAsync to query a single partition.
/// </para>
/// </remarks>
internal sealed class CosmosDbOutboxDocument
{
	private static readonly JsonSerializerOptions HeadersJsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

	/// <summary>
	/// Gets or sets the document ID (message ID).
	/// </summary>
	[JsonPropertyName("id")]
	public string Id { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the partition key (status-based for efficient queries).
	/// </summary>
	[JsonPropertyName("partitionKey")]
	public string PartitionKey { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the message type.
	/// </summary>
	[JsonPropertyName("messageType")]
	public string MessageType { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the message payload as Base64 encoded string.
	/// </summary>
	[JsonPropertyName("payload")]
	public string Payload { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the destination.
	/// </summary>
	[JsonPropertyName("destination")]
	public string Destination { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the message headers as JSON string.
	/// </summary>
	[JsonPropertyName("headers")]
	public string? Headers { get; set; }

	/// <summary>
	/// Gets or sets the outbox status as integer.
	/// </summary>
	[JsonPropertyName("status")]
	public int Status { get; set; }

	/// <summary>
	/// Gets or sets the message priority.
	/// </summary>
	[JsonPropertyName("priority")]
	public int Priority { get; set; }

	/// <summary>
	/// Gets or sets when the message was created (ISO 8601).
	/// </summary>
	[JsonPropertyName("createdAt")]
	public string CreatedAt { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets when the message is scheduled for (ISO 8601).
	/// </summary>
	[JsonPropertyName("scheduledAt")]
	public string? ScheduledAt { get; set; }

	/// <summary>
	/// Gets or sets when the message was sent (ISO 8601).
	/// </summary>
	[JsonPropertyName("sentAt")]
	public string? SentAt { get; set; }

	/// <summary>
	/// Gets or sets when the last attempt was made (ISO 8601).
	/// </summary>
	[JsonPropertyName("lastAttemptAt")]
	public string? LastAttemptAt { get; set; }

	/// <summary>
	/// Gets or sets the retry count.
	/// </summary>
	[JsonPropertyName("retryCount")]
	public int RetryCount { get; set; }

	/// <summary>
	/// Gets or sets the last error message.
	/// </summary>
	[JsonPropertyName("lastError")]
	public string? LastError { get; set; }

	/// <summary>
	/// Gets or sets the correlation ID.
	/// </summary>
	[JsonPropertyName("correlationId")]
	public string? CorrelationId { get; set; }

	/// <summary>
	/// Gets or sets the causation ID.
	/// </summary>
	[JsonPropertyName("causationId")]
	public string? CausationId { get; set; }

	/// <summary>
	/// Gets or sets the tenant ID.
	/// </summary>
	[JsonPropertyName("tenantId")]
	public string? TenantId { get; set; }

	/// <summary>
	/// Gets or sets the ETag for optimistic concurrency.
	/// </summary>
	[JsonPropertyName("_etag")]
	public string? ETag { get; set; }

	/// <summary>
	/// Gets or sets the TTL in seconds for automatic document expiration.
	/// </summary>
	[JsonPropertyName("ttl")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? Ttl { get; set; }

	/// <summary>
	/// Creates the partition key value based on status.
	/// </summary>
	/// <param name="status">The outbox status.</param>
	/// <returns>The partition key value.</returns>
	public static string CreatePartitionKey(OutboxStatus status) => status.ToString();

	/// <summary>
	/// Creates a document from an <see cref="OutboundMessage"/>.
	/// </summary>
	/// <param name="message">The outbound message.</param>
	/// <returns>The Cosmos DB document.</returns>
	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	public static CosmosDbOutboxDocument FromOutboundMessage(OutboundMessage message)
	{
		var document = new CosmosDbOutboxDocument
		{
			Id = message.Id,
			PartitionKey = CreatePartitionKey(message.Status),
			MessageType = message.MessageType,
			Payload = message.Payload.Length > 0 ? Convert.ToBase64String(message.Payload) : string.Empty,
			Destination = message.Destination,
			Status = (int)message.Status,
			Priority = message.Priority,
			CreatedAt = message.CreatedAt.ToString("O"),
			RetryCount = message.RetryCount,
			LastError = message.LastError,
			CorrelationId = message.CorrelationId,
			CausationId = message.CausationId,
			TenantId = message.TenantId
		};

		if (message.ScheduledAt.HasValue)
		{
			document.ScheduledAt = message.ScheduledAt.Value.ToString("O");
		}

		if (message.SentAt.HasValue)
		{
			document.SentAt = message.SentAt.Value.ToString("O");
		}

		if (message.LastAttemptAt.HasValue)
		{
			document.LastAttemptAt = message.LastAttemptAt.Value.ToString("O");
		}

		if (message.Headers.Count > 0)
		{
			document.Headers = JsonSerializer.Serialize(message.Headers, HeadersJsonOptions);
		}

		return document;
	}

	/// <summary>
	/// Converts this document to an <see cref="OutboundMessage"/>.
	/// </summary>
	/// <returns>The outbound message.</returns>
	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions)")]
	public OutboundMessage ToOutboundMessage()
	{
		var message = new OutboundMessage
		{
			Id = Id,
			MessageType = MessageType,
			Payload = string.IsNullOrEmpty(Payload) ? [] : Convert.FromBase64String(Payload),
			Destination = Destination,
			Status = (OutboxStatus)Status,
			Priority = Priority,
			CreatedAt = DateTimeOffset.Parse(CreatedAt, CultureInfo.InvariantCulture),
			RetryCount = RetryCount,
			LastError = LastError,
			CorrelationId = CorrelationId,
			CausationId = CausationId,
			TenantId = TenantId
		};

		if (!string.IsNullOrEmpty(ScheduledAt))
		{
			message.ScheduledAt = DateTimeOffset.Parse(ScheduledAt, CultureInfo.InvariantCulture);
		}

		if (!string.IsNullOrEmpty(SentAt))
		{
			message.SentAt = DateTimeOffset.Parse(SentAt, CultureInfo.InvariantCulture);
		}

		if (!string.IsNullOrEmpty(LastAttemptAt))
		{
			message.LastAttemptAt = DateTimeOffset.Parse(LastAttemptAt, CultureInfo.InvariantCulture);
		}

		if (!string.IsNullOrEmpty(Headers))
		{
			var headers = JsonSerializer.Deserialize<Dictionary<string, object>>(Headers, HeadersJsonOptions);
			if (headers != null)
			{
				foreach (var kvp in headers)
				{
					message.Headers[kvp.Key] = kvp.Value;
				}
			}
		}

		return message;
	}
}
