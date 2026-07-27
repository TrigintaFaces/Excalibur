// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;
using System.Text.Json;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Domain.Model;

namespace Excalibur.EventSourcing.Oracle.Requests;

/// <summary>
/// Data request to get the latest snapshot for an aggregate.
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
	/// The tenant scope. When <see cref="TenantScope.None"/> (the non-multi-tenant default) no tenant
	/// predicate is emitted; when tenant-scoped the statement is restricted to the tenant's own rows.
	/// </param>
	/// <param name="schema">The schema name for the snapshot store table.</param>
	/// <param name="table">The snapshot store table name.</param>
	public GetLatestSnapshotRequest(
		string aggregateId,
		string aggregateType,
		TenantScope scope,
		CancellationToken cancellationToken,
		string schema = "EXCALIBUR",
		string table = "EVENTSTORESNAPSHOTS")
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);

		var qualifiedTable = OracleTableName.Format(schema, table);
		// UNCONDITIONAL. Both scopes emit the same equality term, because TENANTID is now NOT NULL and an
		// untenanted row carries the reserved '__untenanted__' value rather than NULL.
		//
		// Two earlier revisions are worth keeping in view, because this line has been wrong in both
		// directions and the sentinel is what removes the choice:
		//
		//   * `TENANTID = NVL(:TenantId, '')` was broken. Oracle stores the empty string AS NULL -- the
		//     comment stated that premise correctly and then contradicted it, since NVL's replacement value
		//     IS '', which Oracle converts to NULL, so the predicate read `TENANTID = NULL` and was never
		//     true. A single-tenant host wrote snapshots it could not read back.
		//
		//   * `IS NULL` was correct while the column was nullable, but it forced this predicate to be
		//     scope-conditional, and a conditional tenant term is a branch that can be got wrong.
		//
		// A reserved non-empty value is comparable with `=` on every provider, so there is no branch left.
		var tenantPredicate = " AND TENANTID = :TenantId";

#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in OracleTableName.Format
		var sql = $"""
			SELECT SNAPSHOTID AS SnapshotId, AGGREGATEID AS AggregateId, AGGREGATETYPE AS AggregateType,
			       VERSION AS Version, DATA AS Data, CREATEDAT AS CreatedAt, METADATA AS Metadata
			FROM {qualifiedTable}
			WHERE AGGREGATEID = :AggregateId AND AGGREGATETYPE = :AggregateType{tenantPredicate}
			""";
#pragma warning restore CA2100

		var parameters = new DynamicParameters();
		parameters.Add(":AggregateId", aggregateId);
		parameters.Add(":AggregateType", aggregateType);

		// ODP.NET binds by POSITION: a parameter added but not referenced shifts every subsequent value. The
		// predicate above is now unconditional, so the bind is too — emitted and bound always move together,
		// and the position can no longer depend on the scope.
		parameters.Add(":TenantId", KeyedTenantPartition.FromScope(scope).TenantId);
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
				Version = (long)result.Version,
				Data = result.Data,
				CreatedAt = result.CreatedAt,
				Metadata = DeserializeMetadata(result.Metadata),
			};
		};
	}

	/// <summary>
	/// Deserializes the stored binary metadata payload back into a dictionary, inferring CLR primitive
	/// types so that consumers reading typed values (e.g. the integer schema version) observe the
	/// original type rather than a <see cref="JsonElement"/>. Returns <see langword="null"/> when no
	/// metadata was persisted.
	/// </summary>
	private static IDictionary<string, object>? DeserializeMetadata(byte[]? metadata)
	{
		if (metadata is null || metadata.Length == 0)
		{
			return null;
		}

		Dictionary<string, JsonElement>? raw;
#pragma warning disable IL2026, IL3050 // Metadata deserialization inherently uses reflection (matches SqlServerEventStore precedent)
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

	/// <summary>
	/// Converts a <see cref="JsonElement"/> to its inferred CLR primitive.
	/// </summary>
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

	/// <summary>
	/// Provider-typed projection of a snapshot row as Oracle returns it (NUMBER(19) →
	/// <see cref="decimal"/>), converted after materialization.
	/// </summary>
	/// <remarks>
	/// <c>Version</c> is <see cref="decimal"/>, not <see cref="long"/>: Oracle returns NUMBER(19) as a
	/// decimal, which does not bind to a <see cref="long"/> constructor parameter, so Dapper fails to
	/// materialize the row at all. This mirrors <c>LoadEventsRequest.OracleEventRow</c> in this package,
	/// which solved the identical problem for the event stream.
	/// </remarks>
	private sealed record SnapshotData(
		string? SnapshotId,
		string AggregateId,
		string AggregateType,
		decimal Version,
		byte[] Data,
		DateTimeOffset CreatedAt,
		byte[]? Metadata);
}
