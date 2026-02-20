// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

using Excalibur.Domain.Model;

namespace Excalibur.Data.CosmosDb.Snapshots;

/// <summary>
/// Cosmos DB document representation of a stored snapshot.
/// </summary>
/// <remarks>
/// <para>
/// Uses aggregateId as the document ID and aggregateType as the partition key.
/// This ensures one snapshot per aggregate and enables efficient queries within aggregate type boundaries.
/// </para>
/// <para>
/// The version guard in SaveAsync ensures older snapshots don't overwrite newer ones
/// during concurrent save operations using read-check-upsert with ETag-based optimistic concurrency.
/// </para>
/// </remarks>
internal sealed class CosmosDbSnapshotDocument
{
	/// <summary>
	/// Gets or sets the document ID (aggregateId).
	/// </summary>
	[JsonPropertyName("id")]
	public string Id { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the aggregate identifier.
	/// </summary>
	[JsonPropertyName("aggregateId")]
	public string AggregateId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the aggregate type name (partition key).
	/// </summary>
	[JsonPropertyName("aggregateType")]
	public string AggregateType { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the unique snapshot identifier.
	/// </summary>
	[JsonPropertyName("snapshotId")]
	public string SnapshotId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the snapshot version.
	/// </summary>
	[JsonPropertyName("version")]
	public long Version { get; set; }

	/// <summary>
	/// Gets or sets the serialized snapshot data as Base64.
	/// </summary>
	[JsonPropertyName("data")]
	public string Data { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the serialized metadata as Base64.
	/// </summary>
	[JsonPropertyName("metadata")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? Metadata { get; set; }

	/// <summary>
	/// Gets or sets when the snapshot was created (ISO 8601 format).
	/// </summary>
	[JsonPropertyName("createdAt")]
	public string CreatedAt { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the optional TTL for per-document expiration.
	/// </summary>
	/// <remarks>
	/// When set, CosmosDb will automatically delete the document after the specified seconds.
	/// This is useful for automatic cleanup of old snapshots.
	/// </remarks>
	[JsonPropertyName("ttl")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? Ttl { get; set; }

	/// <summary>
	/// Creates the document ID from aggregate ID.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <returns>The document ID string.</returns>
	/// <remarks>
	/// CosmosDb document IDs cannot contain: / \ ? #
	/// Uses URL-safe Base64 encoding to handle all special characters safely.
	/// </remarks>
	public static string CreateId(string aggregateId)
	{
		var bytes = System.Text.Encoding.UTF8.GetBytes(aggregateId);
		return Convert.ToBase64String(bytes)
			.Replace('+', '-')  // URL-safe
			.Replace('/', '_')  // URL-safe
			.TrimEnd('=');      // Remove padding
	}

	/// <summary>
	/// Creates a document from a snapshot.
	/// </summary>
	/// <param name="snapshot">The snapshot to convert.</param>
	/// <returns>The Cosmos DB document representation.</returns>
	public static CosmosDbSnapshotDocument FromSnapshot(ISnapshot snapshot) =>
		new()
		{
			Id = CreateId(snapshot.AggregateId),
			AggregateId = snapshot.AggregateId,
			AggregateType = snapshot.AggregateType,
			SnapshotId = snapshot.SnapshotId,
			Version = snapshot.Version,
			Data = Convert.ToBase64String(snapshot.Data),
			Metadata = SerializeMetadata(snapshot.Metadata),
			CreatedAt = snapshot.CreatedAt.ToString("O")
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
			Data = Convert.FromBase64String(Data),
			Metadata = DeserializeMetadata(Metadata),
			CreatedAt = DateTimeOffset.Parse(CreatedAt, null, System.Globalization.DateTimeStyles.RoundtripKind)
		};

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.SerializeToUtf8Bytes<TValue>(TValue, JsonSerializerOptions)")]
	private static string? SerializeMetadata(IDictionary<string, object>? metadata)
	{
		if (metadata == null || metadata.Count == 0)
		{
			return null;
		}

		var bytes = JsonSerializer.SerializeToUtf8Bytes(metadata);
		return Convert.ToBase64String(bytes);
	}

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(ReadOnlySpan<Byte>, JsonSerializerOptions)")]
	private static IDictionary<string, object>? DeserializeMetadata(string? data)
	{
		if (string.IsNullOrEmpty(data))
		{
			return null;
		}

		var bytes = Convert.FromBase64String(data);
		return JsonSerializer.Deserialize<Dictionary<string, object>>(bytes);
	}
}
