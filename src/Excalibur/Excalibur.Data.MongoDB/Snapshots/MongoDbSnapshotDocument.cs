// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Domain.Model;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Excalibur.Data.MongoDB.Snapshots;

/// <summary>
/// MongoDB document representation of a stored snapshot.
/// </summary>
/// <remarks>
/// <para>
/// Uses a composite string ID (aggregateId:aggregateType) to ensure one snapshot per aggregate.
/// ReplaceOneAsync with IsUpsert=true provides atomic insert-or-update semantics.
/// </para>
/// <para>
/// The version guard in the filter ensures older snapshots don't overwrite newer ones
/// during concurrent save operations.
/// </para>
/// </remarks>
internal sealed class MongoDbSnapshotDocument
{
	/// <summary>
	/// Gets or sets the composite document ID (aggregateId:aggregateType).
	/// </summary>
	[BsonId]
	[BsonRepresentation(BsonType.String)]
	public string Id { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the unique snapshot identifier.
	/// </summary>
	[BsonElement("snapshotId")]
	public string SnapshotId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the aggregate identifier.
	/// </summary>
	[BsonElement("aggregateId")]
	public string AggregateId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the aggregate type name.
	/// </summary>
	[BsonElement("aggregateType")]
	public string AggregateType { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the snapshot version.
	/// </summary>
	[BsonElement("version")]
	public long Version { get; set; }

	/// <summary>
	/// Gets or sets the serialized snapshot data.
	/// </summary>
	[BsonElement("data")]
	public byte[] Data { get; set; } = [];

	/// <summary>
	/// Gets or sets the serialized metadata.
	/// </summary>
	[BsonElement("metadata")]
	[BsonIgnoreIfNull]
	public byte[]? Metadata { get; set; }

	/// <summary>
	/// Gets or sets when the snapshot was created.
	/// </summary>
	[BsonElement("createdAt")]
	public DateTime CreatedAt { get; set; }

	/// <summary>
	/// Creates the composite document ID from aggregate ID and type.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <returns>The composite ID string.</returns>
	public static string CreateId(string aggregateId, string aggregateType) =>
		$"{aggregateId}:{aggregateType}";

	/// <summary>
	/// Creates a document from a snapshot.
	/// </summary>
	/// <param name="snapshot">The snapshot to convert.</param>
	/// <returns>The MongoDB document representation.</returns>
	public static MongoDbSnapshotDocument FromSnapshot(ISnapshot snapshot) =>
		new()
		{
			Id = CreateId(snapshot.AggregateId, snapshot.AggregateType),
			SnapshotId = snapshot.SnapshotId,
			AggregateId = snapshot.AggregateId,
			AggregateType = snapshot.AggregateType,
			Version = snapshot.Version,
			Data = snapshot.Data,
			Metadata = SerializeMetadata(snapshot.Metadata),
			CreatedAt = snapshot.CreatedAt.UtcDateTime
		};

	/// <summary>
	/// Converts the document to a <see cref="Snapshot"/>.
	/// </summary>
	/// <returns>The snapshot representation.</returns>
	public Snapshot ToSnapshot() =>
		new()
		{
			SnapshotId = SnapshotId,
			AggregateId = AggregateId,
			AggregateType = AggregateType,
			Version = Version,
			Data = Data,
			Metadata = DeserializeMetadata(Metadata),
			CreatedAt = new DateTimeOffset(CreatedAt, TimeSpan.Zero)
		};

	private static byte[]? SerializeMetadata(IDictionary<string, object>? metadata)
	{
		if (metadata == null || metadata.Count == 0)
		{
			return null;
		}

		return System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(metadata);
	}

	private static IDictionary<string, object>? DeserializeMetadata(byte[]? data)
	{
		if (data == null || data.Length == 0)
		{
			return null;
		}

		return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(data);
	}
}
