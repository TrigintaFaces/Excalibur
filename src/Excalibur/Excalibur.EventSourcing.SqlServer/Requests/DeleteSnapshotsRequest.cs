// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;

namespace Excalibur.EventSourcing.SqlServer.Requests;

/// <summary>
/// Data request to delete all snapshots for an aggregate.
/// </summary>
public sealed class DeleteSnapshotsRequest : DataRequestBase<IDbConnection, int>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="DeleteSnapshotsRequest"/> class.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="scope">
	/// The tenant scope. The tenant predicate is emitted unconditionally — an untenanted scope binds the
	/// reserved sentinel rather than omitting the term — so the delete is restricted to one partition's rows
	/// (<c>AND TenantId = @TenantId</c>). This predicate is what keeps one tenant's erasure from deleting
	/// another tenant's snapshot of the same aggregate identifier — on a delete the omission destroys
	/// data rather than merely exposing it.
	/// </param>
	/// <param name="schema">The schema name for the snapshot store table. Default: "dbo".</param>
	/// <param name="table">The snapshot store table name. Default: "EventStoreSnapshots".</param>
	public DeleteSnapshotsRequest(
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
		// UNCONDITIONAL: the DDL keys on (AggregateId, AggregateType, TenantId), so an unscoped
		// statement with no tenant filter matches a SUBSET of the key and can reach another tenant's
		// row. Single-tenant is the '' sentinel, not the absence of a tenant.
		// Routed through KeyedTenantPartition, which has no empty inhabitant: an unscoped delete binds the
		// reserved untenanted sentinel rather than an empty term. This matters most on a DELETE — a tenant
		// term that could resolve to empty is how a destructive statement ends up matching every tenant's
		// rows, and that state is now unconstructable rather than merely avoided.
		const string tenantPredicate = " AND TenantId = @TenantId";

#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in SqlTableName.Format
		var sql = $"""
			DELETE FROM {qualifiedTable}
			WHERE AggregateId = @AggregateId AND AggregateType = @AggregateType{tenantPredicate}
			""";
#pragma warning restore CA2100

		var parameters = new DynamicParameters();
		parameters.Add("@AggregateId", aggregateId);
		parameters.Add("@AggregateType", aggregateType);
		parameters.Add("@TenantId", KeyedTenantPartition.FromScope(scope).TenantId);
		Command = CreateCommand(sql, parameters, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
