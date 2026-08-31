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
	[BsonRepresentation(BsonType.DateTime)]
	public DateTime CreatedAt { get; set; }

	/// <summary>
	/// Creates the document identifier from the tenant partition, aggregate identifier and aggregate type.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The tenant leads the composite, matching the convention the grant stores already use across the
	/// document providers.
	/// </para>
	/// <para>
	/// Every id carries a tenant segment, including an untenanted host's: the tenant term is total, so it
	/// always yields the reserved untenanted value rather than nothing. There is deliberately no
	/// tenant-less id shape — one shape per document means a read and a write can never disagree about
	/// which of two shapes to address, which is the failure a second, tenant-omitting form would admit.
	/// </para>
	/// </remarks>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="tenantId">
	/// The owning tenant partition. Required: the caller resolves it from the total tenant term, so an
	/// untenanted host supplies the reserved untenanted value rather than omitting the argument.
	/// </param>
	/// <returns>The document identifier.</returns>
	public static string CreateId(string aggregateId, string aggregateType, string tenantId) =>
		$"t:{tenantId}:{aggregateId}:{aggregateType}";

	/// <summary>
	/// Creates a document from a snapshot.
	/// </summary>
	/// <param name="snapshot">The snapshot to convert.</param>
	/// <param name="tenantId">
	/// The store's ambient tenant partition. Required, and NOT defaulted: a document written under an
	/// omitted partition would carry an identifier no read path composes, so every subsequent load would
	/// miss and silently rebuild from the event stream.
	/// </param>
	/// <returns>The MongoDB document representation.</returns>
	/// <remarks>
	/// The identifier's tenant comes from <paramref name="tenantId"/> — the store's ambient tenant —
	/// NOT from <c>snapshot.TenantId</c>. The two can disagree: a caller may build a snapshot without
	/// setting its tenant while the host is tenant-scoped. Keying the save on the snapshot and the read
	/// on the ambient context would then write one identifier and look up another, so every read would
	/// miss and silently rebuild from the event stream. One authority for the key, on every path.
	/// </remarks>
	public static MongoDbSnapshotDocument FromSnapshot(ISnapshot snapshot, string tenantId) =>
		new()
		{
			Id = CreateId(snapshot.AggregateId, snapshot.AggregateType, tenantId),
			SnapshotId = snapshot.SnapshotId,
			AggregateId = snapshot.AggregateId,
			AggregateType = snapshot.AggregateType,
			Version = snapshot.Version,
			Data = snapshot.Data.ToArray(),
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

#pragma warning disable IL2026, IL3050 // AOT: metadata serialization uses reflection-based JSON for Dictionary<string, object>
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
#pragma warning restore IL2026, IL3050
}
