// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;

using Amazon.DynamoDBv2.Model;

using Excalibur.Domain.Model;

namespace Excalibur.Data.DynamoDb.Snapshots;

/// <summary>
/// DynamoDB document representation of a snapshot using single-table design.
/// </summary>
/// <remarks>
/// <para>
/// Uses single-table design with the following key structure:
/// </para>
/// <list type="bullet">
/// <item><description>PK: SNAPSHOT#{aggregateId} - Partition by aggregate</description></item>
/// <item><description>SK: {aggregateType} - Sort key enables multi-aggregate type queries</description></item>
/// </list>
/// <para>
/// Unlike CosmosDb, DynamoDB partition keys can contain any characters including /, \, ?, #
/// so no URL-safe encoding is required for the aggregateId.
/// </para>
/// </remarks>
internal static class DynamoDbSnapshotDocument
{
	// Attribute names
	public const string PK = "PK";
	public const string SK = "SK";
	public const string SnapshotId = "snapshotId";
	public const string Version = "version";
	public const string AggregateId = "aggregateId";
	public const string AggregateType = "aggregateType";
	public const string Data = "data";
	public const string Metadata = "metadata";
	public const string CreatedAt = "createdAt";
	public const string Ttl = "ttl";

	// Partition key prefix
	public const string SnapshotPrefix = "SNAPSHOT#";

	/// <summary>
	/// Creates the partition key value for a given aggregate ID.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <returns>The partition key value.</returns>
	public static string CreatePK(string aggregateId) => $"{SnapshotPrefix}{aggregateId}";

	/// <summary>
	/// Creates the sort key value for a given aggregate type.
	/// </summary>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <returns>The sort key value.</returns>
	public static string CreateSK(string aggregateType) => aggregateType;

	/// <summary>
	/// Converts an <see cref="ISnapshot"/> to a DynamoDB item.
	/// </summary>
	/// <param name="snapshot">The snapshot to convert.</param>
	/// <param name="ttlSeconds">Optional TTL in seconds (0 = no TTL).</param>
	/// <returns>The DynamoDB item attributes.</returns>
	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	public static Dictionary<string, AttributeValue> FromSnapshot(ISnapshot snapshot, int ttlSeconds = 0)
	{
		var item = new Dictionary<string, AttributeValue>
		{
			[PK] = new() { S = CreatePK(snapshot.AggregateId) },
			[SK] = new() { S = CreateSK(snapshot.AggregateType) },
			[SnapshotId] = new() { S = snapshot.SnapshotId },
			[Version] = new() { N = snapshot.Version.ToString(CultureInfo.InvariantCulture) },
			[AggregateId] = new() { S = snapshot.AggregateId },
			[AggregateType] = new() { S = snapshot.AggregateType },
			[Data] = new() { B = new MemoryStream(snapshot.Data) },
			[CreatedAt] = new() { S = snapshot.CreatedAt.ToString("O", CultureInfo.InvariantCulture) }
		};

		if (snapshot.Metadata is { Count: > 0 })
		{
			var metadataJson = JsonSerializer.Serialize(snapshot.Metadata);
			item[Metadata] = new() { S = metadataJson };
		}

		if (ttlSeconds > 0)
		{
			var ttlValue = DateTimeOffset.UtcNow.AddSeconds(ttlSeconds).ToUnixTimeSeconds();
			item[Ttl] = new() { N = ttlValue.ToString(CultureInfo.InvariantCulture) };
		}

		return item;
	}

	/// <summary>
	/// Converts a DynamoDB item to a <see cref="Snapshot"/>.
	/// </summary>
	/// <param name="item">The DynamoDB item attributes.</param>
	/// <returns>The snapshot representation.</returns>
	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions)")]
	public static Snapshot ToSnapshot(Dictionary<string, AttributeValue> item)
	{
		IDictionary<string, object>? metadata = null;

		if (item.TryGetValue(Metadata, out var metadataAttr) && !string.IsNullOrEmpty(metadataAttr.S))
		{
			metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(metadataAttr.S);
		}

		return new Snapshot
		{
			SnapshotId = item[SnapshotId].S,
			AggregateId = item[AggregateId].S,
			AggregateType = item[AggregateType].S,
			Version = long.Parse(item[Version].N, CultureInfo.InvariantCulture),
			Data = item[Data].B.ToArray(),
			CreatedAt = DateTimeOffset.Parse(item[CreatedAt].S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
			Metadata = metadata
		};
	}
}
