// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;

namespace Excalibur.EventSourcing.SqlServer.Requests;

/// <summary>
/// Data request to delete snapshots older than a specific version.
/// </summary>
public sealed class DeleteSnapshotsOlderThanRequest : DataRequestBase<IDbConnection, int>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="DeleteSnapshotsOlderThanRequest"/> class.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="olderThanVersion">Delete snapshots with version less than this value.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="scope">
	/// The tenant scope. When <see cref="TenantScope.None"/> (the non-multi-tenant default), no tenant
	/// predicate is emitted; when tenant-scoped, the prune is restricted to the tenant's own rows
	/// (<c>AND TenantId = @TenantId</c>), so one tenant's retention policy cannot prune another tenant's
	/// snapshots of the same aggregate identifier.
	/// </param>
	/// <param name="schema">The schema name for the snapshot store table. Default: "dbo".</param>
	/// <param name="table">The snapshot store table name. Default: "EventStoreSnapshots".</param>
	public DeleteSnapshotsOlderThanRequest(
		string aggregateId,
		string aggregateType,
		long olderThanVersion,
		TenantScope scope,
		CancellationToken cancellationToken,
		string schema = "dbo",
		string table = "EventStoreSnapshots")
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);

		var qualifiedTable = SqlTableName.Format(schema, table);
		// UNCONDITIONAL: the DDL keys on (AggregateId, AggregateType, TenantId), so a prune with no
		// tenant filter matches only a SUBSET of the key and can DELETE another tenant's rows. The
		// predicate is therefore always present — there is no unscoped form of this prune.
		//
		// Two layers, and they are easy to conflate:
		//   * TYPE layer — KeyedTenantPartition has no empty inhabitant. An untenanted purge binds the
		//     reserved untenanted partition, never an empty tenant term. An empty term is not
		//     constructable here.
		//   * STORAGE layer — that reserved partition is ENCODED as '' in this provider's column. The
		//     encoding is an implementation detail of the column, not a scope value callers can pass.
		//
		// This is the higher-consequence half of the pair: an empty tenant term on an age-predicated
		// DELETE is precisely how one tenant's retention sweep would reach every tenant's snapshots.
		// The partition type makes that statement unconstructable rather than merely discouraged.
		const string tenantPredicate = " AND TenantId = @TenantId";

#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in SqlTableName.Format
		var sql = $"""
			DELETE FROM {qualifiedTable}
			WHERE AggregateId = @AggregateId
			  AND AggregateType = @AggregateType
			  AND Version < @Version{tenantPredicate}
			""";
#pragma warning restore CA2100

		var parameters = new DynamicParameters();
		parameters.Add("@AggregateId", aggregateId);
		parameters.Add("@AggregateType", aggregateType);
		parameters.Add("@Version", olderThanVersion);
		parameters.Add("@TenantId", KeyedTenantPartition.FromScope(scope).TenantId);
		Command = CreateCommand(sql, parameters, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
