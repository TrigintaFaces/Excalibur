// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;

namespace Excalibur.EventSourcing.Oracle.Requests;

/// <summary>
/// Data request to check whether events for an aggregate have been erased (tombstoned).
/// </summary>
internal sealed class IsErasedRequest : DataRequestBase<IDbConnection, bool>
{
	public IsErasedRequest(
		string aggregateId,
		string aggregateType,
		TenantScope scope,
		CancellationToken cancellationToken,
		string schema = "EXCALIBUR",
		string table = "EVENTSTOREEVENTS")
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);

		var qualifiedTable = OracleTableName.Format(schema, table);

		// The EVENTSTOREEVENTS table is a KEYED (tenant-columned) store, so an unscoped IsErased must NEVER
		// emit an empty tenant predicate: doing so would let one tenant's tombstone answer another tenant's
		// erasure check, making a required GDPR erasure SKIP (logged as already-erased). Route the scope
		// through the keyed partition: default(TenantScope) (or an absent context) becomes the '__untenanted__'
		// sentinel term, so the predicate is UNCONDITIONAL and NULL-safe on the column side (a legacy NULL
		// folds to the sentinel — Oracle also folds '' to NULL, so the sentinel is the non-empty
		// '__untenanted__'). A bare `= :TenantId` would miss legacy NULL untenanted rows; an empty predicate
		// reads across tenants — both are the mutants the tenant-isolation arms forbid. ODP.NET binds by position:
		// :UntenantedSentinel appears first (inside COALESCE), then :TenantId.
		var partition = KeyedTenantPartition.FromScope(scope);
		const string tenantPredicate = " AND COALESCE(TENANTID, :UntenantedSentinel) = :TenantId";

#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in OracleTableName.Format
		var sql = $"""
			SELECT CASE WHEN EXISTS (
			    SELECT 1 FROM {qualifiedTable}
			    WHERE AGGREGATEID = :AggregateId
			      AND AGGREGATETYPE = :AggregateType
			      AND EVENTTYPE = '{ErasedEventMarker.EventType}'{tenantPredicate}
			) THEN 1 ELSE 0 END
			FROM DUAL
			""";
#pragma warning restore CA2100

		// ODP.NET binds by position; add parameters in the exact left-to-right order they appear in the SQL
		// (:AggregateId, :AggregateType, :UntenantedSentinel, :TenantId). The keyed partition always yields a
		// concrete, non-null tenant term, so both tenant parameters are bound unconditionally.
		var parameters = new DynamicParameters();
		parameters.Add(":AggregateId", aggregateId);
		parameters.Add(":AggregateType", aggregateType);
		parameters.Add(":UntenantedSentinel", KeyedTenantPartition.Untenanted.TenantId);
		parameters.Add(":TenantId", partition.TenantId);

		Command = CreateCommand(sql, parameters, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.ExecuteScalarAsync<bool>(Command).ConfigureAwait(false);
	}
}
