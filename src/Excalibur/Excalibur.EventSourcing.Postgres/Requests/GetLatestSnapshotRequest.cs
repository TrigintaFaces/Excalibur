// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;
using System.Text.Json;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Domain.Model;

namespace Excalibur.EventSourcing.Postgres.Requests;

/// <summary>
/// Data request to get the latest snapshot for an aggregate from the Postgres snapshot store.
/// </summary>
public sealed class GetLatestSnapshotRequest : DataRequestBase<IDbConnection, ISnapshot?>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="GetLatestSnapshotRequest"/> class.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="scope">
	/// The tenant scope, or <see cref="TenantScope.None"/> in a single-tenant host.
	/// </param>
	/// <param name="schema">The schema name for the snapshot store table. Default: "public".</param>
	/// <param name="table">The snapshot store table name. Default: "event_store_snapshots".</param>
	public GetLatestSnapshotRequest(
		string aggregateId,
		string aggregateType,
		TenantScope scope,
		CancellationToken cancellationToken,
		string schema = "public",
		string table = "event_store_snapshots")
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);

		var qualifiedTable = PgTableName.Format(schema, table);

#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in PgTableName.Format
		// Unconditional, matching the write: SaveSnapshotRequest binds the same partition and keys
		// ON CONFLICT (aggregate_id, aggregate_type, tenant_id), so tenant_id is ALWAYS in the key. The read
		// must key on it identically — scoped resolves to the tenant, unscoped to the reserved
		// '__untenanted__' partition. A conditional predicate that dropped the filter when unscoped matched
		// any tenant's row. The value comes from KeyedTenantPartition, which has no empty inhabitant, so the
		// COALESCE that used to manufacture '' here has nothing left to do.
		const string tenantPredicate = " AND tenant_id = @TenantId";

		var sql = $"""
			SELECT snapshot_id AS SnapshotId, aggregate_id AS AggregateId, aggregate_type AS AggregateType,
			       version AS Version, data AS Data, metadata AS Metadata, created_at AS CreatedAt
			FROM {qualifiedTable}
			WHERE aggregate_id = @AggregateId AND aggregate_type = @AggregateType{tenantPredicate}
			""";
#pragma warning restore CA2100

		var parameters = new DynamicParameters();
		parameters.Add("@AggregateId", aggregateId);
		parameters.Add("@AggregateType", aggregateType);

		// Unconditional, mirroring the write path: FromScope maps an unscoped scope onto the reserved
		// '__untenanted__' partition — read and write agree on the key in every scope, and neither can
		// produce an empty term.
		parameters.Add("@TenantId", KeyedTenantPartition.FromScope(scope).TenantId);

		Command = CreateCommand(sql, parameters, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
		{
			var result = await connection.QuerySingleOrDefaultAsync<SnapshotData>(Command).ConfigureAwait(false);
			if (result == null)
			{
				return null;
			}

			return new Snapshot
			{
				SnapshotId = result.SnapshotId ?? Guid.NewGuid().ToString(),
				AggregateId = result.AggregateId,
				AggregateType = result.AggregateType,
				Version = result.Version,
				Data = result.Data,
				Metadata = DeserializeMetadata(result.Metadata),
				CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(result.CreatedAt, DateTimeKind.Utc), TimeSpan.Zero),
			};
		};
	}

	/// <summary>
	/// Deserializes the stored binary metadata payload back into a dictionary, inferring CLR primitive
	/// types from the JSON so the version metadata the snapshot was persisted with is restored.
	/// </summary>
	private static IDictionary<string, object>? DeserializeMetadata(byte[]? metadata)
	{
		if (metadata is null || metadata.Length == 0)
		{
			return null;
		}

		Dictionary<string, JsonElement>? raw;
#pragma warning disable IL2026, IL3050 // Metadata deserialization inherently uses reflection (matches the SqlServer/Oracle snapshot store precedent)
		raw = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(metadata);
#pragma warning restore IL2026, IL3050
		if (raw is null)
		{
			return null;
		}

		var result = new Dictionary<string, object>(raw.Count);
		foreach (var (key, element) in raw)
		{
			result[key] = ConvertJsonElement(element)!;
		}

		return result;
	}

	private static object? ConvertJsonElement(JsonElement element)
	{
		switch (element.ValueKind)
		{
			case JsonValueKind.String:
				return element.GetString();
			case JsonValueKind.Number:
				if (element.TryGetInt32(out var intValue))
				{
					return intValue;
				}

				if (element.TryGetInt64(out var longValue))
				{
					return longValue;
				}

				return element.GetDouble();
			case JsonValueKind.True:
				return true;
			case JsonValueKind.False:
				return false;
			case JsonValueKind.Null:
			case JsonValueKind.Undefined:
				return null;
			default:
				return element.Clone();
		}
	}

	private sealed record SnapshotData(
		string? SnapshotId,
		string AggregateId,
		string AggregateType,
		long Version,
		byte[] Data,
		byte[]? Metadata,
		DateTime CreatedAt);
}
