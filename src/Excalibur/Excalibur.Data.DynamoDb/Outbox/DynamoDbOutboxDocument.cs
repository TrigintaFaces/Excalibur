// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;

using Amazon.DynamoDBv2.Model;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Data.DynamoDb.Outbox;

/// <summary>
/// DynamoDB document representation of an outbox message using single-table design.
/// </summary>
/// <remarks>
/// <para>
/// Uses single-table design with the following key structure:
/// </para>
/// <list type="bullet">
/// <item><description>PK: OUTBOX#{status} - Enables efficient status-based queries</description></item>
/// <item><description>SK: {priority:D5}#{createdAt:O}#{messageId} - Enables ordering by priority then time</description></item>
/// <item><description>GSI1PK: MSG#{messageId} - Enables point lookup by message ID</description></item>
/// <item><description>GSI1SK: {messageId}</description></item>
/// <item><description>GSI2PK: SCHEDULED (only for scheduled messages)</description></item>
/// <item><description>GSI2SK: {scheduledAt:O}#{messageId}</description></item>
/// </list>
/// </remarks>
internal static class DynamoDbOutboxDocument
{
	// Attribute names
	public const string PK = "PK";

	public const string SK = "SK";

	public const string GSI1PK = "GSI1PK";

	public const string GSI1SK = "GSI1SK";

	public const string GSI2PK = "GSI2PK";

	public const string GSI2SK = "GSI2SK";

	public const string MessageId = "messageId";

	public const string MessageType = "messageType";

	public const string Destination = "destination";

	public const string Payload = "payload";

	public const string Headers = "headers";

	public const string Status = "status";

	public const string Priority = "priority";

	public const string CreatedAt = "createdAt";

	public const string ScheduledAt = "scheduledAt";

	public const string SentAt = "sentAt";

	public const string LastAttemptAt = "lastAttemptAt";

	public const string RetryCount = "retryCount";

	public const string LastError = "lastError";

	public const string CorrelationId = "correlationId";

	public const string CausationId = "causationId";

	public const string TenantId = "tenantId";

	public const string Ttl = "ttl";

	// Partition key prefixes
	public const string OutboxPrefix = "OUTBOX#";

	public const string MessagePrefix = "MSG#";

	public const string ScheduledPrefix = "SCHEDULED";

	private static readonly JsonSerializerOptions HeadersJsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

	/// <summary>
	/// Creates the partition key value for a given status.
	/// </summary>
	/// <param name="status">The outbox status.</param>
	/// <returns>The partition key value.</returns>
	public static string CreatePK(OutboxStatus status) => $"{OutboxPrefix}{status}";

	/// <summary>
	/// Creates the sort key value for ordering by priority then time.
	/// </summary>
	/// <param name="priority">The message priority.</param>
	/// <param name="createdAt">The creation timestamp.</param>
	/// <param name="messageId">The message ID.</param>
	/// <returns>The sort key value.</returns>
	public static string CreateSK(int priority, DateTimeOffset createdAt, string messageId) =>
		$"{priority:D5}#{createdAt.ToString("O", CultureInfo.InvariantCulture)}#{messageId}";

	/// <summary>
	/// Creates the GSI1 partition key for message lookup.
	/// </summary>
	/// <param name="messageId">The message ID.</param>
	/// <returns>The GSI1 partition key value.</returns>
	public static string CreateGSI1PK(string messageId) => $"{MessagePrefix}{messageId}";

	/// <summary>
	/// Creates the GSI2 sort key for scheduled messages.
	/// </summary>
	/// <param name="scheduledAt">The scheduled timestamp.</param>
	/// <param name="messageId">The message ID.</param>
	/// <returns>The GSI2 sort key value.</returns>
	public static string CreateGSI2SK(DateTimeOffset scheduledAt, string messageId) =>
		$"{scheduledAt.ToString("O", CultureInfo.InvariantCulture)}#{messageId}";

	/// <summary>
	/// Converts an <see cref="OutboundMessage"/> to a DynamoDB item.
	/// </summary>
	/// <param name="message">The outbound message.</param>
	/// <param name="ttlSeconds">Optional TTL in seconds (0 = no TTL).</param>
	/// <returns>The DynamoDB item attributes.</returns>
	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	public static Dictionary<string, AttributeValue> FromOutboundMessage(OutboundMessage message, int ttlSeconds = 0)
	{
		var item = new Dictionary<string, AttributeValue>
		{
			[PK] = new() { S = CreatePK(message.Status) },
			[SK] = new() { S = CreateSK(message.Priority, message.CreatedAt, message.Id) },
			[GSI1PK] = new() { S = CreateGSI1PK(message.Id) },
			[GSI1SK] = new() { S = message.Id },
			[MessageId] = new() { S = message.Id },
			[MessageType] = new() { S = message.MessageType },
			[Destination] = new() { S = message.Destination },
			[Payload] = new() { B = new MemoryStream(message.Payload) },
			[Status] = new() { N = ((int)message.Status).ToString(CultureInfo.InvariantCulture) },
			[Priority] = new() { N = message.Priority.ToString(CultureInfo.InvariantCulture) },
			[CreatedAt] = new() { S = message.CreatedAt.ToString("O", CultureInfo.InvariantCulture) },
			[RetryCount] = new() { N = message.RetryCount.ToString(CultureInfo.InvariantCulture) }
		};

		if (message.ScheduledAt.HasValue)
		{
			item[ScheduledAt] = new() { S = message.ScheduledAt.Value.ToString("O", CultureInfo.InvariantCulture) };
			item[GSI2PK] = new() { S = ScheduledPrefix };
			item[GSI2SK] = new() { S = CreateGSI2SK(message.ScheduledAt.Value, message.Id) };
		}

		if (message.SentAt.HasValue)
		{
			item[SentAt] = new() { S = message.SentAt.Value.ToString("O", CultureInfo.InvariantCulture) };
		}

		if (message.LastAttemptAt.HasValue)
		{
			item[LastAttemptAt] = new() { S = message.LastAttemptAt.Value.ToString("O", CultureInfo.InvariantCulture) };
		}

		if (!string.IsNullOrEmpty(message.LastError))
		{
			item[LastError] = new() { S = message.LastError };
		}

		if (!string.IsNullOrEmpty(message.CorrelationId))
		{
			item[CorrelationId] = new() { S = message.CorrelationId };
		}

		if (!string.IsNullOrEmpty(message.CausationId))
		{
			item[CausationId] = new() { S = message.CausationId };
		}

		if (!string.IsNullOrEmpty(message.TenantId))
		{
			item[TenantId] = new() { S = message.TenantId };
		}

		if (message.Headers.Count > 0)
		{
			item[Headers] = new() { S = JsonSerializer.Serialize(message.Headers, HeadersJsonOptions) };
		}

		if (ttlSeconds > 0)
		{
			var ttlValue = DateTimeOffset.UtcNow.AddSeconds(ttlSeconds).ToUnixTimeSeconds();
			item[Ttl] = new() { N = ttlValue.ToString(CultureInfo.InvariantCulture) };
		}

		return item;
	}

	/// <summary>
	/// Converts a DynamoDB item to an <see cref="OutboundMessage"/>.
	/// </summary>
	/// <param name="item">The DynamoDB item attributes.</param>
	/// <returns>The outbound message.</returns>
	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions)")]
	public static OutboundMessage ToOutboundMessage(Dictionary<string, AttributeValue> item)
	{
		var message = new OutboundMessage
		{
			Id = item[MessageId].S,
			MessageType = item[MessageType].S,
			Destination = item[Destination].S,
			Payload = item[Payload].B.ToArray(),
			Status = (OutboxStatus)int.Parse(item[Status].N, CultureInfo.InvariantCulture),
			Priority = int.Parse(item[Priority].N, CultureInfo.InvariantCulture),
			CreatedAt = DateTimeOffset.Parse(item[CreatedAt].S, CultureInfo.InvariantCulture),
			RetryCount = int.Parse(item[RetryCount].N, CultureInfo.InvariantCulture)
		};

		if (item.TryGetValue(ScheduledAt, out var scheduledAt) && !string.IsNullOrEmpty(scheduledAt.S))
		{
			message.ScheduledAt = DateTimeOffset.Parse(scheduledAt.S, CultureInfo.InvariantCulture);
		}

		if (item.TryGetValue(SentAt, out var sentAt) && !string.IsNullOrEmpty(sentAt.S))
		{
			message.SentAt = DateTimeOffset.Parse(sentAt.S, CultureInfo.InvariantCulture);
		}

		if (item.TryGetValue(LastAttemptAt, out var lastAttemptAt) && !string.IsNullOrEmpty(lastAttemptAt.S))
		{
			message.LastAttemptAt = DateTimeOffset.Parse(lastAttemptAt.S, CultureInfo.InvariantCulture);
		}

		if (item.TryGetValue(LastError, out var lastError) && !string.IsNullOrEmpty(lastError.S))
		{
			message.LastError = lastError.S;
		}

		if (item.TryGetValue(CorrelationId, out var correlationId) && !string.IsNullOrEmpty(correlationId.S))
		{
			message.CorrelationId = correlationId.S;
		}

		if (item.TryGetValue(CausationId, out var causationId) && !string.IsNullOrEmpty(causationId.S))
		{
			message.CausationId = causationId.S;
		}

		if (item.TryGetValue(TenantId, out var tenantId) && !string.IsNullOrEmpty(tenantId.S))
		{
			message.TenantId = tenantId.S;
		}

		if (item.TryGetValue(Headers, out var headers) && !string.IsNullOrEmpty(headers.S))
		{
			var headerDict = JsonSerializer.Deserialize<Dictionary<string, object>>(headers.S, HeadersJsonOptions);
			if (headerDict != null)
			{
				foreach (var kvp in headerDict)
				{
					message.Headers[kvp.Key] = kvp.Value;
				}
			}
		}

		return message;
	}

	/// <summary>
	/// Creates a new item with updated status for TransactWriteItems.
	/// </summary>
	/// <param name="existingItem">The existing DynamoDB item.</param>
	/// <param name="newStatus">The new status.</param>
	/// <param name="ttlSeconds">Optional TTL in seconds (0 = no TTL).</param>
	/// <returns>A new item with updated PK and SK for the new status.</returns>
	public static Dictionary<string, AttributeValue> WithStatus(
		Dictionary<string, AttributeValue> existingItem,
		OutboxStatus newStatus,
		int ttlSeconds = 0)
	{
		var messageId = existingItem[MessageId].S;
		var priority = int.Parse(existingItem[Priority].N, CultureInfo.InvariantCulture);
		var createdAt = DateTimeOffset.Parse(existingItem[CreatedAt].S, CultureInfo.InvariantCulture);

		var newItem = new Dictionary<string, AttributeValue>(existingItem)
		{
			[PK] = new() { S = CreatePK(newStatus) },
			[SK] = new() { S = CreateSK(priority, createdAt, messageId) },
			[Status] = new() { N = ((int)newStatus).ToString(CultureInfo.InvariantCulture) }
		};

		if (newStatus == OutboxStatus.Sent)
		{
			newItem[SentAt] = new() { S = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture) };

			if (ttlSeconds > 0)
			{
				var ttlValue = DateTimeOffset.UtcNow.AddSeconds(ttlSeconds).ToUnixTimeSeconds();
				newItem[Ttl] = new() { N = ttlValue.ToString(CultureInfo.InvariantCulture) };
			}
		}

		return newItem;
	}
}
