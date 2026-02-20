// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Data.DynamoDb.Cdc;

/// <summary>
/// Represents a position in DynamoDB Streams using shard sequence numbers.
/// </summary>
/// <remarks>
/// <para>
/// DynamoDB Streams are partitioned into shards. Each shard has its own sequence
/// of records. To track position, we must maintain sequence numbers for ALL active shards.
/// </para>
/// <para>
/// Shards can split or merge over time. When resuming, unknown shards are started
/// from TRIM_HORIZON to ensure no data is missed.
/// </para>
/// </remarks>
public sealed class DynamoDbCdcPosition : ChangePosition, IEquatable<DynamoDbCdcPosition>
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = false,

	};

	/// <summary>
	/// Gets the Stream ARN for validation.
	/// </summary>

	public string StreamArn { get; }

	/// <summary>
	/// Gets the map of shard ID to sequence number.
	/// </summary>

	public IReadOnlyDictionary<string, string> ShardPositions { get; }

	/// <summary>
	/// Gets the timestamp of the position (for diagnostics).
	/// </summary>

	public override DateTimeOffset? Timestamp { get; }

	/// <summary>
	/// Gets a value indicating whether this position is valid for resuming.
	/// </summary>

	public override bool IsValid => ShardPositions.Count > 0;

	/// <summary>
	/// Gets a value indicating whether this position represents the beginning of the stream.
	/// </summary>
	public bool IsBeginning => ShardPositions.Count == 0 && Timestamp is null;

	private DynamoDbCdcPosition(
		string streamArn,
		IReadOnlyDictionary<string, string>? shardPositions,
		DateTimeOffset? timestamp)
	{
		StreamArn = streamArn ?? string.Empty;
		ShardPositions = shardPositions ?? new Dictionary<string, string>();
		Timestamp = timestamp;

	}

	/// <summary>
	/// Creates a position representing the beginning of the stream (TRIM_HORIZON).
	/// </summary>
	/// <param name="streamArn">The Stream ARN.</param>
	/// <returns>A position that will read from the beginning.</returns>
	public static DynamoDbCdcPosition Beginning(string streamArn)
	{
		return new DynamoDbCdcPosition(streamArn, null, null);

	}

	/// <summary>
	/// Creates a position representing the current time (LATEST).
	/// </summary>
	/// <param name="streamArn">The Stream ARN.</param>
	/// <returns>A position that will read from now.</returns>
	public static DynamoDbCdcPosition Now(string streamArn)
	{
		return new DynamoDbCdcPosition(streamArn, null, DateTimeOffset.UtcNow);

	}

	/// <summary>
	/// Creates a position from shard sequence numbers.
	/// </summary>
	/// <param name="streamArn">The Stream ARN.</param>
	/// <param name="shardPositions">Map of shard ID to sequence number.</param>
	/// <returns>A position that will resume from the specified shard positions.</returns>
	public static DynamoDbCdcPosition FromShardPositions(
		string streamArn,
		IReadOnlyDictionary<string, string> shardPositions)
	{
		ArgumentNullException.ThrowIfNull(shardPositions);
		return new DynamoDbCdcPosition(streamArn, shardPositions, DateTimeOffset.UtcNow);

	}

	/// <summary>
	/// Updates the position for a specific shard.
	/// </summary>
	/// <param name="shardId">The shard ID.</param>
	/// <param name="sequenceNumber">The new sequence number.</param>
	/// <returns>A new position with the updated shard position.</returns>
	public DynamoDbCdcPosition WithShardPosition(string shardId, string sequenceNumber)
	{
		var newPositions = new Dictionary<string, string>(ShardPositions)
		{
			[shardId] = sequenceNumber
		};
		return new DynamoDbCdcPosition(StreamArn, newPositions, DateTimeOffset.UtcNow);

	}

	/// <summary>
	/// Removes a shard from the position (when shard is exhausted).
	/// </summary>
	/// <param name="shardId">The shard ID to remove.</param>
	/// <returns>A new position without the specified shard.</returns>
	public DynamoDbCdcPosition WithoutShard(string shardId)
	{
		var newPositions = new Dictionary<string, string>(ShardPositions);
		_ = newPositions.Remove(shardId);
		return new DynamoDbCdcPosition(StreamArn, newPositions, DateTimeOffset.UtcNow);

	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	public override string ToToken() => ToBase64();

	/// <inheritdoc/>
	public override bool Equals(ChangePosition? other) => other is DynamoDbCdcPosition dynamo && Equals(dynamo);

	/// <inheritdoc/>
	public override bool Equals(object? obj) => obj is DynamoDbCdcPosition dynamo && Equals(dynamo);

	/// <inheritdoc/>
	public override int GetHashCode()
	{
		var hash = new HashCode();
		hash.Add(StreamArn);

		foreach (var kvp in ShardPositions.OrderBy(x => x.Key, StringComparer.Ordinal))
		{
			hash.Add(kvp.Key);
			hash.Add(kvp.Value);
		}

		return hash.ToHashCode();
	}

	/// <summary>
	/// Serializes this position to a base64 string for storage.
	/// </summary>
	/// <returns>A base64-encoded string representation.</returns>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	public string ToBase64()
	{
		var data = new PositionData
		{
			StreamArn = StreamArn,
			ShardPositions = ShardPositions.ToDictionary(x => x.Key, x => x.Value),
			Timestamp = Timestamp,
		};

		var json = JsonSerializer.Serialize(data, JsonOptions);
		return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

	}

	/// <summary>
	/// Serializes this position to a byte array for storage.
	/// </summary>
	/// <returns>A UTF-8 encoded byte array representation.</returns>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	public byte[] ToBytes()
	{
		var data = new PositionData
		{
			StreamArn = StreamArn,
			ShardPositions = ShardPositions.ToDictionary(x => x.Key, x => x.Value),
			Timestamp = Timestamp,
		};

		var json = JsonSerializer.Serialize(data, JsonOptions);
		return Encoding.UTF8.GetBytes(json);
	}

	/// <summary>
	/// Deserializes a position from a base64 string.
	/// </summary>
	/// <param name="base64">The base64-encoded string.</param>
	/// <returns>The deserialized position.</returns>
	/// <exception cref="FormatException">Thrown if the string format is invalid.</exception>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	public static DynamoDbCdcPosition FromBase64(string base64)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(base64);

		try
		{
			var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
			var data = JsonSerializer.Deserialize<PositionData>(json, JsonOptions);

			ArgumentNullException.ThrowIfNull(data, nameof(base64));

			return new DynamoDbCdcPosition(
				data.StreamArn ?? string.Empty,
				data.ShardPositions,
				data.Timestamp);
		}
		catch (FormatException)
		{
			throw;
		}
		catch (Exception ex)
		{
			throw new FormatException("Failed to parse position from base64", ex);

		}
	}

	/// <summary>
	/// Attempts to deserialize a position from a base64 string.
	/// </summary>
	/// <param name="base64">The base64-encoded string.</param>
	/// <param name="position">The deserialized position if successful.</param>
	/// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
	public static bool TryFromBase64(string? base64, out DynamoDbCdcPosition? position)
	{
		if (string.IsNullOrWhiteSpace(base64))
		{
			position = null;
			return false;
		}

		try
		{
			position = FromBase64(base64);
			return true;
		}
		catch
		{
			position = null;
			return false;

		}
	}

	/// <inheritdoc/>
	public bool Equals(DynamoDbCdcPosition? other)
	{
		if (other is null)
		{
			return false;
		}

		if (ReferenceEquals(this, other))
		{
			return true;
		}

		if (!string.Equals(StreamArn, other.StreamArn, StringComparison.Ordinal))
		{
			return false;
		}

		if (ShardPositions.Count != other.ShardPositions.Count)
		{
			return false;
		}

		foreach (var kvp in ShardPositions)
		{
			if (!other.ShardPositions.TryGetValue(kvp.Key, out var otherValue) ||
				!string.Equals(kvp.Value, otherValue, StringComparison.Ordinal))
			{
				return false;
			}
		}

		return true;

	}

	/// <inheritdoc/>
	public override string ToString()
	{
		if (ShardPositions.Count == 0)
		{
			return Timestamp.HasValue ? $"Latest({Timestamp.Value:O})" : "Beginning";
		}

		return $"Shards({ShardPositions.Count})";

	}

	/// <summary>
	/// Internal data structure for JSON serialization.
	/// </summary>
	private sealed class PositionData
	{
		[JsonPropertyName("streamArn")]
		public string? StreamArn { get; set; }

		[JsonPropertyName("shardPositions")]
		public Dictionary<string, string>? ShardPositions { get; set; }

		[JsonPropertyName("timestamp")]
		public DateTimeOffset? Timestamp { get; set; }
	}
}
