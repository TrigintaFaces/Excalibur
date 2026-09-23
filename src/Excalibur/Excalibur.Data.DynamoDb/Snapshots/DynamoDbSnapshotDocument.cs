// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json;

using Amazon.DynamoDBv2.Model;

using Excalibur.Domain.Model;
using Excalibur.Dispatch;

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

	// Partition key prefix
	public const string SnapshotPrefix = "SNAPSHOT#";

	/// <summary>
	/// Creates the partition key from the tenant partition and the aggregate identifier.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The tenant goes in the PARTITION key. A DynamoDB partition key is a per-item attribute rather than
	/// container-level configuration, so this is adoptable without recreating the table, and it also
	/// distributes partitions by tenant.
	/// </para>
	/// <para>
	/// Every key carries a tenant segment, including an untenanted host's: the tenant term is total, so it
	/// always yields the reserved untenanted value rather than nothing. There is deliberately no
	/// tenant-less key shape — one shape per item means a read and a write can never disagree about which
	/// of two shapes to address, which is the failure a second, tenant-omitting form would admit.
	/// </para>
	/// </remarks>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="tenantId">
	/// The owning tenant partition. Required: the caller resolves it from the total tenant term, so an
	/// untenanted host supplies the reserved untenanted value rather than omitting the argument.
	/// </param>
	/// <returns>The partition key.</returns>
	public static string CreatePK(string aggregateId, string tenantId) =>
		SnapshotPrefix + TenantScopedKey.Compose(tenantId, aggregateId);

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
	/// <param name="ttlAttributeName">
	/// The table's configured TTL attribute name. The expiry is written under THIS name: DynamoDB only expires
	/// an item by the attribute named in the table's TTL specification, which is set from the same option, so
	/// writing a fixed name instead would leave every item unexpired once the option is renamed.
	/// </param>
	/// <param name="tenantId">
	/// The store's ambient tenant partition. Required, and NOT defaulted: an item written under an omitted
	/// partition would carry a key no read path composes, so every subsequent load would miss and silently
	/// rebuild from the event stream.
	/// </param>
	/// <param name="jsonOptions">
	/// The store's canonical serializer options, carrying the consumer's resolver when one was supplied.
	/// Both serialization paths write through these, so the stored bytes do not vary with the resolver.
	/// </param>
	/// <param name="hasTypeInfoResolver">
	/// Whether a source-generated resolver is attached. When it is, metadata values are dispatched through
	/// their runtime type rather than through the reflection-based serializer, which is what lets this run
	/// with no reflection available.
	/// </param>
	/// <returns>The DynamoDB item attributes.</returns>
	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	public static Dictionary<string, AttributeValue> FromSnapshot(
		ISnapshot snapshot,
		string tenantId,
		JsonSerializerOptions jsonOptions,
		bool hasTypeInfoResolver,
		int ttlSeconds,
		string ttlAttributeName)
	{
		var item = new Dictionary<string, AttributeValue>
		{
			[PK] = new() { S = CreatePK(snapshot.AggregateId, tenantId) },
			[SK] = new() { S = CreateSK(snapshot.AggregateType) },
			[SnapshotId] = new() { S = snapshot.SnapshotId },
			[Version] = new() { N = snapshot.Version.ToString(CultureInfo.InvariantCulture) },
			[AggregateId] = new() { S = snapshot.AggregateId },
			[AggregateType] = new() { S = snapshot.AggregateType },
			[Data] = new() { B = new MemoryStream(snapshot.Data.ToArray()) },
			[CreatedAt] = new() { S = snapshot.CreatedAt.ToString("O", CultureInfo.InvariantCulture) }
		};

		if (snapshot.Metadata is { Count: > 0 })
		{
			// Both paths write through the SAME canonical options, so a snapshot written with a resolver
			// is byte-identical to one written without. The resolver path dispatches each value through
			// its own runtime type, which is what makes it work with no reflection available.
			var metadataJson = hasTypeInfoResolver
				? Encoding.UTF8.GetString(
					EventSerializationDefaults.SerializeMetadataWithResolver(snapshot.Metadata, jsonOptions))
				: JsonSerializer.Serialize(snapshot.Metadata, jsonOptions);

			item[Metadata] = new() { S = metadataJson };
		}

		if (ttlSeconds > 0)
		{
			var ttlValue = DateTimeOffset.UtcNow.AddSeconds(ttlSeconds).ToUnixTimeSeconds();
			item[ttlAttributeName] = new() { N = ttlValue.ToString(CultureInfo.InvariantCulture) };
		}

		return item;
	}

	/// <summary>
	/// Converts a DynamoDB item to a <see cref="Snapshot"/>.
	/// </summary>
	/// <param name="item">The DynamoDB item attributes.</param>
	/// <param name="jsonOptions">
	/// The store's canonical serializer options, carrying the consumer's resolver when one was supplied.
	/// </param>
	/// <returns>The snapshot representation.</returns>
	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions)")]
	public static Snapshot ToSnapshot(Dictionary<string, AttributeValue> item, JsonSerializerOptions jsonOptions)
	{
		IDictionary<string, object>? metadata = null;

		if (item.TryGetValue(Metadata, out var metadataAttr) && !string.IsNullOrEmpty(metadataAttr.S))
		{
			metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(metadataAttr.S, jsonOptions);
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
