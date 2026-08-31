// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;

namespace Excalibur.EventSourcing.SqlServer.Requests;

/// <summary>
/// Data request to get the current version of an aggregate stream.
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
	/// The tenant scope. The event store is a <strong>keyed</strong> tenant table, so the version
	/// lookup is always partitioned by a non-null tenant term: the resolved tenant when scoped, or the
	/// reserved <c>__untenanted__</c> sentinel when unscoped — routed through
	/// <see cref="KeyedTenantPartition"/>, which has no empty inhabitant. An un-partitioned (all-tenants)
	/// lookup is unrepresentable.
	/// </param>
	/// <param name="schema">The schema name for the event store table. Default: "dbo".</param>
	/// <param name="table">The event store table name. Default: "EventStoreEvents".</param>
	public GetCurrentVersionRequest(
		string aggregateId,
		string aggregateType,
		IDbTransaction? transaction,
		TenantScope scope,
		CancellationToken cancellationToken,
		string schema = "dbo",
		string table = "EventStoreEvents")
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);

		var qualifiedTable = SqlTableName.Format(schema, table);
		// The event store is a KEYED tenant table: the lookup is ALWAYS partitioned by a non-null
		// tenant term. Routing through KeyedTenantPartition binds the resolved tenant when scoped, or the
		// reserved __untenanted__ sentinel when unscoped — so an un-partitioned (all-tenants) read is
		// unconstructable (no empty predicate can be emitted). COALESCE folds a legacy NULL tenant (a
		// pre-migration untenanted row not yet backfilled) to the sentinel, matching the erase/IsErased
		// siblings; a bare `= @TenantId` would miss those rows during the migration window.
		var partition = KeyedTenantPartition.FromScope(scope);
		const string tenantPredicate = " AND COALESCE(TenantId, @UntenantedSentinel) = @TenantId";

		// Deliberately NO lock hint. This read is advisory, and the constraint is the concurrency control.
		//
		// A stale MAX(Version) here is expected rather than a hole: the append runs at READ COMMITTED, and a
		// concurrent writer that claims the same version makes the INSERT violate
		// UQ_EventStoreEvents_Stream (AggregateId, AggregateType, Version, TenantId), which the store
		// translates into a concurrency conflict. The duplicate is unwritable, so the read does not have to
		// be authoritative.
		//
		// An earlier revision carried WITH (UPDLOCK, HOLDLOCK) here to stop concurrent appends deadlocking
		// each other under a SERIALIZABLE transaction. That isolation level is gone, and with it the range
		// locks the hint existed to tame; leaving it would re-serialise exactly what removing SERIALIZABLE
		// stopped serialising.
#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in SqlTableName.Format
		var sql = $"""
			SELECT ISNULL(MAX(Version), -1)
			FROM {qualifiedTable}
			WHERE AggregateId = @AggregateId AND AggregateType = @AggregateType{tenantPredicate}
			""";
#pragma warning restore CA2100

		var parameters = new DynamicParameters();
		parameters.Add("@AggregateId", aggregateId);
		parameters.Add("@AggregateType", aggregateType);
		parameters.Add("@TenantId", partition.TenantId);
		parameters.Add("@UntenantedSentinel", KeyedTenantPartition.Untenanted.TenantId);

		Command = CreateCommand(sql, parameters, transaction, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.ExecuteScalarAsync<long>(Command).ConfigureAwait(false);
	}
}
