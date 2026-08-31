// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Excalibur.EventSourcing.MongoDB;

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
	/// Gets or sets the aggregate stream identifier: the owning tenant followed by the aggregate identifier.
	/// </summary>
	/// <remarks>
	/// The tenant is composed into this value rather than filtered on separately, because the unique index
	/// is <c>(streamId, aggregateType, version)</c> — so the tenant being here is what gives each tenant its
	/// own version sequence for the same aggregate identifier.
	/// </remarks>
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
	[BsonRepresentation(BsonType.DateTime)]
	public DateTimeOffset OccurredAt { get; set; }

	/// <summary>
	/// Gets or sets the global sequence number for ordering.
	/// </summary>
	[BsonElement("globalSequence")]
	public long GlobalSequence { get; set; }

	/// <summary>
	/// Converts the document to a <see cref="StoredEvent"/>.
	/// </summary>
	/// <param name="aggregateId">
	/// The caller-supplied aggregate identifier the read was addressed by. Supplied rather than taken from
	/// <see cref="StreamId"/> because that field is the STORAGE key — it carries the owning tenant as a
	/// leading segment so the unique index versions each tenant's stream independently — and returning it
	/// would hand the caller back a different identifier than the one it asked for.
	/// </param>
	/// <returns>The stored event representation.</returns>
	public StoredEvent ToStoredEvent(string aggregateId) =>
		new(
			EventId,
			aggregateId,
			AggregateType,
			EventType,
			Payload,
			Metadata,
			Version,
			OccurredAt);
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
