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
/// Data request to save (upsert) a snapshot for an aggregate.
/// Uses MERGE for atomic insert-or-update semantics.
/// </summary>
public sealed class SaveSnapshotRequest : DataRequestBase<IDbConnection, int>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="SaveSnapshotRequest"/> class.
	/// </summary>
	/// <param name="snapshot">The snapshot to save.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="scope">
	/// The tenant scope, or <see cref="TenantScope.None"/> in a single-tenant host. The tenant is part of
	/// the merge key in every case: a single-tenant row is keyed under the empty-string sentinel the schema
	/// defaults to, not outside the key. Two tenants holding the same aggregate identifier therefore occupy
	/// separate rows, and an unscoped save cannot match a tenant-scoped row.
	/// </param>
	/// <param name="schema">The schema name for the snapshot store table. Default: "dbo".</param>
	/// <param name="table">The snapshot store table name. Default: "EventStoreSnapshots".</param>
	public SaveSnapshotRequest(
		ISnapshot snapshot,
		TenantScope scope,
		CancellationToken cancellationToken,
		string schema = "dbo",
		string table = "EventStoreSnapshots")
	{
		ArgumentNullException.ThrowIfNull(snapshot);

		var qualifiedTable = SqlTableName.Format(schema, table);

		// Untenanted rows use the reserved '__untenanted__' sentinel, bound through KeyedTenantPartition.
		// The tenant is part of the MERGE UNIQUE key, and encoding untenanted as a concrete non-empty value
		// (never NULL, never '') keeps the upsert an upsert: the sentinel is collision-proof (Scoped()
		// rejects it, so no real tenant can claim the partition), read and write agree on it, and it never
		// crosses the tenant boundary. See ARCHITECTURE.md (tenant isolation).
		//
		// This previously used '' for the same purpose. The empty string cannot be the shared
		// representation: Oracle stores '' as NULL, so the identical intent became a different value on
		// that provider and needed a function-based unique index to stay correct. The sentinel expresses
		// identically everywhere, which is what lets the keyed family share one representation.
		// UNCONDITIONAL, every fragment. The shipped DDL keys on (AggregateId, AggregateType, TenantId),
		// so matching on only the first two columns matches a SUBSET of the key: an unscoped save against
		// a table holding tenant rows for the same aggregate would MATCH a tenant's row and overwrite it.
		// Single-tenant is not "no tenant" here -- it is the '' sentinel the DDL defaults to, so every
		// statement keys on the full triple and the unscoped path stops being a second statement.
		const string tenantKeyPredicate = " AND target.TenantId = source.TenantId";
		const string tenantSourceColumn = ", @TenantId";
		const string tenantSourceName = ", TenantId";
		const string tenantInsertColumn = ", TenantId";
		const string tenantInsertValue = ", source.TenantId";

#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in SqlTableName.Format
		var sql = $"""
			MERGE INTO {qualifiedTable} WITH (HOLDLOCK, ROWLOCK, UPDLOCK) AS target
			USING (SELECT @SnapshotId, @AggregateId, @AggregateType, @Version, @Data, @CreatedAt, @Metadata{tenantSourceColumn})
			    AS source (SnapshotId, AggregateId, AggregateType, Version, Data, CreatedAt, Metadata{tenantSourceName})
			ON target.AggregateId = source.AggregateId
			   AND target.AggregateType = source.AggregateType{tenantKeyPredicate}
			WHEN MATCHED AND source.Version > target.Version THEN
			    UPDATE SET
			        SnapshotId = source.SnapshotId,
			        Version = source.Version,
			        Data = source.Data,
			        CreatedAt = source.CreatedAt,
			        Metadata = source.Metadata
			WHEN NOT MATCHED THEN
			    INSERT (SnapshotId, AggregateId, AggregateType, Version, Data, CreatedAt, Metadata{tenantInsertColumn})
			    VALUES (source.SnapshotId, source.AggregateId, source.AggregateType,
			            source.Version, source.Data, source.CreatedAt, source.Metadata{tenantInsertValue});
			""";
#pragma warning restore CA2100

		var parameters = new DynamicParameters();
		parameters.Add("@TenantId", KeyedTenantPartition.FromScope(scope).TenantId);
		parameters.Add("@SnapshotId", snapshot.SnapshotId);
		parameters.Add("@AggregateId", snapshot.AggregateId);
		parameters.Add("@AggregateType", snapshot.AggregateType);
		parameters.Add("@Version", snapshot.Version);
		parameters.Add("@Data", snapshot.Data.ToArray());
		parameters.Add("@CreatedAt", snapshot.CreatedAt);
		parameters.Add("@Metadata", SerializeMetadata(snapshot.Metadata), DbType.Binary);

		Command = CreateCommand(sql, parameters, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}

	/// <summary>
	/// Serializes the snapshot metadata dictionary to a binary payload for storage so that the
	/// schema-version entry consumed by snapshot upgrading round-trips. Null metadata is stored as
	/// SQL NULL; an empty dictionary is preserved as empty.
	/// </summary>
	private static byte[]? SerializeMetadata(IDictionary<string, object>? metadata)
	{
		if (metadata is null)
		{
			return null;
		}

#pragma warning disable IL2026, IL3050 // Metadata serialization inherently uses reflection (matches SqlServerEventStore precedent)
		return JsonSerializer.SerializeToUtf8Bytes(metadata);
#pragma warning restore IL2026, IL3050
	}
}
