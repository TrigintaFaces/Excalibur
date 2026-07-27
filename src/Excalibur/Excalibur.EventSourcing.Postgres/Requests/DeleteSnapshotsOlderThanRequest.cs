// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Dispatch;

using Excalibur.Data;

namespace Excalibur.EventSourcing.Postgres.Requests;

/// <summary>
/// Data request to delete snapshots older than a specified version from the Postgres snapshot store.
/// </summary>
public sealed class DeleteSnapshotsOlderThanRequest : DataRequestBase<IDbConnection, int>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="DeleteSnapshotsOlderThanRequest"/> class.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="version">Delete snapshots older than this version.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="scope">
	/// The tenant scope, or <see cref="TenantScope.None"/> in a single-tenant host.
	/// </param>
	/// <param name="schema">The schema name for the snapshot store table. Default: "public".</param>
	/// <param name="table">The snapshot store table name. Default: "event_store_snapshots".</param>
	public DeleteSnapshotsOlderThanRequest(
		string aggregateId,
		string aggregateType,
		long version,
		TenantScope scope,
		CancellationToken cancellationToken,
		string schema = "public",
		string table = "event_store_snapshots")
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);

		var qualifiedTable = PgTableName.Format(schema, table);

#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in PgTableName.Format
		// Unconditional, mirroring the snapshot write + GetLatestSnapshotRequest: SaveSnapshotRequest binds
		// the same partition and keys ON CONFLICT (aggregate_id, aggregate_type, tenant_id), so untenanted
		// snapshots store the reserved '__untenanted__' value (NOT NULL — this is the snapshot table, unlike
		// the event table where the write omits the column). The former empty predicate made an unscoped
		// retention sweep span EVERY tenant's snapshots — a destructive over-delete. IS NULL would
		// under-delete, since an untenanted snapshot carries the sentinel and not a NULL.
		const string tenantPredicate = " AND tenant_id = @TenantId";

		var sql = $"""
			DELETE FROM {qualifiedTable}
			WHERE aggregate_id = @AggregateId AND aggregate_type = @AggregateType{tenantPredicate} AND version < @Version
			""";
#pragma warning restore CA2100

		var parameters = new DynamicParameters();
		parameters.Add("@AggregateId", aggregateId);
		parameters.Add("@AggregateType", aggregateType);
		parameters.Add("@Version", version);

		// Unconditional: FromScope maps an unscoped scope onto the reserved '__untenanted__' partition —
		// matching the snapshot write, and never yielding an empty term on a destructive statement.
		parameters.Add("@TenantId", KeyedTenantPartition.FromScope(scope).TenantId);

		Command = CreateCommand(sql, parameters, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
