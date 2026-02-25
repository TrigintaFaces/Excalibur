// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.EventSourcing.Abstractions;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Excalibur.Data.MongoDB.EventSourcing;

/// <summary>
/// MongoDB document representation of a stored event.
/// </summary>
/// <remarks>
/// <para>
/// Uses a UNIQUE compound index on (StreamId, AggregateType, Version) for optimistic concurrency.
/// MongoDB error code 11000 (duplicate key) indicates a version conflict.
/// </para>
/// </remarks>
internal sealed class MongoDbEventDocument
{
	/// <summary>
	/// Gets or sets the document's MongoDB ObjectId.
	/// </summary>
	[BsonId]
	[BsonRepresentation(BsonType.ObjectId)]
	public string? ObjectId { get; set; }

	/// <summary>
	/// Gets or sets the unique event identifier.
	/// </summary>
	[BsonElement("eventId")]
	public string EventId { get; set; } = default!;

	/// <summary>
	/// Gets or sets the aggregate stream identifier.
	/// </summary>
	[BsonElement("streamId")]
	public string StreamId { get; set; } = default!;

	/// <summary>
	/// Gets or sets the aggregate type name.
	/// </summary>
	[BsonElement("aggregateType")]
	public string AggregateType { get; set; } = default!;

	/// <summary>
	/// Gets or sets the event type name.
	/// </summary>
	[BsonElement("eventType")]
	public string EventType { get; set; } = default!;

	/// <summary>
	/// Gets or sets the serialized event payload.
	/// </summary>
	[BsonElement("payload")]
	public byte[] Payload { get; set; } = default!;

	/// <summary>
	/// Gets or sets the serialized event metadata.
	/// </summary>
	[BsonElement("metadata")]
	public byte[]? Metadata { get; set; }

	/// <summary>
	/// Gets or sets the event version within the aggregate stream.
	/// </summary>
	[BsonElement("version")]
	public long Version { get; set; }

	/// <summary>
	/// Gets or sets when the event occurred.
	/// </summary>
	[BsonElement("occurredAt")]
	public DateTimeOffset OccurredAt { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the event has been dispatched.
	/// </summary>
	[BsonElement("isDispatched")]
	public bool IsDispatched { get; set; }

	/// <summary>
	/// Gets or sets when the event was dispatched.
	/// </summary>
	[BsonElement("dispatchedAt")]
	public DateTimeOffset? DispatchedAt { get; set; }

	/// <summary>
	/// Gets or sets the global sequence number for ordering.
	/// </summary>
	[BsonElement("globalSequence")]
	public long GlobalSequence { get; set; }

	/// <summary>
	/// Converts the document to a <see cref="StoredEvent"/>.
	/// </summary>
	/// <returns>The stored event representation.</returns>
	public StoredEvent ToStoredEvent() =>
		new(
			EventId,
			StreamId,
			AggregateType,
			EventType,
			Payload,
			Metadata,
			Version,
			OccurredAt,
			IsDispatched);
}

/// <summary>
/// MongoDB document for sequence counter.
/// </summary>
internal sealed class MongoDbCounterDocument
{
	/// <summary>
	/// Gets or sets the counter name (document ID).
	/// </summary>
	[BsonId]
	public string Id { get; set; } = default!;

	/// <summary>
	/// Gets or sets the current sequence value.
	/// </summary>
	[BsonElement("sequence")]
	public long Sequence { get; set; }
}
