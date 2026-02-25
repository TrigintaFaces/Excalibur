// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Amazon.DynamoDBv2.Model;

namespace Excalibur.Data.DynamoDb.Cdc;

/// <summary>
/// Represents a data change event captured from DynamoDB Streams.
/// </summary>
public sealed class DynamoDbDataChangeEvent
{
	/// <summary>
	/// Gets the position of this change in the stream.
	/// </summary>
	public DynamoDbCdcPosition Position { get; init; } = null!;

	/// <summary>
	/// Gets the type of change (Insert, Modify, Remove).
	/// </summary>
	public DynamoDbDataChangeType ChangeType { get; init; }

	/// <summary>
	/// Gets the shard ID this change came from.
	/// </summary>
	public string ShardId { get; init; } = string.Empty;

	/// <summary>
	/// Gets the sequence number within the shard.
	/// </summary>
	public string SequenceNumber { get; init; } = string.Empty;

	/// <summary>
	/// Gets the item as it appeared after the modification (NewImage).
	/// </summary>
	/// <remarks>
	/// Available for Insert and Modify events when stream view type includes NewImage.
	/// </remarks>
	public Dictionary<string, AttributeValue>? NewImage { get; init; }

	/// <summary>
	/// Gets the item as it appeared before the modification (OldImage).
	/// </summary>
	/// <remarks>
	/// Available for Modify and Remove events when stream view type includes OldImage.
	/// </remarks>
	public Dictionary<string, AttributeValue>? OldImage { get; init; }

	/// <summary>
	/// Gets the key attributes of the item.
	/// </summary>
	public Dictionary<string, AttributeValue> Keys { get; init; } = new();

	/// <summary>
	/// Gets the approximate timestamp when the change occurred.
	/// </summary>
	public DateTimeOffset Timestamp { get; init; }

	/// <summary>
	/// Gets the unique event identifier.
	/// </summary>
	public string? EventId { get; init; }

	/// <summary>
	/// Creates a new instance for an insert event.
	/// </summary>
	public static DynamoDbDataChangeEvent CreateInsert(
		DynamoDbCdcPosition position,
		string shardId,
		string sequenceNumber,
		Dictionary<string, AttributeValue> keys,
		Dictionary<string, AttributeValue>? newImage,
		DateTimeOffset timestamp,
		string? eventId)
	{
		return new DynamoDbDataChangeEvent
		{
			Position = position,
			ChangeType = DynamoDbDataChangeType.Insert,
			ShardId = shardId,
			SequenceNumber = sequenceNumber,
			Keys = keys,
			NewImage = newImage,
			Timestamp = timestamp,
			EventId = eventId,
		};
	}

	/// <summary>
	/// Creates a new instance for a modify event.
	/// </summary>
	public static DynamoDbDataChangeEvent CreateModify(
		DynamoDbCdcPosition position,
		string shardId,
		string sequenceNumber,
		Dictionary<string, AttributeValue> keys,
		Dictionary<string, AttributeValue>? newImage,
		Dictionary<string, AttributeValue>? oldImage,
		DateTimeOffset timestamp,
		string? eventId)
	{
		return new DynamoDbDataChangeEvent
		{
			Position = position,
			ChangeType = DynamoDbDataChangeType.Modify,
			ShardId = shardId,
			SequenceNumber = sequenceNumber,
			Keys = keys,
			NewImage = newImage,
			OldImage = oldImage,
			Timestamp = timestamp,
			EventId = eventId,
		};
	}

	/// <summary>
	/// Creates a new instance for a remove event.
	/// </summary>
	public static DynamoDbDataChangeEvent CreateRemove(
		DynamoDbCdcPosition position,
		string shardId,
		string sequenceNumber,
		Dictionary<string, AttributeValue> keys,
		Dictionary<string, AttributeValue>? oldImage,
		DateTimeOffset timestamp,
		string? eventId)
	{
		return new DynamoDbDataChangeEvent
		{
			Position = position,
			ChangeType = DynamoDbDataChangeType.Remove,
			ShardId = shardId,
			SequenceNumber = sequenceNumber,
			Keys = keys,
			OldImage = oldImage,
			Timestamp = timestamp,
			EventId = eventId,
		};
	}
}
