// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Globalization;
using System.Text;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Outbox;

using StackExchange.Redis;

namespace Excalibur.Outbox.Redis;

/// <summary>Decodes atomic claim responses and maps stored Redis hashes to outbound messages.</summary>
internal static class RedisOutboxMessageCodec
{
	private static readonly UTF8Encoding ClaimResponseTextEncoding = new(false, true);

	internal static List<OutboundMessage> DecodeClaimSnapshots(RedisResult response, int batchSize,
		string claimIdentity, CancellationToken cancellationToken)
	{
		var rows = ReadClaimArray(response);
		if (rows.Length > batchSize)
		{
			throw new InvalidOperationException("Redis acquisition response exceeds the requested batch size.");
		}

		var decoded = new List<OutboundMessage>(rows.Length);
		var ids = new HashSet<string>(StringComparer.Ordinal);
		foreach (var rawRow in rows)
		{
			var row = ReadClaimArray(rawRow);
			if (row.Length != 2)
			{
				throw new InvalidOperationException("Redis acquisition row must contain an ID and hash snapshot.");
			}
			var id = ReadClaimText(row[0]);
			if (!ids.Add(id))
			{
				throw new InvalidOperationException("Redis acquisition response contains duplicate message IDs.");
			}

			var fields = ReadClaimArray(row[1]);
			if (fields.Length == 0 || fields.Length % 2 != 0)
			{
				throw new InvalidOperationException("Redis acquisition hash must contain complete field/value pairs.");
			}
			var names = new HashSet<string>(StringComparer.Ordinal);
			var entries = new HashEntry[fields.Length / 2];
			string? owner = null;
			for (var i = 0; i < fields.Length; i += 2)
			{
				var name = ReadClaimText(fields[i]);
				if (!names.Add(name))
				{
					throw new InvalidOperationException("Redis acquisition hash contains duplicate field names.");
				}
				var value = ReadClaimValue(fields[i + 1]);
				entries[i / 2] = new HashEntry(name, value);
				if (string.Equals(name, "LeasedBy", StringComparison.Ordinal))
				{
					owner = ReadClaimText(fields[i + 1]);
				}
			}
			if (!string.Equals(owner, claimIdentity, StringComparison.Ordinal))
			{
				throw new InvalidOperationException("Redis acquisition hash does not carry the captured claim identity.");
			}

			var message = DeserializeFromHashEntries(id, entries);
			// The mapper does not populate acquisition authority. Copy the validated captured value once.
			message.DispatcherId = owner;
			decoded.Add(message);
		}

		// Validation/mapping of the whole batch precedes cancellation selection, including a malformed later row.
		var selected = new List<OutboundMessage>(decoded.Count);
		foreach (var message in decoded)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				break;
			}
			selected.Add(message);
		}
		return selected;
	}

	private static RedisResult[] ReadClaimArray(RedisResult? value)
	{
		if (value is null || value.IsNull || value.Resp3Type != ResultType.Array)
		{
			throw new InvalidOperationException("Redis acquisition response requires a nonnull array.");
		}
		return (RedisResult[]?)value ?? throw new InvalidOperationException("Redis acquisition array is null.");
	}

	private static RedisValue ReadClaimValue(RedisResult? value)
	{
		if (value is null || value.IsNull
			|| value.Resp3Type is not (ResultType.BulkString or ResultType.SimpleString))
		{
			throw new InvalidOperationException("Redis acquisition hash requires nonnull string scalar values.");
		}
		return (RedisValue)value;
	}

	private static string ReadClaimText(RedisResult? value)
	{
		var bytes = (byte[])ReadClaimValue(value)!;
		var text = ClaimResponseTextEncoding.GetString(bytes);
		if (text.Length == 0)
		{
			throw new InvalidOperationException("Redis acquisition IDs, names and owner must be nonempty text.");
		}
		return text;
	}

	internal static OutboundMessage DeserializeFromHashEntries(string messageId, HashEntry[] entries)
	{
		var dict = entries.ToDictionary(
			e => e.Name.ToString(),
			e => e.Value,
			StringComparer.Ordinal);

		// Restore Headers from the JSON-serialized hash entry written in SerializeToHashEntries
		// (init-only on OutboundMessage → must be set in the object initializer below).
		var headers = dict.TryGetValue("Headers", out var headersValue) && !headersValue.IsNullOrEmpty
			? OutboxHeaderJson.Deserialize((string)headersValue!) ?? new Dictionary<string, object>(StringComparer.Ordinal)
			: new Dictionary<string, object>(StringComparer.Ordinal);

		var message = new OutboundMessage
		{
			Id = messageId,
			MessageType = dict.GetValueOrDefault("MessageType", string.Empty)!,
			Payload = (byte[])dict.GetValueOrDefault("Payload", RedisValue.EmptyString)!,
			Destination = dict.GetValueOrDefault("Destination", string.Empty)!,
			CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds((long)dict.GetValueOrDefault("CreatedAt", 0)),
			Status = (OutboxStatus)(int)dict.GetValueOrDefault("Status", 0),
			Priority = (int)dict.GetValueOrDefault("Priority", 0),
			RetryCount = (int)dict.GetValueOrDefault("RetryCount", 0),
			IsMultiTransport = (int)dict.GetValueOrDefault("IsMultiTransport", 0) == 1,
			Headers = headers,
		};

		if (dict.TryGetValue("CorrelationId", out var correlationId) && !correlationId.IsNullOrEmpty)
		{
			message.CorrelationId = correlationId!;
		}

		if (dict.TryGetValue("CausationId", out var causationId) && !causationId.IsNullOrEmpty)
		{
			message.CausationId = causationId!;
		}

		// Read-tolerant: a row written before this fix (or one whose field rolled off) carries no
		// TenantId entry at all. FromStoredValue folds a missing field the same way it folds a stored
		// null/empty/sentinel — onto Untenanted — so an old-format row reads back identically to one
		// written under the new format, and TenantId is never null after this store reloads it.
		message.TenantId = KeyedTenantPartition.FromStoredValue(
			dict.TryGetValue("TenantId", out var tenantId) ? (string?)tenantId : null).TenantId;

		if (dict.TryGetValue("PartitionKey", out var partitionKey) && !partitionKey.IsNullOrEmpty)
		{
			message.PartitionKey = partitionKey!;
		}

		if (dict.TryGetValue("SequenceNumber", out var sequenceNumber))
		{
			message.SequenceNumber = long.Parse(sequenceNumber.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture);
		}

		if (dict.TryGetValue("GroupKey", out var groupKey) && !groupKey.IsNullOrEmpty)
		{
			message.GroupKey = groupKey!;
		}

		if (dict.TryGetValue("TargetTransports", out var targetTransports) && !targetTransports.IsNullOrEmpty)
		{
			message.TargetTransports = targetTransports!;
		}

		if (dict.TryGetValue("LastError", out var lastError) && !lastError.IsNullOrEmpty)
		{
			message.LastError = lastError!;
		}

		if (dict.TryGetValue("ScheduledAt", out var scheduledAt) && scheduledAt != 0)
		{
			message.ScheduledAt = DateTimeOffset.FromUnixTimeMilliseconds((long)scheduledAt);
		}

		if (dict.TryGetValue("SentAt", out var sentAt) && sentAt != 0)
		{
			message.SentAt = DateTimeOffset.FromUnixTimeMilliseconds((long)sentAt);
		}

		if (dict.TryGetValue("LastAttemptAt", out var lastAttemptAt) && lastAttemptAt != 0)
		{
			message.LastAttemptAt = DateTimeOffset.FromUnixTimeMilliseconds((long)lastAttemptAt);
		}

		return message;
	}
}
