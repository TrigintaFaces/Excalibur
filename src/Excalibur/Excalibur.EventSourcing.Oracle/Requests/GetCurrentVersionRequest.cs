// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;

namespace Excalibur.EventSourcing.Oracle.Requests;

/// <summary>
/// Data request to get the current version of an aggregate stream, using an in-transaction read so the
/// optimistic-concurrency compare is serializable with the subsequent append.
/// </summary>
public sealed class GetCurrentVersionRequest : DataRequestBase<IDbConnection, long>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="GetCurrentVersionRequest"/> class.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="transaction">Optional transaction to participate in.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="scope">
	/// The tenant scope. The event store is a <strong>keyed</strong> tenant table, so the version compare is
	/// always partitioned by a non-null tenant term: the resolved tenant when scoped, or the reserved
	/// <c>__untenanted__</c> sentinel when unscoped — routed through <see cref="KeyedTenantPartition"/>,
	/// which has no empty inhabitant. An un-partitioned (all-tenants) concurrency check is unrepresentable.
	/// </param>
	/// <param name="schema">The schema name for the event store table.</param>
	/// <param name="table">The event store table name.</param>
	public GetCurrentVersionRequest(
		string aggregateId,
		string aggregateType,
		IDbTransaction? transaction,
		TenantScope scope,
		CancellationToken cancellationToken,
		string schema = "EXCALIBUR",
		string table = "EVENTSTOREEVENTS")
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);

		var qualifiedTable = OracleTableName.Format(schema, table);

		// The event store is a KEYED tenant table: the lookup is ALWAYS partitioned by a non-null tenant term.
		// Routing through KeyedTenantPartition binds the resolved tenant when scoped, or the reserved
		// __untenanted__ sentinel when unscoped; COALESCE folds a legacy NULL tenant (a pre-migration
		// untenanted row not yet backfilled) to the sentinel, matching the erase/IsErased siblings. ODP.NET
		// binds by position: the predicate is appended LAST and its two parameters are added last, in the
		// order they appear — :UntenantedSentinel (inside COALESCE), then :TenantId.
		var partition = KeyedTenantPartition.FromScope(scope);
		const string tenantPredicate = " AND COALESCE(TENANTID, :UntenantedSentinel) = :TenantId";

#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in OracleTableName.Format
		var sql = $"""
			SELECT NVL(MAX(VERSION), -1)
			FROM {qualifiedTable}
			WHERE AGGREGATEID = :AggregateId AND AGGREGATETYPE = :AggregateType{tenantPredicate}
			""";
#pragma warning restore CA2100

		var parameters = new DynamicParameters();
		parameters.Add(":AggregateId", aggregateId);
		parameters.Add(":AggregateType", aggregateType);
		parameters.Add(":UntenantedSentinel", KeyedTenantPartition.Untenanted.TenantId);
		parameters.Add(":TenantId", partition.TenantId);

		Command = CreateCommand(sql, parameters, transaction, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.ExecuteScalarAsync<long>(Command).ConfigureAwait(false);
	}
}
