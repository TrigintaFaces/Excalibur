// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Dapper;

using Excalibur.Dispatch;

using Excalibur.Data;

namespace Excalibur.EventSourcing.Oracle.Requests;

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
	/// The tenant scope. The tenant predicate is emitted unconditionally: an untenanted scope binds the
	/// reserved sentinel rather than omitting the term, so the statement is always restricted to a single
	/// partition's rows.
	/// </param>
	/// <param name="schema">The schema name for the snapshot store table.</param>
	/// <param name="table">The snapshot store table name.</param>
	public DeleteSnapshotsRequest(
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
		// SCOPE-CONDITIONAL, and IS NULL is the only predicate that can address the untenanted partition on
		// Oracle. An earlier revision used `TENANTID = NVL(:TenantId, '')` on the premise -- stated correctly
		// in its own comment -- that Oracle stores the empty string AS NULL. That premise invalidates the
		// line: NVL's replacement value IS '', which Oracle converts to NULL, so the predicate evaluated to
		// `TENANTID = NULL` and was never true. A single-tenant host wrote snapshots it could not read back.
		// There is no '' sentinel to compare against here because Oracle cannot store one.
		// UNCONDITIONAL — see the note above: TENANTID is NOT NULL and untenanted rows carry the reserved
		// sentinel, so both scopes emit the same equality term and the branch is gone. On a DELETE that
		// matters twice over: a scope-conditional tenant term is a branch that can be got wrong, and getting
		// it wrong on a destructive statement is how a sweep reaches every tenant's snapshots.
		var tenantPredicate = " AND TENANTID = :TenantId";

#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in OracleTableName.Format
		var sql = $"""
			DELETE FROM {qualifiedTable}
			WHERE AGGREGATEID = :AggregateId AND AGGREGATETYPE = :AggregateType{tenantPredicate}
			""";
#pragma warning restore CA2100

		var parameters = new DynamicParameters();
		parameters.Add(":AggregateId", aggregateId);
		parameters.Add(":AggregateType", aggregateType);

		// ODP.NET binds by POSITION: a parameter added but not referenced shifts every subsequent value. The
		// predicate above is now unconditional, so the bind is too — emitted and bound move together.
		parameters.Add(":TenantId", KeyedTenantPartition.FromScope(scope).TenantId);
		Command = CreateCommand(sql, parameters, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
