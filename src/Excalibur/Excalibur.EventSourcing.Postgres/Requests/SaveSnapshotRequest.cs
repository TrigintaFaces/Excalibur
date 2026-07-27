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
/// Data request to save (upsert) a snapshot for an aggregate.
/// Uses Postgres INSERT ... ON CONFLICT for atomic insert-or-update semantics.
/// </summary>
public sealed class SaveSnapshotRequest : DataRequestBase<IDbConnection, int>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="SaveSnapshotRequest"/> class.
	/// </summary>
	/// <param name="snapshot">The snapshot to save.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="scope">
	/// The tenant scope, or <see cref="TenantScope.None"/> in a single-tenant host.
	/// </param>
	/// <param name="schema">The schema name for the snapshot store table. Default: "public".</param>
	/// <param name="table">The snapshot store table name. Default: "event_store_snapshots".</param>
	public SaveSnapshotRequest(
		ISnapshot snapshot,
		TenantScope scope,
		CancellationToken cancellationToken,
		string schema = "public",
		string table = "event_store_snapshots")
	{
		ArgumentNullException.ThrowIfNull(snapshot);

		var qualifiedTable = PgTableName.Format(schema, table);

#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in PgTableName.Format
		var sql = $"""
			INSERT INTO {qualifiedTable} (snapshot_id, aggregate_id, aggregate_type, version, data, metadata, created_at, tenant_id)
			VALUES (@SnapshotId, @AggregateId, @AggregateType, @Version, @Data, @Metadata, @CreatedAt, @TenantId)
			-- Untenanted rows use the reserved '__untenanted__' tenant key BY DESIGN, bound through
			-- KeyedTenantPartition. The tenant is part of the ON CONFLICT UNIQUE key, and NULL is treated as
			-- DISTINCT in a unique index (pre-PG15 / SQLite), so NULL cannot serve as an upsert key (it would
			-- duplicate untenanted snapshots instead of upserting). The sentinel is collision-proof (Scoped()
			-- rejects it, so no real tenant can claim the partition), read and write agree on it, and it never
			-- crosses the tenant boundary. See ARCHITECTURE.md.
			-- This was '' until the keyed family converged. The empty string works HERE and fails on Oracle,
			-- which stores '' as NULL -- so the same intent became a different value per provider, which is
			-- exactly the divergence the shared sentinel removes. A value that is correct on the provider you
			-- happen to be testing is not a shared representation.
			-- tenant_id is both WRITTEN and KEYED. Naming it in the conflict target while omitting it from
			-- the INSERT would let every row take the column default, so all tenants would collide on one
			-- row exactly as before -- the defect surviving a fix that appears to address it.
			ON CONFLICT (aggregate_id, aggregate_type, tenant_id)
			DO UPDATE SET
			    snapshot_id = EXCLUDED.snapshot_id,
			    version = EXCLUDED.version,
			    data = EXCLUDED.data,
			    metadata = EXCLUDED.metadata,
			    created_at = EXCLUDED.created_at
			""";
#pragma warning restore CA2100

		// Unconditional, unlike the SQL Server MERGE: Postgres infers ON CONFLICT against a unique
		// index that must match EXACTLY, so a two-column target stops matching the three-column key.
		var parameters = new DynamicParameters();
		parameters.Add("@TenantId", KeyedTenantPartition.FromScope(scope).TenantId);
		parameters.Add("@SnapshotId", snapshot.SnapshotId);
		parameters.Add("@AggregateId", snapshot.AggregateId);
		parameters.Add("@AggregateType", snapshot.AggregateType);
		parameters.Add("@Version", snapshot.Version);
		parameters.Add("@Data", snapshot.Data.ToArray(), DbType.Binary);
		parameters.Add("@Metadata", SerializeMetadata(snapshot.Metadata), DbType.Binary);
		parameters.Add("@CreatedAt", snapshot.CreatedAt);

		Command = CreateCommand(sql, parameters, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}

	/// <summary>
	/// Serializes the snapshot metadata dictionary to a binary payload so the version metadata the
	/// snapshot carries round-trips. Null metadata is stored as SQL NULL.
	/// </summary>
	private static byte[]? SerializeMetadata(IDictionary<string, object>? metadata)
	{
		if (metadata is null)
		{
			return null;
		}

#pragma warning disable IL2026, IL3050 // Metadata serialization inherently uses reflection (matches the SqlServer/Oracle snapshot store precedent)
		return JsonSerializer.SerializeToUtf8Bytes(metadata);
#pragma warning restore IL2026, IL3050
	}
}
