// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;
using System.Text.Json;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Domain.Model;

namespace Excalibur.EventSourcing.SqlServer.Requests;

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
	/// The tenant scope. The tenant predicate and its parameter are emitted unconditionally — an untenanted
	/// scope binds the reserved sentinel — so the query is restricted to one partition's rows
	/// (<c>AND TenantId = @TenantId</c>) in the same atomic statement. A tenant-scoped read cannot be
	/// constructed without a tenant, so a predicate-less all-tenants query while tenancy is active is
	/// unrepresentable.
	/// </param>
	/// <param name="schema">The schema name for the snapshot store table. Default: "dbo".</param>
	/// <param name="table">The snapshot store table name. Default: "EventStoreSnapshots".</param>
	public GetLatestSnapshotRequest(
		string aggregateId,
		string aggregateType,
		TenantScope scope,
		CancellationToken cancellationToken,
		string schema = "dbo",
		string table = "EventStoreSnapshots")
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);

		var qualifiedTable = SqlTableName.Format(schema, table);
		// UNCONDITIONAL: the DDL keys on (AggregateId, AggregateType, TenantId), so a statement with no
		// tenant filter matches only a SUBSET of the key and can reach another tenant's row. The
		// predicate is therefore always present — there is no unscoped form of this read.
		//
		// Two layers, and they are easy to conflate:
		//   * TYPE layer — KeyedTenantPartition has no empty inhabitant. An untenanted read binds the
		//     reserved untenanted partition, never an empty tenant term, so the predicate always names
		//     a concrete partition. An empty term is not constructable here.
		//   * STORAGE layer — that reserved partition is ENCODED as '' in this provider's column. The
		//     encoding is an implementation detail of the column, not a scope value callers can pass.
		//
		// The COALESCE that used to manufacture '' at query time is gone: the partition type now supplies
		// a concrete term on every path, so there is nothing left to default. Note '' is not a portable
		// encoding (Oracle folds it to NULL), which is why the shared representation lives in the type
		// and each provider encodes it, rather than the SQL agreeing on a literal.
		const string tenantPredicate = " AND TenantId = @TenantId";

		// Selected unconditionally, like the predicate above. The earlier conditional projected a literal
		// NULL for unscoped reads to avoid naming a column an unmigrated table lacked -- but the statement
		// now FILTERS on that column in every case, so a table without it cannot be read at all and the
		// NULL projection would only misreport a single-tenant row's actual '' tenant as null.
		const string tenantColumn = "TenantId";

#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in SqlTableName.Format
		var sql = $"""
			SELECT SnapshotId, AggregateId, AggregateType, Version, Data, CreatedAt, Metadata, {tenantColumn}
			FROM {qualifiedTable}
			WHERE AggregateId = @AggregateId AND AggregateType = @AggregateType{tenantPredicate}
			""";
#pragma warning restore CA2100

		var parameters = new DynamicParameters();
		parameters.Add("@AggregateId", aggregateId);
		parameters.Add("@AggregateType", aggregateType);
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
				// Read straight through. The column is DATETIMEOFFSET and carries its own offset, so
				// reading it as DateTime and then asserting UTC discarded that offset and returned a
				// different instant than was written for any non-UTC writer.
				CreatedAt = result.CreatedAt,
				Metadata = DeserializeMetadata(result.Metadata),
				TenantId = result.TenantId,
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
		raw = JsonSerializer.Deserialize(metadata, SqlServerSnapshotJsonContext.Default.DictionaryStringJsonElement);
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
	/// Converts a <see cref="JsonElement"/> to its inferred CLR primitive. Integral numbers prefer
	/// <see cref="int"/> (then <see cref="long"/>) so that an <c>is int</c> consumer check succeeds;
	/// non-integral numbers fall back to <see cref="double"/>. Non-primitive values are returned as a
	/// cloned <see cref="JsonElement"/>.
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

	private sealed record SnapshotData(
		string? SnapshotId,
		string AggregateId,
		string AggregateType,
		long Version,
		byte[] Data,
		// DateTimeOffset, matching the DATETIMEOFFSET column in the shipped 002_CreateSnapshotSchema.sql.
		// Declared as DateTime, this record could not be materialised from a table created by that script
		// at all -- Dapper found no constructor accepting the returned column types and the read failed
		// outright. A consumer who ran the documented setup script hit that on their first snapshot read.
		// It stayed invisible because the conformance fixture built its own table with DATETIME2, so the
		// suite only ever exercised a schema no consumer has.
		DateTimeOffset CreatedAt,
		byte[]? Metadata,
		string? TenantId);
}
